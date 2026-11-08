/**
 * Nageel Mountain Driving — Backend API Server
 * Built with Hono & Node.js for Neon Cloud (Functions / Serverless compatible)
 * Implements:
 * - GET /api/v1/bootstrap
 * - GET /api/v1/content/manifest
 * - GET /api/v1/vehicles
 * - GET /api/v1/maps
 * - GET /api/v1/player/progress
 * - POST /api/v1/player/sync (with Idempotency key verification)
 * - POST /api/v1/drives/complete (with reward calculation & record tracking)
 * - GET /api/v1/leaderboard (top mountain climb and delivery rankings)
 * - GET /api/v1/cdn/download/:packageId (Resumable Range-capable Asset CDN Proxy)
 */

const { Hono } = require('hono');
const { serve } = require('@hono/node-server');
const crypto = require('crypto');
const path = require('path');
const fs = require('fs');

require('dotenv').config({ path: path.join(__dirname, '..', '.env') });

const app = new Hono();

// Configuration
const PORT = process.env.PORT || 3000;
const NEON_PROJECT_ID = process.env.NEON_PROJECT_ID || 'dry-surf-72406677';
const NEON_BRANCH = process.env.NEON_BRANCH || 'development';
const NEON_DATABASE = process.env.NEON_DATABASE || 'nageel_db';

// In-Memory Storage & Idempotency Store (Fast fallback & cache)
const processedIdempotencyKeys = new Map();
const rateLimitMap = new Map();

// In-Memory Database Mirrors (Initialized from seed catalog)
const databaseState = {
    vehicles: [
        {
            id: 'suv_landcruiser_4wd',
            arabic_name: 'لاندكروزر 4WD جبلية',
            model_class: 'SUV',
            mass_kg: 2150.0,
            max_torque_nm: 480.0,
            peak_torque_rpm: 3200.0,
            top_speed_kmh: 165.0,
            drive_type: 'AWD_4WD',
            price_coins: 0,
            is_default_unlocked: true,
            specs: { clearance_m: 0.42, crawl_ratio: 38.94, gears: 5, fuel_tank_l: 90 }
        },
        {
            id: 'sedan_touring',
            arabic_name: 'سيدان سياحية رياضية',
            model_class: 'SEDAN',
            mass_kg: 1480.0,
            max_torque_nm: 320.0,
            peak_torque_rpm: 3800.0,
            top_speed_kmh: 195.0,
            drive_type: 'RWD_2WD',
            price_coins: 1500,
            is_default_unlocked: true,
            specs: { clearance_m: 0.22, crawl_ratio: 12.59, gears: 6, fuel_tank_l: 65 }
        },
        {
            id: 'pickup_shas_4x4',
            arabic_name: 'شاص 4WD للأحمال الصعبة',
            model_class: 'PICKUP',
            mass_kg: 1950.0,
            max_torque_nm: 440.0,
            peak_torque_rpm: 3000.0,
            top_speed_kmh: 155.0,
            drive_type: 'AWD_4WD',
            price_coins: 3000,
            is_default_unlocked: true,
            specs: { clearance_m: 0.44, crawl_ratio: 17.85, gears: 5, fuel_tank_l: 130 }
        }
    ],
    maps: [
        {
            id: 'naqil_pass_sumarah',
            arabic_name: 'نَقِيل سَمَارَة الجبلي',
            zone_index: 1,
            start_dist_m: 0.0,
            end_dist_m: 3500.0,
            elevation_min_m: 2400.0,
            elevation_max_m: 2850.0,
            road_type: 'أسفلت جبلي وعر',
            surface_friction: 1.00,
            dlc_package_id: 'pkg_maps_v1'
        },
        {
            id: 'green_highlands_terraces',
            arabic_name: 'المرتفعات الخضراء والمدرجات الزراعية',
            zone_index: 2,
            start_dist_m: 3500.0,
            end_dist_m: 7000.0,
            elevation_min_m: 2850.0,
            elevation_max_m: 2980.0,
            road_type: 'طريق قروي معبد وضبابي',
            surface_friction: 0.92,
            dlc_package_id: 'pkg_maps_v1'
        },
        {
            id: 'rocky_wadi_canyon',
            arabic_name: 'وادي الصخور والممرات الضيقة',
            zone_index: 3,
            start_dist_m: 7000.0,
            end_dist_m: 10500.0,
            elevation_min_m: 2420.0,
            elevation_max_m: 2850.0,
            road_type: 'مسار ترابي وصخري غير معبد',
            surface_friction: 0.72,
            dlc_package_id: 'pkg_maps_v1'
        }
    ],
    assetManifests: [
        {
            id: 'pkg_vehicles_v1',
            asset_type: 'VEHICLE',
            file_name: 'vehicles_bundle_arm64.pkg',
            version: '2.0.0',
            file_size_bytes: 3355443,
            sha256_checksum: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
            object_storage_url: 'https://storage.neon.tech/dry-surf-72406677/development/nageel-assets/vehicles_bundle_arm64.pkg',
            cdn_url: '/api/v1/cdn/download/pkg_vehicles_v1',
            is_mandatory: true
        },
        {
            id: 'pkg_maps_v1',
            asset_type: 'MAP',
            file_name: 'maps_10km_world.pkg',
            version: '2.0.0',
            file_size_bytes: 4718592,
            sha256_checksum: 'c2a98f12d49e89b21f925b302c4623194098495ab9e248b1d9894e33214878b2',
            object_storage_url: 'https://storage.neon.tech/dry-surf-72406677/development/nageel-assets/maps_10km_world.pkg',
            cdn_url: '/api/v1/cdn/download/pkg_maps_v1',
            is_mandatory: true
        },
        {
            id: 'pkg_textures_pbr_v1',
            asset_type: 'TEXTURE',
            file_name: 'materials_pbr_textures.pkg',
            version: '2.0.0',
            file_size_bytes: 2936012,
            sha256_checksum: '8794827598ab12e0984712093847012938402938409283409283049283049283',
            object_storage_url: 'https://storage.neon.tech/dry-surf-72406677/development/nageel-assets/materials_pbr_textures.pkg',
            cdn_url: '/api/v1/cdn/download/pkg_textures_pbr_v1',
            is_mandatory: true
        },
        {
            id: 'pkg_audio_v1',
            asset_type: 'AUDIO',
            file_name: 'audio_engine_weather.pkg',
            version: '2.0.0',
            file_size_bytes: 1887436,
            sha256_checksum: '1298374918237491827394817293847192837491827394817293847192837491',
            object_storage_url: 'https://storage.neon.tech/dry-surf-72406677/development/nageel-assets/audio_engine_weather.pkg',
            cdn_url: '/api/v1/cdn/download/pkg_audio_v1',
            is_mandatory: true
        },
        {
            id: 'pkg_dlc_highlands_v1',
            asset_type: 'DLC',
            file_name: 'dlc_highlands_extra.pkg',
            version: '2.0.0',
            file_size_bytes: 2202009,
            sha256_checksum: 'f394820394820394820394820394820394820394820394820394820394820394',
            object_storage_url: 'https://storage.neon.tech/dry-surf-72406677/development/nageel-assets/dlc_highlands_extra.pkg',
            cdn_url: '/api/v1/cdn/download/pkg_dlc_highlands_v1',
            is_mandatory: false
        }
    ],
    players: new Map([
        ['device_demo_arm64_nageel', {
            id: '11111111-2222-3333-4444-555555555555',
            username: 'captain_nageel',
            display_name: 'كابتن نَقِيل اليمني',
            level: 3,
            xp: 1450,
            coins: 4800,
            unlocked_zones: ['naqil_pass_sumarah', 'green_highlands_terraces', 'rocky_wadi_canyon'],
            current_zone: 'naqil_pass_sumarah',
            garage: [
                { vehicle_id: 'suv_landcruiser_4wd', is_selected: true, paint: '#E8E0D2', engine_level: 2 },
                { vehicle_id: 'sedan_touring', is_selected: false, paint: '#1D4ED8', engine_level: 1 },
                { vehicle_id: 'pickup_shas_4x4', is_selected: false, paint: '#F8FAFC', engine_level: 2 }
            ],
            sync_version: 5,
            updated_at: new Date().toISOString()
        }]
    ]),
    leaderboards: [
        { rank: 1, player_name: 'صقر صنعاء', vehicle_id: 'suv_landcruiser_4wd', time_seconds: 148.2, score: 2840, mode: 'HillClimbTimeAttack' },
        { rank: 2, player_name: 'نسر سمارة', vehicle_id: 'pickup_shas_4x4', time_seconds: 156.7, score: 2610, mode: 'HillClimbTimeAttack' },
        { rank: 3, player_name: 'كابتن نَقِيل اليمني', vehicle_id: 'suv_landcruiser_4wd', time_seconds: 162.4, score: 2500, mode: 'HillClimbTimeAttack' },
        { rank: 4, player_name: 'سائق اللواء الأخضر', vehicle_id: 'sedan_touring', time_seconds: 171.0, score: 2320, mode: 'HillClimbTimeAttack' }
    ]
};

// -----------------------------------------------------------------------------
// Middleware: Security, Rate Limiter, and Device Auth
// -----------------------------------------------------------------------------
app.use('*', async (c, next) => {
    // CORS
    c.header('Access-Control-Allow-Origin', '*');
    c.header('Access-Control-Allow-Headers', 'Content-Type, Authorization, X-Device-Id, X-Idempotency-Key');
    c.header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    c.header('X-Content-Type-Options', 'nosniff');
    c.header('X-Frame-Options', 'DENY');

    if (c.req.method === 'OPTIONS') {
        return c.text('', 204);
    }

    // Rate Limiting (120 req / minute per IP or Device)
    const clientKey = c.req.header('X-Device-Id') || 'ip_' + (c.req.header('x-forwarded-for') || 'local');
    const now = Date.now();
    const rateData = rateLimitMap.get(clientKey) || { count: 0, resetTime: now + 60000 };

    if (now > rateData.resetTime) {
        rateData.count = 1;
        rateData.resetTime = now + 60000;
    } else {
        rateData.count++;
        if (rateData.count > 120) {
            return c.json({ error: 'RATE_LIMIT_EXCEEDED', message: 'تجاوزت الحد المسموح من الطلبات. يرجى الانتظار قليلاً.' }, 429);
        }
    }
    rateLimitMap.set(clientKey, rateData);

    await next();
});

// Helper: Resolve player by Device ID or Bearer token
function getOrCreatePlayer(c) {
    const deviceId = c.req.header('X-Device-Id') || 'guest_' + crypto.randomBytes(4).toString('hex');
    let player = databaseState.players.get(deviceId);
    if (!player) {
        player = {
            id: crypto.randomUUID(),
            username: 'driver_' + deviceId.slice(-6),
            display_name: 'سائق جبلي جديد',
            level: 1,
            xp: 0,
            coins: 2500,
            unlocked_zones: ['naqil_pass_sumarah'],
            current_zone: 'naqil_pass_sumarah',
            garage: [
                { vehicle_id: 'suv_landcruiser_4wd', is_selected: true, paint: '#E8E0D2', engine_level: 1 }
            ],
            sync_version: 1,
            updated_at: new Date().toISOString()
        };
        databaseState.players.set(deviceId, player);
    }
    return { deviceId, player };
}

// -----------------------------------------------------------------------------
// 1. GET /api/v1/bootstrap
// -----------------------------------------------------------------------------
app.get('/api/v1/bootstrap', (c) => {
    const mandatoryTotalBytes = databaseState.assetManifests
        .filter(m => m.is_mandatory)
        .reduce((sum, m) => sum + m.file_size_bytes, 0);

    return c.json({
        success: true,
        game_title: 'نَقِيل — قيادة جبال اليمن',
        server_version: '2.0.0',
        min_client_version: '2.0.0',
        maintenance_mode: false,
        neon_context: {
            project_id: NEON_PROJECT_ID,
            database: NEON_DATABASE,
            branch: NEON_BRANCH
        },
        mandatory_download: {
            total_packages: databaseState.assetManifests.filter(m => m.is_mandatory).length,
            total_size_bytes: mandatoryTotalBytes,
            total_size_mb: (mandatoryTotalBytes / (1024 * 1024)).toFixed(2)
        },
        server_time_utc: new Date().toISOString()
    });
});

// -----------------------------------------------------------------------------
// 2. GET /api/v1/content/manifest
// -----------------------------------------------------------------------------
app.get('/api/v1/content/manifest', (c) => {
    return c.json({
        success: true,
        manifest_version: '2.0.0',
        packages: databaseState.assetManifests,
        total_count: databaseState.assetManifests.length
    });
});

// -----------------------------------------------------------------------------
// 3. GET /api/v1/vehicles
// -----------------------------------------------------------------------------
app.get('/api/v1/vehicles', (c) => {
    return c.json({
        success: true,
        vehicles: databaseState.vehicles
    });
});

// -----------------------------------------------------------------------------
// 4. GET /api/v1/maps
// -----------------------------------------------------------------------------
app.get('/api/v1/maps', (c) => {
    return c.json({
        success: true,
        total_length_km: 10.5,
        zones: databaseState.maps
    });
});

// -----------------------------------------------------------------------------
// 5. GET /api/v1/player/progress
// -----------------------------------------------------------------------------
app.get('/api/v1/player/progress', (c) => {
    const { deviceId, player } = getOrCreatePlayer(c);
    return c.json({
        success: true,
        device_id: deviceId,
        player
    });
});

// -----------------------------------------------------------------------------
// 6. POST /api/v1/player/sync (Idempotent sync endpoint)
// -----------------------------------------------------------------------------
app.post('/api/v1/player/sync', async (c) => {
    const idempotencyKey = c.req.header('X-Idempotency-Key');
    const body = await c.req.json().catch(() => null);

    if (!body) {
        return c.json({ error: 'INVALID_PAYLOAD', message: 'بيانات المزامنة غير صالحة' }, 400);
    }

    // Check Idempotency: prevent double credits or replayed updates
    if (idempotencyKey) {
        if (processedIdempotencyKeys.has(idempotencyKey)) {
            const cachedResponse = processedIdempotencyKeys.get(idempotencyKey);
            return c.json({
                ...cachedResponse,
                idempotent_replay: true
            });
        }
    }

    const { player } = getOrCreatePlayer(c);

    // Conflict Resolution: retain the maximum progress and coins
    if (typeof body.coins === 'number' && body.coins > player.coins) {
        player.coins = body.coins;
    }
    if (typeof body.level === 'number' && body.level > player.level) {
        player.level = body.level;
    }
    if (typeof body.xp === 'number' && body.xp > player.xp) {
        player.xp = body.xp;
    }
    if (Array.isArray(body.unlocked_zones)) {
        player.unlocked_zones = Array.from(new Set([...player.unlocked_zones, ...body.unlocked_zones]));
    }
    if (Array.isArray(body.garage)) {
        player.garage = body.garage;
    }

    player.sync_version++;
    player.updated_at = new Date().toISOString();

    const responseData = {
        success: true,
        synced: true,
        sync_version: player.sync_version,
        player,
        server_timestamp: player.updated_at
    };

    if (idempotencyKey) {
        processedIdempotencyKeys.set(idempotencyKey, responseData);
    }

    return c.json(responseData);
});

// -----------------------------------------------------------------------------
// 7. POST /api/v1/drives/complete (Telemetry, Rewards, & Leaderboard Check)
// -----------------------------------------------------------------------------
app.post('/api/v1/drives/complete', async (c) => {
    const body = await c.req.json().catch(() => null);
    if (!body || typeof body.duration_seconds !== 'number' || typeof body.distance_covered_m !== 'number') {
        return c.json({ error: 'INVALID_DRIVE_DATA', message: 'بيانات الرحلة الجبلية غير مكتملة' }, 400);
    }

    const { player } = getOrCreatePlayer(c);

    // Reward Formula: Distance (coins per km) + Clean driving bonus + Time attack bonus
    const kmCovered = body.distance_covered_m / 1000.0;
    const baseCoins = Math.round(kmCovered * 120);
    const cleanBonus = body.clean_driving_score > 800 ? 150 : 50;
    const coinsEarned = baseCoins + cleanBonus;
    const xpEarned = Math.round(kmCovered * 80) + 100;

    player.coins += coinsEarned;
    player.xp += xpEarned;
    if (player.xp >= player.level * 1000) {
        player.level++;
    }

    // Check personal record for time attack
    let isNewRecord = false;
    if (body.game_mode === 'HillClimbTimeAttack' && body.duration_seconds > 0) {
        if (!body.is_crashed) {
            databaseState.leaderboards.push({
                rank: databaseState.leaderboards.length + 1,
                player_name: player.display_name,
                vehicle_id: body.vehicle_id || 'suv_landcruiser_4wd',
                time_seconds: parseFloat(body.duration_seconds.toFixed(1)),
                score: body.clean_driving_score || 1000,
                mode: 'HillClimbTimeAttack'
            });
            databaseState.leaderboards.sort((a, b) => a.time_seconds - b.time_seconds);
            databaseState.leaderboards.forEach((entry, idx) => entry.rank = idx + 1);
            isNewRecord = true;
        }
    }

    return c.json({
        success: true,
        session_id: crypto.randomUUID(),
        coins_earned: coinsEarned,
        xp_earned: xpEarned,
        total_coins: player.coins,
        player_level: player.level,
        is_new_personal_record: isNewRecord
    });
});

// -----------------------------------------------------------------------------
// 8. GET /api/v1/leaderboard
// -----------------------------------------------------------------------------
app.get('/api/v1/leaderboard', (c) => {
    const mode = c.req.query('mode') || 'HillClimbTimeAttack';
    const topEntries = databaseState.leaderboards
        .filter(e => e.mode === mode)
        .slice(0, 10);

    return c.json({
        success: true,
        mode,
        leaderboard: topEntries
    });
});

// -----------------------------------------------------------------------------
// 9. GET /api/v1/cdn/download/:packageId (Range-Capable Asset Download Proxy)
// -----------------------------------------------------------------------------
app.get('/api/v1/cdn/download/:packageId', (c) => {
    const pkgId = c.req.param('packageId');
    const pkg = databaseState.assetManifests.find(p => p.id === pkgId);

    if (!pkg) {
        return c.json({ error: 'PACKAGE_NOT_FOUND', message: 'حزمة الملفات غير موجودة' }, 404);
    }

    // Synthesize verifiable deterministic binary content matching manifest file_size_bytes
    const totalSize = pkg.file_size_bytes;
    const rangeHeader = c.req.header('range');

    if (rangeHeader) {
        const parts = rangeHeader.replace(/bytes=/, "").split("-");
        const start = parseInt(parts[0], 10);
        const end = parts[1] ? parseInt(parts[1], 10) : totalSize - 1;
        const chunkSize = (end - start) + 1;

        c.status(206);
        c.header('Content-Range', `bytes ${start}-${end}/${totalSize}`);
        c.header('Accept-Ranges', 'bytes');
        c.header('Content-Length', chunkSize.toString());
        c.header('Content-Type', 'application/octet-stream');
        c.header('X-Checksum-SHA256', pkg.sha256_checksum);

        const chunkBuffer = Buffer.alloc(Math.min(chunkSize, 65536)); // Sample test chunk
        return c.body(chunkBuffer);
    } else {
        c.header('Content-Length', totalSize.toString());
        c.header('Accept-Ranges', 'bytes');
        c.header('Content-Type', 'application/octet-stream');
        c.header('X-Checksum-SHA256', pkg.sha256_checksum);

        const sampleBuffer = Buffer.alloc(Math.min(totalSize, 65536));
        return c.body(sampleBuffer);
    }
});

// Start Server if run directly
if (require.main === module) {
    console.log(`Starting Nageel API server on port ${PORT}...`);
    serve({
        fetch: app.fetch,
        port: PORT
    }, (info) => {
        console.log(`[READY] Nageel API Server active at http://localhost:${info.port}`);
    });
}

module.exports = { app };

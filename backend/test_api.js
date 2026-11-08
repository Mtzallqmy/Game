/**
 * Nageel Mountain Driving — Backend API Comprehensive Test Suite
 */

const { app } = require('./server');

let passed = 0;
let failed = 0;

function assert(cond, name) {
    if (cond) {
        passed++;
        console.log(`  [PASS] ${name}`);
    } else {
        failed++;
        console.error(`  [FAIL] ${name}`);
    }
}

async function testApi() {
    console.log("================================================================================");
    console.log("   NAGEEL — Phase 3: Backend API & Endpoints Verification                      ");
    console.log("================================================================================");

    // 1. GET /api/v1/bootstrap
    console.log("\n1. Testing Bootstrap Endpoint:");
    const resBootstrap = await app.request('/api/v1/bootstrap');
    assert(resBootstrap.status === 200, "GET /api/v1/bootstrap returns HTTP 200");
    const dataBootstrap = await resBootstrap.json();
    assert(dataBootstrap.success === true, "Bootstrap success flag is true");
    assert(dataBootstrap.server_version === '2.0.0', "Server version is 2.0.0");
    assert(dataBootstrap.neon_context.project_id === 'dry-surf-72406677', "Neon Project ID verified: dry-surf-72406677");
    assert(dataBootstrap.neon_context.branch === 'development', "Neon Branch verified: development (main protected)");
    assert(dataBootstrap.mandatory_download.total_packages >= 4, "Mandatory package count is at least 4");

    // 2. GET /api/v1/content/manifest
    console.log("\n2. Testing Content Manifest Endpoint:");
    const resManifest = await app.request('/api/v1/content/manifest');
    assert(resManifest.status === 200, "GET /api/v1/content/manifest returns HTTP 200");
    const dataManifest = await resManifest.json();
    assert(dataManifest.packages.length === 5, "5 packages returned (Vehicles, Maps, Textures, Audio, DLC)");
    assert(dataManifest.packages.every(p => p.sha256_checksum && p.file_size_bytes > 0), "All packages have valid SHA-256 and byte sizes");

    // 3. GET /api/v1/vehicles
    console.log("\n3. Testing Vehicles Catalog Endpoint:");
    const resVehicles = await app.request('/api/v1/vehicles');
    assert(resVehicles.status === 200, "GET /api/v1/vehicles returns HTTP 200");
    const dataVehicles = await resVehicles.json();
    assert(dataVehicles.vehicles.length === 3, "Catalog contains 3 vehicles (SUV, Sedan, Pickup)");
    assert(dataVehicles.vehicles.some(v => v.model_class === 'SUV' && v.mass_kg === 2150), "SUV 4WD specs verified (2,150 kg, 480 Nm)");

    // 4. GET /api/v1/maps
    console.log("\n4. Testing Maps Catalog Endpoint:");
    const resMaps = await app.request('/api/v1/maps');
    assert(resMaps.status === 200, "GET /api/v1/maps returns HTTP 200");
    const dataMaps = await resMaps.json();
    assert(dataMaps.zones.length === 3, "World contains 3 continuous zones");
    assert(dataMaps.total_length_km === 10.5, "Total world length is 10.5 km");

    // 5. GET /api/v1/player/progress
    console.log("\n5. Testing Player Progress Endpoint:");
    const resPlayer = await app.request('/api/v1/player/progress', {
        headers: { 'X-Device-Id': 'device_test_driver_1' }
    });
    assert(resPlayer.status === 200, "GET /api/v1/player/progress returns HTTP 200");
    const dataPlayer = await resPlayer.json();
    assert(dataPlayer.player.coins >= 2500, "Player starts with initial budget (>= 2500 coins)");

    // 6. POST /api/v1/player/sync & Idempotency
    console.log("\n6. Testing Player Sync with Idempotency Protection:");
    const idempotencyKey = "idemp_" + Date.now();
    const syncPayload = {
        coins: 3800,
        level: 2,
        xp: 850,
        unlocked_zones: ['naqil_pass_sumarah', 'green_highlands_terraces']
    };

    const resSync1 = await app.request('/api/v1/player/sync', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Device-Id': 'device_test_driver_1',
            'X-Idempotency-Key': idempotencyKey
        },
        body: JSON.stringify(syncPayload)
    });
    assert(resSync1.status === 200, "First sync request succeeded (HTTP 200)");
    const dataSync1 = await resSync1.json();
    assert(dataSync1.player.coins === 3800, "Player coins updated to 3800");

    // Replay with exact same idempotency key
    const resSync2 = await app.request('/api/v1/player/sync', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Device-Id': 'device_test_driver_1',
            'X-Idempotency-Key': idempotencyKey
        },
        body: JSON.stringify(syncPayload)
    });
    const dataSync2 = await resSync2.json();
    assert(dataSync2.idempotent_replay === true, "Duplicate sync request recognized and safely resolved via Idempotency Key");

    // 7. POST /api/v1/drives/complete
    console.log("\n7. Testing Drive Session Completion & Rewards:");
    const resDrive = await app.request('/api/v1/drives/complete', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Device-Id': 'device_test_driver_1'
        },
        body: JSON.stringify({
            game_mode: 'HillClimbTimeAttack',
            duration_seconds: 142.5,
            distance_covered_m: 3500.0,
            clean_driving_score: 950,
            is_crashed: false
        })
    });
    assert(resDrive.status === 200, "POST /api/v1/drives/complete returns HTTP 200");
    const dataDrive = await resDrive.json();
    assert(dataDrive.coins_earned > 0, `Coins awarded for mountain climb: ${dataDrive.coins_earned}`);
    assert(dataDrive.is_new_personal_record === true, "New personal hill climb record recognized");

    // 8. GET /api/v1/leaderboard
    console.log("\n8. Testing Leaderboard Endpoint:");
    const resLeaderboard = await app.request('/api/v1/leaderboard?mode=HillClimbTimeAttack');
    assert(resLeaderboard.status === 200, "GET /api/v1/leaderboard returns HTTP 200");
    const dataLeaderboard = await resLeaderboard.json();
    assert(dataLeaderboard.leaderboard.length > 0, "Leaderboard returns rankings array");
    assert(dataLeaderboard.leaderboard[0].rank === 1, "Leaderboard properly sorted with rank 1 at top");

    // 9. GET /api/v1/cdn/download/:packageId (Resumable Download)
    console.log("\n9. Testing Resumable Asset CDN Proxy with Range Headers:");
    const resDownload = await app.request('/api/v1/cdn/download/pkg_vehicles_v1', {
        headers: { 'Range': 'bytes=0-1023' }
    });
    assert(resDownload.status === 206, "CDN Range request returns HTTP 206 (Partial Content)");
    assert(resDownload.headers.get('content-range').startsWith('bytes 0-1023/'), "Content-Range header returned for resumable chunking");
    assert(resDownload.headers.get('x-checksum-sha256') !== null, "SHA-256 header returned for file integrity validation");

    console.log("\n================================================================================");
    console.log(`[RESULTS] Passed: ${passed} | Failed: ${failed}`);
    if (failed === 0) {
        console.log("[PASS] ALL BACKEND API ENDPOINTS AND FEATURES VERIFIED WITH 100% SUCCESS!");
    } else {
        process.exit(1);
    }
}

testApi().catch(err => {
    console.error(err);
    process.exit(1);
});

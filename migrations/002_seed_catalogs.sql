-- ============================================================================
-- NAGEEL (نَقِيل) — Phase 3: Catalogs & Seed Data Migration
-- ============================================================================

-- 1. SEED VEHICLES
INSERT INTO vehicle_catalog (id, arabic_name, model_class, mass_kg, max_torque_nm, peak_torque_rpm, top_speed_kmh, drive_type, price_coins, is_default_unlocked, specs)
VALUES
(
    'suv_landcruiser_4wd',
    'لاندكروزر 4WD جبلية',
    'SUV',
    2150.0,
    480.0,
    3200.0,
    165.0,
    'AWD_4WD',
    0,
    TRUE,
    '{"clearance_m": 0.42, "crawl_ratio": 38.94, "gears": 5, "fuel_tank_l": 90, "features": ["low_range_transfer_case", "hill_start_assist", "bullbar", "roof_rack"]}'::jsonb
),
(
    'sedan_touring',
    'سيدان سياحية رياضية',
    'SEDAN',
    1480.0,
    320.0,
    3800.0,
    195.0,
    'RWD_2WD',
    1500,
    TRUE,
    '{"clearance_m": 0.22, "crawl_ratio": 12.59, "gears": 6, "fuel_tank_l": 65, "features": ["sport_suspension", "aerodynamic_spoiler", "abs"]}'::jsonb
),
(
    'pickup_shas_4x4',
    'شاص 4WD للأحمال الصعبة',
    'PICKUP',
    1950.0,
    440.0,
    3000.0,
    155.0,
    'AWD_4WD',
    3000,
    TRUE,
    '{"clearance_m": 0.44, "crawl_ratio": 17.85, "gears": 5, "fuel_tank_l": 130, "features": ["leaf_springs", "tubular_roll_bar", "heavy_bed", "snorkel"]}'::jsonb
)
ON CONFLICT (id) DO UPDATE SET
    arabic_name = EXCLUDED.arabic_name,
    mass_kg = EXCLUDED.mass_kg,
    max_torque_nm = EXCLUDED.max_torque_nm,
    specs = EXCLUDED.specs;

-- 2. SEED MAP ZONES (10.5 km Total Continuous World)
INSERT INTO map_catalog (id, arabic_name, zone_index, start_dist_m, end_dist_m, elevation_min_m, elevation_max_m, road_type, surface_friction, dlc_package_id, metadata)
VALUES
(
    'naqil_pass_sumarah',
    'نَقِيل سَمَارَة الجبلي',
    1,
    0.0,
    3500.0,
    2400.0,
    2850.0,
    'أسفلت جبلي وعر',
    1.00,
    'dlc_yemen_naqil_core',
    '{"hairpin_count": 8, "cliff_barrier": true, "landmarks": ["حصن سمارة", "منعطف العقبة الكبير"]}'::jsonb
),
(
    'green_highlands_terraces',
    'المرتفعات الخضراء والمدرجات الزراعية',
    2,
    3500.0,
    7000.0,
    2850.0,
    2980.0,
    'طريق قروي معبد وضبابي',
    0.92,
    'dlc_yemen_highlands',
    '{"fog_density": "high", "terraces": true, "landmarks": ["قرية حجرية", "مدرجات البن"]}'::jsonb
),
(
    'rocky_wadi_canyon',
    'وادي الصخور والممرات الضيقة',
    3,
    7000.0,
    10500.0,
    2420.0,
    2850.0,
    'مسار ترابي وصخري غير معبد',
    0.72,
    'dlc_yemen_wadi',
    '{"dust_storm": true, "natural_boulders": true, "landmarks": ["مضيق الوادي", "مخاضة السيل"]}'::jsonb
)
ON CONFLICT (id) DO UPDATE SET
    arabic_name = EXCLUDED.arabic_name,
    road_type = EXCLUDED.road_type,
    surface_friction = EXCLUDED.surface_friction,
    metadata = EXCLUDED.metadata;

-- 3. SEED ASSET MANIFESTS (Neon Object Storage & CDN)
INSERT INTO asset_manifests (id, asset_type, file_name, version, file_size_bytes, sha256_checksum, object_storage_url, cdn_url, is_mandatory, metadata)
VALUES
(
    'pkg_vehicles_v1',
    'VEHICLE',
    'vehicles_bundle_arm64.pkg',
    '2.0.0',
    3355443, -- ~3.2 MB
    'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
    'https://storage.neon.tech/dry-surf-72406677/development/nageel-assets/vehicles_bundle_arm64.pkg',
    'https://cdn.nageel.app/assets/v2/vehicles_bundle_arm64.pkg',
    TRUE,
    '{"vehicle_count": 3, "format": "addressables_lz4"}'::jsonb
),
(
    'pkg_maps_v1',
    'MAP',
    'maps_10km_world.pkg',
    '2.0.0',
    4718592, -- ~4.5 MB
    'c2a98f12d49e89b21f925b302c4623194098495ab9e248b1d9894e33214878b2',
    'https://storage.neon.tech/dry-surf-72406677/development/nageel-assets/maps_10km_world.pkg',
    'https://cdn.nageel.app/assets/v2/maps_10km_world.pkg',
    TRUE,
    '{"total_length_km": 10.5, "zones": 3}'::jsonb
),
(
    'pkg_textures_pbr_v1',
    'TEXTURE',
    'materials_pbr_textures.pkg',
    '2.0.0',
    2936012, -- ~2.8 MB
    '8794827598ab12e0984712093847012938402938409283409283049283049283',
    'https://storage.neon.tech/dry-surf-72406677/development/nageel-assets/materials_pbr_textures.pkg',
    'https://cdn.nageel.app/assets/v2/materials_pbr_textures.pkg',
    TRUE,
    '{"compression": "etc2_astc", "quality": "mobile_optimized"}'::jsonb
),
(
    'pkg_audio_v1',
    'AUDIO',
    'audio_engine_weather.pkg',
    '2.0.0',
    1887436, -- ~1.8 MB
    '1298374918237491827394817293847192837491827394817293847192837491',
    'https://storage.neon.tech/dry-surf-72406677/development/nageel-assets/audio_engine_weather.pkg',
    'https://cdn.nageel.app/assets/v2/audio_engine_weather.pkg',
    TRUE,
    '{"channels": 2, "sample_rate": 44100, "codec": "vorbis"}'::jsonb
),
(
    'pkg_dlc_highlands_v1',
    'DLC',
    'dlc_highlands_extra.pkg',
    '2.0.0',
    2202009, -- ~2.1 MB
    'f394820394820394820394820394820394820394820394820394820394820394',
    'https://storage.neon.tech/dry-surf-72406677/development/nageel-assets/dlc_highlands_extra.pkg',
    'https://cdn.nageel.app/assets/v2/dlc_highlands_extra.pkg',
    FALSE,
    '{"optional": true, "dlc_id": "dlc_yemen_highlands"}'::jsonb
)
ON CONFLICT (id) DO UPDATE SET
    version = EXCLUDED.version,
    file_size_bytes = EXCLUDED.file_size_bytes,
    sha256_checksum = EXCLUDED.sha256_checksum,
    updated_at = NOW();

-- 4. SEED SAMPLE PILOT DRIVER
INSERT INTO player_profiles (id, username, display_name, device_id)
VALUES
(
    '11111111-2222-3333-4444-555555555555',
    'captain_nageel',
    'كابتن نَقِيل اليمني',
    'device_demo_arm64_nageel'
)
ON CONFLICT (device_id) DO NOTHING;

INSERT INTO player_progress (player_id, level, xp, coins, unlocked_zones, current_zone, completed_missions, total_distance_m)
VALUES
(
    '11111111-2222-3333-4444-555555555555',
    1,
    0,
    2500,
    ARRAY['naqil_pass_sumarah', 'green_highlands_terraces', 'rocky_wadi_canyon'],
    'naqil_pass_sumarah',
    0,
    0.0
)
ON CONFLICT (player_id) DO NOTHING;

INSERT INTO player_garage (player_id, vehicle_id, is_selected, paint_color)
VALUES
(
    '11111111-2222-3333-4444-555555555555',
    'suv_landcruiser_4wd',
    TRUE,
    '#E8E0D2'
)
ON CONFLICT (player_id, vehicle_id) DO NOTHING;

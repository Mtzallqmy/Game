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

-- Asset manifest rows are published ONLY after genuine Unity bundles exist,
-- have been uploaded and hashed with SHA-256. Do not seed fabricated URLs/checksums.
-- Never seed a predictable demo identity in a production-facing database.

-- ============================================================================
-- NAGEEL (نَقِيل) — Phase 3: Production Schema Migration
-- Target Database: nageel_db
-- Target Branch: development (Protected: main is untouched)
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- 1. PLAYER PROFILES TABLE
-- Stores unique identity, device linkage, and account metadata
CREATE TABLE IF NOT EXISTS player_profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(64) NOT NULL,
    display_name VARCHAR(128) NOT NULL,
    avatar_id VARCHAR(32) DEFAULT 'driver_yemeni_default',
    device_id VARCHAR(128) NOT NULL UNIQUE,
    preferred_language VARCHAR(8) DEFAULT 'ar',
    is_banned BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_player_profiles_device ON player_profiles(device_id);
CREATE INDEX IF NOT EXISTS idx_player_profiles_created ON player_profiles(created_at DESC);

-- 2. VEHICLE CATALOG TABLE
-- Defines vehicles, physics specs, drivetrain, and store pricing
CREATE TABLE IF NOT EXISTS vehicle_catalog (
    id VARCHAR(64) PRIMARY KEY,
    arabic_name VARCHAR(128) NOT NULL,
    model_class VARCHAR(32) NOT NULL, -- SUV, SEDAN, PICKUP
    mass_kg FLOAT NOT NULL CHECK (mass_kg > 0),
    max_torque_nm FLOAT NOT NULL CHECK (max_torque_nm > 0),
    peak_torque_rpm FLOAT NOT NULL DEFAULT 3200,
    top_speed_kmh FLOAT NOT NULL CHECK (top_speed_kmh > 0),
    drive_type VARCHAR(32) NOT NULL DEFAULT '4WD',
    price_coins INT NOT NULL DEFAULT 0 CHECK (price_coins >= 0),
    is_default_unlocked BOOLEAN NOT NULL DEFAULT FALSE,
    specs JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 3. MAP CATALOG TABLE
-- Defines the 3 continuous zones of the 10.5 km world network
CREATE TABLE IF NOT EXISTS map_catalog (
    id VARCHAR(64) PRIMARY KEY,
    arabic_name VARCHAR(128) NOT NULL,
    zone_index INT NOT NULL CHECK (zone_index >= 1 AND zone_index <= 3),
    start_dist_m FLOAT NOT NULL CHECK (start_dist_m >= 0),
    end_dist_m FLOAT NOT NULL CHECK (end_dist_m > start_dist_m),
    elevation_min_m FLOAT NOT NULL DEFAULT 2400,
    elevation_max_m FLOAT NOT NULL DEFAULT 2850,
    road_type VARCHAR(64) NOT NULL,
    surface_friction FLOAT NOT NULL CHECK (surface_friction BETWEEN 0.1 AND 1.5),
    dlc_package_id VARCHAR(64) NOT NULL,
    metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 4. PLAYER PROGRESS TABLE
-- Tracks player progression, coins, unlocked zones, and sync version
CREATE TABLE IF NOT EXISTS player_progress (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id UUID NOT NULL REFERENCES player_profiles(id) ON DELETE CASCADE,
    level INT NOT NULL DEFAULT 1 CHECK (level >= 1),
    xp INT NOT NULL DEFAULT 0 CHECK (xp >= 0),
    coins INT NOT NULL DEFAULT 2500 CHECK (coins >= 0),
    unlocked_zones TEXT[] NOT NULL DEFAULT ARRAY['naqil_pass_sumarah'],
    current_zone VARCHAR(64) NOT NULL DEFAULT 'naqil_pass_sumarah',
    completed_missions INT NOT NULL DEFAULT 0 CHECK (completed_missions >= 0),
    total_distance_m FLOAT NOT NULL DEFAULT 0.0 CHECK (total_distance_m >= 0),
    sync_version BIGINT NOT NULL DEFAULT 1,
    last_sync_timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_player_progress_player UNIQUE (player_id)
);

CREATE INDEX IF NOT EXISTS idx_player_progress_coins ON player_progress(coins DESC);
CREATE INDEX IF NOT EXISTS idx_player_progress_level ON player_progress(level DESC);

-- 5. PLAYER GARAGE TABLE
-- Tracks owned vehicles, selected vehicle, and upgrades
CREATE TABLE IF NOT EXISTS player_garage (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id UUID NOT NULL REFERENCES player_profiles(id) ON DELETE CASCADE,
    vehicle_id VARCHAR(64) NOT NULL REFERENCES vehicle_catalog(id) ON DELETE RESTRICT,
    is_selected BOOLEAN NOT NULL DEFAULT FALSE,
    paint_color VARCHAR(16) NOT NULL DEFAULT '#E8E0D2',
    engine_level INT NOT NULL DEFAULT 1 CHECK (engine_level BETWEEN 1 AND 5),
    suspension_level INT NOT NULL DEFAULT 1 CHECK (suspension_level BETWEEN 1 AND 5),
    custom_data JSONB NOT NULL DEFAULT '{}'::jsonb,
    acquired_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_player_garage_vehicle UNIQUE (player_id, vehicle_id)
);

CREATE INDEX IF NOT EXISTS idx_player_garage_player ON player_garage(player_id);

-- 6. DRIVING SESSIONS TABLE
-- Telemetry of completed drives, duration, speed, and rewards
CREATE TABLE IF NOT EXISTS driving_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id UUID NOT NULL REFERENCES player_profiles(id) ON DELETE CASCADE,
    vehicle_id VARCHAR(64) NOT NULL REFERENCES vehicle_catalog(id),
    game_mode VARCHAR(32) NOT NULL DEFAULT 'FreeRoam',
    duration_seconds FLOAT NOT NULL CHECK (duration_seconds > 0),
    distance_covered_m FLOAT NOT NULL CHECK (distance_covered_m >= 0),
    top_speed_kmh FLOAT NOT NULL DEFAULT 0.0,
    avg_speed_kmh FLOAT NOT NULL DEFAULT 0.0,
    clean_driving_score INT NOT NULL DEFAULT 1000,
    coins_earned INT NOT NULL DEFAULT 0 CHECK (coins_earned >= 0),
    xp_earned INT NOT NULL DEFAULT 0 CHECK (xp_earned >= 0),
    session_date TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_driving_sessions_player ON driving_sessions(player_id);
CREATE INDEX IF NOT EXISTS idx_driving_sessions_date ON driving_sessions(session_date DESC);

-- 7. PERSONAL RECORDS & LEADERBOARD TABLE
-- Best split times and challenge scores
CREATE TABLE IF NOT EXISTS personal_records (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id UUID NOT NULL REFERENCES player_profiles(id) ON DELETE CASCADE,
    track_id VARCHAR(64) NOT NULL,
    vehicle_id VARCHAR(64) NOT NULL REFERENCES vehicle_catalog(id),
    game_mode VARCHAR(32) NOT NULL,
    record_time_seconds FLOAT NOT NULL CHECK (record_time_seconds > 0),
    record_score INT NOT NULL DEFAULT 0,
    achieved_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_player_personal_record UNIQUE (player_id, track_id, game_mode)
);

CREATE INDEX IF NOT EXISTS idx_personal_records_leaderboard ON personal_records(track_id, record_time_seconds ASC);

-- 8. ASSET MANIFESTS TABLE (Neon Object Storage & CDN)
-- Manages Addressable packages, file checksums, download sizes, and CDN URLs
CREATE TABLE IF NOT EXISTS asset_manifests (
    id VARCHAR(64) PRIMARY KEY,
    asset_type VARCHAR(32) NOT NULL, -- VEHICLE, MAP, TEXTURE, AUDIO, DLC
    file_name VARCHAR(128) NOT NULL,
    version VARCHAR(32) NOT NULL DEFAULT '1.0.0',
    file_size_bytes BIGINT NOT NULL CHECK (file_size_bytes > 0),
    sha256_checksum VARCHAR(64) NOT NULL,
    object_storage_url TEXT NOT NULL,
    cdn_url TEXT NOT NULL,
    is_mandatory BOOLEAN NOT NULL DEFAULT TRUE,
    metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_asset_manifests_type ON asset_manifests(asset_type);

-- 9. SYNC OPERATIONS TABLE
-- Idempotency protection against duplicate requests and network drops
CREATE TABLE IF NOT EXISTS sync_operations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id UUID NOT NULL REFERENCES player_profiles(id) ON DELETE CASCADE,
    idempotency_key VARCHAR(128) NOT NULL UNIQUE,
    sync_type VARCHAR(32) NOT NULL DEFAULT 'PROGRESS_SYNC',
    status VARCHAR(32) NOT NULL DEFAULT 'COMPLETED', -- PENDING, COMPLETED, REJECTED
    client_timestamp TIMESTAMPTZ NOT NULL,
    server_timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    payload_hash VARCHAR(64) NOT NULL,
    response_payload JSONB
);

CREATE INDEX IF NOT EXISTS idx_sync_operations_idempotency ON sync_operations(idempotency_key);
CREATE INDEX IF NOT EXISTS idx_sync_operations_player ON sync_operations(player_id);

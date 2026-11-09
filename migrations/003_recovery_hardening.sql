-- NAGEEL recovery: no destructive changes; only apply on Neon development.
-- The account ID is from a VERIFIED identity-provider JWT, never from X-Device-Id.
ALTER TABLE player_profiles ADD COLUMN IF NOT EXISTS auth_subject TEXT;
CREATE UNIQUE INDEX IF NOT EXISTS uq_player_profiles_auth_subject
  ON player_profiles(auth_subject) WHERE auth_subject IS NOT NULL;

-- Prevent publishing placeholder or incomplete download manifests.
ALTER TABLE asset_manifests ADD COLUMN IF NOT EXISTS ready_for_download BOOLEAN NOT NULL DEFAULT FALSE;
CREATE INDEX IF NOT EXISTS idx_asset_manifests_published
  ON asset_manifests(ready_for_download) WHERE ready_for_download = TRUE;

-- Restrict future checksum data to actual hexadecimal SHA-256 values.
-- Existing rows are not deleted; the API only exposes ready_for_download rows.

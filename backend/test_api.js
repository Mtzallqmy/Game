'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { createApp } = require('./server');

function fakeDb() {
  return {
    async query(sql) {
      if (sql.includes('current_database()')) return { rows: [{ name: 'nageel_db' }] };
      if (sql.includes('coalesce(sum(file_size_bytes)')) return { rows: [{ packages: 0, bytes: '0' }] };
      if (sql.includes('FROM vehicle_catalog')) return { rows: [{ id: 'suv_landcruiser_4wd' }] };
      if (sql.includes('FROM map_catalog')) return { rows: [{ id: 'naqil_pass_sumarah' }] };
      if (sql.includes('FROM asset_manifests')) return { rows: [] };
      if (sql.includes('FROM personal_records')) return { rows: [] };
      throw new Error('Unexpected DB query: ' + sql);
    }
  };
}
const app = createApp({ db: fakeDb(), authenticate: async () => ({sub: 'verified-user-1'}) });

test('health confirms a real database query, not a fabricated status', async () => {
  const response = await app.request('/api/v1/health');
  assert.equal(response.status, 200);
  assert.equal((await response.json()).database_name, 'nageel_db');
});

test('catalog endpoints use SQL rows', async () => {
  let response = await app.request('/api/v1/vehicles');
  assert.equal((await response.json()).vehicles[0].id, 'suv_landcruiser_4wd');
  response = await app.request('/api/v1/maps');
  assert.equal((await response.json()).zones[0].id, 'naqil_pass_sumarah');
});

test('bootstrap does not claim that fabricated content was downloaded', async () => {
  const response = await app.request('/api/v1/bootstrap');
  assert.equal((await response.json()).mandatory_download.total_packages, 0);
});

test('unpublished packages return 404 rather than generated zero bytes', async () => {
  let response = await app.request('/api/v1/content/manifest');
  assert.deepEqual((await response.json()).packages, []);
  response = await app.request('/api/v1/cdn/download/pkg_maps_v1');
  assert.equal(response.status, 404);
});

test('player profile requires a bearer identity, not a device header', async () => {
  const response = await app.request('/api/v1/player/progress', {
    headers: { 'X-Device-Id': 'device_demo_arm64_nageel' }
  });
  assert.equal(response.status, 401);
});

test('client cannot modify coins/levels via the sync endpoint', async () => {
  const response = await app.request('/api/v1/player/sync', {
    method: 'POST',
    headers: { Authorization: 'Bearer stub', 'Content-Type': 'application/json' },
    body: JSON.stringify({ coins: 99999999, level: 10000, current_zone: 'naqil_pass_sumarah' })
  });
  assert.equal(response.status, 403);
  assert.equal((await response.json()).error, 'SERVER_OWNED_PROGRESS');
});

test('unverified client telemetry never produces rewards or records', async () => {
  const response = await app.request('/api/v1/drives/complete', {
    method: 'POST',
    headers: { Authorization: 'Bearer stub', 'Content-Type': 'application/json' },
    body: JSON.stringify({ distance_covered_m: 1e9, duration_seconds: 1, clean_driving_score: 9999 })
  });
  assert.equal(response.status, 501);
  assert.equal((await response.json()).error, 'DRIVE_VERIFICATION_NOT_IMPLEMENTED');
});

test('leaderboard defaults to verified database records', async () => {
  const response = await app.request('/api/v1/leaderboard');
  assert.deepEqual((await response.json()).leaderboard, []);
});

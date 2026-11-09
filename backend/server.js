'use strict';

// NAGEEL recovery API. All player writes are bound to a verified JWT subject.
// No device-header auth, no fabricated CDN bytes, no client-controlled rewards.
const { Hono } = require('hono');
const { serve } = require('@hono/node-server');
const { Pool } = require('pg');
const crypto = require('node:crypto');
require('dotenv').config({ path: require('node:path').join(__dirname, '..', '.env') });

const PORT = Number(process.env.PORT || 3000);
const AUTH_ISSUER = process.env.NEON_AUTH_ISSUER || process.env.NEON_AUTH_BASE_URL || '';
const AUTH_JWKS = process.env.NEON_AUTH_JWKS_URL || '';
const AUTH_AUDIENCE = process.env.NEON_AUTH_AUDIENCE || '';
const ALLOWED_ORIGINS = new Set((process.env.NAGEEL_ALLOWED_ORIGINS || '').split(',').map(s => s.trim()).filter(Boolean));
const CDN_HOSTS = new Set((process.env.NAGEEL_CDN_HOSTS || '').split(',').map(s => s.trim()).filter(Boolean));
const checksumRe = /^[a-f0-9]{64}$/i;
let jwksCache = { keys: [], until: 0 };

function database() {
  if (!process.env.DATABASE_URL) return null;
  const pool = new Pool({ connectionString: process.env.DATABASE_URL, max: 5, idleTimeoutMillis: 10000 });
  pool.on('error', err => console.error('[Nageel Pool]', err.message));
  return pool;
}

async function verifyJwt(token) {
  if (!AUTH_ISSUER || !AUTH_JWKS) throw new Error('AUTH_NOT_CONFIGURED');
  const parts = String(token).split('.');
  if (parts.length !== 3 || parts.some(p => !p)) throw new Error('INVALID_TOKEN');
  let header, payload;
  try {
    header = JSON.parse(Buffer.from(parts[0], 'base64url').toString('utf8'));
    payload = JSON.parse(Buffer.from(parts[1], 'base64url').toString('utf8'));
  } catch (_) { throw new Error('INVALID_TOKEN'); }
  if (!['EdDSA', 'RS256', 'ES256'].includes(header.alg) || !header.kid) throw new Error('INVALID_TOKEN');
  const now = Math.floor(Date.now() / 1000);
  if (!payload.sub || payload.iss !== AUTH_ISSUER || !Number.isFinite(payload.exp) ||
      payload.exp <= now - 30 || (payload.nbf && payload.nbf > now + 30) ||
      (payload.iat && payload.iat > now + 30)) throw new Error('INVALID_TOKEN');
  if (AUTH_AUDIENCE && ![payload.aud].flat().includes(AUTH_AUDIENCE)) throw new Error('INVALID_TOKEN');

  if (Date.now() >= jwksCache.until) {
    const url = new URL(AUTH_JWKS);
    if (url.protocol !== 'https:') throw new Error('AUTH_NOT_CONFIGURED');
    const response = await fetch(url, { signal: AbortSignal.timeout(5000) });
    if (!response.ok) throw new Error('AUTH_UNAVAILABLE');
    const data = await response.json();
    if (!Array.isArray(data.keys)) throw new Error('AUTH_UNAVAILABLE');
    jwksCache = { keys: data.keys, until: Date.now() + 5 * 60 * 1000 };
  }
  const jwk = jwksCache.keys.find(k => k.kid === header.kid && (!k.alg || k.alg === header.alg) && (!k.use || k.use === 'sig'));
  if (!jwk) throw new Error('INVALID_TOKEN');
  if (header.alg === 'EdDSA' && jwk.kty !== 'OKP') throw new Error('INVALID_TOKEN');
  if (header.alg === 'RS256' && jwk.kty !== 'RSA') throw new Error('INVALID_TOKEN');
  if (header.alg === 'ES256' && jwk.kty !== 'EC') throw new Error('INVALID_TOKEN');
  const key = crypto.createPublicKey({ key: jwk, format: 'jwk' });
  const algorithm = header.alg === 'RS256' ? 'RSA-SHA256' : (header.alg === 'ES256' ? 'sha256' : null);
  const valid = crypto.verify(algorithm, Buffer.from(parts[0] + '.' + parts[1]),
    { key, dsaEncoding: 'ieee-p1363' }, Buffer.from(parts[2], 'base64url'));
  if (!valid) throw new Error('INVALID_TOKEN');
  return { sub: String(payload.sub) };
}

async function getOrCreatePlayer(client, sub) {
  const digest = crypto.createHash('sha256').update(sub).digest('hex');
  const profileResult = await client.query(
    `INSERT INTO player_profiles(username,display_name,device_id,auth_subject)
      VALUES ($1,$2,$3,$4)
      ON CONFLICT (auth_subject) WHERE auth_subject IS NOT NULL DO UPDATE SET updated_at=NOW()
      RETURNING id,username,display_name,avatar_id,preferred_language`,
    ['driver_' + digest.slice(0, 12), 'سائق نَقِيل', 'auth_' + digest, sub]
  );
  const profile = profileResult.rows[0];
  await client.query(
    `INSERT INTO player_progress(player_id) VALUES ($1)
      ON CONFLICT (player_id) DO NOTHING`, [profile.id]);
  await client.query(
    `INSERT INTO player_garage(player_id,vehicle_id,is_selected)
      SELECT $1,id,TRUE FROM vehicle_catalog WHERE id='suv_landcruiser_4wd'
      ON CONFLICT(player_id,vehicle_id) DO NOTHING`, [profile.id]);
  return profile;
}

function createApp({ db = database(), authenticate = verifyJwt } = {}) {
  const app = new Hono();

  app.use('*', async (c, next) => {
    c.header('X-Content-Type-Options', 'nosniff');
    c.header('X-Frame-Options', 'DENY');
    c.header('Cache-Control', 'no-store');
    const origin = c.req.header('Origin');
    if (origin && ALLOWED_ORIGINS.has(origin)) {
      c.header('Access-Control-Allow-Origin', origin);
      c.header('Vary', 'Origin');
      c.header('Access-Control-Allow-Headers', 'Content-Type, Authorization');
      c.header('Access-Control-Allow-Methods', 'GET,POST,OPTIONS');
    }
    if (c.req.method === 'OPTIONS') return c.body(null, 204);
    await next();
  });

  app.use('/api/v1/player/*', async (c, next) => {
    const token = /^Bearer\s+(.+)$/i.exec(c.req.header('Authorization') || '');
    if (!token) return c.json({ error: 'AUTH_REQUIRED' }, 401);
    try { c.set('user', await authenticate(token[1])); }
    catch (e) {
      if (e.message === 'AUTH_NOT_CONFIGURED' || e.message === 'AUTH_UNAVAILABLE')
        return c.json({ error: 'AUTH_SERVICE_UNAVAILABLE' }, 503);
      return c.json({ error: 'INVALID_TOKEN' }, 401);
    }
    await next();
  });
  app.use('/api/v1/drives/*', async (c, next) => {
    const token = /^Bearer\s+(.+)$/i.exec(c.req.header('Authorization') || '');
    if (!token) return c.json({ error: 'AUTH_REQUIRED' }, 401);
    try { c.set('user', await authenticate(token[1])); }
    catch (_) { return c.json({ error: 'INVALID_TOKEN' }, 401); }
    await next();
  });

  app.onError((err, c) => {
    console.error('[Nageel API]', err?.message || 'Internal error');
    return c.json({ error: 'INTERNAL_SERVER_ERROR' }, 500);
  });

  app.get('/api/v1/health', async c => {
    if (!db) return c.json({ online: false, database: false }, 503);
    try {
      const r = await db.query('SELECT current_database() AS name');
      return c.json({ online: true, database: true, database_name: r.rows[0].name });
    } catch (_) { return c.json({ online: false, database: false }, 503); }
  });

  app.get('/api/v1/bootstrap', async c => {
    if (!db) return c.json({ error: 'DATABASE_NOT_CONFIGURED' }, 503);
    const r = await db.query(`SELECT count(*)::int AS packages,
      coalesce(sum(file_size_bytes), 0)::bigint AS bytes
      FROM asset_manifests WHERE ready_for_download=TRUE AND is_mandatory=TRUE`);
    const p = r.rows[0];
    return c.json({
      success: true,
      game_title: 'نَقِيل — قيادة جبال اليمن',
      server_version: '2.1.0-recovery',
      mandatory_download: { total_packages: p.packages, total_size_bytes: Number(p.bytes) },
      server_time_utc: new Date().toISOString()
    });
  });

  app.get('/api/v1/vehicles', async c => {
    if (!db) return c.json({ error: 'DATABASE_NOT_CONFIGURED' }, 503);
    const r = await db.query('SELECT * FROM vehicle_catalog ORDER BY price_coins, id LIMIT 100');
    return c.json({ success: true, vehicles: r.rows });
  });
  app.get('/api/v1/maps', async c => {
    if (!db) return c.json({ error: 'DATABASE_NOT_CONFIGURED' }, 503);
    const r = await db.query('SELECT * FROM map_catalog ORDER BY zone_index LIMIT 100');
    return c.json({ success: true, zones: r.rows });
  });
  app.get('/api/v1/content/manifest', async c => {
    if (!db) return c.json({ error: 'DATABASE_NOT_CONFIGURED' }, 503);
    const r = await db.query(`SELECT id,asset_type,file_name,version,file_size_bytes,
      sha256_checksum,cdn_url,is_mandatory FROM asset_manifests
      WHERE ready_for_download=TRUE ORDER BY asset_type,id LIMIT 200`);
    const packages = r.rows.filter(p => checksumRe.test(p.sha256_checksum) && Number(p.file_size_bytes) > 0).map(p => ({ ...p, file_size_bytes: Number(p.file_size_bytes) }));
    return c.json({ success: true, packages, total_count: packages.length });
  });

  app.get('/api/v1/cdn/download/:packageId', async c => {
    if (!db) return c.json({ error: 'DATABASE_NOT_CONFIGURED' }, 503);
    const id = c.req.param('packageId');
    if (!/^[a-z0-9_-]{1,64}$/.test(id)) return c.json({ error: 'INVALID_PACKAGE_ID' }, 400);
    const r = await db.query(`SELECT cdn_url,sha256_checksum FROM asset_manifests
      WHERE id=$1 AND ready_for_download=TRUE`, [id]);
    if (!r.rows.length) return c.json({ error: 'PACKAGE_NOT_PUBLISHED' }, 404);
    let url;
    try { url = new URL(r.rows[0].cdn_url); } catch (_) { return c.json({ error: 'INVALID_ASSET_URL' }, 503); }
    if (url.protocol !== 'https:' || !CDN_HOSTS.has(url.hostname) ||
        !checksumRe.test(r.rows[0].sha256_checksum)) return c.json({ error: 'ASSET_HOST_NOT_APPROVED' }, 503);
    return c.redirect(url.toString(), 307);
  });

  app.get('/api/v1/player/progress', async c => {
    if (!db) return c.json({ error: 'DATABASE_NOT_CONFIGURED' }, 503);
    const client = await db.connect();
    try {
      await client.query('BEGIN');
      const profile = await getOrCreatePlayer(client, c.get('user').sub);
      const progress = await client.query('SELECT * FROM player_progress WHERE player_id=$1', [profile.id]);
      const garage = await client.query(`SELECT vehicle_id,is_selected,paint_color,engine_level,suspension_level
        FROM player_garage WHERE player_id=$1 ORDER BY acquired_at`, [profile.id]);
      await client.query('COMMIT');
      return c.json({ success: true, player: { ...profile, ...progress.rows[0], garage: garage.rows } });
    } catch (e) { await client.query('ROLLBACK'); throw e; }
    finally { client.release(); }
  });

  // Only non-economic settings may be synchronized from an untrusted client.
  app.post('/api/v1/player/sync', async c => {
    if (!db) return c.json({ error: 'DATABASE_NOT_CONFIGURED' }, 503);
    const body = await c.req.json().catch(() => null);
    if (!body || typeof body !== 'object' || Array.isArray(body))
      return c.json({ error: 'INVALID_PAYLOAD' }, 400);
    const allowed = new Set(['current_zone']);
    if (Object.keys(body).some(k => !allowed.has(k)))
      return c.json({ error: 'SERVER_OWNED_PROGRESS', message: 'العملات والخبرة والمكافآت تحت سيطرة الخادم فقط' }, 403);
    if (typeof body.current_zone !== 'string' || !/^[a-z0-9_]{1,64}$/.test(body.current_zone))
      return c.json({ error: 'INVALID_ZONE' }, 400);
    const client = await db.connect();
    try {
      await client.query('BEGIN');
      const profile = await getOrCreatePlayer(client, c.get('user').sub);
      const updated = await client.query(`UPDATE player_progress SET current_zone=$2,
        sync_version=sync_version+1,updated_at=NOW()
        WHERE player_id=$1 AND $2=ANY(unlocked_zones)
        RETURNING level,xp,coins,current_zone,sync_version`, [profile.id,body.current_zone]);
      if (!updated.rows.length) {
        await client.query('ROLLBACK');
        return c.json({ error: 'ZONE_NOT_UNLOCKED' }, 403);
      }
      await client.query('COMMIT');
      return c.json({ success: true, synced: true, player: updated.rows[0] });
    } catch (e) { await client.query('ROLLBACK'); throw e; }
    finally { client.release(); }
  });

  // No reward/leaderboard credit until the authoritative anti-cheat drive verifier exists.
  app.post('/api/v1/drives/complete', async c => c.json({
    error: 'DRIVE_VERIFICATION_NOT_IMPLEMENTED',
    message: 'لا يمكن اعتماد مكافآت من بيانات رحلة يرسلها العميل دون تحقق موثوق'
  }, 501));

  app.get('/api/v1/leaderboard', async c => {
    if (!db) return c.json({ error: 'DATABASE_NOT_CONFIGURED' }, 503);
    const mode = c.req.query('mode') || 'HillClimbTimeAttack';
    if (!['HillClimbTimeAttack', 'DeliveryMission'].includes(mode))
      return c.json({ error: 'INVALID_MODE' }, 400);
    const r = await db.query(`SELECT p.display_name AS player_name,r.track_id,r.vehicle_id,
       r.record_time_seconds AS time_seconds,r.record_score AS score
       FROM personal_records r JOIN player_profiles p ON p.id=r.player_id
       WHERE r.game_mode=$1 ORDER BY r.record_time_seconds ASC LIMIT 10`, [mode]);
    return c.json({ success: true, mode,
      leaderboard: r.rows.map((entry,i)=>({rank:i+1,...entry})) });
  });
  return app;
}

const app = createApp();
if (require.main === module) {
  if (!process.env.DATABASE_URL) console.error('DATABASE_URL missing: API will return 503 for DB endpoints.');
  serve({ fetch: app.fetch, port: PORT });
  console.log('NAGEEL API listening on port ' + PORT);
}
module.exports = { app, createApp, verifyJwt };

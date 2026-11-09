'use strict';
// NAGEEL: real migrations only; fail closed. Never mark an unexecuted migration as a success.
const fs = require('node:fs');
const path = require('node:path');
const { Client } = require('pg');
require('dotenv').config({path:path.join(__dirname,'..','.env')});

const EXPECTED_BRANCH = 'br-orange-pond-badvwqgc';
const EXPECTED_HOST = 'ep-odd-forest-ba7sm41n.c-8.eu-central-1.aws.neon.tech';
const files = ['001_initial_schema.sql','002_seed_catalogs.sql','003_recovery_hardening.sql'];

async function main() {
  if (process.env.NEON_BRANCH !== 'development' || process.env.NEON_BRANCH_ID !== EXPECTED_BRANCH)
    throw new Error('Refusing migration: development branch ID/name not confirmed');
  const raw = process.env.DATABASE_URL_UNPOOLED || process.env.DATABASE_URL;
  if (!raw) throw new Error('Missing DATABASE_URL_UNPOOLED or DATABASE_URL');
  const url = new URL(raw);
  if (url.hostname !== EXPECTED_HOST || url.pathname !== '/nageel_db')
    throw new Error('Refusing migration: target is not the verified development endpoint');
  if (!raw.includes('sslmode=require')) throw new Error('TLS is required');

  const client = new Client({connectionString:raw});
  await client.connect();
  try {
    const db = await client.query('SELECT current_database() AS name');
    if (db.rows[0].name !== 'nageel_db') throw new Error('Wrong target database');
    await client.query('BEGIN');
    for (const name of files) {
      const sql = fs.readFileSync(path.join(__dirname,name),'utf8');
      await client.query(sql);
      console.log('Applied: '+name);
    }
    const checks = await client.query(`SELECT
      (SELECT count(*)::int FROM information_schema.tables WHERE table_schema='public' AND
        table_name=ANY(ARRAY['player_profiles','vehicle_catalog','map_catalog',
        'player_progress','player_garage','driving_sessions','personal_records',
        'asset_manifests','sync_operations'])) AS tables,
      (SELECT count(*)::int FROM vehicle_catalog) AS vehicles,
      (SELECT count(*)::int FROM map_catalog) AS maps`);
    if (checks.rows[0].tables !== 9 || checks.rows[0].vehicles < 3 || checks.rows[0].maps < 3)
      throw new Error('Post-migration verification failed');
    await client.query('COMMIT');
    console.log('VERIFIED live development schema:', checks.rows[0]);
  } catch (error) {
    await client.query('ROLLBACK');
    throw error;
  } finally { await client.end(); }
}
main().catch(e => { console.error('MIGRATION FAILED:', e.message); process.exitCode=1; });

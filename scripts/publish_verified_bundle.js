'use strict';
// Publish manifest metadata ONLY after both local AND remotely hosted bytes match.
// Usage: node scripts/publish_verified_bundle.js <path> <id> <TYPE> <HTTPS_URL> [mandatory|optional]
// Upload the real Unity Addressables bundle yourself first; this command never fabricates one.
const fs = require('node:fs');
const crypto = require('node:crypto');
const { Pool } = require('pg');
require('dotenv').config();

const EXPECTED_HOST = 'ep-odd-forest-ba7sm41n.c-8.eu-central-1.aws.neon.tech';
const validTypes = new Set(['VEHICLE','MAP','TEXTURE','AUDIO','DLC','CATALOG']);

async function hashFile(filename) {
  const hash = crypto.createHash('sha256');
  for await (const chunk of fs.createReadStream(filename)) hash.update(chunk);
  return hash.digest('hex');
}

async function main() {
  const [filename, packageId, assetType, urlText, requiredText='mandatory'] = process.argv.slice(2);
  if (!filename || !packageId || !assetType || !urlText)
    throw new Error('Usage: publish_verified_bundle.js <local-file> <id> <TYPE> <HTTPS_URL> [mandatory|optional]');
  if (!/^[a-z0-9_-]{1,64}$/.test(packageId) || !validTypes.has(assetType))
    throw new Error('Invalid package ID/type');
  if (!['mandatory','optional'].includes(requiredText)) throw new Error('Invalid mandatory flag');

  const allowed = new Set((process.env.NAGEEL_CDN_HOSTS || '').split(',').map(x=>x.trim()).filter(Boolean));
  const url = new URL(urlText);
  if (url.protocol !== 'https:' || !allowed.has(url.hostname)) throw new Error('CDN host must be HTTPS and approved');
  const base = require('node:path').basename(filename);
  if (!/^[A-Za-z0-9_.-]{1,128}$/.test(base) || base.includes('..')) throw new Error('Unsafe file name');
  const stat = fs.statSync(filename);
  if (!stat.isFile() || stat.size < 1024) throw new Error('Bundle is too small to publish');
  const localSha = await hashFile(filename);

  // We must inspect the FULL remote file, not only HTTP headers / Content-Length.
  const response = await fetch(url, {signal:AbortSignal.timeout(120000)});
  if (!response.ok || !response.body) throw new Error('Remote bundle GET failed: ' + response.status);
  const remoteHash = crypto.createHash('sha256');
  let remoteBytes = 0;
  for await (const chunk of response.body) { remoteBytes += chunk.byteLength; remoteHash.update(chunk); }
  const remoteSha = remoteHash.digest('hex');
  if (remoteBytes !== stat.size || remoteSha !== localSha)
    throw new Error('SHA-256/size mismatch between verified local and remote bytes');

  if (process.env.NEON_BRANCH !== 'development' ||
      process.env.NEON_BRANCH_ID !== 'br-orange-pond-badvwqgc')
    throw new Error('Refusing publication outside development branch');
  const raw = process.env.DATABASE_URL;
  if (!raw || new URL(raw).hostname !== EXPECTED_HOST)
    throw new Error('Refusing publication to a different database endpoint');
  const db = new Pool({connectionString:raw, max:1});
  try {
    await db.query(`INSERT INTO asset_manifests
      (id,asset_type,file_name,version,file_size_bytes,sha256_checksum,
       object_storage_url,cdn_url,is_mandatory,ready_for_download)
      VALUES ($1,$2,$3,$4,$5,$6,$7,$7,$8,TRUE)
      ON CONFLICT(id) DO UPDATE SET
        asset_type=EXCLUDED.asset_type,file_name=EXCLUDED.file_name,
        version=EXCLUDED.version,file_size_bytes=EXCLUDED.file_size_bytes,
        sha256_checksum=EXCLUDED.sha256_checksum,
        object_storage_url=EXCLUDED.object_storage_url,cdn_url=EXCLUDED.cdn_url,
        is_mandatory=EXCLUDED.is_mandatory,ready_for_download=TRUE,updated_at=NOW()`,
      [packageId,assetType,base,process.env.NAGEEL_ASSET_VERSION || 'recovery-1',
       stat.size,localSha,url.toString(),requiredText === 'mandatory']);
    console.log('PUBLISHED verified real bundle:',packageId,'bytes=',stat.size,'SHA256=',localSha);
  } finally { await db.end(); }
}
main().catch(e=>{console.error('PUBLICATION FAILED:',e.message);process.exitCode=1;});

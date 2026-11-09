# نَقِيل | NAGEEL — Real 3D Recovery / Implementation Report

Date: 2026-11-09 · Git branch: `feat/nageel-real-3d-recovery` (from `b462b822`)

Owner credit: **برمجة وتطوير معتز العلقمي – تعز – اليمن – 2026م**

## What was actually changed

| Area | Actual result | Verification state |
| --- | --- | --- |
| Git safety | Dedicated feature branch; original Kotlin Canvas and historical Unity scenes preserved | Confirmed via GitHub |
| Database | Applied 001, safe 002 (vehicles/maps only), 003 directly to Neon `development`, db `nageel_db` | 9 public game tables, 3 vehicles, 3 maps checked live |
| Identity | Enabled Managed Neon Auth (Better Auth) on `development`; backend verifies signed JWTs, no `X-Device-Id` authorization | Provisioned, but client sign-in and E2E login are NOT tested |
| Storage | Created `nageel-addressables` bucket, `public_read`, on `development` | Bucket exists; no actual bundles uploaded |
| API | Replaced in-memory `databaseState` with parameterized PostgreSQL queries and protected player routes | Source implemented; API not yet deployed/live-smoke-tested |
| Rewards | Rejects unsigned/unauthoritative drives with HTTP 501; client may not increase server coins | Security behavior implemented, reward verification remains TODO |
| Manifest | Deleted placeholder package seeds and demo player. `ready_for_download` must be TRUE only after upload+hash | Live DB currently has 0 published packages |
| Downloads | Real HTTPS Range, partial files, full SHA-256 verification, verified local receipt. No dummy byte generation | Source implemented; needs real hosted Unity bundles/device tests |
| Unity | Unity 6 scene generator produces 1.8 km 3D mountain road mesh/collider, 4-wheel Rigidbody SUV prototype, camera, touch controls, generated URP materials, menu | Source committed; not executed (Unity Editor unavailable here) |
| Addressables | Added package, Editor catalog builder, runtime load probe | Requires Unity Editor to generate/build/validate catalog |
| CI | Added backend security behavior tests and GitHub Actions workflow | Must inspect GitHub run before calling it green |
| APK | Build menu action configured for Android 8+, ARM64, IL2CPP | NO Unity build, signed APK, or device verification completed |

## Engineering boundaries

- **No Unreal/Unity Editor is installed in the automation runtime used for this commit.** All `.unity`, `.meta`, Addressables builds, asset imports, and native APK verification remain dependent on a Unity 6 editor run.
- The new `RecoveryMainMenu.unity`, `RecoveryDrive.unity`, generated `.asset` meshes, materials and `.meta` files will be created by the included Unity Editor builder **when run**. They are not in the Git branch yet. Do not report them as committed built scenes.
- Existing `Assets/Scenes/MainMenu.unity`, `Assets/Scenes/NaqilMountainPass.unity`, and Kotlin Canvas source are intentionally preserved for rollback, not presented as the 3D release.
- The generated SUV and procedural textures are technical placeholder geometry. Commission/replace with licensed photoreal vehicle, recorded engine audio, road/rock PBR textures, terrain/props and tuned URP/LOD before art milestone.
- Neon MCP is for development administration, **not** an Android game API. Set `apiBaseUrl` to the deployed HTTPS Game API, which must implement `/api/v1` endpoints.
- Neon Managed Auth has been provisioned, but a Unity mobile sign-in UI/token acquisition flow has not been built or verified. The API refuses player requests without signed JWTs.
- Online credits, verified rewards, leaderboards and cloud saves are not complete until server-authoritative drive verification and identity E2E tests are built.
- Remote download manifests are intentionally EMPTY until actual Addressables bundles are exported, hosted, remote hashes verified, and published via the verified publisher. This is safer than returning fake 100% download progress.

## How to generate and test the real Unity recovery scenes

1. Install **Unity 6.0.23f1** with Android Build Support (Android SDK, NDK and OpenJDK), check out this feature branch, and open the repository root in Unity Hub.
2. Wait until Unity has imported packages (including URP 17.0.3 and Addressables) and generated valid `.meta` files. Resolve **any** import/compiler errors first.
3. Run menu **NAGEEL > Recovery > 1 - Generate Playable 3D Scenes**. It creates `Assets/Scenes/RecoveryMainMenu.unity`, `Assets/Scenes/RecoveryDrive.unity`, a 1.8 km mesh with collision, scene prefab and real URP assets. It does not overwrite the original historical scenes.
4. Run **NAGEEL > Recovery > 3 - Build Real Addressables Catalog** to build a **LOCAL** catalog for the recovery vehicle. Confirm `RecoveryAddressablesRuntime.catalogAssetLoaded` in Play Mode.
5. Open `RecoveryDrive.unity`; verify car forward motion, steering with simultaneous multi-touch, wheel contacts, suspension, slopes, brakes, reset and all camera views. Run the Unity Editor NUnit tests. Fix any failures before enabling release.
6. Commit generated source-controlled `.unity`, `.prefab`, `.asset`, `.meta`, `AddressableAssetsData` data and Unity-compatible project settings. Never commit `Library/`, `Temp/` or credentials.
7. Switch **Build Profiles > Android**, then use **NAGEEL > Recovery > 2 - Build Android ARM64 APK**. The tool throws on a failed build. Validate APK using `aapt dump badging` and `apksigner verify`; test install, driving, memory/FPS on real Android 8+ ARM64 hardware. Signing requires the existing, securely supplied keystore and key alias.

### Backend local setup

```bash
cp .env.example .env
# Insert the REAL DATABASE_URL for the Neon development branch; do not commit .env
npm ci --prefix backend
npm test --prefix backend
npm run start --prefix backend
# GET http://localhost:3000/api/v1/health => 200 ONLY if actual PostgreSQL responds
```

`backend/server.js` and `backend/test_api.js` are the authoritative API. 
The `migrations/run_migrations.js` runner fails closed when the required development hostname/branch do not match; it does not claim success for migrations it did not execute.

### How to publish a REAL asset bundle

1. Build a proper Unity Addressables remote group and remote catalog; do not copy `.obj` placeholders pretending they are addressable binaries.
2. Upload exported bundle to the **development** `nageel-addressables` bucket or an approved HTTPS CDN. Make it publicly readable or use an authorized resolver; never ship admin credentials in the Android APK.
3. Put `NAGEEL_CDN_HOSTS`, `NEON_BRANCH=development`, `NEON_BRANCH_ID` and real dev `DATABASE_URL` in the secure backend environment.
4. Run this after the upload, with a real file and its real HTTPS URL:

```bash
npm run publish-assets --prefix backend -- \
  /absolute/path/to/real_bundle.bundle pkg_vehicles_v1 VEHICLE \
  https://YOUR_APPROVED_HOST/real_bundle.bundle mandatory
```

The publisher streams **every remote byte** and compares its SHA-256 and size to the local bundle before inserting/updating `asset_manifests` with `ready_for_download=TRUE`. Until then, package count stays zero.

## Follow-up work (not yet completed)

- Unity build/run and visual QA, professional 3D car and Yemen-world art, final Arabic typography/audio.
- Production-quality authentication UI and API deployment in a reachable HTTPS host; test real sign-in and JWT.
- True remote Addressables group/catalog and real files in Object Storage/CDN; first-boot download/restore Android tests.
- Server-authoritative drive proof, replay protection, transaction-locked coins/rewards and meaningful leaderboards.
- CI Unity Editor/PlayMode tests with a licensed runner; release signing and upload to GitHub Releases.
- After fully testing the recovery route, migrate worthwhile legacy gameplay features and consider removing redundant Canvas only in a reviewed PR.

**Release gate:** Do not merge to `main` or advertise a 3D APK before Editor, runtime and Android device tests pass.

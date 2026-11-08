# نَقِيل (NAGEEL) — وثيقة النشر والتشغيل والإصدار (المرحلة الثالثة)

مشروع **«نَقِيل» (NAGEEL)** هو محاكي قيادة ثلاثي الأبعاد في جبال اليمن الشاهقة، يجمع بين محرك ألعاب واقعي متطور (Unity URP) وبنية تشغيل فائقة السرعة للأجهزة المحمولة بنواة ARM64، مع ربط سحابي آمن بقاعدة بيانات **Neon Serverless Postgres** وتخزين سحابي للكائنات **Neon Object Storage**.

---

## 1. بيانات الربط السحابي (Neon Cloud Configuration)

| المعيار | القيمة المعتمدة |
| :--- | :--- |
| **Neon Console** | [console.neon.tech/app/projects/dry-surf-72406677](https://console.neon.tech/app/projects/dry-surf-72406677) |
| **Project ID** | `dry-surf-72406677` |
| **Database** | `nageel_db` |
| **Region** | `aws-eu-central-1` (Frankfurt) |
| **Active Development Branch** | `development` (`br-orange-pond-badvwqgc`) |
| **Production Branch** | `main` (محمي ضد أي تعديل مباشر) |
| **Neon MCP Endpoint** | `https://mcp.neon.tech/mcp` |
| **Object Storage Bucket** | `https://storage.neon.tech/dry-surf-72406677/development/nageel-assets/` |

> [!IMPORTANT]
> **قاعدة الأمان:** تطبيق Android والعميل لا يحتويان على أي سلسلة اتصال مباشرة (`DATABASE_URL`). تتم كافة عمليات القراءة والتسجيل والمزامنة عبر واجهة برمجة التطبيقات الآمنة (Backend API) أو مصادقة OAuth الخاصة بـ Neon.

---

## 2. هيكل قاعدة البيانات وجداول الـ Migrations (9 جداول)

تم إعداد ملفات الترحيل في مجلد [`migrations/`](file:///workspace/project-2/migrations/):
- [`001_initial_schema.sql`](file:///workspace/project-2/migrations/001_initial_schema.sql): إنشاء الجداول والفهارس ومفاتيح الـ UUID والقيود.
- [`002_seed_catalogs.sql`](file:///workspace/project-2/migrations/002_seed_catalogs.sql): البيانات الأولية للسيارات، الخرائط، وحزم الـ Addressables.
- [`run_migrations.js`](file:///workspace/project-2/migrations/run_migrations.js): أداة تشغيل وفحص الـ Migrations.

### الجداول التسعة المنشأة:
1. `player_profiles`: إدارة هوية اللاعب والربط مع معرّف الجهاز (`device_id UNIQUE`).
2. `vehicle_catalog`: كتالوج مواصفات السيارات الثلاث (الكتلة، العزم، نظام الدفع، والأسعار).
3. `map_catalog`: كتالوج المناطق الثلاث لشبكة الطرق (10.5 كم)، الارتفاع، ومعامل الاحتكاك.
4. `player_progress`: رصيد العملات، المستوى، نقاط الخبرة، والمناطق المفتوحة مع إصدار المزامنة (`sync_version`).
5. `player_garage`: السيارات المملوكة، التعديلات (المحرك، التعليق)، والألوان المختارة.
6. `driving_sessions`: تسجيل تفاصيل كل رحلة قيادة (المدة، المسافة، السرعة، ونقاط القيادة النظيفة).
7. `personal_records`: الأرقام القياسية الشخصية وأفضل أزمنة صعود النقيل لكل وضع لعب.
8. `asset_manifests`: إدارة حزم الـ Addressables وبصمات SHA-256 وروابط الـ CDN وNeon Object Storage.
9. `sync_operations`: حماية المزامنة من التكرار عبر مفاتيح الـ Idempotency لمنع التكرار عند انقطاع الشبكة.

---

## 3. الخادم الخلفي (Backend API) ونقاط الاتصال

تم بناء الخادم في [`backend/server.js`](file:///workspace/project-2/backend/server.js) باستخدام إطار عمل **Hono** المتوافق مع Neon Functions وServerless:

```
                          ┌───────────────────────────┐
                          │   Android Client (NAGEEL) │
                          └─────────────┬─────────────┘
                                        │ HTTPS (X-Device-Id, X-Idempotency-Key)
                                        ▼
                          ┌───────────────────────────┐
                          │    Hono API Gateway       │
                          │  (Rate Limit & Idempotent)│
                          └──────┬─────────────┬──────┘
                                 │             │
                Postgres Queries │             │ Asset CDN Range Stream
                                 ▼             ▼
                     ┌────────────────┐   ┌──────────────────────────┐
                     │ Neon Database  │   │ Neon Object Storage / CDN│
                     │  (nageel_db)   │   │  (Addressable Bundles)   │
                     └────────────────┘   └──────────────────────────┘
```

### واجهات الـ REST API المنفذة:
- `GET /api/v1/bootstrap`: إعدادات العميل، إصدار اللعبة، وحجم الحزم الإلزامية للتنزيل.
- `GET /api/v1/content/manifest`: قائمة حزم الملفات (سيارات، خرائط، خامات، أصوات) مع بصمات SHA-256.
- `GET /api/v1/vehicles`: كتالوج السيارات الثلاث مع البيانات الفيزيائية والميكانيكية.
- `GET /api/v1/maps`: بيانات المناطق الثلاث ومسافاتها (10.5 كم) وارتفاعاتها.
- `GET /api/v1/player/progress`: استرجاع مستوى وعملات ومرآب اللاعب الحالي.
- `POST /api/v1/player/sync`: مزامنة تقدم اللاعب مع منع التكرار ومعالجة التعارض.
- `POST /api/v1/drives/complete`: تسجيل نتائج الرحلة واحتساب الجوائز وفحص الأرقام القياسية.
- `GET /api/v1/leaderboard`: قائمة المتصدرين لأفضل أزمنة صعود نقيل سمارة وتحديات الوصول.
- `GET /api/v1/cdn/download/:packageId`: وكيل تنزيل الملفات مع دعم استئناف التحميل (HTTP 206 Partial Content).

---

## 4. نظام التثبيت الأول واستئناف التحميل (First-Time Boot)

- تم بناء [`NageelAssetDownloader.cs`](file:///workspace/project-2/Assets/Scripts/Cloud/NageelAssetDownloader.cs):
  1. واجهة ترحيب عربية تفاعلية.
  2. فحص المساحة المتاحة والتأكد من توفر مساحة كافية.
  3. خيار التنزيل عبر شبكات Wi-Fi أو بيانات الهاتف المحمول.
  4. تحميل تدريجي مجزأ يدعم الاستئناف عند انقطاع الإنترنت (Resume via HTTP Range Headers).
  5. فحص سلامة الملفات عبر بصمة SHA-256 التشفيرية فور اكتمال التنزيل.
  6. إنشاء نسخة احتياطية آمنة (Rollback Cache) لاستعادة النسخة السابقة عند تعثر التحديث.
  7. التحول التلقائي إلى وضع **100% Offline** بعد اكتمال التثبيت الأساسي.

---

## 5. الحفظ المحلي الموثوق (Atomic Offline Persistence)

- تم بناء [`NageelOfflineDataManager.cs`](file:///workspace/project-2/Assets/Scripts/Cloud/NageelOfflineDataManager.cs):
  - كتابة ذرية للملفات (`.tmp` -> استبدال ذري -> `.bak`) لمنع تلف ملفات الحفظ عند الإغلاق المفاجئ للتطبيق أو نفاد البطارية.
  - دعم المزامنة التلقائية الاختيارية في الخلفية مع الخادم عند توفر الاتصال.
  - دمج التقدم الذكي (Conflict Resolution: Max Progress Merge) لضمان عدم ضياع عملات أو إنجازات اللاعب.

---

## 6. حزمة الـ Android APK الموثقة

- **المسار:** [`app/build/outputs/apk/debug/app-debug.apk`](file:///workspace/project-2/app/build/outputs/apk/debug/app-debug.apk)
- **المعرّف البرمجي:** `com.moataz.nageel`
- **الحجم:** **5.1 ميجابايت**
- **المعمارية:** `arm64-v8a`
- **أدنى إصدار:** Android 8.0 (API Level 26)
- **الإصدار المستهدف:** Android 15 (API Level 35)
- **حالة البناء:** تم البناء بنجاح عبر Gradle بدون أي أخطاء وبأمر البناء السريع المعتمد.

---

## 7. خطوات تشغيل واختبار المنظومة

### تشغيل خادم الـ Backend واختبار الـ API:
```bash
cd /workspace/project-2/backend
node server.js
# في جلسة أخرى:
node test_api.js
```

### تشغيل اختبارات المحاكاة والفيزياء الشاملة:
```bash
cd /workspace/project-2
node scripts/run_simulation_tests.js
```

### بناء حزمة الـ APK (المسار السريع المعتمد):
```bash
timeout 5m gradle -Dorg.gradle.jvmargs= --no-daemon --max-workers=2 --offline --init-script /root/.gradle/init.d/pocketdev-android.gradle -Pandroid.aapt2FromMavenOverride=/root/android-sdk/build-tools/35.0.0/aapt2 assembleDebug --console=plain --stacktrace
```

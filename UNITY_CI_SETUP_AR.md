# تشغيل مسار Unity الحقيقي — NAGEEL

تم إضافة `.github/workflows/nageel-unity-verify.yml` في فرع الإصلاح. هذا المسار **غير مفعّل تلقائيًا** حتى يتم ضبط المتغير `NAGEEL_UNITY_CI_ENABLED=true` وإعداد ترخيص Unity. لا تعتبر وظيفة `skipped` دليلًا على نجاح اختبارات اللعبة.

## قبل التشغيل
1. من GitHub: **Settings → Secrets and variables → Actions**.
2. أضف سر `UNITY_LICENSE` إذا كنت تستخدم ترخيصًا مدعومًا من GameCI، أو `UNITY_EMAIL` و`UNITY_PASSWORD` وفق طريقة الترخيص المعتمدة لديك. لا تكتبها في المستودع أو المحادثة.
3. من **Variables** أضف `NAGEEL_UNITY_CI_ENABLED` بالقيمة `true`.
4. في Unity: **Edit → Project Settings → Player → Other Settings → Active Input Handling = Both** لأن واجهة القيادة التجريبية تستخدم `Input.touches` و`Input.GetAxisRaw`.
5. افتح المشروع في Unity **6000.0.23f1** أولًا. عالج جميع أخطاء Package Manager قبل البناء. تأكد من تثبيت Android SDK/NDK/OpenJDK وAndroid Build Support.

## معيار النجاح
- لا تعتبر `npm test` دليلًا على تشغيل السيارة.
- يلزم إنشاء ملفات `RecoveryDrive.unity` و`RecoveryMainMenu.unity` و`NageelRecoverySUV.prefab` فعليًا بواسطة Unity.
- يجب التأكد من أن اختبار EditMode يشاهد الملفات الناتجة؛ بعض إعدادات CI تستخدم حاويات مستقلة بين خطوات البناء والاختبار، وقد يلزم توليد المشاهد في خطوة الاختبار أيضًا.
- يجب مراجعة إعداد `game-ci/unity-builder` الفعلي ومخرجاته في Actions؛ هذا المسار أضيف دون تشغيله بعد، وقد يحتاج ضبطًا بحسب ترخيصك وإعدادات المشروع.
- عند إنتاج APK يجب فحص التوقيع، package ID، ABI، minSdk، وتشغيل القيادة على جهاز ARM64 حقيقي.
- ملف APK الناتج من هذا المسار هو **تجريبي وغير معتمد على أجهزة** حتى اجتياز اختبارات التشغيل.

## روابط
- فرع التطوير: https://github.com/Mtzallqmy/Game/tree/feat/nageel-real-3d-recovery
- Pull Request (Draft): https://github.com/Mtzallqmy/Game/pull/1
- GitHub Actions: https://github.com/Mtzallqmy/Game/actions
- Neon development: https://console.neon.tech/app/projects/dry-surf-72406677

حقوق المشروع: **برمجة وتطوير معتز العلقمي – تعز – اليمن – 2026م**.

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

namespace Nageel.Cloud
{
    public enum DownloadState
    {
        NotStarted, CheckingStorage, AwaitingUserConsent, Downloading,
        VerifyingChecksums, CompletedSuccess, FailedFallbackActive
    }

    [Serializable]
    public class DownloadPackageProgress
    {
        public string packageId;
        public string fileName;
        public long totalBytes;
        public long downloadedBytes;
        public float progressPercent;
        public bool isCompleted;
        public bool isVerified;
    }

    [Serializable]
    internal class RemoteManifest
    {
        public bool success;
        public RemotePackage[] packages;
    }

    [Serializable]
    internal class RemotePackage
    {
        public string id;
        public string asset_type;
        public string file_name;
        public string version;
        public long file_size_bytes;
        public string sha256_checksum;
        public string cdn_url;
        public bool is_mandatory;
    }

    /// <summary>
    /// Real HTTPS + resumable Range transport and SHA-256 validation for built bundles.
    /// No fabricated bytes or download-success indicator. Addressables catalogs/bundles
    /// must be built in Unity and published separately before this can complete.
    /// </summary>
    public class NageelAssetDownloader : MonoBehaviour
    {
        public static NageelAssetDownloader Instance { get; private set; }

        [Header("Real API; configure an HTTPS /api/v1 endpoint in the inspector")]
        public string apiBaseUrl = "";
        [Header("Allow-listed download host, not an arbitrary server-provided redirect")]
        public string approvedAssetHost = "br-orange-pond-badvwqgc.storage.c-8.eu-central-1.aws.neon.tech";

        public DownloadState currentState = DownloadState.NotStarted;
        public bool allowMobileData = false;
        public bool isFirstBoot = true;
        public float overallProgress;
        public long totalRequiredBytes;
        public long totalDownloadedBytes;
        public float downloadSpeedKBs;
        public string statusMessageArabic = "لم يبدأ التحميل بعد";

        public string assetsDirectory;
        public string backupDirectory;
        public List<DownloadPackageProgress> packages = new List<DownloadPackageProgress>();

        private string receiptPath;
        private bool busy;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            assetsDirectory = Path.Combine(Application.persistentDataPath,"NageelBundles");
            backupDirectory = Path.Combine(Application.persistentDataPath,"NageelBundleBackup");
            receiptPath = Path.Combine(assetsDirectory,"verified-receipt.json");
            Directory.CreateDirectory(assetsDirectory);
            Directory.CreateDirectory(backupDirectory);
            CheckFirstBootStatus();
        }

        private static bool IsValidSha256(string sha)
        {
            if (String.IsNullOrEmpty(sha) || sha.Length != 64) return false;
            foreach (char c in sha)
                if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        private static bool VerifyFile(string path, long bytes, string expectedHash)
        {
            if (!File.Exists(path) || !IsValidSha256(expectedHash)) return false;
            var info = new FileInfo(path);
            if (info.Length != bytes) return false;
            try
            {
                using (var file = File.OpenRead(path))
                using (var sha = SHA256.Create())
                {
                    byte[] digest = sha.ComputeHash(file);
                    string value = BitConverter.ToString(digest).Replace("-","").ToLowerInvariant();
                    return String.Equals(value, expectedHash, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Nageel Bundles] Hash read failed: " + e.Message);
                return false;
            }
        }

        private bool ValidPackage(RemotePackage pkg)
        {
            if (pkg == null || String.IsNullOrEmpty(pkg.id) ||
                String.IsNullOrEmpty(pkg.file_name) || pkg.file_size_bytes <= 0 ||
                !IsValidSha256(pkg.sha256_checksum)) return false;
            if (Path.GetFileName(pkg.file_name) != pkg.file_name ||
                pkg.file_name.Contains("..") ||
                !System.Text.RegularExpressions.Regex.IsMatch(pkg.file_name, "^[A-Za-z0-9_.-]{1,128}$"))
                return false;
            Uri parsed;
            if (!Uri.TryCreate(pkg.cdn_url, UriKind.Absolute, out parsed) ||
                parsed.Scheme != Uri.UriSchemeHttps) return false;
            return String.Equals(parsed.Host, approvedAssetHost, StringComparison.OrdinalIgnoreCase);
        }

        private void CheckFirstBootStatus()
        {
            bool ready = false;
            try
            {
                if (File.Exists(receiptPath))
                {
                    RemoteManifest receipt = JsonUtility.FromJson<RemoteManifest>(File.ReadAllText(receiptPath));
                    ready = receipt != null && receipt.packages != null && receipt.packages.Length > 0;
                    if (ready)
                    {
                        foreach (RemotePackage pkg in receipt.packages)
                        {
                            if (!ValidPackage(pkg) || !VerifyFile(
                                Path.Combine(assetsDirectory,pkg.file_name),
                                pkg.file_size_bytes,pkg.sha256_checksum)) { ready = false; break; }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ready = false;
                Debug.LogWarning("[Nageel Bundles] Receipt invalid: " + e.Message);
            }
            isFirstBoot = !ready;
            currentState = ready ? DownloadState.CompletedSuccess : DownloadState.AwaitingUserConsent;
            overallProgress = ready ? 1f : 0f;
            statusMessageArabic = ready ?
                "تم التحقق من الحزم المثبتة على الجهاز عبر SHA-256" :
                "الملفات غير مثبتة؛ مطلوب تنزيل حزم فعلية والتحقق منها";
        }

        public void StartDownloadProcess()
        {
            if (!busy) StartCoroutine(RunDownloadPipeline());
        }

        private bool AllowedApiUrl()
        {
            Uri url;
            if (!Uri.TryCreate(apiBaseUrl.TrimEnd('/'),UriKind.Absolute,out url)) return false;
            return url.Scheme == Uri.UriSchemeHttps ||
                (Application.isEditor && url.Scheme == Uri.UriSchemeHttp && url.Host == "localhost");
        }

        private IEnumerator RunDownloadPipeline()
        {
            busy = true;
            totalRequiredBytes = 0;
            totalDownloadedBytes = 0;
            overallProgress = 0f;
            currentState = DownloadState.CheckingStorage;

            if (!AllowedApiUrl())
            {
                Fail("تعذر التحميل: عنوان API آمن غير مهيأ");
                yield break;
            }
            if (Application.internetReachability == NetworkReachability.NotReachable ||
                (!allowMobileData && Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork))
            {
                Fail("التحميل يتطلب اتصال إنترنت مسموحًا به (الواي فاي حاليًا)");
                yield break;
            }

            string url = apiBaseUrl.TrimEnd('/') + "/content/manifest";
            RemoteManifest remote = null;
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 25;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try { remote = JsonUtility.FromJson<RemoteManifest>(req.downloadHandler.text); }
                    catch (Exception e) { Debug.LogWarning(e.Message); }
                }
            }
            if (remote == null || !remote.success || remote.packages == null)
            {
                Fail("تعذر قراءة كتالوج الحزم من الخادم");
                yield break;
            }

            var required = new List<RemotePackage>();
            foreach (var pkg in remote.packages)
            {
                if (!pkg.is_mandatory) continue;
                if (!ValidPackage(pkg))
                {
                    Fail("رفض حزمة غير موثوقة أو ناقصة البيانات: " + pkg.id);
                    yield break;
                }
                required.Add(pkg);
                totalRequiredBytes += pkg.file_size_bytes;
            }
            if (required.Count == 0 || totalRequiredBytes <= 0)
            {
                Fail("لم تُنشر حزم Unity صالحة بعد؛ لن يتم الادعاء بإكمال التثبيت");
                yield break;
            }

            long free = AvailableDiskBytes();
            if (free >= 0 && free < (totalRequiredBytes + 30L * 1024L * 1024L))
            {
                Fail("مساحة الهاتف لا تكفي لتنزيل الحزم والتحقق منها");
                yield break;
            }

            packages.Clear();
            currentState = DownloadState.Downloading;
            long accumulated = 0;
            float started = Time.realtimeSinceStartup;
            foreach (var pkg in required)
            {
                var progress = new DownloadPackageProgress {
                    packageId = pkg.id, fileName = pkg.file_name,totalBytes = pkg.file_size_bytes
                };
                packages.Add(progress);
                string final = Path.Combine(assetsDirectory,pkg.file_name);
                if (VerifyFile(final,pkg.file_size_bytes,pkg.sha256_checksum))
                {
                    progress.isVerified = true;
                    progress.isCompleted = true;
                    progress.downloadedBytes = pkg.file_size_bytes;
                    accumulated += pkg.file_size_bytes;
                    UpdateProgress(accumulated,started);
                    continue;
                }

                string partial = final + ".part";
                bool verified = false;
                for (int attempt=0;attempt<2 && !verified;attempt++)
                {
                    if (attempt > 0 && File.Exists(partial)) File.Delete(partial);
                    long offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;
                    if (offset > pkg.file_size_bytes)
                    {
                        File.Delete(partial); offset = 0;
                    }
                    using (var req = UnityWebRequest.Get(pkg.cdn_url))
                    {
                        req.downloadHandler.Dispose();
                        req.downloadHandler = new DownloadHandlerFile(partial,offset > 0);
                        req.timeout = 120;
                        if (offset > 0) req.SetRequestHeader("Range","bytes=" + offset + "-");
                        var operation = req.SendWebRequest();
                        while (!operation.isDone)
                        {
                            long estimated = (long)(req.downloadProgress * (pkg.file_size_bytes - offset));
                            progress.downloadedBytes = Math.Min(pkg.file_size_bytes,
                                Math.Max(offset,offset + estimated));
                            UpdateProgress(accumulated + progress.downloadedBytes,started);
                            yield return null;
                        }
                        if (req.result != UnityWebRequest.Result.Success)
                        {
                            statusMessageArabic = "تعذر تنزيل " + pkg.file_name + "، إعادة المحاولة";
                            continue;
                        }
                        // A Range-ignorant server returns 200; restart instead of appending duplicate bytes.
                        if (offset > 0 && req.responseCode != 206) continue;
                    }

                    currentState = DownloadState.VerifyingChecksums;
                    verified = VerifyFile(partial,pkg.file_size_bytes,pkg.sha256_checksum);
                    if (!verified) Debug.LogWarning("[Nageel Bundles] checksum mismatch: " + pkg.id);
                }
                if (!verified)
                {
                    Fail("فشل فحص SHA-256 للحزمة " + pkg.file_name);
                    yield break;
                }
                // A previous version is kept until the new hash is proven correct.
                if (File.Exists(final))
                {
                    string previous = Path.Combine(backupDirectory,pkg.file_name + ".previous");
                    if (File.Exists(previous)) File.Delete(previous);
                    File.Move(final,previous);
                }
                File.Move(partial,final);
                progress.isVerified = true;
                progress.isCompleted = true;
                progress.downloadedBytes = pkg.file_size_bytes;
                accumulated += pkg.file_size_bytes;
                UpdateProgress(accumulated,started);
                currentState = DownloadState.Downloading;
            }

            currentState = DownloadState.VerifyingChecksums;
            foreach (var pkg in required)
            {
                if (!VerifyFile(Path.Combine(assetsDirectory,pkg.file_name),
                    pkg.file_size_bytes,pkg.sha256_checksum))
                {
                    Fail("فشل التحقق النهائي من سلامة حزمة " + pkg.file_name);
                    yield break;
                }
            }

            try
            {
                var receipt = new RemoteManifest { success = true,packages = required.ToArray() };
                string temp = receiptPath + ".tmp";
                File.WriteAllText(temp,JsonUtility.ToJson(receipt,true));
                if (File.Exists(receiptPath)) File.Delete(receiptPath);
                File.Move(temp,receiptPath);
            }
            catch (Exception e)
            {
                Fail("فشل حفظ إيصال التحقق: " + e.Message);
                yield break;
            }

            currentState = DownloadState.CompletedSuccess;
            statusMessageArabic = "تم تنزيل الحزم الحقيقية والتحقق من SHA-256؛ التخزين المحلي جاهز";
            overallProgress = 1f;
            isFirstBoot = false;
            busy = false;
        }

        private void UpdateProgress(long bytes, float started)
        {
            totalDownloadedBytes = Math.Min(bytes,totalRequiredBytes);
            overallProgress = totalRequiredBytes > 0 ?
                Mathf.Clamp01((float)totalDownloadedBytes / totalRequiredBytes) : 0f;
            downloadSpeedKBs = (totalDownloadedBytes / 1024f) /
                Math.Max(0.1f,Time.realtimeSinceStartup-started);
            statusMessageArabic = "جارٍ التحميل والتحقق: " + (overallProgress * 100f).ToString("F1") + "%";
        }

        private void Fail(string message)
        {
            currentState = DownloadState.FailedFallbackActive;
            statusMessageArabic = message;
            busy = false;
            Debug.LogWarning("[Nageel Bundles] " + message);
        }

        private long AvailableDiskBytes()
        {
            try
            {
                return new DriveInfo(Path.GetPathRoot(Application.persistentDataPath)).AvailableFreeSpace;
            }
            catch { return -1L; } // Unknown, not a fabricated free-space value.
        }
    }
}

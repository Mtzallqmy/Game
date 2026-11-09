using UnityEngine;
using Nageel.Vehicle;

namespace Nageel.Tests
{
    /// <summary>
    /// Runtime smoke diagnostics. NOT a substitute for PlayMode physics,
    /// rendered-frame, device, download, or end-to-end tests.
    /// Run explicitly from the Inspector while the 3D drive scene is active.
    /// </summary>
    public class NageelPhysicsTests : MonoBehaviour
    {
        [ContextMenu("Run Runtime 3D Diagnostics")]
        public void RunAllTests()
        {
            int passed = 0;
            int failed = 0;
            NageelCarController car = FindObjectOfType<NageelCarController>();
            Check(car != null, "A real active vehicle controller exists", ref passed, ref failed);

            if (car != null)
            {
                Check(car.GetComponent<Rigidbody>() != null, "Vehicle is controlled by Rigidbody physics", ref passed, ref failed);
                Check(car.frontLeftCollider != null && car.frontRightCollider != null &&
                      car.rearLeftCollider != null && car.rearRightCollider != null,
                      "Four WheelColliders are assigned", ref passed, ref failed);
                Check(car.frontLeftMesh != null && car.frontRightMesh != null &&
                      car.rearLeftMesh != null && car.rearRightMesh != null,
                      "Four visual wheels are assigned", ref passed, ref failed);
            }

            bool roadHasMesh = false;
            foreach (var mesh in FindObjectsOfType<MeshCollider>())
            {
                if (mesh.sharedMesh != null && mesh.gameObject.name.Contains("Asphalt"))
                {
                    roadHasMesh = mesh.sharedMesh.vertexCount > 100;
                    if (roadHasMesh) break;
                }
            }
            Check(roadHasMesh, "Driveable 3D road collision mesh exists", ref passed, ref failed);
            Check(UnityEngine.Camera.main != null, "A MainCamera exists", ref passed, ref failed);

            Debug.Log("[NAGEEL RUNTIME SMOKE] Passed=" + passed + " Failed=" + failed +
                      " (manual drive/device testing remains required)");
            if (failed > 0) Debug.LogError("[NAGEEL RUNTIME SMOKE] Scene is NOT ready for build.");
        }

        private static void Check(bool value,string message,ref int passed,ref int failed)
        {
            if (value) { passed++; Debug.Log("[PASS] " + message); }
            else { failed++; Debug.LogError("[FAIL] " + message); }
        }
    }
}

using UnityEngine;
using Nageel.Vehicle;
using Nageel.Camera;

namespace Nageel.Recovery
{
    /// <summary>
    /// Working multi-touch controls for the RecoveryDrive scene.
    /// Each live finger is tested independently, allowing simultaneous steer/throttle.
    /// Draws with IMGUI until the final mobile HUD art is commissioned.
    /// </summary>
    [RequireComponent(typeof(NageelCarController))]
    public sealed class RecoveryTouchOverlay : MonoBehaviour
    {
        private NageelCarController car;
        public CameraController drivingCamera;
        private float throttle;
        private float brake;
        private float steering;

        private void Awake() { car = GetComponent<NageelCarController>(); }

        private static Rect RectFor(int index)
        {
            float w = Screen.width, h = Screen.height;
            float size = Mathf.Clamp(Mathf.Min(w, h) * 0.21f, 72f, 160f);
            float gap = size * 0.14f;
            float bottom = h - size - 20f;
            switch (index)
            {
                case 0: return new Rect(20f, bottom, size, size); // left
                case 1: return new Rect(20f + size + gap, bottom, size, size); // right
                case 2: return new Rect(w - 20f - size * 2f - gap, bottom, size, size); // brake
                case 3: return new Rect(w - 20f - size, bottom, size, size); // throttle
                case 4: return new Rect(w - 130f, 12f, 110f, 46f); // camera
                case 5: return new Rect(20f, 12f, 110f, 46f); // reset
                default: return new Rect(140f, 12f, 110f, 46f); // reverse
            }
        }

        private void Update()
        {
            float left = 0f, right = 0f, gas = 0f, footBrake = 0f;
            float axis = Input.GetAxisRaw("Horizontal");
            left = axis < -0.1f ? 1f : 0f;
            right = axis > 0.1f ? 1f : 0f;
            gas = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f;
            footBrake = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f;
            if (Input.GetKeyDown(KeyCode.R)) ResetCar();
            if (Input.GetKeyDown(KeyCode.C) && drivingCamera != null) drivingCamera.CycleCameraView();
            if (Input.GetKeyDown(KeyCode.G)) car.ToggleReverse();

            foreach (Touch touch in Input.touches)
            {
                if (touch.phase == TouchPhase.Canceled || touch.phase == TouchPhase.Ended) continue;
                Vector2 p = new Vector2(touch.position.x, Screen.height - touch.position.y);
                if (RectFor(0).Contains(p)) left = 1f;
                if (RectFor(1).Contains(p)) right = 1f;
                if (RectFor(2).Contains(p)) footBrake = 1f;
                if (RectFor(3).Contains(p)) gas = 1f;
                if (touch.phase == TouchPhase.Began)
                {
                    if (RectFor(4).Contains(p) && drivingCamera != null) drivingCamera.CycleCameraView();
                    if (RectFor(5).Contains(p)) ResetCar();
                    if (RectFor(6).Contains(p)) car.ToggleReverse();
                }
            }
            steering = Mathf.MoveTowards(steering, right - left, Time.deltaTime * 5f);
            throttle = Mathf.MoveTowards(throttle, gas, Time.deltaTime * 4f);
            brake = Mathf.MoveTowards(brake, footBrake, Time.deltaTime * 6f);
            car.SetInputs(throttle, steering, brake, Input.GetKey(KeyCode.Space));
        }

        private void ResetCar()
        {
            Vector3 p = RecoveryRoadPoint(12f);
            Vector3 fwd = (RecoveryRoadPoint(20f) - RecoveryRoadPoint(12f)).normalized;
            car.ResetVehicle(p + Vector3.up * 1.3f, Quaternion.LookRotation(fwd, Vector3.up));
        }

        public static Vector3 RecoveryRoadPoint(float z)
        {
            return new Vector3(Mathf.Sin(z * 0.006f) * 28f + Mathf.Sin(z * 0.012f) * 11f,
                z * 0.075f + Mathf.Sin(z * 0.012f) * 4f, z);
        }

        private void OnGUI()
        {
            int prev = GUI.skin.box.fontSize;
            GUI.skin.box.fontSize = Mathf.Max(16, Mathf.RoundToInt(Screen.height * 0.038f));
            string[] labels = { "LEFT", "RIGHT", "BRAKE", "GAS", "CAMERA", "RESET", "REVERSE" };
            for (int i = 0; i < labels.Length; i++)
                GUI.Box(RectFor(i), labels[i]);
            GUI.skin.box.fontSize = prev;

            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 36), normal = { textColor = Color.white } };
            GUI.Label(new Rect(Screen.width * 0.33f, 12f, Screen.width * 0.33f, 44f),
                car.currentSpeedKmh.ToString("F0") + " km/h | " + car.currentGear + " gear", style);
        }
    }
}

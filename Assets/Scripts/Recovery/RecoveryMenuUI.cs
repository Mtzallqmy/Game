using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nageel.Recovery
{
    public sealed class RecoveryMenuUI : MonoBehaviour
    {
        private void OnGUI()
        {
            float width = Mathf.Min(Screen.width * 0.78f, 680f);
            float x = (Screen.width - width) / 2f;
            var title = new GUIStyle(GUI.skin.label) {
                fontSize = Mathf.Max(25, Screen.height / 10),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            var caption = new GUIStyle(GUI.skin.label) {
                fontSize = Mathf.Max(13, Screen.height / 35),
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(x, Screen.height * 0.18f, width, 100), "NAGEEL", title);
            GUI.Label(new Rect(x, Screen.height * 0.38f, width, 60),
                "Recovery prototype | Yemen Mountain Driving", caption);
            if (GUI.Button(new Rect(x + width * 0.23f, Screen.height * 0.58f,
                width * 0.54f, 65f), "START 3D DRIVE"))
                SceneManager.LoadScene("RecoveryDrive");
            GUI.Label(new Rect(x, Screen.height - 55f, width, 50),
                "برمجة وتطوير معتز العلقمي - تعز - اليمن - 2026م", caption);
        }
    }
}

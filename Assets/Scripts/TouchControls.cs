using UnityEngine;

namespace DriftX
{
    /// <summary>
    /// Multi-touch on-screen controls read straight from Input.touches (IMGUI only draws them).
    /// Keyboard fallback: A/D or arrows steer, W gas, S brake/reverse, Space handbrake.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        Rect leftR, rightR, gasR, brakeR, handR;
        bool uL, uR, uG, uB, uH;
        GUIStyle style;

        void Layout()
        {
            float w = Screen.width, h = Screen.height;
            float s = Mathf.Min(w, h) * 0.22f;
            float m = s * 0.15f;
            leftR = new Rect(m, h - s - m, s, s);
            rightR = new Rect(m * 2f + s, h - s - m, s, s);
            gasR = new Rect(w - s - m, h - s - m, s, s);
            brakeR = new Rect(w - s * 2f - m * 2f, h - s - m, s, s);
            handR = new Rect(w - s - m, h - s * 1.9f - m * 2f, s, s * 0.8f);
        }

        void Update()
        {
            Layout();
            bool l = false, r = false, g = false, b = false, h = false;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
                Vector2 p = new Vector2(t.position.x, Screen.height - t.position.y);
                if (leftR.Contains(p)) l = true;
                if (rightR.Contains(p)) r = true;
                if (gasR.Contains(p)) g = true;
                if (brakeR.Contains(p)) b = true;
                if (handR.Contains(p)) h = true;
            }

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) l = true;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) r = true;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) g = true;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) b = true;
            if (Input.GetKey(KeyCode.Space)) h = true;

            uL = l; uR = r; uG = g; uB = b; uH = h;
            DriftInput.Steer = (r ? 1f : 0f) - (l ? 1f : 0f);
            DriftInput.Throttle = g ? 1f : 0f;
            DriftInput.Brake = b ? 1f : 0f;
            DriftInput.Handbrake = h;
        }

        void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box);
                style.fontSize = Mathf.RoundToInt(Screen.height * 0.045f);
                style.alignment = TextAnchor.MiddleCenter;
                style.normal.textColor = Color.white;
            }
            Draw(leftR, "<", uL);
            Draw(rightR, ">", uR);
            Draw(gasR, "GAS", uG);
            Draw(brakeR, "BRK", uB);
            Draw(handR, "HAND", uH);
        }

        void Draw(Rect r, string label, bool on)
        {
            Color c = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, on ? 0.9f : 0.35f);
            GUI.Box(r, label, style);
            GUI.color = c;
        }
    }
}

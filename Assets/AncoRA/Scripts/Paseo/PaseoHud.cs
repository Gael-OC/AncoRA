using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.AR
{
    /// <summary>
    /// Always-visible status banner for everyone, plus a detailed team HUD toggled with five taps on the top-left corner.
    /// Drawn inside the safe area (camera cutout and rounded corners).
    /// </summary>
    public sealed class PaseoHud : MonoBehaviour
    {
        const float TapWindowSeconds = 1.5f;

        [SerializeField] PaseoTour tour;

        bool hudVisible;
        int teamTaps;
        float lastTapTime;

        public PaseoTour Tour => tour;
        public bool HudVisible => hudVisible;

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(PaseoTour paseoTour) => tour = paseoTour;

        void OnGUI()
        {
            if (tour == null)
                return;
            var safe = GuiSafeArea.Rect;
            if (GUI.Button(new Rect(safe.x, safe.y, 120, 120), GUIContent.none, GUIStyle.none))
            {
                teamTaps = Time.realtimeSinceStartup - lastTapTime < TapWindowSeconds ? teamTaps + 1 : 1;
                lastTapTime = Time.realtimeSinceStartup;
                if (teamTaps >= 5)
                {
                    hudVisible = !hudVisible;
                    teamTaps = 0;
                }
            }

            float top = DrawBanner(safe) + 12f;
            if (!hudVisible)
                return;

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                fontSize = Mathf.Max(16, Screen.width / 52)
            };
            string text = $"AncoRA · Paseo (EQUIPO) | v{Application.version} | ARSession: {ARSession.state} | " +
                          $"SDK: {(tour.SdkReady ? "listo" : "esperando")}\n{tour.DescribeMaps()}\nAlineación física: NO verificada";
            if (!string.IsNullOrEmpty(tour.Error))
                text += $"\nERROR: {tour.Error}";
            float width = Mathf.Min(safe.width, 950);
            float height = style.CalcHeight(new GUIContent(text), width) + 12;
            GUI.Box(new Rect(safe.x, top, width, height), text, style);
        }

        float DrawBanner(Rect safe)
        {
            string message = PaseoStatusText.Describe(new PaseoStatusInputs
            {
                Error = tour.Error,
                ArUnsupported = ARSession.state == ARSessionState.Unsupported,
                SdkReady = tour.SdkReady,
                SecondsSinceStart = tour.SecondsSinceStart,
                MapStillLoading = tour.FirstMapStillLoading,
                Tracking = ARSession.state == ARSessionState.SessionTracking,
                Buildings = tour.BuildingStatuses()
            }, out var level);

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(20, Screen.width / 34)
            };
            style.normal.textColor = level == PaseoStatusLevel.Ok ? new Color(0.55f, 1f, 0.6f)
                : level == PaseoStatusLevel.Error ? new Color(1f, 0.55f, 0.55f)
                : new Color(1f, 0.93f, 0.55f);
            var content = new GUIContent("AncoRA · " + message);
            float height = style.CalcHeight(content, safe.width) + 12f;
            GUI.Box(new Rect(safe.x, safe.y, safe.width, height), content, style);
            return safe.y + height;
        }
    }
}

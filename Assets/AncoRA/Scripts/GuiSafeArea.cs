using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Usable screen rectangle for IMGUI (top-left origin): inside the display cutout (Screen.safeArea) and clear of
    /// the rounded display corners, which Screen.safeArea does not include. The corner radius comes from Android's
    /// WindowInsets.getRoundedCorner (API 31+), with a dpi-based guess elsewhere.
    /// </summary>
    public static class GuiSafeArea
    {
        const float Margin = 8f;
        // A point at (m, m) from a corner of radius r is on screen when m >= r * (1 - 1/sqrt(2)).
        const float CornerFactor = 0.3f;

        static Rect s_Cached;
        static Rect s_CachedSafeArea;
        static int s_CachedWidth, s_CachedHeight;
        static float s_TopRadius = -1f, s_BottomRadius = -1f;

        public static Rect Rect
        {
            get
            {
                var safe = Screen.safeArea;
                if (safe == s_CachedSafeArea && Screen.width == s_CachedWidth && Screen.height == s_CachedHeight)
                    return s_Cached;
                if (s_TopRadius < 0f)
                    ReadCornerRadii(out s_TopRadius, out s_BottomRadius);

                float top = Mathf.Max(Screen.height - safe.yMax, s_TopRadius * CornerFactor) + Margin;
                float bottom = Mathf.Max(safe.yMin, s_BottomRadius * CornerFactor) + Margin;
                float corner = Mathf.Max(s_TopRadius, s_BottomRadius) * CornerFactor;
                float left = Mathf.Max(safe.xMin, corner) + Margin;
                float right = Mathf.Max(Screen.width - safe.xMax, corner) + Margin;

                s_Cached = new Rect(left, top, Mathf.Max(1f, Screen.width - left - right), Mathf.Max(1f, Screen.height - top - bottom));
                s_CachedSafeArea = safe;
                s_CachedWidth = Screen.width;
                s_CachedHeight = Screen.height;
                return s_Cached;
            }
        }

        static void ReadCornerRadii(out float top, out float bottom)
        {
            // Typical phone corners are a few millimetres; used when the platform does not report them.
            float guess = Screen.dpi > 0f ? Screen.dpi * 0.22f : Screen.width * 0.08f;
            top = bottom = guess;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var activity = UnityEngine.Android.AndroidApplication.currentActivity;
                using var window = activity.Call<AndroidJavaObject>("getWindow");
                using var decor = window.Call<AndroidJavaObject>("getDecorView");
                using var insets = decor.Call<AndroidJavaObject>("getRootWindowInsets");
                if (insets == null)
                    return;
                // RoundedCorner.POSITION_TOP_LEFT = 0, TOP_RIGHT = 1, BOTTOM_RIGHT = 2, BOTTOM_LEFT = 3.
                top = Mathf.Max(Radius(insets, 0), Radius(insets, 1));
                bottom = Mathf.Max(Radius(insets, 2), Radius(insets, 3));
                Debug.Log($"[AncoRA UI] Esquinas redondeadas: arriba {top} px, abajo {bottom} px; safeArea {Screen.safeArea} en {Screen.width}x{Screen.height}.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AncoRA UI] No se pudo leer el radio de las esquinas ({e.Message}); se usa {guess:F0} px.");
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static float Radius(AndroidJavaObject insets, int position)
        {
            using var corner = insets.Call<AndroidJavaObject>("getRoundedCorner", position);
            return corner == null ? 0f : corner.Call<int>("getRadius");
        }
#endif
    }
}

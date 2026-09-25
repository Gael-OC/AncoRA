using System;
using System.IO;
using Immersal.XR;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>Values the team dialled in on the phone; pasted back to the Editor side to be applied to the scene.</summary>
    [Serializable]
    public sealed class EdificioFieldAdjustmentData
    {
        [Serializable] public sealed class MapB { public float[] posicion; public float giroY; }
        [Serializable] public sealed class Frame { public float[] posicion; public float giroY; public float ancho; public float alto; }
        [Serializable] public sealed class Diagnostic { public int mapaQueLocalizo; public int cambiosDeMapa; public float ultimoSaltoCm; public float ultimoSaltoGrados; }

        public int version = 1;
        public string generado;
        public string build;
        public string nota = "AJUSTE A OJO EN CAMPO, sin verificar contra medidas fisicas";
        public MapB mapaB;
        public Frame marco;
        public Diagnostic diagnostico;
    }

    /// <summary>
    /// Team-only live adjustment of the map B alignment and of the facade frame, drawn under the diagnostic HUD
    /// (five taps on the top-left corner). The public never sees it. Nothing is persisted between launches on
    /// purpose: a silently remembered offset would hide a wrong scene value. The team copies the values and they
    /// are applied to the scene from the Editor.
    /// <para>
    /// The SDK reads the map-to-space relation once at start-up, so moving the XR Map transform alone would change
    /// nothing while running. The stored <see cref="MapEntry.Relation"/> is updated too, and the change shows up
    /// at the next localization that comes from map B.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class ImmersalEdificioFieldAdjust : MonoBehaviour
    {
        const string Tag = "[AncoRA Edificio]";
        static readonly float[] PositionSteps = { 1f, 0.25f, 0.05f };
        static readonly float[] AngleSteps = { 5f, 1f, 0.2f };
        static readonly float[] SizeSteps = { 2f, 0.5f, 0.1f };
        static readonly string[] StepNames = { "grueso", "medio", "fino" };

        enum Page { Closed, MapB, Frame }

        [SerializeField] ImmersalEdificioPilotDiagnostics diagnostics;
        [SerializeField] XRMap mapB;
        [SerializeField] EdificioFacadeFrame frame;

        Page page = Page.Closed;
        int stepIndex = 1;
        Vector3 bPosition, bPosition0;
        float bYaw, bYaw0;
        Vector3 fPosition, fPosition0;
        Vector3 fEuler0;
        float fYaw, fYaw0, fWidth, fWidth0, fHeight, fHeight0;
        bool mapBDirty;
        string toast = "";
        float toastUntil;

        void Start()
        {
            bPosition0 = bPosition = mapB.transform.localPosition;
            bYaw0 = bYaw = mapB.transform.localEulerAngles.y;
            fPosition0 = fPosition = frame.transform.localPosition;
            fEuler0 = frame.transform.localEulerAngles;
            fYaw0 = fYaw = fEuler0.y;
            fWidth0 = fWidth = frame.WidthMeters;
            fHeight0 = fHeight = frame.HeightMeters;
        }

        void Update()
        {
            // Keep pushing after the first edit so a late map registration still receives the adjusted relation.
            if (mapBDirty)
                PushMapB();
        }

        void PushMapB()
        {
            var rotation = Quaternion.Euler(0f, bYaw, 0f);
            mapB.transform.localPosition = bPosition;
            mapB.transform.localRotation = rotation;
            if (MapManager.TryGetMapEntry(mapB.mapId, out var entry) && entry?.Relation != null)
            {
                entry.Relation.Position = bPosition;
                entry.Relation.Rotation = rotation;
            }
        }

        void PushFrame()
        {
            frame.transform.localPosition = fPosition;
            frame.transform.localRotation = Quaternion.Euler(fEuler0.x, fYaw, fEuler0.z);
            frame.SetSize(fWidth, fHeight);
        }

        void OnGUI()
        {
            if (diagnostics == null || !diagnostics.HudVisible)
                return;

            float width = Screen.width;
            float row = Mathf.Max(56f, Screen.height / 26f);
            int font = Mathf.Max(16, (int)(row * 0.42f));
            var button = new GUIStyle(GUI.skin.button) { fontSize = font };
            var label = new GUIStyle(GUI.skin.label) { fontSize = font, alignment = TextAnchor.MiddleLeft };
            var value = new GUIStyle(GUI.skin.label) { fontSize = font, alignment = TextAnchor.MiddleCenter };
            label.normal.textColor = value.normal.textColor = Color.white;

            int rows = page == Page.MapB ? 4 : page == Page.Frame ? 6 : 0;
            float footer = page == Page.Closed ? 0f : row;
            float height = row * (1 + rows) + footer;
            float top = Screen.height - height - 8f;
            if (page != Page.Closed)
                GUI.Box(new Rect(0, top, width, height), GUIContent.none);

            // Header: tabs and step size.
            float x = 0, w = width / 4f;
            if (GUI.Button(new Rect(x, top, w, row), "Ajuste: Mapa B", button)) page = page == Page.MapB ? Page.Closed : Page.MapB;
            if (GUI.Button(new Rect(x + w, top, w, row), "Ajuste: Marco", button)) page = page == Page.Frame ? Page.Closed : Page.Frame;
            if (page != Page.Closed && GUI.Button(new Rect(x + 2 * w, top, w, row), $"Paso: {StepNames[stepIndex]}", button))
                stepIndex = (stepIndex + 1) % StepNames.Length;
            if (page != Page.Closed && GUI.Button(new Rect(x + 3 * w, top, w, row), "Cerrar", button))
                page = Page.Closed;

            float y = top + row;
            float pos = PositionSteps[stepIndex], ang = AngleSteps[stepIndex], size = SizeSteps[stepIndex];
            if (page == Page.MapB)
            {
                bool changed = false;
                changed |= Stepper(ref y, row, width, "B pos X (m)", ref bPosition.x, pos, "F2", label, value, button);
                changed |= Stepper(ref y, row, width, "B pos Y (m)", ref bPosition.y, pos, "F2", label, value, button);
                changed |= Stepper(ref y, row, width, "B pos Z (m)", ref bPosition.z, pos, "F2", label, value, button);
                changed |= Stepper(ref y, row, width, "B giro Y (°)", ref bYaw, ang, "F1", label, value, button);
                if (changed)
                {
                    mapBDirty = true;
                    PushMapB();
                }
            }
            else if (page == Page.Frame)
            {
                bool changed = false;
                changed |= Stepper(ref y, row, width, "Marco X (m)", ref fPosition.x, pos, "F2", label, value, button);
                changed |= Stepper(ref y, row, width, "Marco Y (m)", ref fPosition.y, pos, "F2", label, value, button);
                changed |= Stepper(ref y, row, width, "Marco Z (m)", ref fPosition.z, pos, "F2", label, value, button);
                changed |= Stepper(ref y, row, width, "Marco giro Y (°)", ref fYaw, ang, "F1", label, value, button);
                changed |= Stepper(ref y, row, width, "Marco ancho (m)", ref fWidth, size, "F2", label, value, button);
                changed |= Stepper(ref y, row, width, "Marco alto (m)", ref fHeight, size, "F2", label, value, button);
                fWidth = Mathf.Max(0.5f, fWidth);
                fHeight = Mathf.Max(0.5f, fHeight);
                if (changed)
                    PushFrame();
            }

            if (page != Page.Closed)
            {
                float third = width / 3f;
                if (GUI.Button(new Rect(0, y, third, row), "Copiar valores", button))
                    CopyValues();
                if (GUI.Button(new Rect(third, y, third, row), "Restaurar escena", button))
                    RestoreSceneValues();
                if (Time.realtimeSinceStartup < toastUntil)
                    GUI.Label(new Rect(2 * third + 8, y, third - 8, row), toast, label);
            }
        }

        static bool Stepper(ref float y, float row, float width, string name, ref float current, float step, string format,
            GUIStyle label, GUIStyle value, GUIStyle button)
        {
            bool changed = false;
            GUI.Label(new Rect(8, y, width * 0.34f, row), name, label);
            if (GUI.Button(new Rect(width * 0.36f, y, width * 0.2f, row), "−", button)) { current -= step; changed = true; }
            GUI.Label(new Rect(width * 0.56f, y, width * 0.24f, row), current.ToString(format), value);
            if (GUI.Button(new Rect(width * 0.8f, y, width * 0.2f, row), "+", button)) { current += step; changed = true; }
            y += row;
            return changed;
        }

        void RestoreSceneValues()
        {
            bPosition = bPosition0; bYaw = bYaw0;
            fPosition = fPosition0; fYaw = fYaw0; fWidth = fWidth0; fHeight = fHeight0;
            mapBDirty = true;
            PushMapB();
            PushFrame();
            Show("Valores de escena restaurados");
            Debug.Log($"{Tag} Ajuste de campo: valores de la escena restaurados.");
        }

        void CopyValues()
        {
            var data = new EdificioFieldAdjustmentData
            {
                generado = DateTime.Now.ToString("s"),
                build = Application.buildGUID,
                mapaB = new EdificioFieldAdjustmentData.MapB { posicion = new[] { bPosition.x, bPosition.y, bPosition.z }, giroY = bYaw },
                marco = new EdificioFieldAdjustmentData.Frame
                {
                    posicion = new[] { fPosition.x, fPosition.y, fPosition.z }, giroY = fYaw, ancho = fWidth, alto = fHeight
                },
                diagnostico = new EdificioFieldAdjustmentData.Diagnostic
                {
                    mapaQueLocalizo = diagnostics.LastPoseMapId,
                    cambiosDeMapa = diagnostics.MapSwitches,
                    ultimoSaltoCm = diagnostics.LastJumpMeters * 100f,
                    ultimoSaltoGrados = diagnostics.LastJumpDegrees
                }
            };
            string json = JsonUtility.ToJson(data);
            GUIUtility.systemCopyBuffer = json;
            Debug.Log($"{Tag} AJUSTE_CAMPO {json}");
            string saved = "";
            try
            {
                string path = Path.Combine(Application.persistentDataPath, $"edificio-ajuste-campo-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.WriteAllText(path, json);
                saved = $" y guardado en {path}";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} No se pudo guardar el ajuste en disco: {e.Message}");
            }
            Show("Copiado al portapapeles y al log");
            Debug.Log($"{Tag} Ajuste de campo copiado al portapapeles{saved}. Pegarlo tal cual para aplicarlo desde el Editor.");
        }

        void Show(string message)
        {
            toast = message;
            toastUntil = Time.realtimeSinceStartup + 4f;
        }
    }
}

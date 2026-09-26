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
        [Serializable] public sealed class Frame { public float[] posicion; public float giroY; public float ancho; public float alto; public float profundidad; public bool solido; }
        [Serializable] public sealed class Diagnostic { public int mapaQueLocalizo; public int cambiosDeMapa; public float ultimoSaltoCm; public float ultimoSaltoGrados; }

        /// <summary>Teologia demo: the box pose in the frame of each of the two maps (a pose only means something inside its own map).</summary>
        [Serializable]
        public sealed class Teologia
        {
            public int modo;
            public int mapa1Id;
            public int mapa2Id;
            public float[] posMapa1;
            public float giroMapa1;
            public bool fijadaMapa1;
            public float[] posMapa2;
            public float giroMapa2;
            public bool fijadaMapa2;
        }

        public int version = 1;
        public string generado;
        public string build;
        public string nota = "AJUSTE A OJO EN CAMPO, sin verificar contra medidas fisicas";
        // Null when the scene has a single map (Teologia demo).
        public MapB mapaB;
        public Frame marco;
        public Teologia teologia;
        public Diagnostic diagnostico;
    }

    /// <summary>
    /// Team-only live adjustment of the facade frame or box (and of the map B alignment in the two-map building pilot),
    /// drawn under the diagnostic HUD (five taps on the top-left corner). The public never sees it.
    /// <para>
    /// In the two-map building pilot nothing is persisted between launches on purpose: a silently remembered offset
    /// would hide a wrong scene value. The Teologia demo has a <see cref="TeologiaMapSelector"/>, which changes the map
    /// mode by reloading the scene, so there the box is saved per map on the phone (the selector owns that) and the
    /// "Restaurar escena" button clears it. Either way the team copies the values and they are applied to the scene
    /// from the Editor.
    /// </para>
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

        enum Page { Closed, MapB, Maps, Frame }

        [SerializeField] ImmersalEdificioPilotDiagnostics diagnostics;
        [SerializeField] XRMap mapB;
        [SerializeField] EdificioFacadeFrame frame;
        [Tooltip("Teologia demo only: map mode menu and per-map saved box.")]
        [SerializeField] TeologiaMapSelector selector;

        Page page = Page.Closed;
        int stepIndex = 1;
        Vector3 bPosition, bPosition0;
        float bYaw, bYaw0;
        Vector3 fPosition, fPosition0;
        Vector3 fEuler0;
        float fYaw, fYaw0, fWidth, fWidth0, fHeight, fHeight0, fDepth, fDepth0;
        bool fSolid, fSolid0;
        bool mapBDirty;
        // With a selector the second map is managed by it (derived alignment), never by hand.
        bool HasMapB => mapB != null && selector == null;
        bool HasSelector => selector != null;
        bool IsBox => frame != null && frame.IsBox;
        bool PoseEditable => selector == null || selector.BoxPoseEditable;
        string toast = "";
        float toastUntil;

        void Start()
        {
            if (HasMapB)
            {
                bPosition0 = bPosition = mapB.transform.localPosition;
                bYaw0 = bYaw = mapB.transform.localEulerAngles.y;
            }
            fEuler0 = frame.transform.localEulerAngles;
            ReadFrame();
            fPosition0 = fPosition;
            fYaw0 = fYaw;
            fWidth0 = fWidth;
            fHeight0 = fHeight;
            fDepth0 = fDepth;
            fSolid0 = fSolid;
        }

        void ReadFrame()
        {
            fPosition = frame.transform.localPosition;
            fYaw = frame.transform.localEulerAngles.y;
            fWidth = frame.WidthMeters;
            fHeight = frame.HeightMeters;
            fDepth = frame.DepthMeters;
            fSolid = frame.Solid;
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
            frame.SetSize(fWidth, fHeight, fDepth);
            frame.SetSolid(fSolid);
            if (selector != null)
            {
                selector.SaveBox(fPosition, fYaw, new Vector3(fWidth, fHeight, fDepth), fSolid);
                frame.MarkPlaced(true);
            }
        }

        int FrameRows() => !IsBox ? 6 : PoseEditable ? 8 : 5;

        void OnGUI()
        {
            if (diagnostics == null || !diagnostics.HudVisible)
                return;

            // Everything below is drawn inside the safe area (clear of the camera cutout and the rounded corners).
            var safe = GuiSafeArea.Rect;
            float width = safe.width;
            float row = Mathf.Max(56f, Screen.height / 26f);
            int font = Mathf.Max(16, (int)(row * 0.42f));
            var button = new GUIStyle(GUI.skin.button) { fontSize = font };
            var label = new GUIStyle(GUI.skin.label) { fontSize = font, alignment = TextAnchor.MiddleLeft };
            var wrapped = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, font - 4), alignment = TextAnchor.UpperLeft, wordWrap = true };
            var value = new GUIStyle(GUI.skin.label) { fontSize = font, alignment = TextAnchor.MiddleCenter };
            label.normal.textColor = value.normal.textColor = wrapped.normal.textColor = Color.white;

            int rows = page == Page.MapB ? 4 : page == Page.Maps ? 3 : page == Page.Frame ? FrameRows() : 0;
            float footer = page == Page.Closed ? 0f : row;
            float height = row * (1 + rows) + footer;
            float top = safe.height - height;
            GUI.BeginGroup(safe);
            if (page != Page.Closed)
                GUI.Box(new Rect(0, top, width, height), GUIContent.none);

            // Header: tabs and step size. A second tab exists with a map selector (Mapas) or a second map (Mapa B).
            int slots = HasSelector || HasMapB ? 4 : 3;
            float w = width / slots;
            int slot = 0;
            if (HasSelector && GUI.Button(new Rect(w * slot++, top, w, row), "Mapas", button))
                page = page == Page.Maps ? Page.Closed : Page.Maps;
            else if (HasMapB && GUI.Button(new Rect(w * slot++, top, w, row), "Ajuste: Mapa B", button))
                page = page == Page.MapB ? Page.Closed : Page.MapB;
            if (GUI.Button(new Rect(w * slot++, top, w, row), IsBox ? "Ajuste: Caja" : "Ajuste: Marco", button))
                page = page == Page.Frame ? Page.Closed : Page.Frame;
            if (page != Page.Closed && GUI.Button(new Rect(w * slot, top, w, row), $"Paso: {StepNames[stepIndex]}", button))
                stepIndex = (stepIndex + 1) % StepNames.Length;
            slot++;
            if (page != Page.Closed && slots > slot && GUI.Button(new Rect(w * slot, top, w, row), "Cerrar", button))
                page = Page.Closed;

            float y = top + row;
            float pos = PositionSteps[stepIndex], ang = AngleSteps[stepIndex], size = SizeSteps[stepIndex];
            if (page == Page.MapB && HasMapB)
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
            else if (page == Page.Maps && HasSelector)
            {
                float third = width / 3f;
                var modes = new[] { TeologiaMode.Mapa1, TeologiaMode.Mapa2, TeologiaMode.Ambos };
                var names = new[] { "Solo Mapa 1", "Solo Mapa 2", "Ambos" };
                for (int i = 0; i < modes.Length; i++)
                {
                    bool current = selector.Mode == modes[i];
                    if (GUI.Button(new Rect(third * i, y, third, row), (current ? "▶ " : "") + names[i], button) && !current)
                        selector.RequestMode(modes[i]);
                }
                y += row;
                string notice = string.IsNullOrEmpty(selector.Notice)
                    ? "Cambiar de modo reinicia la escena y hay que volver a ubicarse. La caja se guarda por mapa en este teléfono."
                    : selector.Notice;
                GUI.Label(new Rect(8, y, width - 16, row * 2), notice, wrapped);
                y += row * 2;
            }
            else if (page == Page.Frame)
            {
                bool changed = false;
                if (IsBox && !PoseEditable)
                {
                    GUI.Label(new Rect(8, y, width - 16, row), "En «Ambos» la posición sale de las cajas de cada mapa; se ajusta en «Solo Mapa 1» y «Solo Mapa 2».", wrapped);
                    y += row;
                }
                else
                {
                    changed |= Stepper(ref y, row, width, "Marco X (m)", ref fPosition.x, pos, "F2", label, value, button);
                    changed |= Stepper(ref y, row, width, "Marco Y (m)", ref fPosition.y, pos, "F2", label, value, button);
                    changed |= Stepper(ref y, row, width, "Marco Z (m)", ref fPosition.z, pos, "F2", label, value, button);
                    changed |= Stepper(ref y, row, width, "Marco giro Y (°)", ref fYaw, ang, "F1", label, value, button);
                }
                string noun = IsBox ? "Caja" : "Marco";
                changed |= Stepper(ref y, row, width, noun + " ancho (m)", ref fWidth, size, "F2", label, value, button);
                changed |= Stepper(ref y, row, width, noun + " alto (m)", ref fHeight, size, "F2", label, value, button);
                if (IsBox)
                {
                    changed |= Stepper(ref y, row, width, "Caja prof. (m)", ref fDepth, size, "F2", label, value, button);
                    if (frame.HasSolidMaterial)
                    {
                        GUI.Label(new Rect(8, y, width * 0.34f, row), "Relleno", label);
                        if (GUI.Button(new Rect(width * 0.36f, y, width * 0.64f, row), fSolid ? "Sólido (tapa el edificio)" : "Transparente", button))
                        {
                            fSolid = !fSolid;
                            changed = true;
                        }
                    }
                    y += row;
                }
                fWidth = Mathf.Max(0.5f, fWidth);
                fHeight = Mathf.Max(0.5f, fHeight);
                if (IsBox)
                    fDepth = Mathf.Max(0.5f, fDepth);
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
            GUI.EndGroup();
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
            if (HasMapB)
            {
                bPosition = bPosition0; bYaw = bYaw0;
                mapBDirty = true;
                PushMapB();
            }
            if (selector != null)
            {
                // The selector forgets what was saved and puts the scene values back on the box.
                selector.ResetBox();
                ReadFrame();
            }
            else
            {
                fPosition = fPosition0; fYaw = fYaw0; fWidth = fWidth0; fHeight = fHeight0; fDepth = fDepth0; fSolid = fSolid0;
                PushFrame();
            }
            Show("Valores de escena restaurados");
            Debug.Log($"{Tag} Ajuste de campo: valores de la escena restaurados.");
        }

        EdificioFieldAdjustmentData.Teologia BuildTeologiaBlock()
        {
            if (selector == null)
                return null;
            bool set1 = selector.GetPose(selector.MapAId, out var p1, out float y1);
            bool set2 = selector.GetPose(selector.MapBId, out var p2, out float y2);
            return new EdificioFieldAdjustmentData.Teologia
            {
                modo = (int)selector.Mode,
                mapa1Id = selector.MapAId,
                mapa2Id = selector.MapBId,
                posMapa1 = new[] { p1.x, p1.y, p1.z }, giroMapa1 = y1, fijadaMapa1 = set1,
                posMapa2 = new[] { p2.x, p2.y, p2.z }, giroMapa2 = y2, fijadaMapa2 = set2
            };
        }

        void CopyValues()
        {
            var data = new EdificioFieldAdjustmentData
            {
                generado = DateTime.Now.ToString("s"),
                build = Application.buildGUID,
                mapaB = HasMapB ? new EdificioFieldAdjustmentData.MapB { posicion = new[] { bPosition.x, bPosition.y, bPosition.z }, giroY = bYaw } : null,
                marco = new EdificioFieldAdjustmentData.Frame
                {
                    posicion = new[] { fPosition.x, fPosition.y, fPosition.z }, giroY = fYaw, ancho = fWidth, alto = fHeight,
                    profundidad = IsBox ? fDepth : 0f, solido = fSolid
                },
                teologia = BuildTeologiaBlock(),
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

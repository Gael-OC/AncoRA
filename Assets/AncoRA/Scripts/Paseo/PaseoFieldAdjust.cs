using System;
using System.IO;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Team-only bottom bar (visible with the team HUD) to place the box of the map that located last. Every change is
    /// saved on the phone at once; "Copiar valores" exports all boxes for PaseoSetup.ApplyFieldAdjustment.
    /// </summary>
    public sealed class PaseoFieldAdjust : MonoBehaviour
    {
        const string Tag = "[AncoRA Paseo]";
        const float RestoreConfirmSeconds = 3f;
        static readonly float[] PositionSteps = { 1f, 0.25f, 0.05f };
        static readonly float[] AngleSteps = { 5f, 1f, 0.2f };
        static readonly float[] SizeSteps = { 2f, 0.5f, 0.1f };
        static readonly string[] StepNames = { "grueso", "medio", "fino" };

        [SerializeField] PaseoTour tour;
        [SerializeField] PaseoHud hud;

        bool open;
        int stepIndex = 1;
        int editingMapId = -1;
        Vector3 position;
        Vector3 size;
        float yaw;
        bool solid;
        string toast = "";
        float toastUntil;
        float restoreArmedUntil;

        public PaseoTour Tour => tour;
        public PaseoHud Hud => hud;

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(PaseoTour paseoTour, PaseoHud paseoHud)
        {
            tour = paseoTour;
            hud = paseoHud;
        }

        void Read(PaseoMapContent content)
        {
            editingMapId = content.MapId;
            position = content.LocalPosition;
            yaw = content.LocalYaw;
            size = content.SizeMeters;
            solid = content.Box.Solid;
        }

        void OnGUI()
        {
            if (tour == null || hud == null || !hud.HudVisible)
                return;
            var target = tour.EditableContent;
            if (target == null)
                editingMapId = -1;
            else if (target.MapId != editingMapId)
                Read(target);

            var safe = GuiSafeArea.Rect;
            float width = safe.width;
            float row = Mathf.Max(56f, Screen.height / 26f);
            int font = Mathf.Max(16, (int)(row * 0.42f));
            var button = new GUIStyle(GUI.skin.button) { fontSize = font };
            var label = new GUIStyle(GUI.skin.label) { fontSize = font, alignment = TextAnchor.MiddleLeft };
            var value = new GUIStyle(GUI.skin.label) { fontSize = font, alignment = TextAnchor.MiddleCenter };
            var wrapped = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, font - 4), alignment = TextAnchor.MiddleLeft, wordWrap = true };
            label.normal.textColor = value.normal.textColor = wrapped.normal.textColor = Color.white;

            // Rows: header, then (when open) a title line, seven steppers and the fill toggle, then the footer.
            int rows = !open ? 0 : target == null ? 1 : 9;
            float height = row * (1 + rows) + (open ? row : 0f);
            float top = safe.height - height;
            GUI.BeginGroup(safe);
            if (open)
                GUI.Box(new Rect(0, top, width, height), GUIContent.none);

            float third = width / 3f;
            if (GUI.Button(new Rect(0, top, third, row), "Ajuste: Caja", button))
                open = !open;
            if (open && GUI.Button(new Rect(third, top, third, row), $"Paso: {StepNames[stepIndex]}", button))
                stepIndex = (stepIndex + 1) % StepNames.Length;
            if (open && GUI.Button(new Rect(2 * third, top, third, row), "Cerrar", button))
                open = false;

            float y = top + row;
            if (open)
            {
                if (target == null)
                {
                    GUI.Label(new Rect(8, y, width - 16, row), "Ubícate con un edificio para ajustar su caja.", wrapped);
                    y += row;
                }
                else
                {
                    GUI.Label(new Rect(8, y, width - 16, row),
                        $"Editando: {target.BuildingName} (mapa {target.MapId}){(target.Placed ? "" : " - SIN COLOCAR")}", wrapped);
                    y += row;
                    float p = PositionSteps[stepIndex], a = AngleSteps[stepIndex], s = SizeSteps[stepIndex];
                    bool changed = false;
                    changed |= Stepper(ref y, row, width, "X (m)", ref position.x, p, "F2", label, value, button);
                    changed |= Stepper(ref y, row, width, "Y (m)", ref position.y, p, "F2", label, value, button);
                    changed |= Stepper(ref y, row, width, "Z (m)", ref position.z, p, "F2", label, value, button);
                    changed |= Stepper(ref y, row, width, "Giro (°)", ref yaw, a, "F1", label, value, button);
                    changed |= Stepper(ref y, row, width, "Ancho (m)", ref size.x, s, "F2", label, value, button);
                    changed |= Stepper(ref y, row, width, "Alto (m)", ref size.y, s, "F2", label, value, button);
                    changed |= Stepper(ref y, row, width, "Fondo (m)", ref size.z, s, "F2", label, value, button);
                    GUI.Label(new Rect(8, y, width * 0.34f, row), "Relleno", label);
                    if (target.Box.HasSolidMaterial &&
                        GUI.Button(new Rect(width * 0.36f, y, width * 0.64f, row), solid ? "Sólido (tapa el edificio)" : "Transparente", button))
                    {
                        solid = !solid;
                        changed = true;
                    }
                    y += row;
                    size = Vector3.Max(size, Vector3.one * 0.5f);
                    if (changed)
                        tour.SaveEdit(target, position, yaw, size, solid);
                }

                if (GUI.Button(new Rect(0, y, third, row), "Copiar valores", button))
                    CopyValues();
                bool armed = Time.realtimeSinceStartup < restoreArmedUntil;
                if (GUI.Button(new Rect(third, y, third, row), armed ? "Tocar otra vez" : "Restaurar escena", button))
                {
                    if (armed)
                        tour.ForgetSavedAndReload();
                    else
                    {
                        restoreArmedUntil = Time.realtimeSinceStartup + RestoreConfirmSeconds;
                        Show("Borra lo guardado en este teléfono");
                    }
                }
                if (Time.realtimeSinceStartup < toastUntil)
                    GUI.Label(new Rect(2 * third + 8, y, third - 8, row), toast, wrapped);
            }
            GUI.EndGroup();
        }

        static bool Stepper(ref float y, float row, float width, string name, ref float current, float step, string format,
            GUIStyle label, GUIStyle value, GUIStyle button)
        {
            bool changed = false;
            GUI.Label(new Rect(8, y, width * 0.34f, row), name, label);
            if (GUI.Button(new Rect(width * 0.36f, y, width * 0.2f, row), "-", button))
            {
                current -= step;
                changed = true;
            }
            GUI.Label(new Rect(width * 0.56f, y, width * 0.24f, row), current.ToString(format), value);
            if (GUI.Button(new Rect(width * 0.8f, y, width * 0.2f, row), "+", button))
            {
                current += step;
                changed = true;
            }
            y += row;
            return changed;
        }

        void CopyValues()
        {
            string json = JsonUtility.ToJson(tour.BuildAdjustment());
            GUIUtility.systemCopyBuffer = json;
            Debug.Log($"{Tag} AJUSTE_PASEO {json}");
            try
            {
                string path = Path.Combine(Application.persistentDataPath, $"paseo-ajuste-campo-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.WriteAllText(path, json);
                Debug.Log($"{Tag} Ajuste guardado en {path}. Pegarlo en Assets/AncoRA/Paseo/ajuste-campo.json y correr ApplyFieldAdjustment.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} No se pudo guardar el ajuste en disco: {e.Message}");
            }
            Show("Copiado al portapapeles y al log");
        }

        void Show(string message)
        {
            toast = message;
            toastUntil = Time.realtimeSinceStartup + 4f;
        }
    }
}

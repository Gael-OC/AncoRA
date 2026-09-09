using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.AR
{
    /// <summary>
    /// On-device calibration panel for <see cref="ImageAnchorBuildingProbe"/>.
    ///
    /// Everything here exists to answer questions you can only answer standing in front of the real
    /// sign: where the box actually lands, whether the anchor is a real platform anchor or the world
    /// -space fallback, and whether the physical size declared for the reference image matches
    /// reality. The panel is IMGUI on purpose - it needs no scene wiring, so it can be added and
    /// removed without touching the scene file.
    ///
    /// Added automatically by the probe when its debug flag is on.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ImageAnchorBuildingProbe))]
    public sealed class CalibrationDebugHud : MonoBehaviour
    {
        enum BoxStyle
        {
            Solid,
            Translucent,
            Wireframe,
        }

        const float k_VirtualWidth = 700f;

        static readonly float[] k_Steps = { 0.01f, 0.1f, 1f };
        static readonly string[] k_StepLabels = { "1 cm", "10 cm", "1 m" };

        ImageAnchorBuildingProbe m_Probe;
        ARTrackedImageManager m_TrackedImageManager;

        bool m_PanelOpen;
        Vector2 m_Scroll;
        int m_StepIndex = 1;
        BoxStyle m_Style = BoxStyle.Solid;
        bool m_ShowAxes = true;
        float m_MeasuredDistance = 2f;

        readonly List<string> m_TargetNames = new();
        int m_TargetIndex;

        GameObject m_AxisGizmo;
        GameObject m_WireBox;
        Material m_BoxMaterial;
        Color m_BoxColor = new(0.15f, 0.75f, 1f);

        GUIStyle m_Label;
        GUIStyle m_Button;
        GUIStyle m_Header;

        void Awake()
        {
            m_Probe = GetComponent<ImageAnchorBuildingProbe>();
            m_TrackedImageManager = GetComponent<ARTrackedImageManager>();
        }

        void Start()
        {
            RefreshTargetNames();
        }

        void Update()
        {
            SyncAxisGizmo();
            SyncBoxStyle();
        }

        void RefreshTargetNames()
        {
            m_TargetNames.Clear();
            m_TargetNames.Add(string.Empty); // "any image"

            var library = m_TrackedImageManager != null ? m_TrackedImageManager.referenceLibrary : null;
            if (library != null)
            {
                for (var i = 0; i < library.count; i++)
                    m_TargetNames.Add(library[i].name);
            }

            m_TargetIndex = Mathf.Max(0, m_TargetNames.IndexOf(m_Probe.TargetImageName ?? string.Empty));
        }

        // ---------------------------------------------------------------- visuals

        void SyncAxisGizmo()
        {
            var basis = m_Probe.BasisRoot;
            if (basis == null)
                return;

            if (m_AxisGizmo == null)
                m_AxisGizmo = BuildAxisGizmo(basis);

            if (m_AxisGizmo.transform.parent != basis)
                m_AxisGizmo.transform.SetParent(basis, false);

            m_AxisGizmo.SetActive(m_ShowAxes);
        }

        GameObject BuildAxisGizmo(Transform parent)
        {
            var root = new GameObject("Axis Gizmo");
            root.transform.SetParent(parent, false);

            // Right is red, up green, forward blue - the same mapping the Unity Editor uses, so the
            // arrows read the way you already expect them to.
            AddAxis(root.transform, Vector3.right, new Color(0.95f, 0.25f, 0.2f));
            AddAxis(root.transform, Vector3.up, new Color(0.35f, 0.9f, 0.3f));
            AddAxis(root.transform, Vector3.forward, new Color(0.25f, 0.5f, 1f));
            return root;
        }

        void AddAxis(Transform parent, Vector3 direction, Color color)
        {
            const float length = 0.5f;
            const float thickness = 0.02f;

            var axis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            axis.name = $"Axis {direction}";
            foreach (var collider in axis.GetComponents<Collider>())
                Destroy(collider);

            axis.transform.SetParent(parent, false);
            axis.transform.localPosition = direction * (length * 0.5f);
            axis.transform.localScale = new Vector3(
                Mathf.Approximately(Mathf.Abs(direction.x), 1f) ? length : thickness,
                Mathf.Approximately(Mathf.Abs(direction.y), 1f) ? length : thickness,
                Mathf.Approximately(Mathf.Abs(direction.z), 1f) ? length : thickness);

            var renderer = axis.GetComponent<Renderer>();
            var material = new Material(renderer.sharedMaterial);
            SetMaterialColor(material, color);
            renderer.material = material;
        }

        void SyncBoxStyle()
        {
            var contentRenderer = m_Probe.ContentRenderer;
            if (contentRenderer == null)
                return;

            if (m_BoxMaterial == null)
            {
                m_BoxMaterial = new Material(contentRenderer.sharedMaterial);
                contentRenderer.material = m_BoxMaterial;
            }

            if (m_WireBox == null)
                m_WireBox = BuildWireBox(contentRenderer);

            switch (m_Style)
            {
                case BoxStyle.Solid:
                    contentRenderer.enabled = true;
                    m_WireBox.SetActive(false);
                    MakeOpaque(m_BoxMaterial);
                    SetMaterialColor(m_BoxMaterial, m_BoxColor);
                    break;

                case BoxStyle.Translucent:
                    contentRenderer.enabled = true;
                    m_WireBox.SetActive(true);
                    MakeTransparent(m_BoxMaterial);
                    SetMaterialColor(m_BoxMaterial, new Color(m_BoxColor.r, m_BoxColor.g, m_BoxColor.b, 0.35f));
                    break;

                case BoxStyle.Wireframe:
                    contentRenderer.enabled = false;
                    m_WireBox.SetActive(true);
                    break;
            }
        }

        /// <summary>
        /// Builds the 12 edges of the box as a line-topology mesh, parented inside the box so it
        /// picks up the calibrated scale for free. URP has no wireframe mode to switch on, and
        /// GL immediate drawing is unreliable under a scriptable pipeline.
        /// </summary>
        GameObject BuildWireBox(Renderer contentRenderer)
        {
            var bounds = contentRenderer.localBounds;
            var min = bounds.min;
            var max = bounds.max;

            var vertices = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z),
            };

            var indices = new[]
            {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7,
            };

            var mesh = new Mesh { name = "Wire Box" };
            mesh.vertices = vertices;
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();

            var wire = new GameObject("Wire Box");
            wire.transform.SetParent(contentRenderer.transform, false);
            wire.AddComponent<MeshFilter>().sharedMesh = mesh;

            var material = new Material(contentRenderer.sharedMaterial);
            MakeOpaque(material);
            SetMaterialColor(material, new Color(0.2f, 1f, 0.6f));
            wire.AddComponent<MeshRenderer>().material = material;

            return wire;
        }

        static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color * 0.4f);
        }

        static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        static void MakeOpaque(Material material)
        {
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;
        }

        // ---------------------------------------------------------------- panel

        void OnGUI()
        {
            var previousMatrix = GUI.matrix;
            var scale = Screen.width / k_VirtualWidth;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            EnsureStyles();

            var virtualHeight = Screen.height / scale;
            var toggleRect = new Rect(12f, virtualHeight - 74f, 190f, 60f);

            if (GUI.Button(toggleRect, m_PanelOpen ? "Cerrar debug" : "DEBUG", m_Button))
                m_PanelOpen = !m_PanelOpen;

            if (m_PanelOpen)
                DrawPanel(virtualHeight);

            GUI.matrix = previousMatrix;
        }

        void EnsureStyles()
        {
            if (m_Label != null)
                return;

            m_Label = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
            m_Button = new GUIStyle(GUI.skin.button) { fontSize = 20, fixedHeight = 52f };
            m_Header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.35f) }
            };
        }

        void DrawPanel(float virtualHeight)
        {
            var panel = new Rect(12f, 120f, k_VirtualWidth - 24f, virtualHeight - 210f);

            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(panel.x + 10f, panel.y + 10f, panel.width - 20f, panel.height - 20f));
            m_Scroll = GUILayout.BeginScrollView(m_Scroll);

            DrawStatusSection();
            DrawTargetSection();
            DrawStepSection();
            DrawOffsetSection();
            DrawSizeSection();
            DrawViewSection();
            DrawPersistenceSection();
            DrawSizeCheckSection();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawStatusSection()
        {
            GUILayout.Label("ESTADO", m_Header);

            var image = m_Probe.ActiveImage;
            var imageName = image != null ? image.referenceImage.name : "ninguna";
            var trackingState = image != null ? image.trackingState.ToString() : "-";

            GUILayout.Label($"Etapa: {m_Probe.State}  ({m_Probe.StabilityProgress}/{m_Probe.StabilityTarget})", m_Label);
            GUILayout.Label($"Imagen: {imageName}  [{trackingState}]", m_Label);

            var declared = m_Probe.DeclaredImageSize;
            GUILayout.Label($"Tamaño declarado: {declared.x:0.###} x {declared.y:0.###} m", m_Label);
            GUILayout.Label($"Distancia al cartel: {FormatMeters(m_Probe.DistanceToSign)}", m_Label);
            GUILayout.Label($"Distancia a la caja: {FormatMeters(m_Probe.DistanceToBox)}", m_Label);

            var anchored = m_Probe.State == ProbeState.NativeAnchored;
            GUI.color = anchored ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.65f, 0.4f);
            GUILayout.Label($"Ancla: {m_Probe.AnchorStatus}", m_Label);
            GUI.color = Color.white;

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Anclar ahora", m_Button))
                m_Probe.ForceLock();
            if (GUILayout.Button("Recalibrar", m_Button))
                m_Probe.Recalibrate();
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);
        }

        void DrawTargetSection()
        {
            GUILayout.Label("IMAGEN OBJETIVO", m_Header);

            var current = m_TargetIndex >= 0 && m_TargetIndex < m_TargetNames.Count
                ? m_TargetNames[m_TargetIndex]
                : string.Empty;
            var label = string.IsNullOrEmpty(current) ? "cualquiera" : current;

            if (GUILayout.Button($"Objetivo: {label}  (tocar para cambiar)", m_Button))
            {
                if (m_TargetNames.Count <= 1)
                    RefreshTargetNames();

                if (m_TargetNames.Count > 0)
                {
                    m_TargetIndex = (m_TargetIndex + 1) % m_TargetNames.Count;
                    m_Probe.SetTargetImageName(m_TargetNames[m_TargetIndex]);
                }
            }

            GUILayout.Space(10f);
        }

        void DrawStepSection()
        {
            GUILayout.Label("PASO DE AJUSTE", m_Header);
            GUILayout.BeginHorizontal();
            for (var i = 0; i < k_Steps.Length; i++)
            {
                var wasSelected = m_StepIndex == i;
                GUI.color = wasSelected ? new Color(1f, 0.85f, 0.35f) : Color.white;
                if (GUILayout.Button(k_StepLabels[i], m_Button))
                    m_StepIndex = i;
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);
        }

        void DrawOffsetSection()
        {
            GUILayout.Label("POSICIÓN RESPECTO AL CARTEL", m_Header);

            m_Probe.OffsetForward = DrawAdjustable("Adelante / atrás", m_Probe.OffsetForward, -50f, 50f, "m");
            m_Probe.OffsetRight = DrawAdjustable("Izquierda / derecha", m_Probe.OffsetRight, -50f, 50f, "m");
            m_Probe.OffsetUp = DrawAdjustable("Arriba / abajo", m_Probe.OffsetUp, -20f, 50f, "m");
            m_Probe.YawDegrees = DrawAdjustable("Giro (yaw)", m_Probe.YawDegrees, -180f, 180f, "°", 1f);

            GUILayout.Space(10f);
        }

        void DrawSizeSection()
        {
            GUILayout.Label("TAMAÑO DE LA CAJA", m_Header);

            var size = m_Probe.SizeMeters;
            var width = DrawAdjustable("Ancho", size.x, 0.1f, 60f, "m");
            var height = DrawAdjustable("Alto", size.y, 0.1f, 60f, "m");
            var depth = DrawAdjustable("Fondo", size.z, 0.1f, 60f, "m");

            if (!Mathf.Approximately(width, size.x) ||
                !Mathf.Approximately(height, size.y) ||
                !Mathf.Approximately(depth, size.z))
            {
                m_Probe.SizeMeters = new Vector3(width, height, depth);
            }

            if (GUILayout.Button("Cubo de prueba 1 x 1 x 1 m", m_Button))
                m_Probe.SizeMeters = Vector3.one;

            GUILayout.Space(10f);
        }

        void DrawViewSection()
        {
            GUILayout.Label("VISTA", m_Header);

            GUILayout.BeginHorizontal();
            if (DrawToggleButton("Sólido", m_Style == BoxStyle.Solid))
                m_Style = BoxStyle.Solid;
            if (DrawToggleButton("Transp.", m_Style == BoxStyle.Translucent))
                m_Style = BoxStyle.Translucent;
            if (DrawToggleButton("Aristas", m_Style == BoxStyle.Wireframe))
                m_Style = BoxStyle.Wireframe;
            GUILayout.EndHorizontal();

            if (DrawToggleButton(m_ShowAxes ? "Ejes: visibles" : "Ejes: ocultos", m_ShowAxes))
                m_ShowAxes = !m_ShowAxes;

            GUILayout.Space(10f);
        }

        void DrawPersistenceSection()
        {
            GUILayout.Label("CALIBRACIÓN", m_Header);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Guardar", m_Button))
                m_Probe.SaveCalibration();
            if (GUILayout.Button("Cargar", m_Button))
                m_Probe.LoadCalibration();
            if (GUILayout.Button("Reset", m_Button))
                m_Probe.ResetCalibration();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Copiar valores al log", m_Button))
            {
                var values = m_Probe.DescribeCalibration();
                GUIUtility.systemCopyBuffer = values;
                Debug.Log($"Calibración actual (pegar en el Inspector):\n{values}");
            }

            GUILayout.Space(10f);
        }

        void DrawSizeCheckSection()
        {
            GUILayout.Label("VERIFICAR TAMAÑO DECLARADO", m_Header);
            GUILayout.Label(
                "Párate a una distancia medida del cartel y compárala con la que reporta el sistema. " +
                "Si no coinciden, el tamaño declarado en la librería está mal y el cubo va a moverse contigo.",
                m_Label);

            m_MeasuredDistance = DrawAdjustable("Distancia real medida", m_MeasuredDistance, 0.2f, 30f, "m");

            var reported = m_Probe.DistanceToSign;
            var declared = m_Probe.DeclaredImageSize;
            var comparable = reported > 0.01f && declared.x > 0.001f;

            // Both branches must emit the same number of controls: IMGUI runs this method once for
            // the Layout event and again for Repaint, and a tracking update landing between the two
            // would otherwise throw "Mismatched LayoutGroup".
            var factor = comparable ? m_MeasuredDistance / reported : 1f;
            var suggested = declared * factor;

            GUILayout.Label(
                comparable
                    ? $"Reportada: {reported:0.##} m   |   factor: {factor:0.###}"
                    : "Apunta al cartel para poder comparar.",
                m_Label);

            GUI.color = !comparable ? Color.white
                : Mathf.Abs(factor - 1f) < 0.1f ? new Color(0.5f, 1f, 0.5f)
                : new Color(1f, 0.6f, 0.4f);
            GUILayout.Label(
                comparable
                    ? $"Ancho declarado sugerido: {suggested.x:0.###} m  (alto {suggested.y:0.###} m)"
                    : " ",
                m_Label);
            GUI.color = Color.white;

            GUILayout.Space(20f);
        }

        // ---------------------------------------------------------------- widgets

        float DrawAdjustable(string label, float value, float min, float max, string unit, float stepScale = 1f)
        {
            var step = k_Steps[m_StepIndex] * stepScale;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {value:0.###} {unit}", m_Label, GUILayout.Width(360f));
            if (GUILayout.Button("-", m_Button, GUILayout.Width(64f)))
                value -= step;
            if (GUILayout.Button("+", m_Button, GUILayout.Width(64f)))
                value += step;
            GUILayout.EndHorizontal();

            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Height(34f));
            return Mathf.Clamp(value, min, max);
        }

        bool DrawToggleButton(string label, bool selected)
        {
            GUI.color = selected ? new Color(1f, 0.85f, 0.35f) : Color.white;
            var pressed = GUILayout.Button(label, m_Button);
            GUI.color = Color.white;
            return pressed;
        }

        static string FormatMeters(float value) => value < 0f ? "-" : $"{value:0.##} m";
    }
}

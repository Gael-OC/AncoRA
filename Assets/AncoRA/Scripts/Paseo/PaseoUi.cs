using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.AR
{
    /// <summary>
    /// The tour's interface on UI Toolkit: the welcome (permissions, then AR starts), the camera overlay (status pill,
    /// search activity, bottom card, name pills over the boxes) and the team sheet (five taps on the top-left corner).
    /// Views only draw; the decisions live in PaseoStartFlow, PaseoPermissionFlow, PaseoActivity, PaseoCard,
    /// PaseoCardChoice, PaseoMapRow and PaseoBoxNudge.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PaseoUi : MonoBehaviour
    {
        const string Tag = "[AncoRA Paseo]";
        const float RefreshSeconds = 0.2f;
        const float LabelGapMeters = 1.5f;

        [SerializeField] PaseoTour tour;
        [SerializeField] ARSession arSession;
        [SerializeField] StyleSheet styleSheet;
        [SerializeField] Font regularFont;
        [SerializeField] Font strongFont;

        readonly PaseoStartFlow flow = new();
        readonly PaseoTapGesture teamGesture = new();
        readonly List<PaseoLabelItem> labelItems = new();
        readonly List<Vector3> viewportPoints = new();
        readonly List<PaseoMapContent> shownContents = new();
        PaseoPermissionFlow permissions;
        VisualElement root;
        VisualElement safe;
        PaseoWelcomeView welcome;
        PaseoCameraView cameraView;
        PaseoLabelsView labels;
        PaseoTeamSheet sheet;
        Rect appliedSafeArea;
        float nextRefresh;

        public PaseoTour Tour => tour;
        public ARSession ArSession => arSession;
        public StyleSheet StyleSheet => styleSheet;
        public Font RegularFont => regularFont;
        public Font StrongFont => strongFont;

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(PaseoTour paseoTour, ARSession session, StyleSheet css, Font regular, Font strong)
        {
            tour = paseoTour;
            arSession = session;
            styleSheet = css;
            regularFont = regular;
            strongFont = strong;
        }

        void Awake()
        {
            // The SDK clocks wait for the welcome; the scene keeps the ARSession off until the permissions are answered.
            if (tour != null)
                tour.WaitForBegin();
#if UNITY_ANDROID && !UNITY_EDITOR
            permissions = new PaseoPermissionFlow(new AndroidPermissionSystem());
#else
            permissions = new PaseoPermissionFlow(new GrantedPermissionSystem());
#endif
        }

        void Start()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);
            root.AddToClassList("paseo-root");

            labels = new PaseoLabelsView();
            root.Add(labels.Root);
            safe = new VisualElement { name = "area-segura", pickingMode = PickingMode.Ignore };
            safe.AddToClassList("paseo-safe");
            root.Add(safe);

            cameraView = new PaseoCameraView();
            cameraView.HelpRequested += () => welcome.Show();
            safe.Add(cameraView.Root);

            var corner = new VisualElement { name = "esquina-equipo" };
            corner.AddToClassList("paseo-team-corner");
            corner.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (teamGesture.Tap(Time.realtimeSinceStartup))
                    sheet.Toggle();
            });
            safe.Add(corner);

            sheet = new PaseoTeamSheet();
            sheet.EditRequested += ApplyEdit;
            sheet.CopyRequested += CopyValues;
            sheet.RestoreRequested += () => tour.ForgetSavedAndReload();
            sheet.ImageToggleRequested += tour.ToggleReducedImage;
            sheet.SearchToggleRequested += tour.ToggleSearchEveryMap;
            sheet.ShowDetectionTests(PaseoDebug.Enabled);
            safe.Add(sheet.Root);

            welcome = new PaseoWelcomeView();
            welcome.Started += OnStartPressed;
            root.Add(welcome.Root);

            PaseoUiKit.ApplyFonts(root, regularFont, strongFont);
        }

        void OnStartPressed()
        {
            if (flow.State == PaseoStartState.Started)
            {
                welcome.Hide();
                return;
            }
            if (!flow.Press())
                return;
            welcome.ShowMessage("Responde los permisos de cámara y ubicación.");
            permissions.Begin(Time.realtimeSinceStartup);
        }

        void OnPermissionsAnswered()
        {
            flow.Answer(permissions.CameraGranted, permissions.LocationGranted);
            if (flow.State == PaseoStartState.NeedsCamera)
            {
                welcome.ShowMessage("Sin la cámara la app no puede funcionar. Vuelve a tocar Comenzar, o actívala en Ajustes > Apps > AncoRA Paseo > Permisos.");
                Debug.LogWarning($"{Tag} Permiso de cámara negado: la bienvenida se queda.");
                return;
            }
            welcome.Hide();
            arSession.enabled = true;
            tour.Begin(useLocation: flow.LocationGranted);
            Debug.Log($"{Tag} Bienvenida cerrada: cámara permitida, ubicación {(flow.LocationGranted ? "permitida" : "negada")}; se enciende la sesión AR.");
        }

        void Update()
        {
            if (root == null)
                return;
            float now = Time.realtimeSinceStartup;
            if (flow.State == PaseoStartState.Requesting)
            {
                permissions.Update(now, Application.isFocused);
                if (permissions.Done)
                    OnPermissionsAnswered();
            }
            ApplySafeArea();
            if (now < nextRefresh)
                return;
            nextRefresh = now + RefreshSeconds;
            RefreshCamera();
            if (sheet.IsOpen)
                RefreshSheet();
        }

        void LateUpdate()
        {
            if (root != null)
                RefreshLabels();
        }

        // GuiSafeArea.Rect is in screen pixels with a top-left origin, which is what ScreenToPanel expects.
        void ApplySafeArea()
        {
            var area = GuiSafeArea.Rect;
            if (area == appliedSafeArea || root.panel == null)
                return;
            appliedSafeArea = area;
            var topLeft = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(area.xMin, area.yMin));
            var bottomRight = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(area.xMax, area.yMax));
            var screen = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(Screen.width, Screen.height));
            safe.style.left = topLeft.x;
            safe.style.top = topLeft.y;
            safe.style.right = screen.x - bottomRight.x;
            safe.style.bottom = screen.y - bottomRight.y;
        }

        void RefreshCamera()
        {
            bool tracking = ARSession.state == ARSessionState.SessionTracking;
            string status = PaseoStatusText.Describe(new PaseoStatusInputs
            {
                Error = tour.Error,
                ArUnsupported = ARSession.state == ARSessionState.Unsupported,
                SdkReady = tour.SdkReady,
                SecondsSinceStart = tour.SecondsSinceStart,
                MapStillLoading = tour.FirstMapStillLoading,
                Tracking = tracking,
                Buildings = tour.BuildingStatuses()
            }, out var level);
            cameraView.SetStatus(status, level);
            cameraView.SetActivity(tour.Activity());

            shownContents.Clear();
            viewportPoints.Clear();
            var cam = Camera.main;
            if (cam != null)
                foreach (var content in tour.Contents)
                    if (content != null && content.Box != null && content.IsShown)
                    {
                        shownContents.Add(content);
                        viewportPoints.Add(cam.WorldToViewportPoint(content.transform.position));
                    }
            int chosen = PaseoCardChoice.Pick(viewportPoints);
            cameraView.SetCard(chosen < 0
                ? PaseoCard.Choose(tour.SdkReady, tracking, null, null)
                : PaseoCard.Choose(tour.SdkReady, tracking, shownContents[chosen].BuildingName, shownContents[chosen].Description,
                    tour.IsConfirmed(shownContents[chosen])));
        }

        void RefreshLabels()
        {
            var cam = Camera.main;
            labelItems.Clear();
            foreach (var content in tour.Contents)
            {
                if (content == null || content.Box == null)
                    continue;
                Vector2? position = null;
                if (cam != null && root.panel != null && content.IsShown)
                {
                    var top = content.transform.position + content.transform.up * (content.SizeMeters.y * 0.5f + LabelGapMeters);
                    if (cam.WorldToViewportPoint(top).z > 0f)
                        position = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, top, cam);
                }
                labelItems.Add(new PaseoLabelItem(content.Placed ? content.BuildingName : content.BuildingName + " (sin colocar)", position));
            }
            if (labels.Set(labelItems))
                PaseoUiKit.ApplyFonts(labels.Root, regularFont, strongFont);
        }

        void RefreshSheet()
        {
            string summary = $"v{Application.version} · ARSession {ARSession.state} · SDK {(tour.SdkReady ? "listo" : "esperando")} · " +
                             $"{tour.LocalizationSummary} · {tour.GpsDescription}";
            if (!string.IsNullOrEmpty(tour.Error))
                summary += $"\nERROR: {tour.Error}";
            if (sheet.SetStatus(summary, tour.MapRows()))
                PaseoUiKit.ApplyFonts(sheet.Root, regularFont, strongFont);
            sheet.SetDetectionTests(tour.ReducedImage, tour.SearchEveryMap);
            var target = tour.EditableContent;
            sheet.SetTarget(target == null ? null : $"{target.BuildingName} · mapa {target.MapId}{(target.Placed ? "" : " · sin colocar")}");
        }

        void ApplyEdit(PaseoBoxEdit edit)
        {
            var content = tour.EditableContent;
            if (content == null)
                return;
            var position = PaseoBoxNudge.Move(content.LocalPosition, content.LocalYaw, edit.Right, edit.Closer, edit.Up);
            var size = Vector3.Max(content.SizeMeters + new Vector3(edit.Width, edit.Height, edit.Depth), Vector3.one * 0.5f);
            bool solid = edit.ToggleSolid && content.Box.HasSolidMaterial ? !content.Box.Solid : content.Box.Solid;
            tour.SaveEdit(content, position, content.LocalYaw + edit.Turn, size, solid);
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
            sheet.ShowToast("Copiado al portapapeles y al log.");
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        sealed class AndroidPermissionSystem : IPaseoPermissionSystem
        {
            public bool Has(string permission) => UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission);
            public void Request(string permission) => UnityEngine.Android.Permission.RequestUserPermission(permission);
        }
#else
        sealed class GrantedPermissionSystem : IPaseoPermissionSystem
        {
            public bool Has(string permission) => true;
            public void Request(string permission) { }
        }
#endif
    }
}

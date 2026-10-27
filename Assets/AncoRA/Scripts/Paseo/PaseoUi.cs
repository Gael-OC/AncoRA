using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.AR
{
    /// <summary>
    /// The tour's interface on UI Toolkit: the welcome (permissions, then AR starts), the camera overlay (status pill,
    /// search activity, bottom card and name pills over the boxes) and the team sheet
    /// (five taps on the top-left corner).
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
        string loggedStatus;
        PaseoCard loggedCard;

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
            cameraView.HelpRequested += () =>
            {
                LogUi("ayuda=1");
                welcome.Show();
            };
            safe.Add(cameraView.Root);

            var corner = new VisualElement { name = "esquina-equipo" };
            corner.AddToClassList("paseo-team-corner");
            corner.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (!teamGesture.Tap(Time.realtimeSinceStartup))
                    return;
                sheet.Toggle();
                LogUi($"hoja_abierta={(sheet.IsOpen ? 1 : 0)}");
            });
            safe.Add(corner);

            sheet = new PaseoTeamSheet();
            sheet.EditRequested += ApplyEdit;
            sheet.CopyRequested += CopyValues;
            sheet.RestoreRequested += () => tour.ForgetSavedAndReload();
            sheet.ShareRequested += ShareLog;
            sheet.Pressed += action => LogUi($"hoja={action}");
            safe.Add(sheet.Root);
            AttachRecordPanel();

            welcome = new PaseoWelcomeView();
            welcome.Started += () =>
            {
                LogUi("comenzar=1");
                OnStartPressed();
            };
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
            RefreshRecordingBadge();
            if (status != loggedStatus)
            {
                loggedStatus = status;
                LogUi($"estado={Compact(status)}");
            }

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
            var card = chosen < 0
                ? PaseoCard.Choose(tour.SdkReady, tracking, null, null, lookingAtGround: tour.LookingAtGround)
                : PaseoCard.Choose(tour.SdkReady, tracking, shownContents[chosen].BuildingName, shownContents[chosen].Description,
                    tour.IsConfirmed(shownContents[chosen]));
            cameraView.SetCard(card);
            if (!card.Equals(loggedCard))
            {
                loggedCard = card;
                LogUi($"tarjeta={card.Kind} titulo={Compact(card.Title)} confirmada={(card.Confirmed ? 1 : 0)}");
            }
        }

        // Record mode badge: the point being recorded and how long, or why it stopped.
        void RefreshRecordingBadge()
        {
            var r = tour.Recorder;
            if (r == null || !r.Available)
                return;
            if (r.Enabled)
                cameraView.SetRecording($"● Grabando {r.Selection.Label} · {r.PointSeconds(Time.realtimeSinceStartup):F0} s · {r.PointImages} imágenes");
            else
                cameraView.SetRecording(r.Notice, warning: true);
        }

        // Debug lines of what the screen shows and what the user touches (PaseoDebug).
        static void LogUi(string what)
        {
            if (PaseoDebug.Enabled)
                Debug.Log($"{Tag} DEPURACION_UI {what} t={Time.realtimeSinceStartup:F1}");
        }

        // One token for key=value lines: spaces become underscores.
        static string Compact(string text) => string.IsNullOrEmpty(text) ? "-" : text.Replace(' ', '_');

        async void ShareLog()
        {
            sheet.ShowToast("Preparando el registro para compartir…");
            try
            {
                sheet.ShowToast(await PaseoShare.ShareAsync());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} Compartir el registro falló: {e.Message}");
                sheet.ShowToast("No se pudo compartir: " + e.Message);
            }
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

        // Record mode (PaseoRecorder) exists in development builds only.
        void AttachRecordPanel()
        {
            var recorder = tour.Recorder;
            if (recorder == null || !recorder.Available)
                return;
            var panel = sheet.AttachRecorder(recorder.Selection);
            panel.SetBuildings(tour.Contents.Where(c => c != null).Select(c => c.BuildingId));
            panel.ToggleRequested += want =>
            {
                LogUi($"grabar={(want ? 1 : 0)}");
                if (!want)
                {
                    recorder.Disable();
                    return;
                }
                if (exportingRecordings)
                    sheet.ShowToast("Espera a que termine la copia a Descargas.");
                else if (!recorder.TryEnable(Time.realtimeSinceStartup, out string reason))
                    sheet.ShowToast(reason);
            };
            panel.CopyRequested += CopyRecordings;
            panel.DeleteRequested += DeleteRecordings;
        }

        bool exportingRecordings;

        async void CopyRecordings()
        {
            var recorder = tour.Recorder;
            if (exportingRecordings || recorder == null)
                return;
            if (recorder.Enabled)
            {
                sheet.ShowToast("Para la grabación antes de copiar.");
                return;
            }
            exportingRecordings = true;
            try
            {
                sheet.ShowToast(await PaseoRecordings.CopyToDownloadsAsync(PaseoSessionLog.RecordingsRoot, sheet.ShowToast));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} Copiar las grabaciones falló: {e.Message}");
                sheet.ShowToast("No se pudo copiar: " + e.Message);
            }
            finally
            {
                exportingRecordings = false;
            }
        }

        void DeleteRecordings()
        {
            var recorder = tour.Recorder;
            if (recorder == null || exportingRecordings)
                return;
            try
            {
                recorder.Disable();
                long freed = PaseoRecordings.DeleteAll(PaseoSessionLog.RecordingsRoot);
                recorder.ResetCounts();
                Debug.Log($"{Tag} Grabaciones borradas del teléfono: {PaseoRecordings.SizeText(freed)}.");
                sheet.ShowToast($"Borradas las grabaciones del teléfono ({PaseoRecordings.SizeText(freed)}).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} Borrar las grabaciones falló: {e.Message}");
                sheet.ShowToast("No se pudieron borrar: " + e.Message);
            }
        }

        static string RecordSummary(PaseoRecorder r)
        {
            string free = r.LastFreeBytes >= 0 ? $" · libre {PaseoRecordings.SizeText(r.LastFreeBytes)}" : "";
            string dropped = r.Dropped > 0 ? $" · {r.Dropped} omitidas (disco lento)" : "";
            string text = $"{r.Saved} imágenes · {PaseoRecordings.SizeText(r.Bytes)} de {PaseoRecordings.SizeText(r.MaxBytes)}{free}{dropped}";
            return r.Notice.Length > 0 ? r.Notice + "\n" + text : text;
        }

        void RefreshSheet()
        {
            if (tour.Recorder != null && sheet.Record != null)
                sheet.Record.SetState(tour.Recorder.Enabled, RecordSummary(tour.Recorder));
            string summary = $"v{Application.version} · ARSession {ARSession.state} · SDK {(tour.SdkReady ? "listo" : "esperando")} · " +
                             $"{tour.LocalizationSummary} · {tour.GpsDescription}";
            if (!string.IsNullOrEmpty(tour.Error))
                summary += $"\nERROR: {tour.Error}";
            if (sheet.SetStatus(summary, tour.MapRows()))
                PaseoUiKit.ApplyFonts(sheet.Root, regularFont, strongFont);
            var target = tour.EditableContent;
            sheet.SetTarget(target == null ? null : $"{target.BuildingName}{(target.Placed ? "" : " · sin colocar")}");
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

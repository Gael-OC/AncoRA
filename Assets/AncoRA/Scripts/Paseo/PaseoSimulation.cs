#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Immersal.XR;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace AncorRA.AR
{
    /// <summary>Whether the Play-mode simulated view is on; the menus AncoRA/Paseo/Simulación/… flip it.</summary>
    public static class PaseoSimulationSettings
    {
        public const string ActiveKey = "AncoRA.Paseo.VistaSimulada";

        public static bool Active
        {
            get => EditorPrefs.GetBool(ActiveKey, false);
            set => EditorPrefs.SetBool(ActiveKey, value);
        }
    }

    /// <summary>
    /// The simulated view: press Play in the Unity Editor and see the app as if standing on the sidewalk in front of a
    /// building, already localized, to iterate on eras, park and interface without a build. It uses AR Foundation's XR
    /// Simulation (the camera moves with WASD while the right mouse button is held; PaseoSimulationSetup builds the
    /// environment with a gray block per building). No localization accuracy is tested here and the real camera image is not
    /// simulated.
    /// <para>
    /// Created by <see cref="Bootstrap"/> after the scene loads, only in the Editor and only when the view is on, so neither
    /// the scene file nor a player carries it. It presses «Comenzar» for the team, stops the real Immersal search
    /// (PaseoTour.EnterSimulation) and, once ARCore (here, the simulation) tracks, hands every XR Space its authored pose
    /// through its pose filter (PaseoTour.SimulateLocation → PaseoPoseProcessor.SimulateLock), so the content stands exactly
    /// where edificio.json put it. Keys (the game view must have the focus): 1–4 the sidewalk in front of each building at
    /// 25 m, with Shift at 50 m, with Ctrl or Alt at 15 m (Ctrl+number can be an Editor window shortcut, Alt is the alias);
    /// R the exact location again; E a deliberate error of 1.5 m and 3° to preview a misalignment (not while the right mouse
    /// button is held: there E moves the camera up).
    /// </para>
    /// </summary>
    public sealed class PaseoSimulation : MonoBehaviour
    {
        const string Tag = "[AncoRA Paseo]";
        const string SimulationCameraName = "SimulationCamera";
        const float RefreshSeconds = 2f;

        PaseoTour tour;
        PaseoUi ui;
        List<PaseoSimBuilding> buildings;
        float groundY;
        bool startPressed;
        bool injectedOnce;
        bool errorOn;
        float nextRefresh;
        bool warnedNoCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!PaseoSimulationSettings.Active || FindAnyObjectByType<PaseoSimulation>() != null)
                return;
            if (FindAnyObjectByType<PaseoTour>() == null)
                return;
            new GameObject("AncoRA Vista simulada").AddComponent<PaseoSimulation>();
        }

        void Start()
        {
            tour = FindAnyObjectByType<PaseoTour>();
            ui = FindAnyObjectByType<PaseoUi>();
            if (tour == null)
            {
                Debug.LogWarning($"{Tag} SIMULACION no hay PaseoTour en la escena; la vista simulada no hace nada.");
                enabled = false;
                return;
            }
            tour.EnterSimulation();

            // The boxes as the scene holds them now, before any injected error moves them.
            string root = PaseoSimulationGeometry.DataRoot;
            var inputs = tour.Contents.Select(c => new PaseoSimInput(c.BuildingId, c.BuildingName, c.transform.position,
                c.transform.eulerAngles.y, c.SizeMeters, PaseoSimulationGeometry.ReadFront(root, c.BuildingId))).ToList();
            buildings = PaseoSimulationGeometry.Build(inputs);
            groundY = PaseoSimulationGeometry.GroundY(buildings);
            Debug.Log($"{Tag} SIMULACION teclas: {string.Join(", ", buildings.Select((b, i) => $"{i + 1}={b.Name}").Take(9))}; " +
                      $"Shift = {PaseoSimulationGeometry.FarDistanceMeters:F0} m, Ctrl o Alt = {PaseoSimulationGeometry.NearDistanceMeters:F0} m, " +
                      $"sin modificador {PaseoSimulationGeometry.DefaultDistanceMeters:F0} m; R = ubicación exacta; E = error de " +
                      $"{PaseoSimulationGeometry.ErrorMeters:F1} m y {PaseoSimulationGeometry.ErrorDegrees:F0}°.");
            foreach (var b in buildings)
                Debug.Log($"{Tag} SIMULACION frente de {b.Name}: ({b.Front.x:F2}, {b.Front.z:F2}) {(b.FrontDeclared ? "de parque.frente" : "supuesto: cara larga hacia afuera")}.");
        }

        void Update()
        {
            if (tour == null)
                return;
            if (!startPressed && ui != null && ui.PressStartForSimulation())
            {
                startPressed = true;
                Debug.Log($"{Tag} SIMULACION se pulsó «Comenzar» (permisos concedidos en el Editor).");
            }

            bool tracking = ARSession.state == ARSessionState.SessionTracking;
            var cam = Camera.main;
            if (tracking && tour.Begun && cam != null)
            {
                // First time, and again whenever tracking dropped and the filters were reset.
                if (!injectedOnce || !tour.AllSpacesHavePose)
                {
                    injectedOnce = true;
                    Inject(errorOn, cam);
                }
                else if (Time.realtimeSinceStartup >= nextRefresh)
                {
                    nextRefresh = Time.realtimeSinceStartup + RefreshSeconds;
                    tour.RefreshSimulatedLocation();
                }
            }

            HandleKeys(cam);
        }

        void HandleKeys(Camera cam)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                        Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            for (int i = 0; i < 4 && i < buildings.Count; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    GoTo(buildings[i], PaseoSimulationGeometry.DistanceFor(shift, ctrl));

            if (Input.GetKeyDown(KeyCode.R) && cam != null && tour.Begun)
            {
                errorOn = false;
                Inject(false, cam);
            }
            // With the right mouse button held, E is the simulation's «up» key.
            if (Input.GetKeyDown(KeyCode.E) && !Input.GetMouseButton(1) && cam != null && tour.Begun)
            {
                errorOn = true;
                Inject(true, cam);
            }
        }

        void Inject(bool withError, Camera cam)
        {
            var cameraPosition = cam.transform.position;
            var right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
            tour.SimulateLocation(space =>
            {
                var initial = space.InitialPose;
                var authored = new Pose(initial.GetPosition(), initial.rotation);
                return withError
                    ? PaseoSimulationGeometry.WithError(authored, cameraPosition, right, PaseoSimulationGeometry.ErrorMeters, PaseoSimulationGeometry.ErrorDegrees)
                    : authored;
            }, cameraPosition);
            if (withError)
                Debug.Log($"{Tag} SIMULACION error inyectado: {PaseoSimulationGeometry.ErrorMeters:F1} m a la derecha de la cámara y " +
                          $"{PaseoSimulationGeometry.ErrorDegrees:F0}° de giro (R vuelve a la ubicación exacta).");
            else
                Debug.Log($"{Tag} SIMULACION ubicación exacta: el contenido queda donde lo dejó edificio.json.");
        }

        void GoTo(PaseoSimBuilding building, float distance)
        {
            var camera = GameObject.Find(SimulationCameraName);
            if (camera == null)
            {
                if (!warnedNoCamera)
                    Debug.LogWarning($"{Tag} SIMULACION no hay cámara simulada: falta activar la vista simulada (AncoRA/Paseo/Simulación) y reentrar a Play.");
                warnedNoCamera = true;
                return;
            }
            float clear = PaseoSimulationGeometry.ClearDistance(building, distance, buildings);
            var pose = PaseoSimulationGeometry.Viewpoint(building, clear, groundY);
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            string note = clear < distance ? $" (pedido {distance:F0} m: más lejos hay otro edificio)" : "";
            Debug.Log($"{Tag} SIMULACION cámara frente a {building.Name} a {clear:F1} m{note} (posición {pose.position.x:F1}, {pose.position.y:F1}, {pose.position.z:F1}).");
        }
    }
}
#endif

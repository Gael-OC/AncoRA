using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Immersal;
using Immersal.XR;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Localization method of the tour, in place of the SDK's DeviceLocalization (the only method of the Localizer, set by
    /// PaseoSetup.Prepare). DeviceLocalization always asks the plugin for every loaded map: in the field test of 14-oct a
    /// try against the seven maps took 9.5 s on average when nothing matched. This one asks only for the maps
    /// <see cref="NextSearch"/> chooses for each attempt (PaseoSearchPlan, through PaseoTour) and reports every attempt,
    /// with or without a match.
    /// </summary>
    public sealed class PaseoLocalization : MonoBehaviour, ILocalizationMethod
    {
        const string Tag = "[AncoRA Paseo]";

        /// <summary>Looking further down than this (degrees, up is positive) sees mostly ground: no attempt is made.</summary>
        public const float MinPitchDegrees = -30f;

        readonly HashSet<int> configured = new();
        int attempts;
        bool skippingGround;

        /// <summary>What the next attempt searches; unset, every attempt searches every map.</summary>
        public Func<PaseoSearch> NextSearch { get; set; }

        /// <summary>Main thread, as an attempt starts: what it really searches (<see cref="Resolve"/>).</summary>
        public event Action<PaseoSearch> AttemptStarted;

        /// <summary>Main thread, as an attempt ends: what it searched, how long it took and what it found.</summary>
        public event Action<PaseoAttempt> AttemptFinished;

        /// <summary>Whether this attempt's image is kept as a debug photo (PaseoDebugImages); unset, none is.</summary>
        public Func<bool> CaptureImage { get; set; }

        /// <summary>Main thread, after AttemptFinished, with the image CaptureImage asked for: pixels, width, height, channels.</summary>
        public event Action<PaseoAttempt, byte[], int, int, int> ImageCaptured;

        /// <summary>
        /// The SDK configured this method. That is the last step of its own start (ImmersalSDK.ConfigureComponents), so
        /// from here a second start cannot overlap the first one (PaseoSdkStart).
        /// </summary>
        public bool Configured { get; private set; }

        public ConfigurationMode ConfigurationMode => ConfigurationMode.Always;

        public IMapOption[] MapOptions => new IMapOption[] { new MapLoadingOption() };

        public Task<bool> Configure(ILocalizationMethodConfiguration configuration)
        {
            if (configuration.MapsToAdd != null)
                foreach (var map in configuration.MapsToAdd)
                    configured.Add(map.mapId);
            if (configuration.MapsToRemove != null)
            {
                foreach (var map in configuration.MapsToRemove)
                    configured.Remove(map.mapId);
                if (configured.Count == 0)
                    return Task.FromResult(false);
            }
            Configured = true;
            Debug.Log($"{Tag} Método de ubicación del paseo configurado con {configured.Count} mapa(s): busca por edificio.");
            return Task.FromResult(true);
        }

        public async Task<ILocalizationResult> Localize(ICameraData cameraData, CancellationToken cancellationToken)
        {
            // Field test of 26-oct: about a third of the attempts looked at feet and floor. They cost CPU and tell nothing
            // about a map, so they are skipped before the search plan advances; the next one comes after the pace interval.
            float pitch = PitchDegrees(cameraData.CameraRotationOnCapture);
            if (pitch < MinPitchDegrees)
            {
                if (!skippingGround)
                    Debug.Log($"{Tag} Intento omitido: la cámara apunta al suelo (inclinación {pitch:F0}°).");
                skippingGround = true;
                return new LocalizationResult();
            }
            skippingGround = false;

            var wanted = NextSearch?.Invoke() ?? new PaseoSearch(null, null, configured.ToArray(), PaseoSearchReason.All, double.NaN);
            var search = Resolve(wanted, configured, id => MapHandleMapping.TryGetHandle(id, out _), out int[] pluginIds);
            int number = ++attempts;

            using IImageData imageData = cameraData.GetImageData();
            AttemptStarted?.Invoke(search);
            IntPtr pixels = imageData.UnmanagedDataPointer;
            byte[] photo = CaptureImage?.Invoke() == true ? cameraData.GetBytes() : null;
            // Same call as Core.LocalizeImage(ICameraData, …), with the map ids instead of n = 0 (every loaded map).
            int n = pluginIds?.Length ?? 0;
            int[] ids = pluginIds ?? new int[1];
            int width = cameraData.Width;
            int height = cameraData.Height;
            int channels = cameraData.Channels == 0 ? 1 : cameraData.Channels;
            Vector4 intrinsics = cameraData.Intrinsics;
            Quaternion rotation = cameraData.CameraRotationOnCapture * cameraData.ScreenOrientation;
            rotation.SwitchHandedness();

            float start = Time.realtimeSinceStartup;
            LocalizeInfo info = await Task.Run(() =>
                Core.LocalizeImage(n, ids, width, height, ref intrinsics, pixels, channels, (int)SolverType.Default, ref rotation), cancellationToken);
            float now = Time.realtimeSinceStartup;

            var result = new LocalizationResult();
            if (info.mapId > 0)
            {
                result.Success = true;
                result.MapId = info.mapId;
                result.LocalizeInfo = info;
            }
            var attempt = new PaseoAttempt(number, search, now - start, result.MapId, result.Success ? info.confidence : 0, now,
                width, height, cameraData.CameraPositionOnCapture, cameraData.CameraRotationOnCapture.eulerAngles.y,
                result.Success ? info.rmse : double.NaN);
            AttemptFinished?.Invoke(attempt);
            if (photo != null)
                ImageCaptured?.Invoke(attempt, photo, width, height, channels);
            return result;
        }

        public Task StopAndCleanUp()
        {
            // A full SDK reset configures the localizer again with every map.
            configured.Clear();
            Configured = false;
            return Task.CompletedTask;
        }

        public Task OnMapRegistered(XRMap map)
        {
            // As DeviceLocalization: a map needs its loading option (PaseoSetup embeds the .bytes).
            if (map.MapOptions.FirstOrDefault(option => option.Name == "MapLoading") is not MapLoadingOption)
            {
                Debug.LogWarning($"{Tag} El mapa {map.mapName} no traía opción de carga; se usa su .bytes embebido.");
                map.MapOptions.Add(new MapLoadingOption
                {
                    m_SerializedDataSource = map.mapFile == null ? 1 : 0,
                    DownloadVisualizationAtRuntime = false
                });
            }
            return Task.CompletedTask;
        }

        /// <summary>Elevation of the viewing direction in degrees: 0 at the horizon, negative looking down, as DEPURACION_POSE's incl.</summary>
        public static float PitchDegrees(Quaternion cameraRotation)
        {
            float up = (cameraRotation * Vector3.forward).y;
            return Mathf.Asin(Mathf.Clamp(up, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// What an attempt really searches and the ids to hand the plugin (null: every loaded map, n = 0). A building is
        /// searched with its maps the SDK configured and the plugin loaded; when none is usable the attempt searches every
        /// loaded map instead, and says so (no building, reason All).
        /// </summary>
        public static PaseoSearch Resolve(PaseoSearch wanted, ICollection<int> configuredMaps, Func<int, bool> inPlugin, out int[] pluginIds)
        {
            var loaded = configuredMaps.Where(inPlugin).ToArray();
            var mapIds = wanted.MapIds ?? Array.Empty<int>();
            bool usable = wanted.BuildingId != null && mapIds.Any(id => configuredMaps.Contains(id) && inPlugin(id));
            if (!usable)
            {
                pluginIds = null;
                return new PaseoSearch(null, null, loaded, PaseoSearchReason.All, double.NaN);
            }
            pluginIds = Searchable(mapIds, configuredMaps, inPlugin);
            return new PaseoSearch(wanted.BuildingId, wanted.BuildingName, pluginIds ?? loaded, wanted.Reason, wanted.GpsDistanceMeters);
        }

        /// <summary>
        /// The map ids to hand the plugin: the wanted ones the SDK configured and the plugin loaded (one id without a
        /// handle fails the whole attempt). Null means every loaded map (n = 0), used when nothing usable is left or
        /// when the subset is every map anyway.
        /// </summary>
        public static int[] Searchable(int[] wanted, ICollection<int> configuredMaps, Func<int, bool> inPlugin)
        {
            var usable = wanted.Where(id => configuredMaps.Contains(id) && inPlugin(id)).Distinct().ToArray();
            if (usable.Length == 0 || usable.Length == configuredMaps.Count(inPlugin))
                return null;
            return usable;
        }
    }
}

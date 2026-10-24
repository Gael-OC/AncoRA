using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
    /// Since 1.3.6 it also runs the team's A/B tests (team sheet, development builds), all off by default: CLAHE contrast
    /// enhancement of the image before the plugin sees it, the plugin's gravity solver (Lean), and a search with only the
    /// building's located map and the prior position of the camera in it.
    /// </summary>
    public sealed class PaseoLocalization : MonoBehaviour, ILocalizationMethod
    {
        const string Tag = "[AncoRA Paseo]";

        // The SDK's own defaults for a search with a prior position (DeviceLocalization); the plugin crashes with 0 neighbours.
        const int PriorNeighboursMin = 60;
        const int PriorNeighboursMax = 720;
        const float PriorRadiusMeters = 6f;
        const float PriorFilterRadius = 0f;

        readonly HashSet<int> configured = new();
        // Buildings whose last attempt with a prior failed: the next attempt for each uses the normal search.
        readonly HashSet<string> priorSkips = new();
        int attempts;
        // The ~2 MB buffers of the contrast test, reused by every attempt (the attempts do not overlap; if one ever did, it
        // would get buffers of its own).
        byte[] sourceBuffer;
        byte[] enhancedBuffer;
        bool buffersBusy;
        bool reportedNoEnhance;

        /// <summary>What the next attempt searches; unset, every attempt searches every map.</summary>
        public Func<PaseoSearch> NextSearch { get; set; }

        /// <summary>The team's contrast test: CLAHE of the grey image before localizing (Variant), or on alternate attempts.</summary>
        public PaseoAbMode Contrast { get; set; }

        /// <summary>The team's solver test: the plugin's gravity solver, Lean (Variant), or on alternate attempts.</summary>
        public PaseoAbMode Solver { get; set; }

        /// <summary>The team's prior test: a building whose map is located is searched with that map only, near where the camera is.</summary>
        public bool UsePrior { get; set; }

        /// <summary>
        /// Main thread: the map of the searched building that is located now (its XR Space has a pose in this tracking period),
        /// or 0; unset, none is. Only asked when <see cref="UsePrior"/> is on.
        /// </summary>
        public Func<PaseoSearch, int> LocatedMap { get; set; }

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
            var wanted = NextSearch?.Invoke() ?? new PaseoSearch(null, null, configured.ToArray(), PaseoSearchReason.All, double.NaN);
            var search = Resolve(wanted, configured, id => MapHandleMapping.TryGetHandle(id, out _), out int[] pluginIds);
            int number = ++attempts;

            using IImageData imageData = cameraData.GetImageData();
            // What the team's tests do to this attempt, decided here on the main thread (with the image held, so a failure
            // here still releases it).
            var choice = PaseoAbTest.Choose(Contrast, Solver, number);
            int priorMap = ChoosePrior(search, cameraData, out int priorHandle, out Vector3 priorPosition);
            if (priorMap > 0)
                search = OnlyMap(search, priorMap);
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
            int solverType = (int)(choice.Gravity ? SolverType.Lean : SolverType.Default);

            // The contrast test needs a grey image; a colour one cannot be equalised channel by channel without changing hue.
            var contrast = !choice.Contrast ? PaseoContrastUse.Normal : channels == 1 ? PaseoContrastUse.Enhanced : PaseoContrastUse.NotApplicable;
            if (contrast == PaseoContrastUse.NotApplicable && !reportedNoEnhance)
            {
                reportedNoEnhance = true;
                Debug.LogWarning($"{Tag} El realce de contraste no se aplica: la imagen de la cámara trae {channels} canales, no 1.");
            }
            byte[] source = null, enhanced = null;
            bool sharedBuffers = contrast == PaseoContrastUse.Enhanced && TakeBuffers(width * height, out source, out enhanced);
            float enhanceMilliseconds = float.NaN;

            float start = Time.realtimeSinceStartup;
            LocalizeInfo info;
            try
            {
                info = await Task.Run(() =>
                {
                    GCHandle pin = default;
                    IntPtr image = pixels;
                    try
                    {
                        if (contrast == PaseoContrastUse.Enhanced)
                        {
                            // Worker thread, while the camera image is still held: copy it, equalise it, and hand the plugin the result.
                            var timer = System.Diagnostics.Stopwatch.StartNew();
                            Marshal.Copy(pixels, source, 0, width * height);
                            PaseoImageEnhance.Clahe(source, enhanced, width, height);
                            enhanceMilliseconds = (float)timer.Elapsed.TotalMilliseconds;
                            pin = GCHandle.Alloc(enhanced, GCHandleType.Pinned);
                            image = pin.AddrOfPinnedObject();
                        }
                        return priorMap > 0
                            ? LocalizeWithPrior(priorHandle, width, height, ref intrinsics, image, channels, ref priorPosition)
                            : Core.LocalizeImage(n, ids, width, height, ref intrinsics, image, channels, solverType, ref rotation);
                    }
                    finally
                    {
                        if (pin.IsAllocated)
                            pin.Free();
                    }
                }, cancellationToken);
            }
            finally
            {
                if (sharedBuffers)
                    buffersBusy = false;
            }
            float now = Time.realtimeSinceStartup;

            var result = new LocalizationResult();
            if (info.mapId > 0)
            {
                result.Success = true;
                result.MapId = info.mapId;
                result.LocalizeInfo = info;
            }
            if (priorMap > 0 && !result.Success)
                priorSkips.Add(search.BuildingId);
            var attempt = new PaseoAttempt(number, search, now - start, result.MapId, result.Success ? info.confidence : 0, now,
                width, height, cameraData.CameraPositionOnCapture, cameraData.CameraRotationOnCapture.eulerAngles.y,
                contrast, choice.Gravity, priorMap > 0, contrast == PaseoContrastUse.Enhanced ? enhanceMilliseconds : float.NaN,
                result.Success ? info.rmse : double.NaN);
            AttemptFinished?.Invoke(attempt);
            if (photo != null)
                ImageCaptured?.Invoke(attempt, photo, width, height, channels);
            return result;
        }

        // Main thread. With the prior test on, the map of the searched building to search with a prior position (see PriorMap),
        // its plugin handle and the camera position in it, computed as the SDK does for its Prior solver (DeviceLocalization);
        // 0 for the normal search. An attempt that has to skip the prior still consumes the building's pending skip.
        int ChoosePrior(PaseoSearch search, ICameraData cameraData, out int handle, out Vector3 position)
        {
            handle = 0;
            position = default;
            bool skip = search.BuildingId != null && priorSkips.Remove(search.BuildingId);
            int located = UsePrior && !skip && search.BuildingId != null ? LocatedMap?.Invoke(search) ?? 0 : 0;
            int map = PriorMap(UsePrior, search, located, skip, configured, id => MapHandleMapping.TryGetHandle(id, out _));
            if (map <= 0 || !MapHandleMapping.TryGetHandle(map, out handle) || !MapManager.TryGetMapEntry(map, out MapEntry entry) ||
                entry.SceneParent == null || entry.Relation == null)
                return 0;
            Matrix4x4 mapPose = entry.SceneParent.ToMapSpace(cameraData.CameraPositionOnCapture, Quaternion.identity);
            Vector3 prior = entry.Relation.ApplyInverseRelation(mapPose).GetPosition();
            prior.SwitchHandedness();
            position = prior;
            return map;
        }

        // The same call as Core.LocalizeImageWithPrior, for one map handle instead of n = 0 (every map). The prior has no solver
        // choice and ignores the camera rotation.
        static LocalizeInfo LocalizeWithPrior(int mapHandle, int width, int height, ref Vector4 intrinsics, IntPtr pixels, int channels,
            ref Vector3 priorPosition)
        {
            Vector3 priorScale = Vector3.one;
            GCHandle handles = GCHandle.Alloc(new[] { mapHandle }, GCHandleType.Pinned);
            try
            {
                LocalizeInfo result = Native.icvLocalizePrior(1, handles.AddrOfPinnedObject(), width, height, ref intrinsics, pixels, channels,
                    ref priorPosition, ref priorScale, PriorNeighboursMin, PriorNeighboursMax, PriorRadiusMeters, PriorFilterRadius);
                // result.mapId is a plugin handle at this point -> convert, as the SDK does.
                if (MapHandleMapping.TryGetMapId(result.mapId, out int mapId))
                {
                    result.mapId = mapId;
                    return result;
                }
            }
            finally
            {
                if (handles.IsAllocated)
                    handles.Free();
            }
            return new LocalizeInfo { mapId = -1 };
        }

        // The buffers of the contrast test for an image of this many bytes: the shared ones, or (if an attempt still holds them)
        // new ones. True when the shared ones were taken, which the caller gives back.
        bool TakeBuffers(int length, out byte[] source, out byte[] enhanced)
        {
            if (buffersBusy)
            {
                source = new byte[length];
                enhanced = new byte[length];
                return false;
            }
            if (sourceBuffer == null || sourceBuffer.Length != length)
            {
                sourceBuffer = new byte[length];
                enhancedBuffer = new byte[length];
            }
            buffersBusy = true;
            source = sourceBuffer;
            enhanced = enhancedBuffer;
            return true;
        }

        public Task StopAndCleanUp()
        {
            // A full SDK reset configures the localizer again with every map.
            configured.Clear();
            priorSkips.Clear();
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
                // The camera heading stays in the log; the difference to a building does not apply to a search over everything.
                return new PaseoSearch(null, null, loaded, PaseoSearchReason.All, double.NaN, wanted.HeadingDegrees);
            }
            pluginIds = Searchable(mapIds, configuredMaps, inPlugin);
            return new PaseoSearch(wanted.BuildingId, wanted.BuildingName, pluginIds ?? loaded, wanted.Reason, wanted.GpsDistanceMeters,
                wanted.HeadingDegrees, wanted.HeadingDifferenceDegrees);
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

        /// <summary>
        /// The map an attempt searches with a prior position, or 0 for the normal search. The prior test needs the searched
        /// building to be a real one (not the fallback over every map), a map of it that is located now
        /// (<paramref name="locatedMap"/>, which PaseoTour decides) that the SDK configured and the plugin loaded, and the
        /// building's previous prior attempt not to have failed (<paramref name="skipOnce"/>): a stale prior position must not
        /// block the normal search.
        /// </summary>
        public static int PriorMap(bool enabled, PaseoSearch search, int locatedMap, bool skipOnce, ICollection<int> configuredMaps, Func<int, bool> inPlugin)
        {
            if (!enabled || skipOnce || search.BuildingId == null || locatedMap <= 0)
                return 0;
            bool usable = search.MapIds != null && search.MapIds.Contains(locatedMap) && configuredMaps.Contains(locatedMap) && inPlugin(locatedMap);
            return usable ? locatedMap : 0;
        }

        /// <summary>The same search narrowed to one map, so its log line says what the plugin was really asked.</summary>
        public static PaseoSearch OnlyMap(PaseoSearch search, int mapId) =>
            new(search.BuildingId, search.BuildingName, new[] { mapId }, search.Reason, search.GpsDistanceMeters,
                search.HeadingDegrees, search.HeadingDifferenceDegrees);
    }
}

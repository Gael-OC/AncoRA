using System;
using System.Collections.Generic;
using System.IO;

namespace AncorRA.AR
{
    public enum PaseoMultiSetKind
    {
        Map,
        MapSet
    }

    public enum PaseoMultiSetConfigStatus
    {
        Ready,
        Missing,
        Invalid,
        NoCredentials,
        NoCode
    }

    /// <summary>
    /// What the side-by-side test with MultiSet reads from multiset.json in the phone's persistentDataPath (never from the
    /// APK, the scene or git: the credentials are the team's). Optional values have defaults; a map code that no building
    /// names is simply not matched to a building.
    /// </summary>
    public sealed class PaseoMultiSetConfig
    {
        public const string FileName = "multiset.json";
        public const int DefaultMaxQueries = 120;
        public const float DefaultSecondsAfterLocated = 10f;
        public const float DefaultGpsRadiusMeters = 25f;
        // The SDK's own range for the GPS hint radius.
        public const float MinGpsRadiusMeters = 5f;
        public const float MaxGpsRadiusMeters = 100f;
        public const float MinSecondsAfterLocated = 1f;

        readonly Dictionary<string, string> buildings = new(StringComparer.Ordinal);

        public string ClientId { get; private set; } = "";
        public string ClientSecret { get; private set; } = "";
        /// <summary>The map or map set code the SDK localizes against.</summary>
        public string Code { get; private set; } = "";
        public PaseoMultiSetKind Kind { get; private set; } = PaseoMultiSetKind.Map;
        /// <summary>MultiSet map code to the building folder id of the tour (Teologia, X1, CienciasBasicas, EIC).</summary>
        public IReadOnlyDictionary<string, string> Buildings => buildings;
        /// <summary>Whether the phone's GPS goes along as a hint. Off unless asked: it only helps when the map is georeferenced in the portal.</summary>
        public bool GpsHint { get; private set; }
        public float GpsRadiusMeters { get; private set; } = DefaultGpsRadiusMeters;
        /// <summary>Queries per session; the free plan has 1000 in all.</summary>
        public int MaxQueries { get; private set; } = DefaultMaxQueries;
        /// <summary>Seconds between queries once a location was accepted.</summary>
        public float SecondsAfterLocated { get; private set; } = DefaultSecondsAfterLocated;

        public bool HasCredentials => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

        /// <summary>The building a map code belongs to; null when multiset.json does not say.</summary>
        public string BuildingFor(string mapCode) =>
            !string.IsNullOrEmpty(mapCode) && buildings.TryGetValue(mapCode, out string building) ? building : null;

        /// <summary>Throws FormatException when the text is not a valid multiset.json.</summary>
        public static PaseoMultiSetConfig Parse(string json)
        {
            if (PaseoMiniJson.Parse(json) is not Dictionary<string, object> root)
                throw new FormatException("multiset.json debe ser un objeto { … }.");
            var config = new PaseoMultiSetConfig
            {
                ClientId = Text(root, "clientId"),
                ClientSecret = Text(root, "clientSecret"),
                Code = Text(root, "codigo")
            };
            string kind = Text(root, "tipo").ToLowerInvariant();
            config.Kind = kind switch
            {
                "" or "map" => PaseoMultiSetKind.Map,
                "mapset" => PaseoMultiSetKind.MapSet,
                _ => throw new FormatException($"«tipo» debe ser map o mapset (dice «{kind}»).")
            };
            if (root.TryGetValue("edificios", out object table) && table != null)
            {
                if (table is not Dictionary<string, object> pairs)
                    throw new FormatException("«edificios» debe ser un objeto { \"MAP_XXXX\": \"Teologia\" }.");
                foreach (var pair in pairs)
                    config.buildings[pair.Key] = pair.Value as string ?? throw new FormatException($"«edificios» → «{pair.Key}» debe ser un texto.");
            }
            config.GpsHint = Flag(root, "pistaGps", false);
            config.GpsRadiusMeters = Clamp(Number(root, "radioGps", DefaultGpsRadiusMeters), MinGpsRadiusMeters, MaxGpsRadiusMeters);
            double queries = Number(root, "maxConsultas", DefaultMaxQueries);
            if (queries < 1)
                throw new FormatException("«maxConsultas» debe ser al menos 1.");
            config.MaxQueries = (int)Math.Min(queries, 100000);
            config.SecondsAfterLocated = (float)Math.Max(Number(root, "segundosTrasUbicar", DefaultSecondsAfterLocated), MinSecondsAfterLocated);
            return config;
        }

        /// <summary>
        /// Reads folder/multiset.json. Anything short of Ready leaves MultiSet off; <paramref name="message"/> says why, in one
        /// line, without ever quoting the credentials.
        /// </summary>
        public static PaseoMultiSetConfigStatus Load(string folder, out PaseoMultiSetConfig config, out string message)
        {
            config = null;
            string path = Path.Combine(folder ?? "", FileName);
            if (!File.Exists(path))
            {
                message = $"MultiSet apagado: falta {FileName} en {folder}";
                return PaseoMultiSetConfigStatus.Missing;
            }
            try
            {
                config = Parse(File.ReadAllText(path));
            }
            catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
            {
                message = $"MultiSet apagado: {FileName} no se pudo leer ({e.Message})";
                return PaseoMultiSetConfigStatus.Invalid;
            }
            var status = Check(config, out message);
            if (status != PaseoMultiSetConfigStatus.Ready)
                config = null;
            return status;
        }

        public static PaseoMultiSetConfigStatus Check(PaseoMultiSetConfig config, out string message)
        {
            if (!config.HasCredentials)
            {
                message = $"MultiSet apagado: {FileName} no trae clientId y clientSecret";
                return PaseoMultiSetConfigStatus.NoCredentials;
            }
            if (string.IsNullOrWhiteSpace(config.Code))
            {
                message = $"MultiSet apagado: {FileName} no trae el código del mapa («codigo»)";
                return PaseoMultiSetConfigStatus.NoCode;
            }
            message = "";
            return PaseoMultiSetConfigStatus.Ready;
        }

        static string Text(Dictionary<string, object> root, string key)
        {
            if (!root.TryGetValue(key, out object value) || value == null)
                return "";
            return value as string ?? throw new FormatException($"«{key}» debe ser un texto.");
        }

        static bool Flag(Dictionary<string, object> root, string key, bool fallback)
        {
            if (!root.TryGetValue(key, out object value) || value == null)
                return fallback;
            return value is bool flag ? flag : throw new FormatException($"«{key}» debe ser true o false.");
        }

        static double Number(Dictionary<string, object> root, string key, double fallback)
        {
            if (!root.TryGetValue(key, out object value) || value == null)
                return fallback;
            return value is double number && !double.IsNaN(number) ? number : throw new FormatException($"«{key}» debe ser un número.");
        }

        static float Clamp(double value, float min, float max) => (float)Math.Min(Math.Max(value, min), max);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// edificio.json of one building of the tour. Field names are Spanish because the team edits these files by hand,
    /// and they match the JSON keys one to one (JsonUtility).
    /// </summary>
    [Serializable]
    public sealed class PaseoBuildingConfig
    {
        public string nombre;
        public string descripcion = "";
        public float[] tamano = { 20f, 8f, 12f };
        public bool solido;
        // Optional low-poly replacement, baked by Prepare. Blank keeps the diagnostic box.
        public string modelo;
        // True: the building keeps locating (its maps still count) but its box and card are not drawn.
        public bool oculta;
        // False: the localization does not search this building's maps (its box is still placed through the campus frame by
        // whichever building locates). Saves the time of attempts that cannot match, e.g. a demo from a single viewpoint.
        public bool buscar = true;
        public PaseoGpsPoint gps;
        // Pose of the building box in the frame shared by the whole campus (campus mode); absent = per-building frame.
        public PaseoCampusPose campus;
        // Animated park over and around the box; JsonUtility builds an empty one when absent (see PaseoConfig.HasPark).
        public PaseoParkConfig parque;
        public List<PaseoMapConfig> mapas = new();
    }

    /// <summary>Centre of the building box on the ground (degrees, WGS84), from the drone cloud.</summary>
    [Serializable]
    public sealed class PaseoGpsPoint
    {
        public double latitud;
        public double longitud;
    }

    /// <summary>
    /// Pose of the building box in the campus frame, from the campus cloud (metres and degrees, Unity axes: x east, y up,
    /// z north). Not a PaseoBoxPose: that one defaults to a filler 12 m in front of the camera. «nube» is the crop of the
    /// campus cloud this pose was measured in; the app ignores it and it only has to survive a rewrite of the file.
    /// </summary>
    [Serializable]
    public sealed class PaseoCampusPose
    {
        public float[] posicion;
        public float giro;
        public string nube;
    }

    /// <summary>Animated park of a building: «tema» picks the content, «escala» the size of the characters, «frente» the front edge.</summary>
    [Serializable]
    public sealed class PaseoParkConfig
    {
        public static readonly string[] Themes = { "ciencias", "pista", "saludo", "baile", "paseo", "pasillo" };
        public static readonly string[] Fronts = { "+z", "-z", "+x", "-x" };

        public string tema;
        public float escala = 1.5f;
        public string frente = "+z";

        // Theme «pasillo»: a few people walking back and forth along a line that is not tied to the box, e.g. the corridor between
        // two buildings. «desde» and «hasta» are (este, norte) in the campus frame and «suelo» the height of the ground there, like
        // «campus.posicion» (x east, y up, z north); «personas» is how many walk.
        public float[] desde;
        public float[] hasta;
        public float suelo;
        public int personas = 3;

        // The same line in the box frame (x, ground height, z), set by SetBoxLocalLine; not part of edificio.json.
        [NonSerialized] public Vector3 localFrom;
        [NonSerialized] public Vector3 localTo;

        public bool HasLine => desde != null && desde.Length == 2 && hasta != null && hasta.Length == 2;

        /// <summary>Puts the campus line in the frame of the box that holds the park (its pose in the campus frame).</summary>
        public void SetBoxLocalLine(PaseoCampusPose pose)
        {
            var toBox = Quaternion.Inverse(Quaternion.Euler(0f, pose.giro, 0f));
            Vector3 Local(float[] point) =>
                toBox * new Vector3(point[0] - pose.posicion[0], suelo - pose.posicion[1], point[1] - pose.posicion[2]);
            localFrom = Local(desde);
            localTo = Local(hasta);
        }
    }

    [Serializable]
    public sealed class PaseoMapConfig
    {
        public int id;
        public string archivo;
        public PaseoBoxPose caja = new();
    }

    /// <summary>
    /// Box pose in the frame of its own map. The box is one per building: every map of a building holds the same physical
    /// box in its own frame, and PaseoBuildingFrame relates the frames of the maps through those poses.
    /// </summary>
    [Serializable]
    public sealed class PaseoBoxPose
    {
        public float[] posicion = { 0f, 0f, 12f };
        public float giro;
        public bool colocada;
    }

    public sealed class PaseoValidation
    {
        public readonly List<string> Errors = new();
        public readonly List<string> Warnings = new();
        public bool Ok => Errors.Count == 0;
    }

    public static class PaseoConfig
    {
        public const string FileName = "edificio.json";

        // A filler value, not a measurement: the team sizes each box on site.
        public static readonly float[] DefaultSize = { 20f, 8f, 12f };
        public static readonly string[] BuildingModels = { "academico", "pabellon", "clasico" };

        static readonly HashSet<int> SampleMapIds = new() { 90687, 90688, 90689, 90690 };
        static readonly Regex MapFilePattern = new(@"^(\d+)-[^\\/]+\.bytes$");

        public static PaseoBuildingConfig Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("edificio.json está vacío.");
            PaseoBuildingConfig config;
            try
            {
                config = JsonUtility.FromJson<PaseoBuildingConfig>(json);
            }
            catch (ArgumentException e)
            {
                throw new FormatException($"edificio.json no es JSON válido: {e.Message}");
            }
            if (config == null)
                throw new FormatException("edificio.json no es un objeto JSON.");

            // JsonUtility leaves absent arrays and objects null or empty; normalise so callers never check.
            if (config.tamano == null || config.tamano.Length == 0)
                config.tamano = (float[])DefaultSize.Clone();
            config.mapas ??= new List<PaseoMapConfig>();
            config.descripcion ??= "";
            foreach (var map in config.mapas)
            {
                map.caja ??= new PaseoBoxPose();
                if (map.caja.posicion == null || map.caja.posicion.Length == 0)
                    map.caja.posicion = new[] { 0f, 0f, 12f };
            }
            return config;
        }

        public static string Serialize(PaseoBuildingConfig config)
        {
            string json = JsonUtility.ToJson(config, true);
            // JsonUtility writes a nested object even when it is absent; a building without «campus» must not gain an
            // empty one, or every rewrite of a per-building edificio.json would change it. It sits before «mapas».
            if (!HasCampus(config))
                json = EmptyCampus.Replace(json, "");
            return HasPark(config) ? json : EmptyPark.Replace(json, "");
        }

        static readonly Regex EmptyCampus = new(@"[ \t]*""campus"":\s*\{[^{}]*\},\r?\n");
        static readonly Regex EmptyPark = new(@"[ \t]*""parque"":\s*\{[^{}]*\},\r?\n");

        /// <summary>JsonUtility builds an empty «parque» when the key is absent: a park needs a non-blank «tema».</summary>
        public static bool HasPark(PaseoBuildingConfig config) =>
            config.parque != null && !string.IsNullOrWhiteSpace(config.parque.tema);

        /// <summary>JsonUtility leaves an absent «gps» at 0, 0 (in the Atlantic): that means no centre.</summary>
        public static bool HasGps(PaseoBuildingConfig config) => config.gps != null && (config.gps.latitud != 0.0 || config.gps.longitud != 0.0);

        /// <summary>
        /// JsonUtility builds an empty «campus» when the key is absent, so presence means a position with three numbers
        /// (Validate refuses a position of any other length).
        /// </summary>
        public static bool HasCampus(PaseoBuildingConfig config) =>
            config.campus != null && config.campus.posicion != null && config.campus.posicion.Length == 3;

        /// <summary>
        /// Campus mode: every building that has maps carries «campus», so the whole tour is one XR Space. False when none
        /// does (one XR Space per building) and, as ValidateTour refuses, for a mix.
        /// </summary>
        public static bool IsCampusTour(IEnumerable<PaseoBuildingConfig> buildings)
        {
            var withMaps = buildings.Where(b => b.mapas.Count > 0).ToList();
            return withMaps.Count > 0 && withMaps.All(HasCampus);
        }

        public static int? IdFromFileName(string file)
        {
            var match = MapFilePattern.Match(file ?? "");
            return match.Success && int.TryParse(match.Groups[1].Value, out int id) ? id : null;
        }

        public static PaseoValidation Validate(string buildingId, PaseoBuildingConfig config, ICollection<string> filesInFolder,
            bool rejectSampleIds)
        {
            var result = new PaseoValidation();
            string where = $"Edificio {buildingId}";
            if (string.IsNullOrWhiteSpace(config.nombre))
                result.Errors.Add($"{where}: falta «nombre».");
            if (config.tamano == null || config.tamano.Length != 3 || config.tamano.Any(v => !IsFinite(v) || v < 0.1f))
                result.Errors.Add($"{where}: «tamano» debe tener tres medidas (ancho, alto, fondo) de al menos 0,1 m.");
            if (!string.IsNullOrWhiteSpace(config.modelo) && Array.IndexOf(BuildingModels, config.modelo) < 0)
                result.Errors.Add($"{where}: «modelo» desconocido (válidos: {string.Join(", ", BuildingModels)}).");
            if (string.IsNullOrWhiteSpace(config.descripcion))
                result.Warnings.Add($"{where}: sin «descripcion»; la tarjeta del edificio mostrará solo el nombre.");
            if (!HasGps(config))
                result.Warnings.Add($"{where}: sin «gps» (centro de la caja); la búsqueda por cercanía lo deja después de los edificios con GPS.");
            else if (Math.Abs(config.gps.latitud) > 90.0 || Math.Abs(config.gps.longitud) > 180.0)
                result.Errors.Add($"{where}: «gps» fuera de rango (latitud ±90, longitud ±180).");
            if (config.mapas.Count == 0)
                result.Warnings.Add($"{where}: no tiene mapas todavía; se omite de la escena.");
            bool campus = HasCampus(config);
            var campusPose = config.campus;
            if (campusPose != null && campusPose.posicion != null && campusPose.posicion.Length != 0 && campusPose.posicion.Length != 3)
                result.Errors.Add($"{where}: «campus.posicion» debe tener tres números (este, altura, norte).");
            else if (campus && (campusPose.posicion.Any(v => !IsFinite(v)) || !IsFinite(campusPose.giro)))
                result.Errors.Add($"{where}: «campus.posicion» debe tener tres números y «giro» un número.");

            if (HasPark(config))
            {
                var park = config.parque;
                if (Array.IndexOf(PaseoParkConfig.Themes, park.tema) < 0)
                    result.Errors.Add($"{where}: «parque.tema» «{park.tema}» desconocido (válidos: {string.Join(", ", PaseoParkConfig.Themes)}).");
                if (!IsFinite(park.escala) || park.escala < 0.5f || park.escala > 4f)
                    result.Errors.Add($"{where}: «parque.escala» debe estar entre 0,5 y 4.");
                if (Array.IndexOf(PaseoParkConfig.Fronts, park.frente) < 0)
                    result.Errors.Add($"{where}: «parque.frente» debe ser +z, -z, +x o -x.");
                if (park.tema == "pasillo")
                {
                    if (!park.HasLine || park.desde.Concat(park.hasta).Any(v => !IsFinite(v)) || !IsFinite(park.suelo))
                        result.Errors.Add($"{where}: «parque» «pasillo» necesita «desde» y «hasta» con dos números (este, norte) y «suelo».");
                    else if (Mathf.Abs(park.desde[0] - park.hasta[0]) + Mathf.Abs(park.desde[1] - park.hasta[1]) < 1f)
                        result.Errors.Add($"{where}: «parque.desde» y «hasta» deben estar a más de 1 m.");
                    if (!campus)
                        result.Errors.Add($"{where}: «parque» «pasillo» se mide en el marco del campus: falta «campus».");
                    if (park.personas < 1 || park.personas > 8)
                        result.Errors.Add($"{where}: «parque.personas» debe estar entre 1 y 8.");
                }
            }

            var seen = new HashSet<int>();
            foreach (var map in config.mapas)
            {
                string label = $"{where}, mapa {map.id}";
                if (!seen.Add(map.id))
                    result.Errors.Add($"{label}: está repetido.");
                // A map's place in the campus space is the campus pose of the box over the pose it holds in its own frame.
                if (campus && !map.caja.colocada)
                    result.Errors.Add($"{label}: el edificio tiene «campus», así que la caja de cada mapa debe estar colocada (el campus relaciona los mapas a través de ella).");
                if (rejectSampleIds && SampleMapIds.Contains(map.id))
                    result.Errors.Add($"{label}: es un mapa de ejemplo del SDK, no del campus.");
                if (map.caja.posicion.Length != 3 || map.caja.posicion.Any(v => !IsFinite(v)) || !IsFinite(map.caja.giro))
                    result.Errors.Add($"{label}: «caja.posicion» debe tener tres números y «giro» un número.");
                if (string.IsNullOrWhiteSpace(map.archivo))
                {
                    result.Errors.Add($"{label}: falta «archivo».");
                    continue;
                }
                if (!filesInFolder.Contains(map.archivo))
                    result.Errors.Add($"{label}: no existe {map.archivo} en la carpeta.");
                int? fileId = IdFromFileName(map.archivo);
                if (fileId == null)
                    result.Errors.Add($"{label}: «{map.archivo}» no tiene la forma <id>-<nombre>.bytes.");
                else if (fileId.Value != map.id)
                    result.Errors.Add($"{label}: el archivo {map.archivo} es del mapa {fileId.Value}.");
            }

            // The maps of a building share one XR Space and one box, related through the box pose each one holds.
            int placed = config.mapas.Count(m => m.caja.colocada);
            if (placed > 0 && placed < config.mapas.Count)
                result.Errors.Add($"{where}: mezcla mapas con la caja colocada y sin colocar; los mapas de un edificio comparten una sola caja, así que o están todos colocados o ninguno.");
            else if (placed == 0 && config.mapas.Count > 1)
                result.Errors.Add($"{where}: tiene {config.mapas.Count} mapas con la caja sin colocar; sin colocar no hay cómo relacionar los mapas entre sí (colocar la caja en cada uno, por ejemplo con caja_dron.js, o dejar un solo mapa).");

            foreach (string file in filesInFolder)
                if (file.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase) && config.mapas.All(m => m.archivo != file))
                    result.Warnings.Add($"{where}: {file} está en la carpeta pero no en edificio.json; no se usa.");
            return result;
        }

        public static List<string> ValidateTour(IEnumerable<KeyValuePair<string, PaseoBuildingConfig>> buildings)
        {
            var errors = new List<string>();
            var owner = new Dictionary<int, string>();
            var withCampus = new List<string>();
            var withoutCampus = new List<string>();
            foreach (var pair in buildings)
            {
                foreach (var map in pair.Value.mapas)
                {
                    if (owner.TryGetValue(map.id, out string other) && other != pair.Key)
                        errors.Add($"El mapa {map.id} aparece en {other} y en {pair.Key}.");
                    else
                        owner[map.id] = pair.Key;
                }
                // A building without maps is left out of the scene, so it takes no part in the choice of mode.
                if (pair.Value.mapas.Count > 0)
                    (HasCampus(pair.Value) ? withCampus : withoutCampus).Add(pair.Key);
            }
            // Campus mode builds one XR Space for everything, which needs every box in the one campus frame.
            if (withCampus.Count > 0 && withoutCampus.Count > 0)
                errors.Add($"«campus» está en {string.Join(", ", withCampus)} pero falta en {string.Join(", ", withoutCampus)}: o lo tienen todos los edificios con mapas (un solo XR Space para el campus) o ninguno (un XR Space por edificio).");
            return errors;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

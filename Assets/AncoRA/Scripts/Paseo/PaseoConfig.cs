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
        public float[] tamano = { 20f, 8f, 12f };
        public bool solido;
        public List<PaseoMapConfig> mapas = new();
    }

    [Serializable]
    public sealed class PaseoMapConfig
    {
        public int id;
        public string archivo;
        public PaseoBoxPose caja = new();
    }

    /// <summary>Box pose in the frame of its map (= its XR Space, since the XR Map sits at identity).</summary>
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
            foreach (var map in config.mapas)
            {
                map.caja ??= new PaseoBoxPose();
                if (map.caja.posicion == null || map.caja.posicion.Length == 0)
                    map.caja.posicion = new[] { 0f, 0f, 12f };
            }
            return config;
        }

        public static string Serialize(PaseoBuildingConfig config) => JsonUtility.ToJson(config, true);

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
            if (config.mapas.Count == 0)
                result.Warnings.Add($"{where}: no tiene mapas todavía; se omite de la escena.");

            var seen = new HashSet<int>();
            foreach (var map in config.mapas)
            {
                string label = $"{where}, mapa {map.id}";
                if (!seen.Add(map.id))
                    result.Errors.Add($"{label}: está repetido.");
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

            foreach (string file in filesInFolder)
                if (file.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase) && config.mapas.All(m => m.archivo != file))
                    result.Warnings.Add($"{where}: {file} está en la carpeta pero no en edificio.json; no se usa.");
            return result;
        }

        public static List<string> ValidateTour(IEnumerable<KeyValuePair<string, PaseoBuildingConfig>> buildings)
        {
            var errors = new List<string>();
            var owner = new Dictionary<int, string>();
            foreach (var pair in buildings)
                foreach (var map in pair.Value.mapas)
                {
                    if (owner.TryGetValue(map.id, out string other) && other != pair.Key)
                        errors.Add($"El mapa {map.id} aparece en {other} y en {pair.Key}.");
                    else
                        owner[map.id] = pair.Key;
                }
            return errors;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

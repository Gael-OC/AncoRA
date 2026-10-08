using System;
using System.Collections.Generic;
using System.Linq;

namespace AncorRA.AR
{
    /// <summary>
    /// What "Copiar valores" writes on the phone and PaseoSetup.ApplyFieldAdjustment reads in the Editor. Field names are
    /// Spanish and match the JSON keys (JsonUtility).
    /// </summary>
    [Serializable]
    public sealed class PaseoAdjustment
    {
        public string generado;
        public string build;
        public List<PaseoAdjustmentBuilding> edificios = new();

        /// <summary>
        /// Writes the adjustment into the building configs. All or nothing: when it returns problems, nothing changed.
        /// Size and fill always apply to a known building; a map pose applies only if the team placed it.
        /// </summary>
        public List<string> ApplyTo(IDictionary<string, PaseoBuildingConfig> configs)
        {
            var problems = new List<string>();
            var buildings = edificios ?? new List<PaseoAdjustmentBuilding>();
            foreach (var building in buildings)
            {
                if (building.id == null || !configs.TryGetValue(building.id, out var config))
                {
                    problems.Add($"El ajuste trae el edificio «{building.id}», que no tiene carpeta en el paseo.");
                    continue;
                }
                if (building.tamano == null || building.tamano.Length != 3 || building.tamano.Any(v => !IsFinite(v) || v < 0.1f))
                    problems.Add($"Edificio {building.id}: «tamano» inválido en el ajuste.");
                foreach (var map in building.mapas ?? new List<PaseoAdjustmentMap>())
                {
                    if (config.mapas.All(m => m.id != map.id))
                        problems.Add($"Edificio {building.id}: el mapa {map.id} no está en su edificio.json.");
                    else if (map.colocada && (map.posicion == null || map.posicion.Length != 3 ||
                                              map.posicion.Any(v => !IsFinite(v)) || !IsFinite(map.giro)))
                        problems.Add($"Edificio {building.id}, mapa {map.id}: posición o giro inválidos en el ajuste.");
                }
            }
            if (problems.Count > 0)
                return problems;

            foreach (var building in buildings)
            {
                var config = configs[building.id];
                config.tamano = (float[])building.tamano.Clone();
                config.solido = building.solido;
                foreach (var map in building.mapas ?? new List<PaseoAdjustmentMap>())
                {
                    if (!map.colocada)
                        continue;
                    var box = config.mapas.First(m => m.id == map.id).caja;
                    box.posicion = (float[])map.posicion.Clone();
                    box.giro = map.giro;
                    box.colocada = true;
                }
            }
            return problems;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class PaseoAdjustmentBuilding
    {
        public string id;
        public float[] tamano;
        public bool solido;
        public List<PaseoAdjustmentMap> mapas = new();
    }

    [Serializable]
    public sealed class PaseoAdjustmentMap
    {
        public int id;
        public float[] posicion;
        public float giro;
        public bool colocada;
    }
}

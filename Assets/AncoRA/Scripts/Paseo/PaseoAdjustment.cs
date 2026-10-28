using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// What "Copiar valores" writes on the phone and PaseoSetup.ApplyFieldAdjustment reads in the Editor. Field names are
    /// Spanish and match the JSON keys (JsonUtility). There is one box per building, so the adjustment carries one pose
    /// per building, in the building frame (the frame of its reference map, PaseoBuildingFrame). In campus mode the pose is
    /// in the campus frame instead, which is what «referencia» 0 says.
    /// </summary>
    [Serializable]
    public sealed class PaseoAdjustment
    {
        public string generado;
        public string build;
        public List<PaseoAdjustmentBuilding> edificios = new();

        /// <summary>
        /// Writes the adjustment into the building configs. All or nothing: when it returns problems, nothing changed.
        /// Size and fill always apply to a known building; the box pose applies only if the team placed it, and is
        /// written back to the «caja» of every map of the building (PaseoBuildingFrame.ApplyBoxPose, or ApplyCampusBoxPose
        /// for a building with «campus»: the pose is then in the campus frame and becomes its campus pose).
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
                if (!building.colocada)
                    continue;
                if (building.posicion == null || building.posicion.Length != 3 || building.posicion.Any(v => !IsFinite(v)) ||
                    !IsFinite(building.giro))
                    problems.Add($"Edificio {building.id}: posición o giro inválidos en el ajuste.");
                else if (config.mapas.Count == 0)
                    problems.Add($"Edificio {building.id}: no tiene mapas donde escribir la caja.");
                else if (PaseoBuildingFrame.IsCampus(config))
                {
                    // The campus has no reference map: the phone says 0 when its box is in the campus frame.
                    if (building.referencia != 0)
                        problems.Add($"Edificio {building.id}: la caja del ajuste está en el marco del mapa {building.referencia}, pero hoy el edificio usa el marco del campus; el ajuste viene de otra versión de la escena.");
                }
                else if (building.referencia != 0 && building.referencia != PaseoBuildingFrame.ReferenceMapId(config))
                    problems.Add($"Edificio {building.id}: la caja del ajuste está en el marco del mapa {building.referencia}, pero hoy el mapa de referencia es el {PaseoBuildingFrame.ReferenceMapId(config)}; el ajuste viene de otra versión de la escena.");
            }
            if (problems.Count > 0)
                return problems;

            foreach (var building in buildings)
            {
                var config = configs[building.id];
                config.tamano = (float[])building.tamano.Clone();
                config.solido = building.solido;
                if (!building.colocada)
                    continue;
                var position = new Vector3(building.posicion[0], building.posicion[1], building.posicion[2]);
                if (PaseoBuildingFrame.IsCampus(config))
                    PaseoBuildingFrame.ApplyCampusBoxPose(config, position, building.giro);
                else
                    PaseoBuildingFrame.ApplyBoxPose(config, position, building.giro);
            }
            return problems;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class PaseoAdjustmentBuilding
    {
        public string id;
        // Id of the map whose frame the box pose is in (the reference map of the scene the phone ran); 0 = not checked,
        // and in campus mode the box pose is in the campus frame.
        public int referencia;
        public float[] tamano;
        public bool solido;
        public float[] posicion;
        public float giro;
        public bool colocada;
    }
}

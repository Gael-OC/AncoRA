using System;
using System.Collections.Generic;

namespace AncorRA.AR
{
    /// <summary>
    /// The recording point the team picks on the sheet before each recording of ~10 s: building × point × distance, plus the
    /// light. It names the point («X1-C-30m») and every recorded image carries that name, so the field data can be grouped
    /// by point and by hour without reading anything by hand. Points A–F are the six chalk marks of every building; each
    /// has a usual distance (A, B at 15 m; C, D at 30 m; E, F at 50 m) that picking it fills in, and the distance can still
    /// be changed.
    /// </summary>
    public sealed class PaseoRecordSelection
    {
        public static readonly string[] Points = { "A", "B", "C", "D", "E", "F" };
        public static readonly int[] Distances = { 15, 30, 50 };
        public static readonly string[] Lights = { "sol", "nublado", "sombra", "parcial" };

        // Short names for the labels; a building that is not here is written with its id.
        static readonly Dictionary<string, string> Codes = new()
        {
            ["CienciasBasicas"] = "CB",
            ["Teologia"] = "TEO"
        };

        /// <summary>Raised on every change, so the recorder knows a new point started.</summary>
        public int Version { get; private set; }
        public string Building { get; private set; } = "";
        public string Point { get; private set; } = "";
        public int Meters { get; private set; }
        public string Light { get; private set; } = "";

        public static string BuildingCode(string buildingId) =>
            Codes.TryGetValue(buildingId ?? "", out var code) ? code : buildingId ?? "";

        public static int UsualDistance(string point)
        {
            int index = Array.IndexOf(Points, point);
            return index < 0 ? 0 : Distances[Math.Min(index / 2, Distances.Length - 1)];
        }

        public static string Name(string building, string point, int meters) => $"{building}-{point}-{meters}m";

        /// <summary>True once the building, the point, its distance and the light are all chosen.</summary>
        public bool Complete => Building.Length > 0 && Point.Length > 0 && Meters > 0 && Light.Length > 0;

        /// <summary>«X1-C-30m», or an empty text while the building, point or distance is missing.</summary>
        public string Label => Building.Length > 0 && Point.Length > 0 && Meters > 0 ? Name(Building, Point, Meters) : "";

        public string Describe() => Label.Length == 0 ? "Sin punto elegido" : Light.Length == 0 ? Label : $"{Label} · {Light}";

        public void SetBuilding(string building)
        {
            Building = building ?? "";
            Version++;
        }

        public void SetPoint(string point)
        {
            Point = point ?? "";
            int usual = UsualDistance(Point);
            if (usual > 0)
                Meters = usual;
            Version++;
        }

        public void SetMeters(int meters)
        {
            Meters = meters;
            Version++;
        }

        public void SetLight(string light)
        {
            Light = light ?? "";
            Version++;
        }
    }
}

using System;
using System.Collections.Generic;

namespace AncorRA.AR
{
    /// <summary>
    /// The point and light chosen before a recording. New field sessions use A/B at 10 m, C/D at 15 m, E at 20 m,
    /// and F at 25 m. Points are identified by existing site references, never permanent chalk marks.
    /// Explicit distances in old recording filenames remain readable (Name does not reinterpret them).
    /// </summary>
    public sealed class PaseoRecordSelection
    {
        public static readonly string[] Points = { "A", "B", "C", "D", "E", "F" };
        public static readonly int[] Distances = { 10, 15, 20, 25 };
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
            return index < 0 ? 0 : index < 2 ? 10 : index < 4 ? 15 : index == 4 ? 20 : 25;
        }

        public static string Name(string building, string point, int meters) => $"{building}-{point}-{meters}m";

        /// <summary>True once the building, the point, its distance and the light are all chosen.</summary>
        public bool Complete => Building.Length > 0 && Point.Length > 0 && Meters > 0 && Light.Length > 0;

        /// <summary>«X1-C-15m», or an empty text while the building, point or distance is missing.</summary>
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

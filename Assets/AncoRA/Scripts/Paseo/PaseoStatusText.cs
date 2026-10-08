using System;
using System.Collections.Generic;
using System.Linq;

namespace AncorRA.AR
{
    public enum PaseoStatusLevel { Waiting, Ok, Error }

    public enum PaseoBuildingState { Searching, Located, Held, MapFailed }

    public readonly struct PaseoBuildingStatus
    {
        public readonly string Name;
        public readonly PaseoBuildingState State;

        public PaseoBuildingStatus(string name, PaseoBuildingState state)
        {
            Name = name;
            State = state;
        }
    }

    public struct PaseoStatusInputs
    {
        public string Error;
        public bool ArUnsupported;
        public bool SdkReady;
        public float SecondsSinceStart;
        public string MapStillLoading;
        public bool Tracking;
        public IReadOnlyList<PaseoBuildingStatus> Buildings;
    }

    /// <summary>
    /// The always-visible banner, in plain Spanish. Only Latin-1 characters: IMGUI's default font on Android has no
    /// check marks or other symbols.
    /// </summary>
    public static class PaseoStatusText
    {
        public const float SlowStartSeconds = 15f;

        public static string Describe(PaseoStatusInputs s, out PaseoStatusLevel level)
        {
            if (!string.IsNullOrEmpty(s.Error))
            {
                level = PaseoStatusLevel.Error;
                return "Error: " + s.Error;
            }
            if (s.ArUnsupported)
            {
                level = PaseoStatusLevel.Error;
                return "Este teléfono no es compatible con realidad aumentada (ARCore).";
            }
            if (!s.SdkReady)
            {
                if (s.SecondsSinceStart > SlowStartSeconds)
                {
                    level = PaseoStatusLevel.Error;
                    return "El sistema de ubicación no arrancó. Cierra y vuelve a abrir la app.";
                }
                level = PaseoStatusLevel.Waiting;
                return "Iniciando...";
            }
            level = PaseoStatusLevel.Waiting;
            if (!string.IsNullOrEmpty(s.MapStillLoading))
                return $"Cargando mapa {s.MapStillLoading}...";
            if (!s.Tracking)
                return "Mueve el teléfono despacio para que la cámara reconozca el entorno.";

            var buildings = s.Buildings ?? Array.Empty<PaseoBuildingStatus>();
            if (!buildings.Any(b => b.State == PaseoBuildingState.Located || b.State == PaseoBuildingState.Held))
                return s.SecondsSinceStart > SlowStartSeconds
                    ? "Acércate a un edificio del paseo y muévete despacio."
                    : "Apunta a un edificio del paseo.";

            level = PaseoStatusLevel.Ok;
            return string.Join(" · ", buildings.Select(b => b.State switch
            {
                PaseoBuildingState.Located => $"{b.Name}: ubicado",
                PaseoBuildingState.Held => $"{b.Name}: ubicado (mantenido)",
                PaseoBuildingState.MapFailed => $"{b.Name}: el mapa no cargó",
                _ => $"{b.Name}: buscando..."
            }));
        }
    }
}

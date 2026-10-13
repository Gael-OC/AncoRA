using System.Collections.Generic;
using System.Globalization;

namespace AncorRA.AR
{
    public enum PaseoTone
    {
        Ok,
        Neutral,
        Warning,
        Error
    }

    public struct PaseoMapRowInputs
    {
        public int MapId;
        public string BuildingName;
        public bool Loaded;
        public bool Failed;
        public bool Located;
        public bool Shown;
        public bool HasFilter;
        public bool Placed;
        public bool PhoneValues;
        public PaseoPoseReport? Last;
        public int Used;
        public int Seen;
    }

    /// <summary>One line of the team sheet per map, in Spanish, with a decimal comma whatever the phone's language.</summary>
    public readonly struct PaseoMapRow
    {
        public static readonly NumberFormatInfo Numbers = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

        public readonly string Title;
        public readonly string Detail;
        public readonly PaseoTone Tone;

        public PaseoMapRow(string title, string detail, PaseoTone tone)
        {
            Title = title;
            Detail = detail;
            Tone = tone;
        }

        public static PaseoMapRow Build(PaseoMapRowInputs s)
        {
            var parts = new List<string>();
            if (s.Failed)
                parts.Add("NO CARGÓ");
            else if (!s.Loaded)
                parts.Add("cargando");
            if (!s.HasFilter)
                parts.Add("SIN FILTRO");
            parts.Add(s.Shown ? "visible" : s.Located ? "ubicado" : "sin ubicar");
            if (s.Last is { } r)
            {
                string move = float.IsNaN(r.BoxMoveMeters) ? "" : string.Format(Numbers, ", movería {0:F2} m", r.BoxMoveMeters);
                parts.Add(string.Format(Numbers, "{0}, conf {1:F0}{2}", PaseoPoseProcessor.VerdictName(r.Verdict), r.Confidence, move));
            }
            if (s.Seen > 0)
                parts.Add($"{s.Used}/{s.Seen} usadas");
            if (!s.Placed)
                parts.Add("CAJA SIN COLOCAR");
            if (s.PhoneValues)
                parts.Add("valores del teléfono");

            var tone = s.Failed || !s.HasFilter ? PaseoTone.Error
                : !s.Placed || !s.Loaded ? PaseoTone.Warning
                : s.Located ? PaseoTone.Ok
                : PaseoTone.Neutral;
            return new PaseoMapRow($"{s.MapId} {s.BuildingName}", string.Join(" · ", parts), tone);
        }
    }
}

namespace AncorRA.AR
{
    public enum PaseoActivityTone { None, Searching, Match, Discarded }

    public readonly struct PaseoActivityLine
    {
        public readonly string Text;
        public readonly PaseoActivityTone Tone;

        public PaseoActivityLine(string text, PaseoActivityTone tone)
        {
            Text = text;
            Tone = tone;
        }

        public static PaseoActivityLine None => default;
    }

    public struct PaseoActivityInputs
    {
        // SDK ready and AR tracking: attempts are running.
        public bool Active;
        // Name of the building the attempt in progress searches; null for every map.
        public string SearchingFor;
        public int FailuresInARow;
        // The last finished attempt found a map, of this building, and what the filter made of it (null until judged).
        public bool LastMatched;
        public string MatchedBuilding;
        public PaseoPoseReport? MatchReport;
        public float SecondsSinceLast;
    }

    /// <summary>
    /// The line under the status pill that says what the search is doing: which building it looks for, how many images
    /// went by without a match, and each match, used or discarded by the filter. Field test 14-oct: the team tried
    /// Teología several times and could not tell whether the app saw anything at all.
    /// </summary>
    public static class PaseoActivity
    {
        // How long a match stays announced before the line goes back to the search.
        public const float MatchSeconds = 4f;

        public static PaseoActivityLine Describe(PaseoActivityInputs s)
        {
            if (!s.Active)
                return PaseoActivityLine.None;
            if (s.LastMatched && s.SecondsSinceLast <= MatchSeconds)
                return Match(s.MatchedBuilding, s.MatchReport);

            string target = s.SearchingFor == null ? "Buscando en todos los edificios" : $"Buscando {s.SearchingFor}";
            string text = s.FailuresInARow switch
            {
                <= 0 => target + "…",
                1 => target + " · 1 imagen sin coincidencia",
                _ => $"{target} · {s.FailuresInARow} imágenes sin coincidencia"
            };
            return new PaseoActivityLine(text, PaseoActivityTone.Searching);
        }

        /// <summary>
        /// The filter's verdict on the match an attempt found. The filter judges it after the attempt ends (SceneUpdater,
        /// then the XR Space), so a report made before the attempt ended belongs to an earlier match.
        /// </summary>
        public static PaseoPoseReport? ReportFor(PaseoAttempt attempt, PaseoPoseReport? lastReport, float lastReportAt) =>
            attempt.Matched && lastReport.HasValue && lastReportAt >= attempt.FinishedAt ? lastReport : null;

        static PaseoActivityLine Match(string building, PaseoPoseReport? report)
        {
            if (report is not { } r)
                return new PaseoActivityLine($"Coincidencia con {building}", PaseoActivityTone.Match);
            return r.Verdict switch
            {
                PaseoPoseVerdict.Tilted => new PaseoActivityLine(
                    string.Format(PaseoMapRow.Numbers, "Coincidencia con {0} descartada: imagen inclinada {1:F0}°", building, r.TiltDegrees),
                    PaseoActivityTone.Discarded),
                PaseoPoseVerdict.Rejected => new PaseoActivityLine(
                    string.Format(PaseoMapRow.Numbers, "Coincidencia con {0} descartada: movería la caja {1:F1} m", building, r.BoxMoveMeters),
                    PaseoActivityTone.Discarded),
                _ => new PaseoActivityLine(string.Format(PaseoMapRow.Numbers, "Coincidencia con {0} · confianza {1:F0}", building, r.Confidence),
                    PaseoActivityTone.Match)
            };
        }
    }
}

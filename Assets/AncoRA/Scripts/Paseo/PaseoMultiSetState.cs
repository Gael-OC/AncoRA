using System.Globalization;

namespace AncorRA.AR
{
    public enum PaseoMultiSetState
    {
        /// <summary>No usable multiset.json: MultiSet stays off and the rest of the app runs as always.</summary>
        NoConfig,
        /// <summary>multiset.json is fine; the SDK starts once the tour begins and AR tracks.</summary>
        Waiting,
        Authenticating,
        Authenticated,
        Error,
        /// <summary>The team paused the queries from the sheet.</summary>
        Paused,
        /// <summary>The session's query cap was reached.</summary>
        CapReached
    }

    /// <summary>How the state of the MultiSet probe reads in the log (MULTISET_ESTADO) and on the team sheet.</summary>
    public static class PaseoMultiSetStates
    {
        public static string LogName(PaseoMultiSetState state) => state switch
        {
            PaseoMultiSetState.NoConfig => "sin-config",
            PaseoMultiSetState.Waiting => "esperando",
            PaseoMultiSetState.Authenticating => "autenticando",
            PaseoMultiSetState.Authenticated => "autenticado",
            PaseoMultiSetState.Error => "error",
            PaseoMultiSetState.Paused => "apagado",
            _ => "tope"
        };

        /// <summary>
        /// The sheet's status row: «MultiSet: autenticado · 14 consultas · última 1.8 s, conf 0.71 (Teología)». Numbers use a
        /// point, like the log. <paramref name="detail"/> is the reason for NoConfig or Error; <paramref name="last"/> and
        /// <paramref name="lastBuildingName"/> describe the latest query, if any.
        /// </summary>
        public static string Describe(PaseoMultiSetState state, int queries, PaseoMultiSetAttempt? last, string lastBuildingName, string detail)
        {
            var inv = CultureInfo.InvariantCulture;
            string count = queries == 1 ? "1 consulta" : $"{queries} consultas";
            string latest = "";
            if (last is { } attempt)
            {
                string where = attempt.Matched
                    ? string.IsNullOrEmpty(lastBuildingName) ? $" ({attempt.MapCode})" : $" ({lastBuildingName})"
                    : " (sin coincidencia)";
                latest = string.Format(inv, " · última {0:F1} s, conf {1:F2}{2}", attempt.Seconds, attempt.Confidence, where);
            }
            switch (state)
            {
                case PaseoMultiSetState.NoConfig:
                    return string.IsNullOrEmpty(detail) ? "MultiSet: apagado (sin multiset.json)" : "MultiSet: " + detail.Replace("MultiSet ", "");
                case PaseoMultiSetState.Waiting:
                    return "MultiSet: listo, espera a que comience el paseo";
                case PaseoMultiSetState.Authenticating:
                    return "MultiSet: autenticando…";
                case PaseoMultiSetState.Error:
                    return string.IsNullOrEmpty(detail) ? "MultiSet: error de autenticación" : "MultiSet: error · " + detail;
                case PaseoMultiSetState.Paused:
                    return $"MultiSet: en pausa · {count}{latest}";
                case PaseoMultiSetState.CapReached:
                    return $"MultiSet: tope alcanzado · {count}{latest}";
                default:
                    return $"MultiSet: autenticado · {count}{latest}";
            }
        }
    }
}

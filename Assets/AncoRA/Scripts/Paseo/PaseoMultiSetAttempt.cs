namespace AncorRA.AR
{
    /// <summary>One MultiSet query and what came back, for the INTENTO_MULTISET line and the team sheet.</summary>
    public readonly struct PaseoMultiSetAttempt
    {
        public readonly int Number;
        public readonly float Seconds;
        /// <summary>The map code MultiSet matched; null when it found nothing.</summary>
        public readonly string MapCode;
        public readonly float Confidence;
        public readonly bool Accepted;
        /// <summary>The tour building the map code belongs to (multiset.json «edificios»); null when it names none.</summary>
        public readonly string BuildingId;
        public readonly bool GpsHint;
        /// <summary>Seconds since «Comenzar» when the answer arrived (PaseoTour.SecondsSinceStart).</summary>
        public readonly float At;

        public PaseoMultiSetAttempt(int number, float seconds, string mapCode, float confidence, bool accepted, string buildingId,
            bool gpsHint, float at)
        {
            Number = number;
            Seconds = seconds;
            MapCode = mapCode;
            Confidence = confidence;
            Accepted = accepted;
            BuildingId = buildingId;
            GpsHint = gpsHint;
            At = at;
        }

        public bool Matched => !string.IsNullOrEmpty(MapCode);
    }
}

namespace AncorRA.AR
{
    public readonly struct PaseoAttempt
    {
        public readonly int Number;
        public readonly PaseoSearch Search;
        public readonly float Seconds;
        public readonly int MatchedMapId;
        public readonly int Confidence;
        public readonly float FinishedAt;

        public PaseoAttempt(int number, PaseoSearch search, float seconds, int matchedMapId, int confidence, float finishedAt)
        {
            Number = number;
            Search = search;
            Seconds = seconds;
            MatchedMapId = matchedMapId;
            Confidence = confidence;
            FinishedAt = finishedAt;
        }

        public bool Matched => MatchedMapId > 0;
    }
}

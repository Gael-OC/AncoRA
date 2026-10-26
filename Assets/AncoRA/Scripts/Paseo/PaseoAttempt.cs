using UnityEngine;

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
        // The image handed to the plugin and where the AR camera stood when it was taken (debug log).
        public readonly int ImageWidth;
        public readonly int ImageHeight;
        public readonly Vector3 CameraPosition;
        public readonly float CameraYaw;
        // The reprojection error of a match (NaN without one).
        public readonly double Rmse;

        public PaseoAttempt(int number, PaseoSearch search, float seconds, int matchedMapId, int confidence, float finishedAt,
            int imageWidth = 0, int imageHeight = 0, Vector3 cameraPosition = default, float cameraYaw = 0f, double rmse = double.NaN)
        {
            Number = number;
            Search = search;
            Seconds = seconds;
            MatchedMapId = matchedMapId;
            Confidence = confidence;
            FinishedAt = finishedAt;
            ImageWidth = imageWidth;
            ImageHeight = imageHeight;
            CameraPosition = cameraPosition;
            CameraYaw = cameraYaw;
            Rmse = rmse;
        }

        public bool Matched => MatchedMapId > 0;
    }
}

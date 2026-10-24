using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>What the contrast test did to the image of an attempt: nothing, CLAHE, or nothing because it could not.</summary>
    public enum PaseoContrastUse { Normal, Enhanced, NotApplicable }

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
        // The team's tests of 1.3.6: contrast enhancement of the image, the gravity solver (Lean), a search with the
        // prior position of a located map; the milliseconds the enhancement took (NaN if not applied); the reprojection
        // error of a match (NaN without one).
        public readonly PaseoContrastUse Contrast;
        public readonly bool Gravity;
        public readonly bool Prior;
        public readonly float EnhanceMilliseconds;
        public readonly double Rmse;

        public PaseoAttempt(int number, PaseoSearch search, float seconds, int matchedMapId, int confidence, float finishedAt,
            int imageWidth = 0, int imageHeight = 0, Vector3 cameraPosition = default, float cameraYaw = 0f,
            PaseoContrastUse contrast = PaseoContrastUse.Normal, bool gravity = false, bool prior = false,
            float enhanceMilliseconds = float.NaN, double rmse = double.NaN)
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
            Contrast = contrast;
            Gravity = gravity;
            Prior = prior;
            EnhanceMilliseconds = enhanceMilliseconds;
            Rmse = rmse;
        }

        public bool Matched => MatchedMapId > 0;
    }
}

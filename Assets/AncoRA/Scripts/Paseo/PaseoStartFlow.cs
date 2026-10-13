namespace AncorRA.AR
{
    public enum PaseoStartState
    {
        Welcome,
        Requesting,
        Started,
        NeedsCamera
    }

    /// <summary>
    /// The welcome button asks for the camera and the location, and only then AR starts. Without the camera nothing
    /// works, so the welcome stays and explains; without the location the tour starts anyway (no arrows, no distances).
    /// </summary>
    public sealed class PaseoStartFlow
    {
        public PaseoStartState State { get; private set; } = PaseoStartState.Welcome;
        public bool LocationGranted { get; private set; }

        /// <summary>The button was pressed: true when the permissions must be asked now.</summary>
        public bool Press()
        {
            if (State is PaseoStartState.Requesting or PaseoStartState.Started)
                return false;
            State = PaseoStartState.Requesting;
            return true;
        }

        public void Answer(bool camera, bool location)
        {
            if (State != PaseoStartState.Requesting)
                return;
            State = camera ? PaseoStartState.Started : PaseoStartState.NeedsCamera;
            LocationGranted = location;
        }
    }
}

namespace AncorRA.AR
{
    /// <summary>Platform side of the permission requests (Android, or always granted in the Editor).</summary>
    public interface IPaseoPermissionSystem
    {
        bool Has(string permission);
        void Request(string permission);
    }

    /// <summary>
    /// Asks for the camera, then the location, one system dialog at a time. The answer is read back with Has once the app
    /// gets its focus back (the dialog closed), all on Unity's main thread: Android's own callbacks arrive on its UI
    /// thread. A request that shows no dialog (a permission denied for good) is checked again after a short wait instead
    /// of hanging. Without the camera the flow stops: the location is not even asked.
    /// </summary>
    public sealed class PaseoPermissionFlow
    {
        public const string Camera = "android.permission.CAMERA";
        public const string Location = "android.permission.ACCESS_FINE_LOCATION";
        // Longer than a system dialog takes to appear and take the focus.
        public const float NoDialogSeconds = 1f;

        readonly IPaseoPermissionSystem system;
        int stage; // 0 camera, 1 location, 2 done
        bool waiting;
        bool sawDialog;
        float askedAt;

        public PaseoPermissionFlow(IPaseoPermissionSystem permissionSystem) => system = permissionSystem;

        public bool Done { get; private set; }
        public bool CameraGranted { get; private set; }
        public bool LocationGranted { get; private set; }

        public void Begin(float now)
        {
            stage = 0;
            waiting = false;
            Done = false;
            CameraGranted = LocationGranted = false;
            Advance(now, ask: true);
        }

        /// <summary>Call every frame while the flow runs.</summary>
        public void Update(float now, bool focused)
        {
            if (!waiting)
                return;
            if (!focused)
            {
                sawDialog = true;
                return;
            }
            if (!sawDialog && now - askedAt < NoDialogSeconds)
                return;
            waiting = false;
            Advance(now, ask: false);
        }

        void Advance(float now, bool ask)
        {
            while (stage < 2)
            {
                string permission = stage == 0 ? Camera : Location;
                bool granted = system.Has(permission);
                if (!granted && ask)
                {
                    system.Request(permission);
                    waiting = true;
                    sawDialog = false;
                    askedAt = now;
                    return;
                }
                if (stage == 0)
                    CameraGranted = granted;
                else
                    LocationGranted = granted;
                stage = stage == 0 && !granted ? 2 : stage + 1;
                ask = true;
            }
            Done = true;
        }
    }
}

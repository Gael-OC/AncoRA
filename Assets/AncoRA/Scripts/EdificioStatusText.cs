namespace AncorRA.AR
{
    public enum EdificioStatusLevel { Waiting, Ok, Error }

    /// <summary>
    /// Plain-Spanish status line for the on-screen banner of the building/Teologia demo: what the app is doing right
    /// now, in the order the steps really happen. Pure function of the diagnostic state so it can be checked in the
    /// Editor without a phone. "Ubicado" only means the SDK returned a pose and the content was enabled; it does not
    /// say the content is physically aligned.
    /// </summary>
    public static class EdificioStatusText
    {
        public const float SlowStartSeconds = 15f;

        public struct Inputs
        {
            public string Error;
            public bool ArUnsupported;
            public bool SdkReady;
            public float SecondsSinceStart;
            // Label of the first map whose native load is not confirmed yet; null when all are loaded.
            public string MapStillLoading;
            public bool Tracking;
            public bool EverLocalized;
            public bool Localized;
            public bool ContentShown;
            // The content stays where it was (phone tracking) because Immersal has not corrected it recently.
            public bool PositionHeld;
            public int Attempts;
            public int Successes;
            public int LastMapId;
            // "Caja" or "Marco".
            public string ContentName;
        }

        public static string Describe(Inputs s, out EdificioStatusLevel level)
        {
            string content = string.IsNullOrEmpty(s.ContentName) ? "Marco" : s.ContentName;
            level = EdificioStatusLevel.Waiting;

            if (!string.IsNullOrEmpty(s.Error))
            {
                level = EdificioStatusLevel.Error;
                return "ERROR: " + s.Error;
            }
            if (s.ArUnsupported)
            {
                level = EdificioStatusLevel.Error;
                return "Este teléfono no es compatible con realidad aumentada (ARCore).";
            }
            if (!s.SdkReady)
                return s.SecondsSinceStart > SlowStartSeconds
                    ? "Iniciando la cámara y el SDK de Immersal… tarda más de lo normal: revisa el permiso de cámara y reinicia la app."
                    : "Iniciando la cámara y el SDK de Immersal…";
            if (s.MapStillLoading != null)
                return $"Cargando el mapa {s.MapStillLoading}…";
            if (!s.Tracking)
                return "Mapa cargado. Mueve el teléfono despacio para que la cámara reconozca el entorno.";
            if (s.ContentShown && s.PositionHeld)
                return $"{content} visible, posición mantenida por el tracking del teléfono (sin corrección reciente de Immersal). Apunta al edificio por una cara mapeada para corregirla.";
            if (s.ContentShown)
            {
                level = EdificioStatusLevel.Ok;
                return $"Ubicado con el mapa {s.LastMapId}. {content} visible: si no calza con el edificio, ajústala con el panel de abajo.";
            }
            if (s.Localized)
                return $"Ubicando… aplicando la posición ({content.ToLowerInvariant()}).";
            if (s.EverLocalized)
                return "Se perdió la ubicación. Vuelve a apuntar al edificio por una cara mapeada.";
            return $"Mapa cargado. Apunta al edificio por una cara mapeada y muévete despacio. Intentos: {s.Attempts}, aciertos: {s.Successes}.";
        }
    }
}

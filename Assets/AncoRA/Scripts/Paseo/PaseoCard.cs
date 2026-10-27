using System;

namespace AncorRA.AR
{
    public enum PaseoCardKind
    {
        None,
        Hint,
        Building
    }

    /// <summary>
    /// What the bottom card shows: the recognised building, or while searching a hint of what to do. The guide of the
    /// next phase replaces the single hint with step-by-step directions.
    /// </summary>
    public readonly struct PaseoCard : IEquatable<PaseoCard>
    {
        public const string HintTitle = "Apunta a un edificio del paseo";
        public const string HintSubtitle = "Muévete despacio frente a su fachada.";
        public const string LookUpTitle = "Levanta el teléfono";
        public const string LookUpSubtitle = "Apunta a la fachada del edificio, a la altura del pecho.";

        public readonly PaseoCardKind Kind;
        public readonly string Title;
        public readonly string Subtitle;
        // A building shown on its first localization only, until a second one agrees (PaseoPoseFilter).
        public readonly bool Confirmed;

        PaseoCard(PaseoCardKind kind, string title, string subtitle, bool confirmed)
        {
            Kind = kind;
            Title = title;
            Subtitle = subtitle;
            Confirmed = confirmed;
        }

        public static PaseoCard None => default;

        public static PaseoCard Hint(string title, string subtitle) => new(PaseoCardKind.Hint, title, subtitle, false);

        public static PaseoCard Building(string name, string description, bool confirmed = true) =>
            new(PaseoCardKind.Building, name, description ?? "", confirmed);

        public static PaseoCard Choose(bool sdkReady, bool tracking, string shownBuilding, string description, bool confirmed = true,
            bool lookingAtGround = false)
        {
            if (!string.IsNullOrEmpty(shownBuilding))
                return Building(shownBuilding, description, confirmed);
            if (!sdkReady || !tracking)
                return None;
            return lookingAtGround ? Hint(LookUpTitle, LookUpSubtitle) : Hint(HintTitle, HintSubtitle);
        }

        public bool Equals(PaseoCard other) =>
            Kind == other.Kind && Title == other.Title && Subtitle == other.Subtitle && Confirmed == other.Confirmed;

        public override bool Equals(object obj) => obj is PaseoCard other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind, Title, Subtitle, Confirmed);
    }
}

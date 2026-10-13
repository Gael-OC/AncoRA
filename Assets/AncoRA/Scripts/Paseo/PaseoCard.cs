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

        public readonly PaseoCardKind Kind;
        public readonly string Title;
        public readonly string Subtitle;

        PaseoCard(PaseoCardKind kind, string title, string subtitle)
        {
            Kind = kind;
            Title = title;
            Subtitle = subtitle;
        }

        public static PaseoCard None => default;

        public static PaseoCard Hint(string title, string subtitle) => new(PaseoCardKind.Hint, title, subtitle);

        public static PaseoCard Building(string name, string description) => new(PaseoCardKind.Building, name, description ?? "");

        public static PaseoCard Choose(bool sdkReady, bool tracking, string shownBuilding, string description)
        {
            if (!string.IsNullOrEmpty(shownBuilding))
                return Building(shownBuilding, description);
            return sdkReady && tracking ? Hint(HintTitle, HintSubtitle) : None;
        }

        public bool Equals(PaseoCard other) => Kind == other.Kind && Title == other.Title && Subtitle == other.Subtitle;

        public override bool Equals(object obj) => obj is PaseoCard other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind, Title, Subtitle);
    }
}

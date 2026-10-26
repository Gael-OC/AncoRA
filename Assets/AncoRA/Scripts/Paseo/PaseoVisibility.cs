using System;
using System.Collections.Generic;

namespace AncorRA.AR
{
    /// <summary>
    /// Decides which boxes are shown. Every building has its own XR Space (with all its maps inside), so a located
    /// building stays valid until the phone loses tracking, whichever of its maps located it.
    /// </summary>
    public sealed class PaseoVisibility
    {
        public const float HeldAfterSeconds = 10f;

        sealed class Entry
        {
            public bool Located;
            public float LastSuccess;
        }

        readonly Dictionary<string, Entry> entries = new();
        readonly List<string> buildings = new();

        public IReadOnlyList<string> Buildings => buildings;

        public void AddBuilding(string buildingId)
        {
            if (entries.ContainsKey(buildingId))
                throw new ArgumentException($"El edificio {buildingId} está repetido en el paseo.");
            entries[buildingId] = new Entry();
            buildings.Add(buildingId);
        }

        /// <summary>Call when the building's XR Space has received the pose of a successful localization with any of its maps.</summary>
        public bool MarkLocated(string buildingId, float time)
        {
            if (!entries.TryGetValue(buildingId, out var entry))
                return false;
            entry.Located = true;
            entry.LastSuccess = time;
            return true;
        }

        /// <summary>Losing the phone's own tracking invalidates every placed space: each building must localize again.</summary>
        public void LoseTracking()
        {
            foreach (var entry in entries.Values)
                entry.Located = false;
        }

        public bool IsLocated(string buildingId) => entries.TryGetValue(buildingId, out var entry) && entry.Located;

        public bool IsHeld(string buildingId, float now) =>
            entries.TryGetValue(buildingId, out var entry) && entry.Located && now - entry.LastSuccess >= HeldAfterSeconds;
    }
}

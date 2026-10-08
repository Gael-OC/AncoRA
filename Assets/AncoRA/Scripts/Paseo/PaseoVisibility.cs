using System;
using System.Collections.Generic;
using System.Linq;

namespace AncorRA.AR
{
    /// <summary>
    /// Decides which box is shown. Every map has its own XR Space, so a located map stays valid until the phone loses
    /// tracking. Per building, the map that located last wins; a sequence number (not the time) breaks ties, so two
    /// results in the same localization cycle are still ordered.
    /// </summary>
    public sealed class PaseoVisibility
    {
        public const float HeldAfterSeconds = 10f;

        sealed class Entry
        {
            public string Building;
            public bool Located;
            public long Sequence;
            public float LastSuccess;
        }

        readonly Dictionary<int, Entry> maps = new();
        readonly List<string> buildings = new();
        long sequence;

        public IReadOnlyList<string> Buildings => buildings;

        public void AddMap(int mapId, string buildingId)
        {
            if (maps.ContainsKey(mapId))
                throw new ArgumentException($"El mapa {mapId} está repetido en el paseo.");
            maps[mapId] = new Entry { Building = buildingId };
            if (!buildings.Contains(buildingId))
                buildings.Add(buildingId);
        }

        /// <summary>Call when the map's XR Space has received the pose of a successful localization.</summary>
        public bool MarkLocated(int mapId, float time)
        {
            if (!maps.TryGetValue(mapId, out var entry))
                return false;
            entry.Located = true;
            entry.Sequence = ++sequence;
            entry.LastSuccess = time;
            return true;
        }

        /// <summary>Losing the phone's own tracking invalidates every placed space: each map must localize again.</summary>
        public void LoseTracking()
        {
            foreach (var entry in maps.Values)
                entry.Located = false;
        }

        public bool IsLocated(int mapId) => maps.TryGetValue(mapId, out var entry) && entry.Located;

        public int VisibleMap(string buildingId)
        {
            int best = -1;
            long bestSequence = -1;
            foreach (var pair in maps)
                if (pair.Value.Located && pair.Value.Building == buildingId && pair.Value.Sequence > bestSequence)
                {
                    best = pair.Key;
                    bestSequence = pair.Value.Sequence;
                }
            return best;
        }

        public bool IsHeld(int mapId, float now) =>
            maps.TryGetValue(mapId, out var entry) && entry.Located && now - entry.LastSuccess >= HeldAfterSeconds;

        public IEnumerable<int> MapsOf(string buildingId) =>
            maps.Where(pair => pair.Value.Building == buildingId).Select(pair => pair.Key);
    }
}

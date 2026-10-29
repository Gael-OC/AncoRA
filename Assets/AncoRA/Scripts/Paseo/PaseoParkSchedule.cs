using System.Collections.Generic;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>Which move a stationary character does now: it cycles through its repertoire, one slot at a time.</summary>
    public static class PaseoParkSchedule
    {
        public const float DefaultSlotSeconds = 6f;

        /// <summary>
        /// Move at «timeSeconds». phase01 offsets the character by a fraction of the whole repertoire cycle so neighbours are
        /// not in step. An empty or null repertoire gives Bailar.
        /// </summary>
        public static PaseoMove MoveAt(IReadOnlyList<PaseoMove> repertoire, float timeSeconds, float phase01,
            float slotSeconds = DefaultSlotSeconds)
        {
            if (repertoire == null || repertoire.Count == 0)
                return PaseoMove.Bailar;
            float slots = timeSeconds / Mathf.Max(0.01f, slotSeconds) + phase01 * repertoire.Count;
            int index = Mathf.FloorToInt(slots) % repertoire.Count;
            if (index < 0)
                index += repertoire.Count;
            return repertoire[index];
        }
    }
}

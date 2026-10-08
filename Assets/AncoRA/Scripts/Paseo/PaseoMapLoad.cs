using System.Collections.Generic;

namespace AncorRA.AR
{
    /// <summary>
    /// Native load state of each map. MapManager.MapRegisteredAndLoaded fires only for maps that loaded; when
    /// Core.LoadMap fails the SDK says nothing and still becomes ready, so whatever has not reported by then failed.
    /// </summary>
    public sealed class PaseoMapLoad
    {
        enum State { Pending, Loaded, Failed }

        readonly Dictionary<int, State> states = new();
        readonly List<int> order = new();

        public void Add(int mapId)
        {
            states[mapId] = State.Pending;
            order.Add(mapId);
        }

        public void Report(int mapId, int points)
        {
            if (states.ContainsKey(mapId))
                states[mapId] = points > 0 ? State.Loaded : State.Failed;
        }

        /// <summary>Call when the SDK is ready. Returns the maps that never reported, now marked as failed.</summary>
        public List<int> FailPending()
        {
            var failed = new List<int>();
            foreach (int id in order)
                if (states[id] == State.Pending)
                {
                    states[id] = State.Failed;
                    failed.Add(id);
                }
            return failed;
        }

        public bool IsLoaded(int mapId) => states.TryGetValue(mapId, out var state) && state == State.Loaded;

        public bool IsFailed(int mapId) => states.TryGetValue(mapId, out var state) && state == State.Failed;

        /// <summary>First map still waiting for its load event, or -1.</summary>
        public int FirstPending
        {
            get
            {
                foreach (int id in order)
                    if (states[id] == State.Pending)
                        return id;
                return -1;
            }
        }
    }
}

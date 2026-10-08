using System.Collections.Generic;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Box values the team adjusted on this phone. A pose only means something inside its own map, so it is stored per
    /// map; size and fill belong to the building and are shared by its maps. If the meaning of a value changes, bump the
    /// prefix (v1 → v2) instead of reusing it silently.
    /// </summary>
    public sealed class PaseoBoxStore
    {
        public const string DefaultPrefix = "AncoRA.Paseo.v1.";

        readonly string prefix;

        public PaseoBoxStore(string keyPrefix = DefaultPrefix) => prefix = keyPrefix;

        public bool TryLoadPose(int mapId, out Vector3 position, out float yaw)
        {
            string key = prefix + mapId;
            if (PlayerPrefs.GetInt(key + ".Set", 0) != 1)
            {
                position = default;
                yaw = 0f;
                return false;
            }
            position = new Vector3(PlayerPrefs.GetFloat(key + ".X"), PlayerPrefs.GetFloat(key + ".Y"), PlayerPrefs.GetFloat(key + ".Z"));
            yaw = PlayerPrefs.GetFloat(key + ".Yaw");
            return true;
        }

        public void SavePose(int mapId, Vector3 position, float yaw)
        {
            string key = prefix + mapId;
            PlayerPrefs.SetFloat(key + ".X", position.x);
            PlayerPrefs.SetFloat(key + ".Y", position.y);
            PlayerPrefs.SetFloat(key + ".Z", position.z);
            PlayerPrefs.SetFloat(key + ".Yaw", yaw);
            PlayerPrefs.SetInt(key + ".Set", 1);
            PlayerPrefs.Save();
        }

        public bool TryLoadSize(string buildingId, out Vector3 size, out bool solid)
        {
            string key = prefix + buildingId;
            if (PlayerPrefs.GetInt(key + ".SizeSet", 0) != 1)
            {
                size = default;
                solid = false;
                return false;
            }
            size = new Vector3(PlayerPrefs.GetFloat(key + ".W"), PlayerPrefs.GetFloat(key + ".H"), PlayerPrefs.GetFloat(key + ".D"));
            solid = PlayerPrefs.GetInt(key + ".Solid", 0) == 1;
            return true;
        }

        public void SaveSize(string buildingId, Vector3 size, bool solid)
        {
            string key = prefix + buildingId;
            PlayerPrefs.SetFloat(key + ".W", size.x);
            PlayerPrefs.SetFloat(key + ".H", size.y);
            PlayerPrefs.SetFloat(key + ".D", size.z);
            PlayerPrefs.SetInt(key + ".Solid", solid ? 1 : 0);
            PlayerPrefs.SetInt(key + ".SizeSet", 1);
            PlayerPrefs.Save();
        }

        public void Clear(IEnumerable<int> mapIds, IEnumerable<string> buildingIds)
        {
            foreach (int mapId in mapIds)
                foreach (string suffix in new[] { ".X", ".Y", ".Z", ".Yaw", ".Set" })
                    PlayerPrefs.DeleteKey(prefix + mapId + suffix);
            foreach (string buildingId in buildingIds)
                foreach (string suffix in new[] { ".W", ".H", ".D", ".Solid", ".SizeSet" })
                    PlayerPrefs.DeleteKey(prefix + buildingId + suffix);
            PlayerPrefs.Save();
        }
    }
}

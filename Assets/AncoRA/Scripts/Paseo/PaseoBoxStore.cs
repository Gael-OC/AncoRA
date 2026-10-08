using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Box values the team adjusted on this phone. A pose only means something inside its own map, so it is stored per
    /// map; size and fill belong to the building and are shared by its maps. Each value remembers the baked scene value it
    /// was edited from: once the scene is baked again with different values, the phone value is stale and is dropped, so
    /// a later "Copiar valores" never carries it back into edificio.json. If the meaning of a value changes, bump the
    /// prefix (v1 → v2) instead of reusing it silently.
    /// </summary>
    public sealed class PaseoBoxStore
    {
        public const string DefaultPrefix = "AncoRA.Paseo.v1.";

        static readonly string[] PoseSuffixes = { ".X", ".Y", ".Z", ".Yaw", ".Set", ".Base" };
        static readonly string[] SizeSuffixes = { ".W", ".H", ".D", ".Solid", ".SizeSet", ".SizeBase" };

        readonly string prefix;

        public PaseoBoxStore(string keyPrefix = DefaultPrefix) => prefix = keyPrefix;

        public static string Fingerprint(Vector3 position, float yaw, bool placed) =>
            string.Join("|", F(position.x), F(position.y), F(position.z), F(yaw), placed ? "1" : "0");

        public static string Fingerprint(Vector3 size, bool solid) =>
            string.Join("|", F(size.x), F(size.y), F(size.z), solid ? "1" : "0");

        static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        public bool HasPose(int mapId) => PlayerPrefs.GetInt(prefix + mapId + ".Set", 0) == 1;

        public bool HasSize(string buildingId) => PlayerPrefs.GetInt(prefix + buildingId + ".SizeSet", 0) == 1;

        /// <summary>False when nothing is saved, or when it was edited from another baked pose (then it is deleted).</summary>
        public bool TryLoadPose(int mapId, string bakedFingerprint, out Vector3 position, out float yaw)
        {
            string key = prefix + mapId;
            position = default;
            yaw = 0f;
            if (!HasPose(mapId))
                return false;
            if (PlayerPrefs.GetString(key + ".Base", "") != bakedFingerprint)
            {
                Delete(key, PoseSuffixes);
                return false;
            }
            position = new Vector3(PlayerPrefs.GetFloat(key + ".X"), PlayerPrefs.GetFloat(key + ".Y"), PlayerPrefs.GetFloat(key + ".Z"));
            yaw = PlayerPrefs.GetFloat(key + ".Yaw");
            return true;
        }

        public void SavePose(int mapId, Vector3 position, float yaw, string bakedFingerprint)
        {
            string key = prefix + mapId;
            PlayerPrefs.SetFloat(key + ".X", position.x);
            PlayerPrefs.SetFloat(key + ".Y", position.y);
            PlayerPrefs.SetFloat(key + ".Z", position.z);
            PlayerPrefs.SetFloat(key + ".Yaw", yaw);
            PlayerPrefs.SetString(key + ".Base", bakedFingerprint);
            PlayerPrefs.SetInt(key + ".Set", 1);
            PlayerPrefs.Save();
        }

        /// <summary>False when nothing is saved, or when it was edited from another baked size (then it is deleted).</summary>
        public bool TryLoadSize(string buildingId, string bakedFingerprint, out Vector3 size, out bool solid)
        {
            string key = prefix + buildingId;
            size = default;
            solid = false;
            if (!HasSize(buildingId))
                return false;
            if (PlayerPrefs.GetString(key + ".SizeBase", "") != bakedFingerprint)
            {
                Delete(key, SizeSuffixes);
                return false;
            }
            size = new Vector3(PlayerPrefs.GetFloat(key + ".W"), PlayerPrefs.GetFloat(key + ".H"), PlayerPrefs.GetFloat(key + ".D"));
            solid = PlayerPrefs.GetInt(key + ".Solid", 0) == 1;
            return true;
        }

        public void SaveSize(string buildingId, Vector3 size, bool solid, string bakedFingerprint)
        {
            string key = prefix + buildingId;
            PlayerPrefs.SetFloat(key + ".W", size.x);
            PlayerPrefs.SetFloat(key + ".H", size.y);
            PlayerPrefs.SetFloat(key + ".D", size.z);
            PlayerPrefs.SetInt(key + ".Solid", solid ? 1 : 0);
            PlayerPrefs.SetString(key + ".SizeBase", bakedFingerprint);
            PlayerPrefs.SetInt(key + ".SizeSet", 1);
            PlayerPrefs.Save();
        }

        public void Clear(IEnumerable<int> mapIds, IEnumerable<string> buildingIds)
        {
            foreach (int mapId in mapIds)
                Delete(prefix + mapId, PoseSuffixes);
            foreach (string buildingId in buildingIds)
                Delete(prefix + buildingId, SizeSuffixes);
        }

        static void Delete(string key, string[] suffixes)
        {
            foreach (string suffix in suffixes)
                PlayerPrefs.DeleteKey(key + suffix);
            PlayerPrefs.Save();
        }
    }
}

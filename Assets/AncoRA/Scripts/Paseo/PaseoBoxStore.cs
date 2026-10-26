using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Box values the team adjusted on this phone. There is one box per building, so pose, size and fill are all stored
    /// per building; the pose is in the building frame (the frame of its reference map, PaseoBuildingFrame). Each value
    /// remembers the baked scene value it was edited from: once the scene is baked again with different values, the phone
    /// value is stale and is dropped, so a later "Copiar valores" never carries it back into edificio.json. If the
    /// meaning of a value changes, bump the prefix instead of reusing it silently (v1 held one pose per map, in the
    /// frame of that map; v2 holds one per building).
    /// </summary>
    public sealed class PaseoBoxStore
    {
        public const string DefaultPrefix = "AncoRA.Paseo.v2.";

        static readonly string[] PoseSuffixes = { ".X", ".Y", ".Z", ".Yaw", ".Set", ".Base" };
        static readonly string[] SizeSuffixes = { ".W", ".H", ".D", ".Solid", ".SizeSet", ".SizeBase" };

        readonly string prefix;

        public PaseoBoxStore(string keyPrefix = DefaultPrefix) => prefix = keyPrefix;

        public static string Fingerprint(Vector3 position, float yaw, bool placed) =>
            string.Join("|", F(position.x), F(position.y), F(position.z), F(yaw), placed ? "1" : "0");

        public static string Fingerprint(Vector3 size, bool solid) =>
            string.Join("|", F(size.x), F(size.y), F(size.z), solid ? "1" : "0");

        static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        public bool HasPose(string buildingId) => PlayerPrefs.GetInt(prefix + buildingId + ".Set", 0) == 1;

        public bool HasSize(string buildingId) => PlayerPrefs.GetInt(prefix + buildingId + ".SizeSet", 0) == 1;

        /// <summary>False when nothing is saved, or when it was edited from another baked pose (then it is deleted).</summary>
        public bool TryLoadPose(string buildingId, string bakedFingerprint, out Vector3 position, out float yaw)
        {
            string key = prefix + buildingId;
            position = default;
            yaw = 0f;
            if (!HasPose(buildingId))
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

        public void SavePose(string buildingId, Vector3 position, float yaw, string bakedFingerprint)
        {
            string key = prefix + buildingId;
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

        public void Clear(IEnumerable<string> buildingIds)
        {
            foreach (string buildingId in buildingIds)
            {
                Delete(prefix + buildingId, PoseSuffixes);
                Delete(prefix + buildingId, SizeSuffixes);
            }
        }

        static void Delete(string key, string[] suffixes)
        {
            foreach (string suffix in suffixes)
                PlayerPrefs.DeleteKey(key + suffix);
            PlayerPrefs.Save();
        }
    }
}

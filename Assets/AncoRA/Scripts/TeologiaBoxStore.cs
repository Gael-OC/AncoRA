using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// What the team dialled in on the phone for the Teologia demo, kept in PlayerPrefs: the selected map mode, the box
    /// pose in the frame of each map (a pose only means something inside its own map), and the box size and fill, which
    /// belong to the building and are shared by both maps. Keys carry a version prefix: if the meaning of a saved value
    /// ever changes, bump the prefix instead of reusing it (a silently reused value leaves the box shifted with no clue).
    /// </summary>
    public sealed class TeologiaBoxStore
    {
        readonly string prefix;

        public TeologiaBoxStore(string keyPrefix) => prefix = keyPrefix;

        string PoseKey(int mapId, string field) => $"{prefix}Caja.Pos.{mapId}.{field}";

        public int LoadMode(int fallback) => PlayerPrefs.GetInt(prefix + "Modo", fallback);

        public void SaveMode(int mode)
        {
            PlayerPrefs.SetInt(prefix + "Modo", mode);
            PlayerPrefs.Save();
        }

        public bool TryLoadPose(int mapId, out Vector3 position, out float yawDegrees)
        {
            position = default;
            yawDegrees = 0f;
            if (PlayerPrefs.GetInt(PoseKey(mapId, "set"), 0) != 1)
                return false;
            position = new Vector3(PlayerPrefs.GetFloat(PoseKey(mapId, "x")), PlayerPrefs.GetFloat(PoseKey(mapId, "y")),
                PlayerPrefs.GetFloat(PoseKey(mapId, "z")));
            yawDegrees = PlayerPrefs.GetFloat(PoseKey(mapId, "yaw"));
            return true;
        }

        public void SavePose(int mapId, Vector3 position, float yawDegrees)
        {
            PlayerPrefs.SetFloat(PoseKey(mapId, "x"), position.x);
            PlayerPrefs.SetFloat(PoseKey(mapId, "y"), position.y);
            PlayerPrefs.SetFloat(PoseKey(mapId, "z"), position.z);
            PlayerPrefs.SetFloat(PoseKey(mapId, "yaw"), yawDegrees);
            PlayerPrefs.SetInt(PoseKey(mapId, "set"), 1);
            PlayerPrefs.Save();
        }

        public void ClearPose(int mapId)
        {
            foreach (string field in new[] { "x", "y", "z", "yaw", "set" })
                PlayerPrefs.DeleteKey(PoseKey(mapId, field));
            PlayerPrefs.Save();
        }

        public bool TryLoadSize(out Vector3 sizeMeters, out bool solid)
        {
            sizeMeters = default;
            solid = false;
            if (PlayerPrefs.GetInt(prefix + "Caja.Tam.set", 0) != 1)
                return false;
            sizeMeters = new Vector3(PlayerPrefs.GetFloat(prefix + "Caja.Tam.w"), PlayerPrefs.GetFloat(prefix + "Caja.Tam.h"),
                PlayerPrefs.GetFloat(prefix + "Caja.Tam.d"));
            solid = PlayerPrefs.GetInt(prefix + "Caja.Solido", 1) == 1;
            return true;
        }

        public void SaveSize(Vector3 sizeMeters, bool solid)
        {
            PlayerPrefs.SetFloat(prefix + "Caja.Tam.w", sizeMeters.x);
            PlayerPrefs.SetFloat(prefix + "Caja.Tam.h", sizeMeters.y);
            PlayerPrefs.SetFloat(prefix + "Caja.Tam.d", sizeMeters.z);
            PlayerPrefs.SetInt(prefix + "Caja.Solido", solid ? 1 : 0);
            PlayerPrefs.SetInt(prefix + "Caja.Tam.set", 1);
            PlayerPrefs.Save();
        }

        public void ClearSize()
        {
            foreach (string key in new[] { "Caja.Tam.w", "Caja.Tam.h", "Caja.Tam.d", "Caja.Solido", "Caja.Tam.set" })
                PlayerPrefs.DeleteKey(prefix + key);
            PlayerPrefs.Save();
        }
    }
}

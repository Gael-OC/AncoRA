using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The frame the maps of one building share. All of them sit in one XR Space; each map keeps the same physical box in
    /// its own frame (edificio.json «caja»), so the XR Map of every map except the reference gets a local transform
    /// T = B_R · B_M⁻¹ (B = box pose, yaw only). Seen through T, the box of map M lands exactly on the box of the
    /// reference map R, and a localization with any map moves the same space and the same box. The reference map is the
    /// first one with a placed box (the first map when none is), and its transform is the identity.
    /// </summary>
    public static class PaseoBuildingFrame
    {
        /// <summary>Box pose as a matrix: position, and a turn of <paramref name="yaw"/> degrees around the vertical.</summary>
        public static Matrix4x4 BoxMatrix(Vector3 position, float yaw) =>
            Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), Vector3.one);

        /// <summary>Position and yaw in [0, 360) of a matrix made of a turn around the vertical and a move.</summary>
        public static void Decompose(Matrix4x4 pose, out Vector3 position, out float yaw)
        {
            position = new Vector3(pose.m03, pose.m13, pose.m23);
            yaw = NormalizeDegrees(Mathf.Atan2(pose.m02, pose.m22) * Mathf.Rad2Deg);
        }

        public static float NormalizeDegrees(float degrees)
        {
            float value = degrees % 360f;
            if (value < 0f)
                value += 360f;
            return value >= 360f ? 0f : value;
        }

        /// <summary>The transform of the XR Map of M inside the building space: T = B_R · B_M⁻¹.</summary>
        public static Matrix4x4 MapToBuilding(Vector3 referencePosition, float referenceYaw, Vector3 mapBoxPosition, float mapBoxYaw) =>
            BoxMatrix(referencePosition, referenceYaw) * BoxMatrix(mapBoxPosition, mapBoxYaw).inverse;

        /// <summary>Index in <c>mapas</c> of the reference map: the first with a placed box, or the first map.</summary>
        public static int ReferenceIndex(PaseoBuildingConfig building)
        {
            for (int i = 0; i < building.mapas.Count; i++)
                if (building.mapas[i].caja.colocada)
                    return i;
            return 0;
        }

        /// <summary>Id of the reference map, or 0 when the building has no maps.</summary>
        public static int ReferenceMapId(PaseoBuildingConfig building) =>
            building.mapas.Count == 0 ? 0 : building.mapas[ReferenceIndex(building)].id;

        /// <summary>
        /// The local transform of every XR Map of the building, in the order of <c>mapas</c>. The reference map and any
        /// map whose box is not placed get the identity (PaseoConfig.Validate refuses a building that mixes placed and
        /// unplaced boxes, or that has several maps and none placed: there is no way to relate them).
        /// </summary>
        public static Matrix4x4[] MapTransforms(PaseoBuildingConfig building)
        {
            var transforms = new Matrix4x4[building.mapas.Count];
            for (int i = 0; i < transforms.Length; i++)
                transforms[i] = Matrix4x4.identity;
            if (transforms.Length == 0)
                return transforms;
            int reference = ReferenceIndex(building);
            var r = building.mapas[reference].caja;
            for (int i = 0; i < transforms.Length; i++)
            {
                var box = building.mapas[i].caja;
                if (i == reference || !box.colocada)
                    continue;
                transforms[i] = MapToBuilding(ToVector(r.posicion), r.giro, ToVector(box.posicion), box.giro);
            }
            return transforms;
        }

        /// <summary>The box of the reference map: position and yaw in the building frame, and whether it is placed.</summary>
        public static void ReferenceBox(PaseoBuildingConfig building, out Vector3 position, out float yaw, out bool placed)
        {
            var box = building.mapas[ReferenceIndex(building)].caja;
            position = ToVector(box.posicion);
            yaw = box.giro;
            placed = box.colocada;
        }

        /// <summary>
        /// Writes a new box pose, given in the building frame, into the «caja» of every map: B_M = T_M⁻¹ · B_R', with the
        /// transforms T_M derived from the poses as they are now. The reference map takes the pose as given.
        /// </summary>
        public static void ApplyBoxPose(PaseoBuildingConfig building, Vector3 position, float yaw)
        {
            var transforms = MapTransforms(building);
            int reference = ReferenceIndex(building);
            var target = BoxMatrix(position, yaw);
            for (int i = 0; i < building.mapas.Count; i++)
            {
                var box = building.mapas[i].caja;
                if (i == reference)
                {
                    box.posicion = new[] { position.x, position.y, position.z };
                    box.giro = NormalizeDegrees(yaw);
                }
                else
                {
                    Decompose(transforms[i].inverse * target, out var p, out float y);
                    box.posicion = new[] { p.x, p.y, p.z };
                    box.giro = y;
                }
                box.colocada = true;
            }
        }

        static Vector3 ToVector(float[] values) => new(values[0], values[1], values[2]);
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>Where the building box stands in the frame of the MultiSet map space: centre, rotation and size.</summary>
    public readonly struct PaseoMultiSetRelation
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Size;

        public PaseoMultiSetRelation(Vector3 position, Quaternion rotation, Vector3 size)
        {
            Position = position;
            Rotation = rotation;
            Size = size;
        }
    }

    public enum PaseoMultiSetRelationState
    {
        /// <summary>No relation was saved and Immersal's box is solid: the relation is saved now.</summary>
        New,
        /// <summary>A relation was saved earlier: MultiSet can place the box by itself.</summary>
        Saved,
        /// <summary>No relation saved and Immersal's box cannot be trusted now: nothing to save or compare.</summary>
        NoImmersal
    }

    /// <summary>What one accepted MultiSet location says about the box of its building.</summary>
    public readonly struct PaseoMultiSetEvaluation
    {
        public readonly PaseoMultiSetRelationState State;
        /// <summary>The relation to use for the cyan box; Size is meaningful only when <see cref="HasRelation"/>.</summary>
        public readonly PaseoMultiSetRelation Relation;
        public readonly bool HasRelation;
        /// <summary>How far MultiSet's box is from Immersal's (largest corner move, m); NaN when there is nothing to compare.</summary>
        public readonly float DifferenceMeters;

        public PaseoMultiSetEvaluation(PaseoMultiSetRelationState state, PaseoMultiSetRelation relation, bool hasRelation, float differenceMeters)
        {
            State = state;
            Relation = relation;
            HasRelation = hasRelation;
            DifferenceMeters = differenceMeters;
        }
    }

    /// <summary>
    /// The geometry that ties MultiSet to Immersal. MultiSet moves its map space (a GameObject) to where its map stands in
    /// the AR world; Immersal moves one XR Space per map. A box that Immersal has placed has a world pose; the same box seen
    /// from the MultiSet map space is a fixed relation, saved once and then reused: the next time MultiSet localizes, that
    /// relation puts the box without Immersal. The difference between the two boxes is measured like PaseoPoseFilter does:
    /// the largest move of any box corner.
    /// </summary>
    public static class PaseoMultiSetGeometry
    {
        /// <summary>The box (world pose and size) in the frame of the map space (world pose).</summary>
        public static PaseoMultiSetRelation FromWorld(Vector3 spacePosition, Quaternion spaceRotation, Vector3 boxPosition,
            Quaternion boxRotation, Vector3 size)
        {
            var inverse = Quaternion.Inverse(spaceRotation);
            return new PaseoMultiSetRelation(inverse * (boxPosition - spacePosition), inverse * boxRotation, size);
        }

        /// <summary>Where the relation puts the box in the world, given where the map space is now.</summary>
        public static void ToWorld(PaseoMultiSetRelation relation, Vector3 spacePosition, Quaternion spaceRotation,
            out Vector3 position, out Quaternion rotation)
        {
            position = spacePosition + spaceRotation * relation.Position;
            rotation = spaceRotation * relation.Rotation;
        }

        /// <summary>How far the box moves between two poses: the largest displacement of its eight corners (PaseoPoseFilter's measure).</summary>
        public static float MaxCornerMove(Vector3 centerA, Quaternion rotationA, Vector3 centerB, Quaternion rotationB, Vector3 size)
        {
            var half = size * 0.5f;
            float largest = 0f;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z);
                largest = Mathf.Max(largest, Vector3.Distance(centerA + rotationA * corner, centerB + rotationB * corner));
            }
            return largest;
        }

        /// <summary>
        /// Decides what to do with a MultiSet location of a building. <paramref name="saved"/> is the relation kept for this
        /// map and building, if any; the Immersal arguments are its box, passed only when it is shown, confirmed and recent
        /// (<paramref name="immersalUsable"/>). The map space pose is MultiSet's, just received.
        /// </summary>
        public static PaseoMultiSetEvaluation Evaluate(bool hasSaved, PaseoMultiSetRelation saved, bool immersalUsable,
            Vector3 immersalPosition, Quaternion immersalRotation, Vector3 immersalSize, Vector3 spacePosition, Quaternion spaceRotation)
        {
            if (hasSaved)
            {
                float difference = float.NaN;
                if (immersalUsable)
                {
                    ToWorld(saved, spacePosition, spaceRotation, out var position, out var rotation);
                    difference = MaxCornerMove(position, rotation, immersalPosition, immersalRotation, immersalSize);
                }
                return new PaseoMultiSetEvaluation(PaseoMultiSetRelationState.Saved, saved, true, difference);
            }
            if (!immersalUsable)
                return new PaseoMultiSetEvaluation(PaseoMultiSetRelationState.NoImmersal, default, false, float.NaN);
            var relation = FromWorld(spacePosition, spaceRotation, immersalPosition, immersalRotation, immersalSize);
            // Saved from Immersal's own box, so both boxes agree by construction: nothing to compare yet.
            return new PaseoMultiSetEvaluation(PaseoMultiSetRelationState.New, relation, true, float.NaN);
        }
    }

    /// <summary>
    /// The relations saved on this phone, per MultiSet map (or map set) code and building, under their own versioned
    /// prefix. PlayerPrefs cannot list its keys, so an index of the saved pairs lets «Olvidar relación MultiSet» clear them.
    /// If the meaning of a value changes, bump the prefix (v1 → v2) instead of reusing it silently.
    /// </summary>
    public sealed class PaseoMultiSetStore
    {
        public const string DefaultPrefix = "AncoRA.Paseo.MultiSet.v1.";
        const string IndexKey = "Index";
        const char IndexSeparator = '|';

        static readonly string[] Suffixes = { ".X", ".Y", ".Z", ".QX", ".QY", ".QZ", ".QW", ".W", ".H", ".D", ".Set" };

        readonly string prefix;

        public PaseoMultiSetStore(string keyPrefix = DefaultPrefix) => prefix = keyPrefix;

        string Key(string code, string building) => prefix + Clean(code) + "/" + Clean(building);

        // The index separates pairs with '|', so neither part may carry one.
        static string Clean(string part) => (part ?? "").Replace(IndexSeparator, '_');

        public bool Has(string code, string building) => PlayerPrefs.GetInt(Key(code, building) + ".Set", 0) == 1;

        public int Count => Entries().Count;

        public bool TryLoad(string code, string building, out PaseoMultiSetRelation relation)
        {
            relation = default;
            if (!Has(code, building))
                return false;
            string key = Key(code, building);
            relation = new PaseoMultiSetRelation(
                new Vector3(PlayerPrefs.GetFloat(key + ".X"), PlayerPrefs.GetFloat(key + ".Y"), PlayerPrefs.GetFloat(key + ".Z")),
                new Quaternion(PlayerPrefs.GetFloat(key + ".QX"), PlayerPrefs.GetFloat(key + ".QY"), PlayerPrefs.GetFloat(key + ".QZ"),
                    PlayerPrefs.GetFloat(key + ".QW", 1f)),
                new Vector3(PlayerPrefs.GetFloat(key + ".W"), PlayerPrefs.GetFloat(key + ".H"), PlayerPrefs.GetFloat(key + ".D")));
            return true;
        }

        public void Save(string code, string building, PaseoMultiSetRelation relation)
        {
            string key = Key(code, building);
            PlayerPrefs.SetFloat(key + ".X", relation.Position.x);
            PlayerPrefs.SetFloat(key + ".Y", relation.Position.y);
            PlayerPrefs.SetFloat(key + ".Z", relation.Position.z);
            PlayerPrefs.SetFloat(key + ".QX", relation.Rotation.x);
            PlayerPrefs.SetFloat(key + ".QY", relation.Rotation.y);
            PlayerPrefs.SetFloat(key + ".QZ", relation.Rotation.z);
            PlayerPrefs.SetFloat(key + ".QW", relation.Rotation.w);
            PlayerPrefs.SetFloat(key + ".W", relation.Size.x);
            PlayerPrefs.SetFloat(key + ".H", relation.Size.y);
            PlayerPrefs.SetFloat(key + ".D", relation.Size.z);
            PlayerPrefs.SetInt(key + ".Set", 1);
            var entries = Entries();
            string pair = Clean(code) + "/" + Clean(building);
            if (!entries.Contains(pair))
            {
                entries.Add(pair);
                PlayerPrefs.SetString(prefix + IndexKey, string.Join(IndexSeparator.ToString(), entries));
            }
            PlayerPrefs.Save();
        }

        /// <summary>Forgets every saved relation. Returns how many there were.</summary>
        public int ClearAll()
        {
            var entries = Entries();
            foreach (string pair in entries)
                foreach (string suffix in Suffixes)
                    PlayerPrefs.DeleteKey(prefix + pair + suffix);
            PlayerPrefs.DeleteKey(prefix + IndexKey);
            PlayerPrefs.Save();
            return entries.Count;
        }

        List<string> Entries()
        {
            string index = PlayerPrefs.GetString(prefix + IndexKey, "");
            return index.Length == 0 ? new List<string>() : new List<string>(index.Split(IndexSeparator));
        }
    }
}

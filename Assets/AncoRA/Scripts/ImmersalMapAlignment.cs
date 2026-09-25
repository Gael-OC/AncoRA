using Immersal.XR;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Holds the manual map-to-XR-Space alignment chosen by the team in the Editor.
    /// <para>
    /// Why this exists: <see cref="XRMap.ApplyAlignment"/> rewrites the XR Map transform from the portal
    /// metadata (identity for un-aligned free-plan maps), and the XR Map inspector calls it after metadata
    /// changes. <c>MapManager.RegisterMap</c> reads the XR Map local pose once at SDK start and uses it as
    /// the map-to-space relation. This component re-applies the stored values in Awake, which runs before
    /// the SDK registers maps, so a later ApplyAlignment call cannot silently discard the manual result.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    [DisallowMultipleComponent]
    public sealed class ImmersalMapAlignment : MonoBehaviour
    {
        [SerializeField] XRMap map;

        [Tooltip("Reference map (A): its XR Map stays at the XR Space origin with identity rotation.")]
        [SerializeField] bool isReference;

        [Tooltip("Position of this XR Map inside XR Space, in meters (Unity axes).")]
        [SerializeField] Vector3 localPosition;

        [Tooltip("Rotation of this XR Map inside XR Space, in degrees (Unity Euler, Y is the vertical axis).")]
        [SerializeField] Vector3 localEulerAngles;

        [Tooltip("Tick after the team aligned this map against measured physical details. Not a proof of alignment.")]
        [SerializeField] bool adjustedByTeam;

        [TextArea(2, 6)]
        [SerializeField] string fieldNotes = "";

        public XRMap Map => map;
        public bool IsReference => isReference;
        public bool AdjustedByTeam => isReference || adjustedByTeam;
        public Vector3 LocalPosition => isReference ? Vector3.zero : localPosition;
        public Quaternion LocalRotation => isReference ? Quaternion.identity : Quaternion.Euler(localEulerAngles);

        public void Initialize(XRMap targetMap, bool reference, Vector3 position, Vector3 eulerAngles)
        {
            map = targetMap;
            isReference = reference;
            localPosition = position;
            localEulerAngles = eulerAngles;
            Apply();
        }

        /// <summary>Writes the stored alignment to the XR Map transform (uniform scale 1).</summary>
        [ContextMenu("Aplicar alineación manual")]
        public void Apply()
        {
            if (map == null)
                return;
            var t = map.transform;
            t.localPosition = LocalPosition;
            t.localRotation = LocalRotation;
            t.localScale = Vector3.one;
        }

        /// <summary>True when the XR Map transform still equals the stored alignment.</summary>
        public bool TransformMatchesStoredValues(float positionTolerance = 1e-4f, float angleTolerance = 0.01f)
        {
            if (map == null)
                return false;
            var t = map.transform;
            return (t.localPosition - LocalPosition).sqrMagnitude <= positionTolerance * positionTolerance &&
                   Quaternion.Angle(t.localRotation, LocalRotation) <= angleTolerance &&
                   (t.localScale - Vector3.one).sqrMagnitude <= 1e-8f;
        }

        void Awake() => Apply();

#if UNITY_EDITOR
        // Keep Scene View and Inspector edits of this component in sync with the XR Map transform.
        void OnValidate()
        {
            if (!Application.isPlaying)
                Apply();
        }
#endif
    }
}

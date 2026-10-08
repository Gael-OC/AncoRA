using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Building name floating over its box. Turns only around Y to face the camera, like a sign. Uses a legacy TextMesh
    /// with Unity's built-in font because the project has no TextMeshPro resources imported.
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    public sealed class PaseoLabel : MonoBehaviour
    {
        const float GapAboveBoxMeters = 1.5f;

        TextMesh textMesh;
        MeshRenderer meshRenderer;

        void Awake() => Init();

        void Init()
        {
            if (textMesh == null)
                textMesh = GetComponent<TextMesh>();
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();
        }

        /// <summary>Sets the text and sits the label just above a box of the given height (box pivot = centre).</summary>
        public void Show(string text, float boxHeight)
        {
            Init();
            textMesh.text = text;
            transform.localPosition = new Vector3(0f, boxHeight * 0.5f + GapAboveBoxMeters, 0f);
        }

        public void SetVisible(bool visible)
        {
            Init();
            meshRenderer.enabled = visible;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || meshRenderer == null || !meshRenderer.enabled)
                return;
            // TextMesh reads correctly when the camera looks along the label's +Z.
            var away = transform.position - cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f)
                return;
            transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AncorRA.AR
{
    /// <summary>
    /// The camera look of the era on show. One global URP volume per era (built by PaseoSetup.Prepare from
    /// PaseoEpochs.Look); an era change crossfades their weights over the same time as the models. The film wear
    /// (scratches, dust, flicker) is not post-processing: the interface draws it (PaseoFilmOverlay) from
    /// <see cref="Scratches"/> and <see cref="Flicker"/>. Post-processing is switched off while no volume weighs anything.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaseoEpochFilter : MonoBehaviour
    {
        // Index = PaseoEpoch; «Actualidad» has none.
        [SerializeField] Volume[] volumes = new Volume[PaseoEpochs.Count];
        [SerializeField] UniversalAdditionalCameraData cameraData;

        PaseoEpoch from = PaseoEpoch.Actualidad;
        PaseoEpoch to = PaseoEpoch.Actualidad;
        float blend = 1f;
        float start;
        bool changing;

        public PaseoEpoch Epoch => to;
        public float Scratches { get; private set; }
        public float Flicker { get; private set; }
        public Volume VolumeFor(PaseoEpoch epoch) => volumes != null && (int)epoch < volumes.Length ? volumes[(int)epoch] : null;

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(Volume[] eraVolumes, UniversalAdditionalCameraData camera)
        {
            volumes = (Volume[])eraVolumes.Clone();
            cameraData = camera;
            Apply();
        }

        public void SetEpoch(PaseoEpoch epoch, bool animate)
        {
            if (epoch == to && !changing)
                return;
            from = to;
            to = epoch;
            changing = animate;
            start = Time.unscaledTime;
            blend = animate ? 0f : 1f;
            Apply();
        }

        void Update()
        {
            if (!changing)
                return;
            // The filter follows the whole change, as if the leaving model were there.
            var step = PaseoEpochs.Evaluate(Time.unscaledTime - start, hasOutgoing: true);
            blend = step.FilterBlend;
            changing = !step.Done;
            Apply();
        }

        void Apply()
        {
            bool any = false;
            for (int i = 0; volumes != null && i < volumes.Length; i++)
            {
                if (volumes[i] == null)
                    continue;
                var epoch = (PaseoEpoch)i;
                float weight = (epoch == to ? blend : 0f) + (epoch == from && from != to ? 1f - blend : 0f);
                volumes[i].weight = weight;
                volumes[i].enabled = weight > 0.001f;
                any |= volumes[i].enabled;
            }
            if (cameraData != null)
                cameraData.renderPostProcessing = any;
            var a = PaseoEpochs.Look(from);
            var b = PaseoEpochs.Look(to);
            Scratches = Mathf.Lerp(a.Scratches, b.Scratches, blend);
            Flicker = Mathf.Lerp(a.Flicker, b.Flicker, blend);
        }
    }
}

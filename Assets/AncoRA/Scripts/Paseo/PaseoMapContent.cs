using System.Collections.Generic;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The content of one building: its box, child of the building's XR Space (one space, one box and one pose filter per
    /// building, with all its maps inside the space). The box pose is in the building frame, which is the frame of the
    /// reference map (PaseoBuildingFrame). In campus mode all the buildings share one space: the box pose is then in the
    /// campus frame and there is no reference map (<see cref="ReferenceMapId"/> is 0). The name and description are drawn
    /// by the interface (PaseoUi). The class keeps its old name so the scene's script reference stays valid.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaseoMapContent : MonoBehaviour
    {
        // The building's maps in edificio.json order, and the one whose frame is the building frame (0 in campus mode).
        [SerializeField] int[] mapIds = System.Array.Empty<int>();
        [SerializeField] int referenceMapId;
        [SerializeField] string buildingId;
        [SerializeField] string buildingName;
        [SerializeField] string description;
        [SerializeField] EdificioFacadeFrame box;
        // Centre of the building box on the ground (edificio.json «gps»), for the search by distance (PaseoSearchPlan).
        [SerializeField] bool hasGps;
        [SerializeField] double latitude;
        [SerializeField] double longitude;
        // The animated park over and around the box (null when edificio.json has no «parque»).
        [SerializeField] PaseoPark park;
        // Normalized mesh baked in the Editor; scales with the measured box and shares its pose.
        [SerializeField] Transform buildingModel;
        [SerializeField] string modelStyle;
        // Era models (index = PaseoEpoch). 2000 is the demo model above and «Actualidad» has none; an era without its own
        // model falls back to the demo model.
        [SerializeField] Transform[] epochModels = new Transform[PaseoEpochs.Count];

        // edificio.json «oculta»: the maps still locate, but the box and the card of this building are not drawn.
        [SerializeField] bool boxHidden;
        // edificio.json «buscar»: false keeps this building's maps out of the localization search.
        [SerializeField] bool searchable = true;

        // The building is on screen (the tour located it).
        bool shown;
        // The park switch of the camera view: the park shows only while the box does and the switch is on.
        bool parkEnabled = true;
        bool showDiagnosticBox;

        // The era on show and, while it changes, the one leaving (PaseoEpochs.Evaluate drives both cuts).
        PaseoEpoch epoch = PaseoEpoch.Y2000;
        PaseoEpoch leaving;
        bool changing;
        bool changeHasOutgoing;
        float changeStart;
        MaterialPropertyBlock revealBlock;
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int RevealRangeId = Shader.PropertyToID("_RevealRange");

        public IReadOnlyList<int> MapIds => mapIds;
        public int ReferenceMapId => referenceMapId;
        public string BuildingId => buildingId;
        public string BuildingName => buildingName;
        public string Description => description;
        public bool HasGps => hasGps;
        public double Latitude => latitude;
        public double Longitude => longitude;
        /// <summary>The building is located and on screen (its box is drawn unless <see cref="BoxHidden"/>).</summary>
        public bool IsShown => shown;
        /// <summary>The box is drawn: located and not switched off in edificio.json.</summary>
        public bool IsDrawn => shown && !boxHidden;
        public bool BoxHidden => boxHidden;
        public bool Searchable => searchable;
        public EdificioFacadeFrame Box => box;
        public PaseoPark Park => park;
        public Transform BuildingModel => buildingModel;
        public string ModelStyle => modelStyle;
        public PaseoEpoch Epoch => epoch;
        public bool EpochChanging => changing;
        /// <summary>The building has at least one model (otherwise the diagnostic box is drawn, whatever the era).</summary>
        public bool HasModels => buildingModel != null || System.Array.Exists(epochModels ?? System.Array.Empty<Transform>(), m => m != null);

        /// <summary>The model drawn for an era: its own, else the demo model; none for «Actualidad».</summary>
        public Transform ModelFor(PaseoEpoch value)
        {
            if (value == PaseoEpoch.Actualidad)
                return null;
            var own = epochModels != null && (int)value < epochModels.Length ? epochModels[(int)value] : null;
            return own != null ? own : buildingModel;
        }

        /// <summary>The model registered for that era only (no fallback), for validation.</summary>
        public Transform OwnEpochModel(PaseoEpoch value) =>
            epochModels != null && (int)value < epochModels.Length ? epochModels[(int)value] : null;
        public bool DiagnosticBoxShown => showDiagnosticBox;

        public void SetDiagnosticBox(bool value)
        {
            showDiagnosticBox = value;
            box.SetOutlineOnly(HasModels && value);
            SetVisible(shown);
        }
        public bool Placed => box != null && box.PlacedByTeam;
        public Vector3 LocalPosition => transform.localPosition;
        public float LocalYaw => transform.localEulerAngles.y;
        public Vector3 SizeMeters => new(box.WidthMeters, box.HeightMeters, box.DepthMeters);

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void Configure(int[] maps, int referenceMap, string building, string displayName, string buildingDescription,
            EdificioFacadeFrame frame)
        {
            mapIds = (int[])maps.Clone();
            referenceMapId = referenceMap;
            buildingId = building;
            buildingName = displayName;
            description = buildingDescription ?? "";
            box = frame;
        }

        /// <summary>Editor setup (PaseoSetup.Prepare): the building centre, or none.</summary>
        public void SetGps(bool known, double centreLatitude, double centreLongitude)
        {
            hasGps = known;
            latitude = known ? centreLatitude : 0.0;
            longitude = known ? centreLongitude : 0.0;
        }

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void SetBoxHidden(bool hidden) => boxHidden = hidden;

        /// <summary>Editor setup (PaseoSetup.Prepare).</summary>
        public void SetSearchable(bool value) => searchable = value;

        /// <summary>Editor setup (PaseoSetup.Prepare): the park built under the box, or none.</summary>
        public void SetPark(PaseoPark builtPark) => park = builtPark;

        /// <summary>Editor setup: replaces the diagnostic frame with an opaque model.</summary>
        public void SetBuildingModel(Transform model, string style)
        {
            buildingModel = model;
            modelStyle = style;
            if (buildingModel != null)
                buildingModel.localScale = SizeMeters;
            SetVisible(shown);
        }

        /// <summary>Editor setup (PaseoSetup.Prepare): the model of 1950 or 1980.</summary>
        public void SetEpochModel(PaseoEpoch value, Transform model)
        {
            if (value != PaseoEpoch.Y1950 && value != PaseoEpoch.Y1980)
                throw new System.ArgumentException($"La época {value} no lleva modelo propio.");
            if (epochModels == null || epochModels.Length != PaseoEpochs.Count)
                System.Array.Resize(ref epochModels, PaseoEpochs.Count);
            epochModels[(int)value] = model;
            if (model != null)
                model.localScale = SizeMeters;
            SetVisible(shown);
        }

        /// <summary>
        /// Shows another era. Animated only while the building is drawn: the leaving model comes down from the top, then
        /// the new one rises from the ground. Asking again mid-change starts over from the era already chosen.
        /// </summary>
        public void SetEpoch(PaseoEpoch value, bool animate)
        {
            if (value == epoch)
                return;
            var from = ModelFor(epoch);
            var to = ModelFor(value);
            leaving = epoch;
            epoch = value;
            changing = animate && shown && !boxHidden && from != to;
            changeHasOutgoing = from != null;
            changeStart = Time.unscaledTime;
            ApplyModels();
            if (changing)
                Step();
        }

        void Update()
        {
            if (changing)
                Step();
        }

        void Step()
        {
            var step = PaseoEpochs.Evaluate(Time.unscaledTime - changeStart, changeHasOutgoing);
            if (step.Done)
            {
                changing = false;
                ApplyModels();
                return;
            }
            var to = ModelFor(epoch);
            var from = ModelFor(leaving);
            if (from != null && from != to)
                SetReveal(from, step.OutReveal);
            if (to != null)
                SetReveal(to, step.InReveal);
        }

        void SetReveal(Transform model, float reveal)
        {
            if (!model.TryGetComponent<Renderer>(out var renderer))
                return;
            if (reveal >= 1f)
            {
                // Back in the SRP batch once the model is whole.
                renderer.SetPropertyBlock(null);
                return;
            }
            revealBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(revealBlock);
            var bounds = model.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null
                ? filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            revealBlock.SetVector(RevealRangeId, new Vector4(bounds.min.y - 0.05f, bounds.max.y + 0.05f, 0f, 0f));
            revealBlock.SetFloat(RevealId, reveal);
            renderer.SetPropertyBlock(revealBlock);
        }

        void ApplyModels()
        {
            var current = ModelFor(epoch);
            var outgoing = changing ? ModelFor(leaving) : null;
            foreach (var model in AllModels())
            {
                bool on = shown && !boxHidden && (model == current || model == outgoing);
                model.gameObject.SetActive(on);
                if (!changing)
                    SetReveal(model, 1f);
            }
        }

        IEnumerable<Transform> AllModels()
        {
            if (buildingModel != null)
                yield return buildingModel;
            if (epochModels == null)
                yield break;
            foreach (var model in epochModels)
                if (model != null && model != buildingModel)
                    yield return model;
        }

        public void SetPose(Vector3 localPosition, float yaw, bool placed)
        {
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            box.MarkPlaced(placed);
        }

        public void SetSize(Vector3 size, bool solid)
        {
            box.SetSize(size.x, size.y, size.z);
            box.SetSolid(solid);
            foreach (var model in AllModels())
                model.localScale = size;
            if (park != null)
                park.Relayout(size);
        }

        public void SetVisible(bool visible)
        {
            shown = visible;
            box.SetVisible(visible && !boxHidden && (!HasModels || showDiagnosticBox));
            ApplyModels();
            ApplyPark();
        }

        public void SetParkEnabled(bool enabled)
        {
            parkEnabled = enabled;
            ApplyPark();
        }

        void ApplyPark()
        {
            if (park != null)
                park.gameObject.SetActive(shown && parkEnabled && !boxHidden);
        }
    }
}

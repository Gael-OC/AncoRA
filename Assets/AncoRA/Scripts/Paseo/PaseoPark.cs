using System;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// The animated park of one building: block characters and props (trees, atom, flag, disco ball) as children of the
    /// building box. The scene holds the whole hierarchy (PaseoSetup builds it from built-in meshes and shared materials);
    /// this component only moves transforms, so it creates no mesh or material at runtime and has nothing to release.
    /// What goes where comes from <see cref="PaseoParkLayout"/>: the rig arrays follow the order of its plan.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaseoPark : MonoBehaviour
    {
        /// <summary>The transforms of one block character. Shoulder and hip pivots sit at the joints, the limbs hang below.</summary>
        [Serializable]
        public sealed class ActorRig
        {
            public Transform root;
            public Transform body;
            public Transform head;
            public Transform leftArm;
            public Transform rightArm;
            public Transform leftLeg;
            public Transform rightLeg;
        }

        /// <summary>A part that turns about its local Y (amplitude 0) or swings back and forth about it (amplitude in degrees).</summary>
        [Serializable]
        public sealed class Spinner
        {
            public Transform part;
            public float degreesPerSecond;
            public float phaseDegrees;
            public float amplitudeDegrees;
        }

        [SerializeField] string buildingId;
        [SerializeField] string theme;
        [SerializeField] float scale = 1.5f;
        [SerializeField] string front = "+z";
        [SerializeField] ActorRig[] actors = Array.Empty<ActorRig>();
        [SerializeField] Transform[] props = Array.Empty<Transform>();
        [SerializeField] Spinner[] spinners = Array.Empty<Spinner>();

        // Runtime state, rebuilt by Relayout.
        PaseoParkPlan plan;
        Vector3 boxSize;
        Vector3[] basePositions = Array.Empty<Vector3>();
        PaseoParkPath[] paths = Array.Empty<PaseoParkPath>();
        float startTime;

        public string BuildingId => buildingId;
        public string Theme => theme;
        public float Scale => scale;
        public string Front => front;
        public int ActorCount => actors.Length;
        public int PropCount => props.Length;

        /// <summary>Editor setup (PaseoSetup.Prepare): the park data and its rigs, in the order of the layout plan.</summary>
        public void Configure(string building, PaseoParkConfig config, ActorRig[] actorRigs, Transform[] propRoots, Spinner[] spinning)
        {
            buildingId = building;
            theme = config.tema;
            scale = config.escala;
            front = config.frente;
            actors = actorRigs;
            props = propRoots;
            spinners = spinning;
        }

        void OnEnable()
        {
            startTime = Time.time;
            if (plan != null)
                return;
            var frame = GetComponentInParent<EdificioFacadeFrame>();
            if (frame != null)
                Relayout(new Vector3(frame.WidthMeters, frame.HeightMeters, frame.DepthMeters));
        }

        /// <summary>Places every character and prop for a box of this size (the team can resize the box in the field).</summary>
        public void Relayout(Vector3 size)
        {
            boxSize = size;
            var config = new PaseoParkConfig { tema = theme, escala = scale, frente = front };
            plan = PaseoParkLayout.Build(config, size, buildingId);
            if (plan.Actors.Count != actors.Length || plan.Props.Count != props.Length)
            {
                Debug.LogError($"[AncoRA Paseo] El parque de {buildingId} no coincide con su plan (personajes {actors.Length}/{plan.Actors.Count}, " +
                               $"adornos {props.Length}/{plan.Props.Count}); correr Prepare.");
                plan = null;
                return;
            }
            if (basePositions.Length != actors.Length)
            {
                basePositions = new Vector3[actors.Length];
                paths = new PaseoParkPath[actors.Length];
            }
            for (int i = 0; i < actors.Length; i++)
            {
                var spot = plan.Actors[i];
                var root = actors[i].root;
                root.localScale = Vector3.one * scale;
                if (spot.Walks)
                {
                    float groundY = PaseoParkLayout.Resolve(Vector2.zero, spot.Surface, size).y;
                    basePositions[i] = new Vector3(0f, groundY, 0f);
                    paths[i] = spot.Surface == PaseoSurface.Techo
                        ? PaseoParkPath.Roof(size, spot.PathMargin)
                        : PaseoParkPath.Ground(size, spot.PathMargin);
                }
                else
                {
                    basePositions[i] = PaseoParkLayout.Resolve(spot.Normalized, spot.Surface, size);
                    paths[i] = null;
                    root.localPosition = basePositions[i];
                    root.localRotation = Quaternion.Euler(0f, spot.Yaw, 0f);
                }
            }
            for (int i = 0; i < props.Length; i++)
            {
                var spot = plan.Props[i];
                var position = PaseoParkLayout.Resolve(spot.Normalized, spot.Surface, size);
                position.y += spot.HeightAbove * scale;
                props[i].localPosition = position;
                props[i].localScale = Vector3.one * (scale * spot.Scale);
            }
        }

        void Update()
        {
            if (plan == null)
                return;
            float t = Time.time - startTime;
            for (int i = 0; i < actors.Length; i++)
                Animate(actors[i], plan.Actors[i], basePositions[i], paths[i], t);
            for (int i = 0; i < spinners.Length; i++)
            {
                var s = spinners[i];
                float angle = s.amplitudeDegrees > 0f
                    ? s.amplitudeDegrees * Mathf.Sin((s.degreesPerSecond * t + s.phaseDegrees) * Mathf.Deg2Rad)
                    : s.degreesPerSecond * t + s.phaseDegrees;
                s.part.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        void Animate(ActorRig rig, PaseoActorSpot spot, Vector3 basePosition, PaseoParkPath path, float t)
        {
            var move = PaseoParkSchedule.MoveAt(spot.Repertoire, t, spot.Phase01);
            float time = t;
            Vector3 position = basePosition;
            if (spot.Walks && path != null)
            {
                float distance = PaseoBlockMotion.Speed(move) * t + spot.PathStart01 * path.Length;
                path.Sample(distance, out var xz, out float yaw);
                position.x = xz.x;
                position.z = xz.y;
                rig.root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
            var pose = PaseoBlockMotion.Evaluate(move, time, spot.Phase01);
            position.y += pose.bodyLift * scale;
            rig.root.localPosition = position;
            // Pitch is positive forward, which is a negative turn about X; an arm raised outward turns about Z, mirrored on the left.
            rig.leftArm.localRotation = Quaternion.Euler(-pose.leftShoulder, 0f, -pose.leftShoulderRoll);
            rig.rightArm.localRotation = Quaternion.Euler(-pose.rightShoulder, 0f, pose.rightShoulderRoll);
            rig.leftLeg.localRotation = Quaternion.Euler(-pose.leftHip, 0f, 0f);
            rig.rightLeg.localRotation = Quaternion.Euler(-pose.rightHip, 0f, 0f);
            rig.head.localRotation = Quaternion.Euler(0f, pose.headYaw, 0f);
            rig.body.localRotation = Quaternion.Euler(0f, pose.bodyYaw, pose.bodySway);
        }
    }
}

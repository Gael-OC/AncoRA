#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using AncorRA.AR;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AncorRA.Editor
{
    /// <summary>Shared materials of the park: the palette (indexed by PaseoActorSpot.ColorIndex) and the fixed parts.</summary>
    internal sealed class PaseoBlockMaterials
    {
        public Material[] Palette;
        public Material Skin, Pants, Face, Trunk, Leaves, Metal;
    }

    /// <summary>
    /// Builds the hierarchy of a building's park under its box (PaseoSetup.Prepare). Only Unity's built-in meshes and shared
    /// material assets are used, so the player creates no mesh or material at runtime. Sizes are at scale 1; PaseoPark scales
    /// each character and prop root.
    /// </summary>
    internal static class PaseoParkBuilder
    {
        public const string ShaderName = "AncoRA/Paseo/Bloques";
        // Where Unity reports the path of its built-in primitive meshes.
        public const string BuiltinMeshAssetPath = "Library/unity default resources";

        // Palette order: ColorIndex picks one of these.
        const int Red = 0, Cyan = 7;

        static readonly Color[] PaletteColors =
        {
            new(0.90f, 0.22f, 0.20f), // red
            new(0.20f, 0.45f, 0.90f), // blue
            new(1.00f, 0.85f, 0.15f), // yellow
            new(0.25f, 0.75f, 0.35f), // green
            new(1.00f, 0.55f, 0.10f), // orange
            new(0.60f, 0.35f, 0.85f), // purple
            new(1.00f, 0.50f, 0.70f), // pink
            new(0.20f, 0.80f, 0.90f), // cyan
            new(0.95f, 0.95f, 0.95f), // white
            new(0.15f, 0.15f, 0.18f), // blackish
        };

        // Character proportions (metres at scale 1, total 1.8 = PaseoParkLayout.ActorHeightMeters).
        const float LegHeight = 0.75f, LegSize = 0.26f, LegOffsetX = 0.135f;
        const float TorsoHeight = 0.65f, TorsoWidth = 0.54f, TorsoDepth = 0.30f;
        const float HeadSize = 0.40f;
        const float ArmLength = 0.62f, ArmSize = 0.20f, ArmOffsetX = 0.37f, ShoulderDrop = 0.05f;

        public static PaseoBlockMaterials CreateMaterials(string folder, Shader shader)
        {
            Directory.CreateDirectory(folder);
            var materials = new PaseoBlockMaterials { Palette = new Material[PaseoParkLayout.ColorCount] };
            for (int i = 0; i < PaseoParkLayout.ColorCount; i++)
                materials.Palette[i] = CreateMaterial($"{folder}/PaseoBloque-{i}.mat", shader, PaletteColors[i]);
            materials.Skin = CreateMaterial($"{folder}/PaseoBloque-piel.mat", shader, new Color(0.96f, 0.76f, 0.58f));
            materials.Pants = CreateMaterial($"{folder}/PaseoBloque-pantalon.mat", shader, new Color(0.25f, 0.30f, 0.45f));
            materials.Face = CreateMaterial($"{folder}/PaseoBloque-cara.mat", shader, new Color(0.08f, 0.08f, 0.10f));
            materials.Trunk = CreateMaterial($"{folder}/PaseoBloque-tronco.mat", shader, new Color(0.45f, 0.30f, 0.15f));
            materials.Leaves = CreateMaterial($"{folder}/PaseoBloque-hojas.mat", shader, new Color(0.20f, 0.60f, 0.25f));
            materials.Metal = CreateMaterial($"{folder}/PaseoBloque-metal.mat", shader, new Color(0.80f, 0.85f, 0.90f));
            return materials;
        }

        static Material CreateMaterial(string path, Shader shader, Color color)
        {
            var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            // CreateAsset replaces an existing asset at the same path.
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Creates «Parque» under the box with one rig per character and one root per prop, in plan order.</summary>
        public static PaseoPark Build(Transform box, string buildingId, PaseoParkConfig config, Vector3 boxSize, PaseoBlockMaterials materials)
        {
            var plan = PaseoParkLayout.Build(config, boxSize, buildingId);
            var parkObject = new GameObject("Parque");
            parkObject.transform.SetParent(box, false);
            var park = parkObject.AddComponent<PaseoPark>();

            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            var parts = new Parts { Cube = cube, Sphere = sphere, Cylinder = cylinder, Materials = materials };

            var rigs = new List<PaseoPark.ActorRig>();
            for (int i = 0; i < plan.Actors.Count; i++)
                rigs.Add(BuildActor(parkObject.transform, i, materials.Palette[plan.Actors[i].ColorIndex % materials.Palette.Length], parts));
            var props = new List<Transform>();
            var spinners = new List<PaseoPark.Spinner>();
            for (int i = 0; i < plan.Props.Count; i++)
                props.Add(BuildProp(parkObject.transform, i, plan.Props[i].Kind, parts, spinners));

            park.Configure(buildingId, config, rigs.ToArray(), props.ToArray(), spinners.ToArray());
            park.Relayout(boxSize);
            return park;
        }

        sealed class Parts
        {
            public Mesh Cube, Sphere, Cylinder;
            public PaseoBlockMaterials Materials;
        }

        static Transform Empty(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        // One renderer: a built-in mesh with a shared material, no collider (a new GameObject has none), no shadows.
        static Transform Part(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition, Vector3 localScale,
            Vector3? eulerAngles = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            if (eulerAngles.HasValue)
                go.transform.localRotation = Quaternion.Euler(eulerAngles.Value);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return go.transform;
        }

        static PaseoPark.ActorRig BuildActor(Transform parent, int index, Material shirt, Parts p)
        {
            var m = p.Materials;
            var root = Empty(parent, $"Personaje {index + 1}", Vector3.zero);
            var body = Empty(root, "Cuerpo", Vector3.zero);
            float hipY = LegHeight;
            float neckY = LegHeight + TorsoHeight;
            Part(body, "Torso", p.Cube, shirt, new Vector3(0f, hipY + TorsoHeight / 2f, 0f), new Vector3(TorsoWidth, TorsoHeight, TorsoDepth));

            var head = Empty(body, "Cabeza", new Vector3(0f, neckY, 0f));
            Part(head, "Cráneo", p.Cube, m.Skin, new Vector3(0f, HeadSize / 2f, 0f), Vector3.one * HeadSize);
            // A dark block on the front (+Z) so the facing reads from afar.
            Part(head, "Cara", p.Cube, m.Face, new Vector3(0f, HeadSize * 0.55f, HeadSize / 2f), new Vector3(0.28f, 0.12f, 0.04f));

            float shoulderY = neckY - ShoulderDrop;
            var leftArm = Empty(body, "Brazo izquierdo", new Vector3(-ArmOffsetX, shoulderY, 0f));
            Part(leftArm, "Brazo", p.Cube, shirt, new Vector3(0f, -ArmLength / 2f, 0f), new Vector3(ArmSize, ArmLength, ArmSize));
            var rightArm = Empty(body, "Brazo derecho", new Vector3(ArmOffsetX, shoulderY, 0f));
            Part(rightArm, "Brazo", p.Cube, shirt, new Vector3(0f, -ArmLength / 2f, 0f), new Vector3(ArmSize, ArmLength, ArmSize));

            var leftLeg = Empty(body, "Pierna izquierda", new Vector3(-LegOffsetX, hipY, 0f));
            Part(leftLeg, "Pierna", p.Cube, m.Pants, new Vector3(0f, -LegHeight / 2f, 0f), new Vector3(LegSize, LegHeight, LegSize));
            var rightLeg = Empty(body, "Pierna derecha", new Vector3(LegOffsetX, hipY, 0f));
            Part(rightLeg, "Pierna", p.Cube, m.Pants, new Vector3(0f, -LegHeight / 2f, 0f), new Vector3(LegSize, LegHeight, LegSize));

            return new PaseoPark.ActorRig
            {
                root = root, body = body, head = head, leftArm = leftArm, rightArm = rightArm, leftLeg = leftLeg, rightLeg = rightLeg
            };
        }

        static Transform BuildProp(Transform parent, int index, PaseoPropKind kind, Parts p, List<PaseoPark.Spinner> spinners)
        {
            var m = p.Materials;
            var root = Empty(parent, $"{kind} {index + 1}", Vector3.zero);
            switch (kind)
            {
                case PaseoPropKind.Arbol:
                    Part(root, "Tronco", p.Cylinder, m.Trunk, new Vector3(0f, 0.7f, 0f), new Vector3(0.4f, 0.7f, 0.4f));
                    Part(root, "Copa baja", p.Cube, m.Leaves, new Vector3(0f, 2.2f, 0f), new Vector3(1.8f, 1.6f, 1.8f));
                    Part(root, "Copa alta", p.Cube, m.Leaves, new Vector3(0f, 3.4f, 0f), new Vector3(1.2f, 1.2f, 1.2f), new Vector3(0f, 45f, 0f));
                    break;
                case PaseoPropKind.Atomo:
                    Part(root, "Núcleo", p.Sphere, m.Palette[Red], Vector3.zero, Vector3.one * 0.9f);
                    var tilts = new[] { new Vector3(0f, 0f, 0f), new Vector3(60f, 60f, 0f), new Vector3(-60f, 120f, 0f) };
                    for (int k = 0; k < tilts.Length; k++)
                    {
                        var ring = Empty(root, $"Órbita {k + 1}", Vector3.zero);
                        ring.localRotation = Quaternion.Euler(tilts[k]);
                        var orbit = Empty(ring, "Giro", Vector3.zero);
                        Part(orbit, "Electrón", p.Sphere, m.Palette[Cyan], new Vector3(1.4f, 0f, 0f), Vector3.one * 0.35f);
                        spinners.Add(new PaseoPark.Spinner { part = orbit, degreesPerSecond = 120f, phaseDegrees = k * 120f });
                    }
                    break;
                case PaseoPropKind.Bandera:
                    Part(root, "Asta", p.Cylinder, m.Metal, new Vector3(0f, 2f, 0f), new Vector3(0.12f, 2f, 0.12f));
                    var wave = Empty(root, "Ondear", new Vector3(0f, 3.5f, 0f));
                    Part(wave, "Tela", p.Cube, m.Palette[Red], new Vector3(0.7f, 0f, 0f), new Vector3(1.4f, 0.9f, 0.05f));
                    spinners.Add(new PaseoPark.Spinner { part = wave, degreesPerSecond = 180f, amplitudeDegrees = 20f });
                    break;
                case PaseoPropKind.BolaDisco:
                    var spin = Empty(root, "Giro", Vector3.zero);
                    // A tilted cube reads its spin better than a sphere.
                    Part(spin, "Bola", p.Cube, m.Metal, Vector3.zero, Vector3.one * 1.1f, new Vector3(35f, 0f, 35f));
                    spinners.Add(new PaseoPark.Spinner { part = spin, degreesPerSecond = 60f });
                    break;
            }
            return root;
        }
    }
}
#endif

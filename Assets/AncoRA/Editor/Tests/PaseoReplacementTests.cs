using System.Reflection;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoReplacementTests
    {
        const string WithCube = "{\"nombre\":\"Teología\",\"descripcion\":\"Algo.\"," +
                                "\"reemplazo\":{\"activo\":true,\"escala\":4,\"posicion\":[0,1,0],\"girar\":45},\"mapas\":[]}";

        static string WithModel(string file) =>
            "{\"nombre\":\"Teología\",\"descripcion\":\"Algo.\",\"reemplazo\":{\"activo\":true,\"modelo\":\"" + file + "\"},\"mapas\":[]}";

        static PaseoValidation Validate(string json, params string[] files) =>
            PaseoConfig.Validate("Teologia", PaseoConfig.Parse(json), files, true);

        [Test]
        public void AReplacementWithoutModelIsACubeThatHidesTheBuilding()
        {
            var config = PaseoConfig.Parse(WithCube);
            Assert.IsTrue(PaseoConfig.HasReplacement(config));
            Assert.AreEqual("", config.reemplazo.modelo);
            Assert.AreEqual(4f, config.reemplazo.escala);
            Assert.AreEqual(45f, config.reemplazo.girar);
            Assert.IsTrue(config.reemplazo.ocultar, "hides the real building unless told otherwise");
            Assert.AreEqual(1f, config.reemplazo.margen);
            Assert.IsTrue(Validate(WithCube).Ok);
        }

        [Test]
        public void WithoutModelTheShapeIsACubeUnlessAnotherIsNamed()
        {
            Assert.IsTrue(PaseoConfig.TryShape(PaseoConfig.Parse(WithCube).reemplazo.forma, out var cube));
            Assert.AreEqual(PrimitiveType.Cube, cube);
            foreach (var (name, type) in new[]
                     {
                         ("cubo", PrimitiveType.Cube), ("esfera", PrimitiveType.Sphere), ("Cilindro", PrimitiveType.Cylinder),
                         ("cápsula", PrimitiveType.Capsule), ("capsula", PrimitiveType.Capsule)
                     })
            {
                Assert.IsTrue(PaseoConfig.TryShape(name, out var parsed), name);
                Assert.AreEqual(type, parsed, name);
            }
            Assert.IsTrue(Validate(WithCube.Replace("\"girar\":45", "\"girar\":45,\"forma\":\"esfera\"")).Ok);
        }

        [Test]
        public void AnUnknownShapeIsAnError()
        {
            var validation = Validate(WithCube.Replace("\"girar\":45", "\"girar\":45,\"forma\":\"pirámide\""));
            Assert.IsFalse(validation.Ok);
            Assert.IsTrue(validation.Errors.Exists(e => e.Contains("forma")));
        }

        [Test]
        public void EveryShapeStandsOnItsBase()
        {
            // Unity's cube and sphere are 1 m across around their centre; the cylinder and the capsule are 2 m tall.
            Assert.AreEqual(0.5f, PaseoReplacementLayout.ShapeBaseHeight(PrimitiveType.Cube));
            Assert.AreEqual(0.5f, PaseoReplacementLayout.ShapeBaseHeight(PrimitiveType.Sphere));
            Assert.AreEqual(1f, PaseoReplacementLayout.ShapeBaseHeight(PrimitiveType.Cylinder));
            Assert.AreEqual(1f, PaseoReplacementLayout.ShapeBaseHeight(PrimitiveType.Capsule));
        }

        [Test]
        public void WithoutReplacementTheBuildingKeepsItsBox()
        {
            var config = PaseoConfig.Parse("{\"nombre\":\"X1\",\"descripcion\":\"Algo.\",\"mapas\":[]}");
            Assert.IsFalse(PaseoConfig.HasReplacement(config));
            var validation = PaseoConfig.Validate("X1", config, new string[0], true);
            Assert.IsTrue(validation.Ok);
            Assert.IsFalse(validation.Warnings.Exists(w => w.Contains("reemplazo")));
        }

        [Test]
        public void AModelMustBeInTheBuildingFolder()
        {
            Assert.IsFalse(Validate(WithModel("cubo.fbx")).Ok);
            Assert.IsTrue(Validate(WithModel("cubo.fbx"), "cubo.fbx").Ok);
            Assert.IsTrue(Validate(WithModel("Cubo.OBJ"), "Cubo.OBJ").Ok);
            Assert.IsTrue(Validate(WithModel("cubo.prefab"), "cubo.prefab").Ok);
        }

        [Test]
        public void AModelUnityCannotImportIsAnError()
        {
            // .glb needs the glTFast package, which the project does not have.
            var validation = Validate(WithModel("cubo.glb"), "cubo.glb");
            Assert.IsFalse(validation.Ok);
            Assert.IsTrue(validation.Errors.Exists(e => e.Contains(".glb")));
        }

        [Test]
        public void ScaleMarginAndOffsetMustBeSensible()
        {
            Assert.IsFalse(Validate(WithCube.Replace("\"escala\":4", "\"escala\":0")).Ok);
            Assert.IsFalse(Validate(WithCube.Replace("\"girar\":45", "\"girar\":45,\"margen\":-1")).Ok);
            Assert.IsFalse(Validate(WithCube.Replace("[0,1,0]", "[0,1]")).Ok);
        }

        [Test]
        public void ApplyingAFieldAdjustmentKeepsTheReplacement()
        {
            var again = PaseoConfig.Parse(PaseoConfig.Serialize(PaseoConfig.Parse(WithCube)));
            Assert.IsTrue(PaseoConfig.HasReplacement(again));
            Assert.AreEqual(4f, again.reemplazo.escala);
            CollectionAssert.AreEqual(new[] { 0f, 1f, 0f }, again.reemplazo.posicion);
        }

        [Test]
        public void TheMaskCoversTheBoxAndItsMarginOnEverySide()
        {
            Assert.AreEqual(new Vector3(24.3f, 10.3f, 12.7f), PaseoReplacementLayout.MaskSize(new Vector3(22.3f, 8.3f, 10.7f), 1f));
            Assert.AreEqual(new Vector3(22.3f, 8.3f, 10.7f), PaseoReplacementLayout.MaskSize(new Vector3(22.3f, 8.3f, 10.7f), 0f));
        }

        [Test]
        public void TheModelStandsOnTheGroundUnderTheBoxCentre()
        {
            // The box pivot is its centre: the ground is half its height below.
            var position = PaseoReplacementLayout.ModelPosition(new Vector3(22.3f, 8.3f, 10.7f), new Vector3(2f, 1f, -3f));
            Assert.AreEqual(2f, position.x, 1e-5f);
            Assert.AreEqual(-4.15f + 1f, position.y, 1e-5f);
            Assert.AreEqual(-3f, position.z, 1e-5f);
        }

        [Test]
        public void TheModelTurnsAtItsSpeedFromItsYaw()
        {
            Assert.AreEqual(100f, PaseoReplacementLayout.SpinYaw(10f, 45f, 2f), 1e-4f);
            Assert.AreEqual(90f, PaseoReplacementLayout.SpinYaw(0f, 90f, 5f), 1e-4f);
            Assert.AreEqual(30f, PaseoReplacementLayout.SpinYaw(30f, 0f, 1000f), 1e-4f);
        }

        [Test]
        public void TheSlackDefaultsToThreeMetresAndCanBeZeroButNotNegative()
        {
            Assert.AreEqual(3f, PaseoConfig.Parse(WithCube).reemplazo.holgura);
            Assert.AreEqual(5.5f, PaseoConfig.Parse(WithCube.Replace("\"girar\":45", "\"girar\":45,\"holgura\":5.5")).reemplazo.holgura);
            Assert.IsTrue(Validate(WithCube.Replace("\"girar\":45", "\"girar\":45,\"holgura\":0")).Ok);
            var validation = Validate(WithCube.Replace("\"girar\":45", "\"girar\":45,\"holgura\":-1"));
            Assert.IsFalse(validation.Ok);
            Assert.IsTrue(validation.Errors.Exists(e => e.Contains("holgura")));
        }

        [Test]
        public void ApplyingAFieldAdjustmentKeepsTheSlack()
        {
            var again = PaseoConfig.Parse(PaseoConfig.Serialize(PaseoConfig.Parse(WithCube.Replace("\"girar\":45", "\"girar\":45,\"holgura\":4"))));
            Assert.AreEqual(4f, again.reemplazo.holgura);
        }

        // ---------------------------------------------------------------- the scene objects

        GameObject host;
        Material markMaterial;
        Material paintMaterial;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
                Object.DestroyImmediate(host);
            if (markMaterial != null)
                Object.DestroyImmediate(markMaterial);
            if (paintMaterial != null)
                Object.DestroyImmediate(paintMaterial);
        }

        // The box and, when asked, its replacement as PaseoSetup.Prepare builds them. Any shader will do: nothing is drawn here.
        PaseoMapContent MakeBuilding(bool withReplacement, float slack = 3f)
        {
            host = new GameObject("Edificio de prueba");
            var boxObject = new GameObject("Caja");
            boxObject.transform.SetParent(host.transform, false);
            var frame = boxObject.AddComponent<EdificioFacadeFrame>();
            frame.Configure(10f, 6f, 8f, null, null, null, false);
            var content = boxObject.AddComponent<PaseoMapContent>();
            content.Configure(1, "Prueba", "Prueba", "", frame);
            if (!withReplacement)
                return content;
            var shader = Shader.Find("Hidden/Internal-Colored");
            markMaterial = new Material(shader);
            paintMaterial = new Material(shader);
            var standIn = new GameObject("Reemplazo");
            standIn.transform.SetParent(boxObject.transform, false);
            var model = new GameObject("Modelo");
            model.transform.SetParent(standIn.transform, false);
            var replacement = standIn.AddComponent<PaseoReplacement>();
            replacement.Configure(frame, markMaterial, paintMaterial, true, 1f, slack, model.transform, "", "cubo", 4f, Vector3.zero, 0f, 0f);
            content.SetReplacement(replacement);
            return content;
        }

        [Test]
        public void TheMaskIsTwoDrawsOfOneMeshAndOnlyTheMarkSlidesTowardsTheCamera()
        {
            var content = MakeBuilding(withReplacement: true, slack: 4.5f);
            content.SetVisible(true);
            var mark = content.Replacement.transform.Find("Marca");
            var paint = content.Replacement.transform.Find("Fondo");
            Assert.IsNotNull(mark);
            Assert.IsNotNull(paint);
            Assert.IsTrue(mark.gameObject.activeSelf && paint.gameObject.activeSelf);
            Assert.AreSame(markMaterial, mark.GetComponent<MeshRenderer>().sharedMaterial);
            Assert.AreSame(paintMaterial, paint.GetComponent<MeshRenderer>().sharedMaterial);
            var mesh = mark.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh);
            Assert.AreSame(mesh, paint.GetComponent<MeshFilter>().sharedMesh, "both draws share the generated mesh");
            Assert.AreEqual(new Vector3(12f, 8f, 10f), mesh.bounds.size, "the box grown by the margin on every side");

            // The slack is per building, so it rides on the mark's renderer and the shared materials stay untouched.
            var block = new MaterialPropertyBlock();
            Assert.IsTrue(mark.GetComponent<MeshRenderer>().HasPropertyBlock());
            mark.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            Assert.AreEqual(4.5f, block.GetFloat("_Holgura"));
            Assert.IsFalse(paint.GetComponent<MeshRenderer>().HasPropertyBlock());
            Assert.AreEqual(4.5f, content.Replacement.SlackMeters);

            content.SetVisible(false);
            Assert.IsFalse(mark.gameObject.activeSelf || paint.gameObject.activeSelf, "both draws or none");
        }

        [Test]
        public void ASlackOfZeroReachesTheMarkInsteadOfTheMaterialDefault()
        {
            var content = MakeBuilding(withReplacement: true, slack: 0f);
            content.SetVisible(true);
            var renderer = content.Replacement.transform.Find("Marca").GetComponent<MeshRenderer>();
            Assert.IsTrue(renderer.HasPropertyBlock(), "the block overrides the 3 m the shared material carries");
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.AreEqual(0f, block.GetFloat("_Holgura"));
        }

        [Test]
        public void TheMaskMeshIsReleasedWithTheReplacement()
        {
            var content = MakeBuilding(withReplacement: true);
            content.SetVisible(true);
            var mesh = content.Replacement.transform.Find("Marca").GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh);
            // OnDestroy does not run in EditMode for a plain MonoBehaviour: call it as the player would.
            typeof(PaseoReplacement).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(content.Replacement, null);
            Assert.IsTrue(mesh == null, "a generated mesh has no owner: the replacement destroys it");
            Assert.IsNull(content.Replacement.transform.Find("Marca").GetComponent<MeshFilter>().sharedMesh);
            Assert.IsNull(content.Replacement.transform.Find("Fondo").GetComponent<MeshFilter>().sharedMesh);
        }

        [Test]
        public void WhileAdjustingABuildingWithAReplacementShowsItsBoxInsteadAndStaysOnScreen()
        {
            var content = MakeBuilding(withReplacement: true);
            content.SetVisible(true);
            Assert.IsFalse(content.Box.IsVisible, "the replacement takes the box's place");
            Assert.IsTrue(content.Replacement.IsShown);

            content.SetAdjusting(true);
            Assert.IsTrue(content.IsAdjusting);
            Assert.IsTrue(content.Box.IsVisible, "the box can be aligned against the real building");
            Assert.IsFalse(content.Replacement.IsShown);
            Assert.IsFalse(content.Replacement.Model.gameObject.activeSelf);
            Assert.IsFalse(content.Replacement.transform.Find("Marca").gameObject.activeSelf);
            Assert.IsFalse(content.Replacement.transform.Find("Fondo").gameObject.activeSelf);
            Assert.IsTrue(content.IsShown, "the building is still on screen: the card and the name follow it");

            content.SetAdjusting(false);
            Assert.IsFalse(content.Box.IsVisible);
            Assert.IsTrue(content.Replacement.IsShown);
            Assert.IsTrue(content.Replacement.Model.gameObject.activeSelf);
            Assert.IsTrue(content.Replacement.transform.Find("Fondo").gameObject.activeSelf);
        }

        [Test]
        public void ABuildingLocatedWhileAdjustingAppearsAsItsBox()
        {
            var content = MakeBuilding(withReplacement: true);
            content.SetAdjusting(true);
            Assert.IsFalse(content.IsShown);
            Assert.IsFalse(content.Box.IsVisible, "nothing to show before the tour locates it");

            content.SetVisible(true);
            Assert.IsTrue(content.Box.IsVisible);
            Assert.IsFalse(content.Replacement.IsShown);

            content.SetVisible(false);
            Assert.IsFalse(content.Box.IsVisible);
            Assert.IsFalse(content.IsShown);
        }

        [Test]
        public void AdjustingChangesNothingForABuildingWithoutReplacement()
        {
            var content = MakeBuilding(withReplacement: false);
            content.SetVisible(true);
            Assert.IsTrue(content.Box.IsVisible);
            content.SetAdjusting(true);
            Assert.IsTrue(content.Box.IsVisible);
            content.SetAdjusting(false);
            Assert.IsTrue(content.Box.IsVisible);
            content.SetVisible(false);
            Assert.IsFalse(content.Box.IsVisible);
        }

        // ---------------------------------------------------------------- the view modes

        // What is on screen: the yellow box, the two draws of the mask, the model.
        static void AssertDrawn(PaseoMapContent content, bool box, bool mask, bool model, string when)
        {
            var standIn = content.Replacement.transform;
            Assert.AreEqual(box, content.Box.IsVisible, "box " + when);
            Assert.AreEqual(mask, standIn.Find("Marca").gameObject.activeSelf, "mark " + when);
            Assert.AreEqual(mask, standIn.Find("Fondo").gameObject.activeSelf, "paint " + when);
            Assert.AreEqual(model, content.Replacement.Model.gameObject.activeSelf, "model " + when);
        }

        [Test]
        public void EachViewModeDrawsItsOwnPartsOfABuildingWithAReplacement()
        {
            var content = MakeBuilding(withReplacement: true);
            content.SetVisible(true);
            AssertDrawn(content, box: false, mask: true, model: true, "a content draws its finished building by itself");

            content.SetViewMode(PaseoViewMode.Box);
            AssertDrawn(content, box: true, mask: false, model: false, "in Caja");
            content.SetViewMode(PaseoViewMode.Background);
            AssertDrawn(content, box: false, mask: true, model: false, "in Fondo");
            content.SetViewMode(PaseoViewMode.Object);
            AssertDrawn(content, box: false, mask: true, model: true, "in Objeto");
            Assert.IsTrue(content.IsShown, "the mode changes what is drawn, not whether the building is on screen");
        }

        [Test]
        public void AdjustingShowsTheBoxInEveryModeAndTheModeComesBack()
        {
            var content = MakeBuilding(withReplacement: true);
            content.SetVisible(true);
            foreach (var mode in new[] { PaseoViewMode.Box, PaseoViewMode.Background, PaseoViewMode.Object })
            {
                content.SetViewMode(mode);
                var drawn = PaseoViewModes.PartsFor(mode, true, false, true);
                AssertDrawn(content, drawn.Box, drawn.Mask, drawn.Model, "in " + mode);

                content.SetAdjusting(true);
                AssertDrawn(content, box: true, mask: false, model: false, $"adjusting in {mode}");
                Assert.IsTrue(content.IsShown);

                content.SetAdjusting(false);
                AssertDrawn(content, drawn.Box, drawn.Mask, drawn.Model, $"after adjusting in {mode}");
            }
        }

        [Test]
        public void TheViewModeOutlastsVisibilityAndTheMaskAloneNeedsNoModel()
        {
            var content = MakeBuilding(withReplacement: true);
            content.SetViewMode(PaseoViewMode.Background);
            Assert.IsFalse(content.Box.IsVisible, "off screen nothing is drawn, whatever the mode");
            Assert.IsFalse(content.Replacement.IsShown);
            Assert.IsFalse(content.Replacement.Model.gameObject.activeSelf);

            content.SetVisible(true);
            AssertDrawn(content, box: false, mask: true, model: false, "a building located in Fondo appears as Fondo");
            Assert.IsNotNull(content.Replacement.transform.Find("Marca").GetComponent<MeshFilter>().sharedMesh, "the mask alone builds its mesh");

            content.SetVisible(false);
            Assert.IsFalse(content.Replacement.IsShown);
            Assert.IsFalse(content.Box.IsVisible);
            content.SetVisible(true);
            AssertDrawn(content, box: false, mask: true, model: false, "still Fondo after losing and finding the building");
        }

        [Test]
        public void ChangingTheViewModeToTheSameOneDoesNothing()
        {
            var content = MakeBuilding(withReplacement: true);
            content.SetVisible(true);
            content.SetViewMode(PaseoViewMode.Background);
            content.Box.SetVisible(true); // a change from outside that Apply would undo if it ran again
            content.SetViewMode(PaseoViewMode.Background);
            Assert.IsTrue(content.Box.IsVisible, "a repeated mode is not applied again");
            Assert.AreEqual(PaseoViewMode.Background, content.ViewMode);
        }

        [Test]
        public void ABuildingWithoutReplacementShowsItsBoxInEveryMode()
        {
            var content = MakeBuilding(withReplacement: false);
            content.SetVisible(true);
            foreach (var mode in new[] { PaseoViewMode.Box, PaseoViewMode.Background, PaseoViewMode.Object })
            {
                content.SetViewMode(mode);
                Assert.IsTrue(content.Box.IsVisible, "box in " + mode);
                Assert.IsTrue(content.IsShown);
            }
            content.SetVisible(false);
            Assert.IsFalse(content.Box.IsVisible);
        }

        [Test]
        public void TheMaskAndTheModelShowIndependently()
        {
            var content = MakeBuilding(withReplacement: true);
            var replacement = content.Replacement;
            var standIn = replacement.transform;

            replacement.SetParts(showMask: true, showModel: false);
            Assert.IsTrue(replacement.IsShown && replacement.MaskShown && !replacement.ModelShown);
            Assert.IsTrue(standIn.Find("Marca").gameObject.activeSelf && standIn.Find("Fondo").gameObject.activeSelf);
            Assert.IsFalse(replacement.Model.gameObject.activeSelf);

            replacement.SetParts(showMask: false, showModel: true);
            Assert.IsTrue(replacement.IsShown && !replacement.MaskShown && replacement.ModelShown);
            Assert.IsFalse(standIn.Find("Marca").gameObject.activeSelf || standIn.Find("Fondo").gameObject.activeSelf);
            Assert.IsTrue(replacement.Model.gameObject.activeSelf);

            replacement.SetParts(showMask: false, showModel: false);
            Assert.IsFalse(replacement.IsShown);
            Assert.IsFalse(replacement.Model.gameObject.activeSelf);

            replacement.SetVisible(true);
            Assert.IsTrue(replacement.MaskShown && replacement.ModelShown, "SetVisible still shows the whole replacement");
            replacement.SetVisible(false);
            Assert.IsFalse(replacement.MaskShown || replacement.ModelShown);
        }
    }
}

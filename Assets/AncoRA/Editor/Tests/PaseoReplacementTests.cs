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
    }
}

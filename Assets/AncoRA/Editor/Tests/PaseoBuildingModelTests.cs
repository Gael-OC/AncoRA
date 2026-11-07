using System.Linq;
using AncorRA.AR;
using AncorRA.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoBuildingModelTests
    {
        [TestCase("academico", 19.9f, 7.4f, 10.7f)]
        [TestCase("pabellon", 27.2f, 4.7f, 9.4f)]
        [TestCase("clasico", 22.3f, 8.3f, 10.7f)]
        public void DemoMeshesCoverTheMeasuredBoxAndStaySmall(string style, float w, float h, float d)
        {
            var mesh = PaseoBuildingModelBuilder.CreateMesh(style, new Vector3(w, h, d));
            try
            {
                var bounds = mesh.bounds;
                Assert.LessOrEqual(bounds.min.y, -0.5f);
                Assert.Greater(bounds.max.y, 0.5f, "roof covers the original top");
                Assert.Less(bounds.min.x, -0.5f);
                Assert.Greater(bounds.max.x, 0.5f);
                Assert.Less(bounds.min.z, -0.5f);
                Assert.Greater(bounds.max.z, 0.5f);
                Assert.Less(mesh.triangles.Length / 3, 3000);
                Assert.AreEqual(5, mesh.subMeshCount);
                Assert.IsTrue(mesh.normals.All(n => n.sqrMagnitude > 0.9f));
                // The first primitive is the closed shell; its six faces must point outwards for back-face culling.
                var vertices = mesh.vertices;
                for (int i = 0; i < 24; i++)
                    Assert.Greater(Vector3.Dot(vertices[i], mesh.normals[i]), 0f);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ModelFollowsVisibilityAndSizeIndependentlyOfTheParkSwitch()
        {
            var host = new GameObject("Caja de prueba");
            try
            {
                var frame = host.AddComponent<EdificioFacadeFrame>();
                frame.Configure(20f, 8f, 12f, null, null, null, false);
                var content = host.AddComponent<PaseoMapContent>();
                content.Configure(new[] { 152259 }, 0, "Teologia", "Teología", "", frame);
                var model = new GameObject("Edificio").transform;
                model.SetParent(host.transform, false);
                content.SetBuildingModel(model, "clasico");
                Assert.IsFalse(model.gameObject.activeSelf, "not drawn before localization");
                content.SetVisible(true);
                Assert.IsTrue(model.gameObject.activeSelf);
                Assert.IsFalse(frame.IsVisible, "no diagnostic fill or edges over the model");
                content.SetParkEnabled(false);
                Assert.IsTrue(model.gameObject.activeSelf);
                content.SetSize(new Vector3(25f, 10f, 15f), false);
                Assert.AreEqual(content.SizeMeters, model.localScale);
                content.SetVisible(false);
                Assert.IsFalse(model.gameObject.activeSelf, "tracking loss hides the model too");
                content.SetBoxHidden(true);
                content.SetVisible(true);
                Assert.IsFalse(model.gameObject.activeSelf, "oculta still takes precedence");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void ConfigKeepsTheModelAndRejectsUnknownStyles()
        {
            const string json = "{\"nombre\":\"Teología\",\"modelo\":\"clasico\",\"mapas\":[]}";
            var config = PaseoConfig.Parse(json);
            Assert.AreEqual("clasico", PaseoConfig.Parse(PaseoConfig.Serialize(config)).modelo);
            Assert.IsTrue(PaseoConfig.Validate("Teologia", config, new string[0], false).Ok);
            config.modelo = "desconocido";
            Assert.IsTrue(PaseoConfig.Validate("Teologia", config, new string[0], false).Errors.Any(e => e.Contains("modelo")));
        }
    }
}

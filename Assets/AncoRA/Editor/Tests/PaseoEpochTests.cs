using System.Linq;
using AncorRA.AR;
using AncorRA.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoEpochTests
    {
        [TestCase(PaseoEpoch.Y1950, 19.9f, 7.4f, 10.7f)]
        [TestCase(PaseoEpoch.Y1950, 27.2f, 4.7f, 9.4f)]
        [TestCase(PaseoEpoch.Y1950, 22.3f, 8.3f, 10.7f)]
        [TestCase(PaseoEpoch.Y1980, 19.9f, 7.4f, 10.7f)]
        [TestCase(PaseoEpoch.Y1980, 27.2f, 4.7f, 9.4f)]
        [TestCase(PaseoEpoch.Y1980, 22.3f, 8.3f, 10.7f)]
        public void EraMeshesCoverTheMeasuredBoxAndStaySmall(PaseoEpoch epoch, float w, float h, float d)
        {
            var mesh = PaseoBuildingModelBuilder.CreateEpochMesh(epoch, new Vector3(w, h, d));
            try
            {
                var bounds = mesh.bounds;
                Assert.LessOrEqual(bounds.min.y, -0.5f + 1e-4f, "down to the ground");
                Assert.Greater(bounds.max.y, 0.5f, "roof covers the original top");
                Assert.Less(bounds.min.x, -0.5f);
                Assert.Greater(bounds.max.x, 0.5f);
                Assert.Less(bounds.min.z, -0.5f);
                Assert.Greater(bounds.max.z, 0.5f);
                // Nothing reaches far outside the box: the model must not cover the neighbours.
                Assert.Less(bounds.size.x * w, w + 3f);
                Assert.Less(bounds.size.z * d, d + 6f);
                Assert.Less(mesh.triangles.Length / 3, 3000);
                Assert.AreEqual(5, mesh.subMeshCount);
                Assert.IsTrue(mesh.normals.All(n => n.sqrMagnitude > 0.9f));
                var vertices = mesh.vertices;
                for (int i = 0; i < 24; i++)
                    Assert.Greater(Vector3.Dot(vertices[i], mesh.normals[i]), 0f, "closed shell faces outwards");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void TodayAndTwoThousandHaveNoOwnMesh()
        {
            Assert.Throws<System.ArgumentException>(() => PaseoBuildingModelBuilder.CreateEpochMesh(PaseoEpoch.Y2000, Vector3.one * 10f));
            Assert.Throws<System.ArgumentException>(() => PaseoBuildingModelBuilder.CreateEpochMesh(PaseoEpoch.Actualidad, Vector3.one * 10f));
        }

        [Test]
        public void ChangeTakesTheOldModelDownThenBuildsTheNewOne()
        {
            var start = PaseoEpochs.Evaluate(0f, hasOutgoing: true);
            Assert.AreEqual(1f, start.OutReveal, 1e-5f);
            Assert.AreEqual(0f, start.InReveal, 1e-5f);
            Assert.AreEqual(0f, start.FilterBlend, 1e-5f);
            var gap = PaseoEpochs.Evaluate(PaseoEpochs.OutSeconds + PaseoEpochs.GapSeconds * 0.5f, hasOutgoing: true);
            Assert.AreEqual(0f, gap.OutReveal, 1e-5f, "the old model is gone before the new one starts");
            Assert.AreEqual(0f, gap.InReveal, 1e-5f);
            Assert.IsFalse(gap.Done);
            var end = PaseoEpochs.Evaluate(PaseoEpochs.TotalSeconds, hasOutgoing: true);
            Assert.AreEqual(1f, end.InReveal, 1e-5f);
            Assert.AreEqual(1f, end.FilterBlend, 1e-5f);
            Assert.IsTrue(end.Done);
            // From «Actualidad» there is nothing to take down: the new model rises at once.
            var fromToday = PaseoEpochs.Evaluate(PaseoEpochs.InSeconds * 0.5f, hasOutgoing: false);
            Assert.Greater(fromToday.InReveal, 0.4f);
            Assert.IsTrue(PaseoEpochs.Evaluate(PaseoEpochs.InSeconds, hasOutgoing: false).Done);
            float previous = -1f;
            for (float t = 0f; t <= PaseoEpochs.TotalSeconds; t += 0.05f)
            {
                float blend = PaseoEpochs.Evaluate(t, hasOutgoing: true).FilterBlend;
                Assert.GreaterOrEqual(blend, previous, "the filter only moves forward");
                previous = blend;
            }
        }

        [Test]
        public void KeysRoundTripAndUnknownFallsBack()
        {
            foreach (var epoch in PaseoEpochs.All)
                Assert.AreEqual(epoch, PaseoEpochs.Parse(PaseoEpochs.Key(epoch)));
            Assert.AreEqual(PaseoEpochs.Default, PaseoEpochs.Parse("1920"));
            Assert.AreEqual(PaseoEpochs.Default, PaseoEpochs.Parse(null));
        }

        [Test]
        public void OnlyTodayLooksLikeTheCamera()
        {
            Assert.IsTrue(PaseoEpochs.Look(PaseoEpoch.Actualidad).IsNeutral);
            Assert.IsFalse(PaseoEpochs.Look(PaseoEpoch.Y1950).IsNeutral);
            Assert.IsFalse(PaseoEpochs.Look(PaseoEpoch.Y1980).IsNeutral);
            Assert.IsFalse(PaseoEpochs.Look(PaseoEpoch.Y2000).IsNeutral);
            // The oldest era wears the film the most.
            Assert.Greater(PaseoEpochs.Look(PaseoEpoch.Y1950).Scratches, PaseoEpochs.Look(PaseoEpoch.Y1980).Scratches);
            Assert.Greater(PaseoEpochs.Look(PaseoEpoch.Y1950).Grain, PaseoEpochs.Look(PaseoEpoch.Y2000).Grain);
        }

        [Test]
        public void EachEraShowsItsModelAndTodayShowsNone()
        {
            var host = new GameObject("Caja de prueba");
            try
            {
                var frame = host.AddComponent<EdificioFacadeFrame>();
                frame.Configure(20f, 8f, 12f, null, null, null, false);
                var content = host.AddComponent<PaseoMapContent>();
                content.Configure(new[] { 152259 }, 0, "Teologia", "Teología", "", frame);
                Transform Model(string name)
                {
                    var model = new GameObject(name).transform;
                    model.SetParent(host.transform, false);
                    return model;
                }
                var demo = Model("2000");
                var old = Model("1950");
                var concrete = Model("1980");
                content.SetBuildingModel(demo, "clasico");
                content.SetEpochModel(PaseoEpoch.Y1950, old);
                content.SetEpochModel(PaseoEpoch.Y1980, concrete);
                Assert.AreEqual(content.SizeMeters, old.localScale);
                content.SetVisible(true);
                Assert.IsTrue(demo.gameObject.activeSelf && !old.gameObject.activeSelf && !concrete.gameObject.activeSelf);

                content.SetEpoch(PaseoEpoch.Y1950, animate: false);
                Assert.IsTrue(old.gameObject.activeSelf && !demo.gameObject.activeSelf && !concrete.gameObject.activeSelf);
                content.SetEpoch(PaseoEpoch.Actualidad, animate: false);
                Assert.IsFalse(old.gameObject.activeSelf || demo.gameObject.activeSelf || concrete.gameObject.activeSelf,
                    "today the camera shows the real building");
                Assert.IsFalse(frame.IsVisible, "and no diagnostic box either");

                // Animated: both models stay on until the change is over.
                content.SetEpoch(PaseoEpoch.Y1980, animate: false);
                content.SetEpoch(PaseoEpoch.Y1950, animate: true);
                Assert.IsTrue(content.EpochChanging);
                Assert.IsTrue(old.gameObject.activeSelf && concrete.gameObject.activeSelf);

                content.SetSize(new Vector3(25f, 10f, 15f), false);
                Assert.AreEqual(content.SizeMeters, concrete.localScale);
                Assert.AreEqual(content.SizeMeters, demo.localScale);

                content.SetVisible(false);
                Assert.IsFalse(old.gameObject.activeSelf || concrete.gameObject.activeSelf, "tracking loss hides every model");
                Assert.Throws<System.ArgumentException>(() => content.SetEpochModel(PaseoEpoch.Y2000, demo));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void HiddenBuildingChangesEraWithoutAnimation()
        {
            var host = new GameObject("Caja de prueba");
            try
            {
                var frame = host.AddComponent<EdificioFacadeFrame>();
                frame.Configure(20f, 8f, 12f, null, null, null, false);
                var content = host.AddComponent<PaseoMapContent>();
                content.Configure(new[] { 152259 }, 0, "Teologia", "Teología", "", frame);
                var demo = new GameObject("2000").transform;
                demo.SetParent(host.transform, false);
                content.SetBuildingModel(demo, "clasico");
                content.SetEpoch(PaseoEpoch.Actualidad, animate: true);
                Assert.IsFalse(content.EpochChanging, "nothing to animate while the building is not located");
                Assert.AreEqual(PaseoEpoch.Actualidad, content.Epoch);
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}

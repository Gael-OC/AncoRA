using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoHiddenBoxTests
    {
        GameObject boxObject;
        PaseoMapContent content;
        MeshRenderer renderer;

        [SetUp]
        public void SetUp()
        {
            boxObject = new GameObject("Caja");
            var frame = boxObject.AddComponent<EdificioFacadeFrame>();
            frame.Configure(4f, 3f, 4f, null, null, null, false);
            content = boxObject.AddComponent<PaseoMapContent>();
            content.Configure(new[] { 1 }, 1, "EIC", "EIC", "", frame);
            renderer = boxObject.GetComponent<MeshRenderer>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(boxObject);

        [Test]
        public void ALocatedBuildingIsDrawnByDefault()
        {
            content.SetVisible(true);
            Assert.IsTrue(content.IsShown);
            Assert.IsTrue(content.IsDrawn);
            Assert.IsTrue(renderer.enabled);
        }

        [Test]
        public void AHiddenBoxStaysLocatedButIsNotDrawn()
        {
            content.SetBoxHidden(true);
            content.SetVisible(true);
            Assert.IsTrue(content.IsShown, "the tour still counts the building as located");
            Assert.IsFalse(content.IsDrawn);
            Assert.IsFalse(renderer.enabled);
        }
    }
}

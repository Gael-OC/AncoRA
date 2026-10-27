using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoRecordSelectionTests
    {
        [Test]
        public void ThePointNameIsBuildingPointAndDistance()
        {
            Assert.AreEqual("X1-C-30m", PaseoRecordSelection.Name("X1", "C", 30));
        }

        [Test]
        public void EachPairOfPointsHasItsUsualDistance()
        {
            Assert.AreEqual(15, PaseoRecordSelection.UsualDistance("A"));
            Assert.AreEqual(15, PaseoRecordSelection.UsualDistance("B"));
            Assert.AreEqual(30, PaseoRecordSelection.UsualDistance("C"));
            Assert.AreEqual(30, PaseoRecordSelection.UsualDistance("D"));
            Assert.AreEqual(50, PaseoRecordSelection.UsualDistance("E"));
            Assert.AreEqual(50, PaseoRecordSelection.UsualDistance("F"));
            Assert.AreEqual(0, PaseoRecordSelection.UsualDistance("Z"));
        }

        [Test]
        public void NothingIsChosenAtFirstAndTheLabelIsEmpty()
        {
            var selection = new PaseoRecordSelection();
            Assert.IsFalse(selection.Complete);
            Assert.AreEqual("", selection.Label);
            Assert.AreEqual("Sin punto elegido", selection.Describe());
        }

        [Test]
        public void ThePointNeedsBuildingPointDistanceAndLight()
        {
            var selection = new PaseoRecordSelection();
            selection.SetBuilding("X1");
            selection.SetPoint("C");
            Assert.AreEqual("X1-C-30m", selection.Label);
            Assert.IsFalse(selection.Complete, "the light is missing");
            Assert.AreEqual("X1-C-30m", selection.Describe());
            selection.SetLight("sol");
            Assert.IsTrue(selection.Complete);
            Assert.AreEqual("X1-C-30m · sol", selection.Describe());
        }

        [Test]
        public void PickingAPointFillsTheDistanceAndThePickedDistanceWins()
        {
            var selection = new PaseoRecordSelection();
            selection.SetPoint("E");
            Assert.AreEqual(50, selection.Meters);
            selection.SetMeters(30);
            Assert.AreEqual(30, selection.Meters);
            selection.SetPoint("A");
            Assert.AreEqual(15, selection.Meters);
        }

        [Test]
        public void EveryChangeRaisesTheVersionSoTheRecorderSeesANewPoint()
        {
            var selection = new PaseoRecordSelection();
            int before = selection.Version;
            selection.SetLight("nublado");
            Assert.Greater(selection.Version, before);
        }

        [Test]
        public void BuildingsAreWrittenWithTheirShortCode()
        {
            Assert.AreEqual("CB", PaseoRecordSelection.BuildingCode("CienciasBasicas"));
            Assert.AreEqual("TEO", PaseoRecordSelection.BuildingCode("Teologia"));
            Assert.AreEqual("X1", PaseoRecordSelection.BuildingCode("X1"));
            Assert.AreEqual("EIC", PaseoRecordSelection.BuildingCode("EIC"));
            Assert.AreEqual("", PaseoRecordSelection.BuildingCode(null));
        }
    }
}

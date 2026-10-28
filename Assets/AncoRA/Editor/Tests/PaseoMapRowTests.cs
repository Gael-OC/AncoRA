using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoMapRowTests
    {
        static PaseoMapRowInputs Healthy() => new()
        {
            MapId = 152259, BuildingName = "Teología", Loaded = true, HasFilter = true, Placed = true
        };

        [Test]
        public void AMapThatDidNotLoadIsAnError()
        {
            var inputs = Healthy();
            inputs.Loaded = false;
            inputs.Failed = true;
            var row = PaseoMapRow.Build(inputs);
            Assert.AreEqual(PaseoTone.Error, row.Tone);
            StringAssert.Contains("NO CARGÓ", row.Detail);
        }

        [Test]
        public void AShownMapReadsItsLastLocalizationWithADecimalComma()
        {
            var inputs = Healthy();
            inputs.Located = true;
            inputs.Shown = true;
            inputs.Last = new PaseoPoseReport(PaseoPoseVerdict.Accepted, 38f, 0.6f, 0.23f, 18f);
            inputs.Used = 1;
            inputs.Seen = 2;
            var row = PaseoMapRow.Build(inputs);
            Assert.AreEqual("152259 Teología", row.Title);
            Assert.AreEqual("visible · aceptada, conf 38, movería 0,23 m · 1/2 usadas", row.Detail);
            Assert.AreEqual(PaseoTone.Ok, row.Tone);
        }

        [Test]
        public void BeforeTheFirstLockThereIsNoMove()
        {
            var inputs = Healthy();
            inputs.Last = new PaseoPoseReport(PaseoPoseVerdict.Tilted, 12f, 6.4f, float.NaN, 25f);
            inputs.Seen = 1;
            Assert.AreEqual("sin ubicar · inclinada, conf 12 · 0/1 usadas", PaseoMapRow.Build(inputs).Detail);
        }

        [Test]
        public void AHeldLocalizationReadsAsPending()
        {
            var inputs = Healthy();
            inputs.Last = new PaseoPoseReport(PaseoPoseVerdict.Pending, 14f, 0.6f, float.NaN, 25f);
            inputs.Seen = 1;
            Assert.AreEqual("sin ubicar · pendiente, conf 14 · 0/1 usadas", PaseoMapRow.Build(inputs).Detail);
        }

        [Test]
        public void AMapNotLocatedYetIsNeutral()
        {
            var row = PaseoMapRow.Build(Healthy());
            Assert.AreEqual("sin ubicar", row.Detail);
            Assert.AreEqual(PaseoTone.Neutral, row.Tone);
        }

        [Test]
        public void AnUnplacedBoxIsAWarning()
        {
            var inputs = Healthy();
            inputs.Placed = false;
            var row = PaseoMapRow.Build(inputs);
            Assert.AreEqual(PaseoTone.Warning, row.Tone);
            StringAssert.Contains("CAJA SIN COLOCAR", row.Detail);
        }
    }
}

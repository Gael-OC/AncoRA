using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoActivityTests
    {
        // Searching Teología, nothing tried yet.
        static PaseoActivityInputs Searching() => new()
        {
            Active = true,
            SearchingFor = "Teología",
            SecondsSinceLast = 99f
        };

        static PaseoActivityInputs Matched(PaseoPoseReport? report)
        {
            var s = Searching();
            s.LastMatched = true;
            s.MatchedBuilding = "Teología";
            s.MatchReport = report;
            s.SecondsSinceLast = 1f;
            return s;
        }

        static PaseoPoseReport Report(PaseoPoseVerdict verdict, float confidence = 14f, float tilt = 0.8f, float move = 0.3f) =>
            new(verdict, confidence, tilt, move, 18f);

        [Test]
        public void NothingShowsBeforeTheSdkAndTrackingAreUp()
        {
            var s = Searching();
            s.Active = false;
            Assert.AreEqual(PaseoActivityTone.None, PaseoActivity.Describe(s).Tone);
        }

        [Test]
        public void BeforeAnyImageItSaysWhatItLooksFor()
        {
            var line = PaseoActivity.Describe(Searching());
            Assert.AreEqual("Buscando Teología…", line.Text);
            Assert.AreEqual(PaseoActivityTone.Searching, line.Tone);
        }

        [Test]
        public void WithoutABuildingItSearchesThemAll()
        {
            var s = Searching();
            s.SearchingFor = null;
            Assert.AreEqual("Buscando en todos los edificios…", PaseoActivity.Describe(s).Text);
        }

        [Test]
        public void ItCountsTheImagesWithoutAMatch()
        {
            // Field test 14-oct: the team tried Teología several times and could not tell whether anything was happening.
            var s = Searching();
            s.FailuresInARow = 1;
            Assert.AreEqual("Buscando Teología · 1 imagen sin coincidencia", PaseoActivity.Describe(s).Text);
            s.FailuresInARow = 7;
            Assert.AreEqual("Buscando Teología · 7 imágenes sin coincidencia", PaseoActivity.Describe(s).Text);
        }

        [Test]
        public void AMatchTheFilterUsedIsAnnouncedWithItsConfidence()
        {
            var line = PaseoActivity.Describe(Matched(Report(PaseoPoseVerdict.Locked)));
            Assert.AreEqual("Coincidencia con Teología · confianza 14", line.Text);
            Assert.AreEqual(PaseoActivityTone.Match, line.Tone);
        }

        [Test]
        public void AMatchNotYetJudgedIsStillAMatch()
        {
            var line = PaseoActivity.Describe(Matched(null));
            Assert.AreEqual("Coincidencia con Teología", line.Text);
            Assert.AreEqual(PaseoActivityTone.Match, line.Tone);
        }

        [Test]
        public void ATiltedMatchIsAnnouncedAsDiscarded()
        {
            var line = PaseoActivity.Describe(Matched(Report(PaseoPoseVerdict.Tilted, tilt: 7.4f)));
            Assert.AreEqual("Coincidencia con Teología descartada: imagen inclinada 7°", line.Text);
            Assert.AreEqual(PaseoActivityTone.Discarded, line.Tone);
        }

        [Test]
        public void AMatchFarFromTheBoxIsAnnouncedAsDiscarded()
        {
            var line = PaseoActivity.Describe(Matched(Report(PaseoPoseVerdict.Rejected, move: 2.34f)));
            Assert.AreEqual("Coincidencia con Teología descartada: movería la caja 2,3 m", line.Text);
            Assert.AreEqual(PaseoActivityTone.Discarded, line.Tone);
        }

        [Test]
        public void TheMatchMessageFadesBackToSearching()
        {
            var s = Matched(Report(PaseoPoseVerdict.Accepted));
            s.SecondsSinceLast = PaseoActivity.MatchSeconds + 0.1f;
            Assert.AreEqual(PaseoActivityTone.Searching, PaseoActivity.Describe(s).Tone);
        }
    }
}

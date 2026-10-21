using System.Linq;
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoSearchPlanTests
    {
        // Box centres measured from the drone clouds: Teología is 21 m from X1, 28 m from Ciencias Básicas and 32 m from EIC.
        static readonly PaseoSearchBuilding CienciasBasicas = new("CienciasBasicas", "Ciencias Básicas", new[] { 152192, 152196, 152257 }, -29.963828, -71.349024);
        static readonly PaseoSearchBuilding Eic = new("EIC", "EIC", new[] { 152242 }, -29.964325, -71.349254);
        static readonly PaseoSearchBuilding Teologia = new("Teologia", "Teología", new[] { 152259 }, -29.964054, -71.349144);
        static readonly PaseoSearchBuilding X1 = new("X1", "X1", new[] { 152198, 152200 }, -29.963987, -71.349344);
        static readonly PaseoSearchBuilding[] Campus = { CienciasBasicas, Eic, Teologia, X1 };

        static PaseoGpsFix At(PaseoSearchBuilding building) => PaseoGpsFix.From(true, building.Latitude, building.Longitude, 6f, 1.0);

        // The building each of the next attempts searches; null means every map.
        static string[] Next(PaseoSearchPlan plan, int count, PaseoGpsFix fix, string inView = null) =>
            Enumerable.Range(0, count).Select(_ => plan.Next(fix, inView).BuildingId).ToArray();

        [Test]
        public void DistancesBetweenBuildingsAreInMetres()
        {
            Assert.AreEqual(21.0, PaseoGeo.DistanceMeters(Teologia.Latitude, Teologia.Longitude, X1.Latitude, X1.Longitude), 1.0);
            Assert.AreEqual(60.0, PaseoGeo.DistanceMeters(CienciasBasicas.Latitude, CienciasBasicas.Longitude, Eic.Latitude, Eic.Longitude), 1.0);
        }

        [Test]
        public void WithoutGpsEveryAttemptSearchesEveryMap()
        {
            var plan = new PaseoSearchPlan(Campus);
            var search = plan.Next(PaseoGpsFix.None, null);
            Assert.IsNull(search.BuildingId);
            Assert.AreEqual(PaseoSearchReason.All, search.Reason);
            CollectionAssert.AreEquivalent(new[] { 152192, 152196, 152257, 152242, 152259, 152198, 152200 }, search.MapIds);
            Assert.IsNull(plan.Next(PaseoGpsFix.None, null).BuildingId);
        }

        [Test]
        public void EveryMapIsTheSearchOverTheWholeTourAndLeavesTheCycleAlone()
        {
            // The team's detection test «todos los mapas» (nicowo) compares it with the search by building on the spot.
            var plan = new PaseoSearchPlan(Campus);
            var everything = plan.EveryMap;
            Assert.IsNull(everything.BuildingId);
            Assert.AreEqual(PaseoSearchReason.All, everything.Reason);
            CollectionAssert.AreEquivalent(new[] { 152192, 152196, 152257, 152242, 152259, 152198, 152200 }, everything.MapIds);
            Assert.AreEqual("Teologia", plan.Next(At(Teologia), null).BuildingId);
        }

        [Test]
        public void WithGpsTheNearestBuildingGetsHalfTheAttemptsAndEverySixthSearchesEverything()
        {
            // Field test 14-oct: a try against all seven maps took 9.5 s on average when nothing matched.
            var plan = new PaseoSearchPlan(Campus);
            CollectionAssert.AreEqual(new[] { "Teologia", "X1", "Teologia", "CienciasBasicas", "Teologia", null, "Teologia" },
                Next(plan, 7, At(Teologia)));
        }

        [Test]
        public void ASearchForOneBuildingListsOnlyItsMaps()
        {
            var search = new PaseoSearchPlan(Campus).Next(At(CienciasBasicas), null);
            Assert.AreEqual("CienciasBasicas", search.BuildingId);
            Assert.AreEqual("Ciencias Básicas", search.BuildingName);
            Assert.AreEqual(PaseoSearchReason.Gps, search.Reason);
            CollectionAssert.AreEqual(new[] { 152192, 152196, 152257 }, search.MapIds);
            Assert.AreEqual(0.0, search.GpsDistanceMeters, 0.5);
        }

        [Test]
        public void ABuildingAlreadyInViewIsSearchedFirst()
        {
            var plan = new PaseoSearchPlan(Campus);
            var first = plan.Next(At(Teologia), "X1");
            Assert.AreEqual("X1", first.BuildingId);
            Assert.AreEqual(PaseoSearchReason.InView, first.Reason);
            CollectionAssert.AreEqual(new[] { "Teologia", "X1", "CienciasBasicas" }, Next(plan, 3, At(Teologia), "X1"));
        }

        [Test]
        public void InViewWithoutGpsTheOthersKeepTheirOrder()
        {
            var plan = new PaseoSearchPlan(Campus);
            CollectionAssert.AreEqual(new[] { "EIC", "CienciasBasicas", "EIC", "Teologia", "EIC", null }, Next(plan, 6, PaseoGpsFix.None, "EIC"));
        }

        [Test]
        public void BuildingsWithoutGpsComeAfterTheRankedOnes()
        {
            var unknown = new PaseoSearchBuilding("Nuevo", "Nuevo", new[] { 1 });
            var plan = new PaseoSearchPlan(new[] { unknown, Teologia, X1 });
            CollectionAssert.AreEqual(new[] { "Teologia", "X1", "Teologia", "Nuevo" }, Next(plan, 4, At(Teologia)));
        }

        [Test]
        public void WithFewerBuildingsTheMissingRanksFallBackToTheNearest()
        {
            var plan = new PaseoSearchPlan(new[] { Teologia, X1 });
            CollectionAssert.AreEqual(new[] { "Teologia", "X1", "Teologia", "Teologia", "Teologia", null }, Next(plan, 6, At(Teologia)));
        }

        [Test]
        public void ResetStartsTheCycleAgain()
        {
            var plan = new PaseoSearchPlan(Campus);
            plan.Next(At(Teologia), null);
            plan.Reset();
            Assert.AreEqual("Teologia", plan.Next(At(Teologia), null).BuildingId);
        }

        [Test]
        public void AFreshAccurateFixIsUsable()
        {
            Assert.IsTrue(PaseoGpsFix.From(true, -29.96, -71.35, 8f, 2.0).Valid);
        }

        [Test]
        public void AStaleInaccurateOrStoppedFixIsNot()
        {
            Assert.IsFalse(PaseoGpsFix.From(true, -29.96, -71.35, 8f, PaseoGpsFix.MaxAgeSeconds + 1.0).Valid);
            Assert.IsFalse(PaseoGpsFix.From(true, -29.96, -71.35, PaseoGpsFix.MaxAccuracyMeters + 1f, 2.0).Valid);
            Assert.IsFalse(PaseoGpsFix.From(true, -29.96, -71.35, 0f, 2.0).Valid);
            Assert.IsFalse(PaseoGpsFix.From(false, -29.96, -71.35, 8f, 2.0).Valid);
            Assert.IsFalse(PaseoGpsFix.None.Valid);
        }
    }
}

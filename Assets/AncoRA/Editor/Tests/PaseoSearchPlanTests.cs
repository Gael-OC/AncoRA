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

        static string[] NextWith(PaseoSearchPlan plan, int count, PaseoGpsFix fix, PaseoHeading heading, string inView = null, bool rankByHeading = true) =>
            Enumerable.Range(0, count).Select(_ => plan.Next(fix, heading, inView, rankByHeading).BuildingId).ToArray();

        // A phone 10 m south of Teología's centre: Teología is the nearest building (10 m), then EIC (23 m), X1 (26 m) and Ciencias
        // Básicas (37 m). By GPS they lie at bearings 0° (Teología), 208° (EIC), 312° (X1) and 18° (Ciencias Básicas).
        static readonly PaseoGpsFix SouthOfTeologia =
            PaseoGpsFix.From(true, Teologia.Latitude - 10.0 / 111319.49, Teologia.Longitude, 6f, 1.0);

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
        public void TheBearingToABuildingIsACompassAngle()
        {
            // Measured on the drone clouds: X1 stands west-north-west of Teología, Ciencias Básicas north-north-east and EIC south-south-west.
            Assert.AreEqual(291.1, PaseoGeo.BearingDegrees(Teologia.Latitude, Teologia.Longitude, X1.Latitude, X1.Longitude), 0.5);
            Assert.AreEqual(24.7, PaseoGeo.BearingDegrees(Teologia.Latitude, Teologia.Longitude, CienciasBasicas.Latitude, CienciasBasicas.Longitude), 0.5);
            Assert.AreEqual(199.4, PaseoGeo.BearingDegrees(Teologia.Latitude, Teologia.Longitude, Eic.Latitude, Eic.Longitude), 0.5);
            // And back: the opposite bearing.
            Assert.AreEqual(111.1, PaseoGeo.BearingDegrees(X1.Latitude, X1.Longitude, Teologia.Latitude, Teologia.Longitude), 0.5);
        }

        [Test]
        public void TheBearingOfTheCardinalDirectionsIs0And90And180And270()
        {
            const double lat = -29.964, lon = -71.349, step = 0.001;
            Assert.AreEqual(0.0, PaseoGeo.BearingDegrees(lat, lon, lat + step, lon), 1e-6);
            Assert.AreEqual(90.0, PaseoGeo.BearingDegrees(lat, lon, lat, lon + step), 1e-6);
            Assert.AreEqual(180.0, PaseoGeo.BearingDegrees(lat, lon, lat - step, lon), 1e-6);
            Assert.AreEqual(270.0, PaseoGeo.BearingDegrees(lat, lon, lat, lon - step), 1e-6);
            Assert.AreEqual(0.0, PaseoGeo.BearingDegrees(lat, lon, lat, lon), "the same point");
        }

        [Test]
        public void TheDifferenceBetweenTwoBearingsIsTheShortWayRound()
        {
            Assert.AreEqual(20.0, PaseoGeo.AngleDifferenceDegrees(350.0, 10.0), 1e-9);
            Assert.AreEqual(20.0, PaseoGeo.AngleDifferenceDegrees(10.0, 350.0), 1e-9);
            Assert.AreEqual(180.0, PaseoGeo.AngleDifferenceDegrees(0.0, 180.0), 1e-9);
            Assert.AreEqual(0.0, PaseoGeo.AngleDifferenceDegrees(90.0, 90.0), 1e-9);
            Assert.AreEqual(60.0, PaseoGeo.AngleDifferenceDegrees(-30.0, 30.0), 1e-9);
            Assert.AreEqual(0.0, PaseoGeo.NormalizeDegrees(360.0), 1e-9);
            Assert.AreEqual(359.0, PaseoGeo.NormalizeDegrees(-1.0), 1e-9);
        }

        [Test]
        public void AHeadingTowardsABuildingSearchesItFirstEvenThoughAnotherIsNearer()
        {
            // The field logs of 21 and 23-oct: the team looked at X1 and the GPS ranking kept searching Teología, the nearest.
            var plan = new PaseoSearchPlan(Campus);
            var search = plan.Next(SouthOfTeologia, PaseoHeading.Of(312.0), null);
            Assert.AreEqual("X1", search.BuildingId);
            Assert.AreEqual(PaseoSearchReason.Compass, search.Reason);
            CollectionAssert.AreEqual(new[] { 152198, 152200 }, search.MapIds);
            Assert.AreEqual(26.0, search.GpsDistanceMeters, 1.0, "X1 is farther than Teología (10 m)");
            Assert.AreEqual(312.0, search.HeadingDegrees, 1e-9);
            Assert.AreEqual(0.0, search.HeadingDifferenceDegrees, 1.5);
        }

        [Test]
        public void TheCycleKeepsItsShapeWithAHeading()
        {
            var plan = new PaseoSearchPlan(Campus);
            CollectionAssert.AreEqual(new[] { "X1", "Teologia", "X1", "EIC", "X1", null, "X1" },
                NextWith(plan, 7, SouthOfTeologia, PaseoHeading.Of(312.0)));
        }

        [Test]
        public void TheBuildingsInFrontComeFirstNearestFirstThenTheOthersByDistance()
        {
            // Facing north from the south of Teología: Teología (0°) and Ciencias Básicas (18°) are in front; EIC and X1 are not.
            CollectionAssert.AreEqual(new[] { "Teologia", "CienciasBasicas", "Teologia", "EIC", "Teologia", null },
                NextWith(new PaseoSearchPlan(Campus), 6, SouthOfTeologia, PaseoHeading.Of(0.0)));
            var plan = new PaseoSearchPlan(Campus);
            plan.Next(SouthOfTeologia, PaseoHeading.Of(0.0), null);
            var second = plan.Next(SouthOfTeologia, PaseoHeading.Of(0.0), null);
            Assert.AreEqual("CienciasBasicas", second.BuildingId);
            Assert.AreEqual(PaseoSearchReason.Compass, second.Reason);
        }

        [Test]
        public void ABuildingAtTheLimitOfTheViewIsStillInFrontAndOnePastItIsNot()
        {
            // X1 lies at 312.1° from the phone; a heading up to ViewHalfAngleDegrees away from that still faces it. Turning towards
            // the west keeps Teología (0°) and Ciencias Básicas (18°) out of the view.
            double x1 = PaseoGeo.BearingDegrees(SouthOfTeologia.Latitude, SouthOfTeologia.Longitude, X1.Latitude, X1.Longitude);
            var inside = new PaseoSearchPlan(Campus).Next(SouthOfTeologia, PaseoHeading.Of(x1 - PaseoSearchPlan.ViewHalfAngleDegrees + 1.0), null);
            Assert.AreEqual("X1", inside.BuildingId);
            Assert.AreEqual(PaseoSearchReason.Compass, inside.Reason);
            var outside = new PaseoSearchPlan(Campus).Next(SouthOfTeologia, PaseoHeading.Of(x1 - PaseoSearchPlan.ViewHalfAngleDegrees - 1.0), null);
            Assert.AreNotEqual(PaseoSearchReason.Compass, outside.Reason);
        }

        [Test]
        public void AHeadingThatFacesNoBuildingFallsBackToTheNearestByGps()
        {
            // Facing east (100°): nothing is within 35° of it from here.
            var first = new PaseoSearchPlan(Campus).Next(SouthOfTeologia, PaseoHeading.Of(100.0), null);
            Assert.AreEqual("Teologia", first.BuildingId);
            Assert.AreEqual(PaseoSearchReason.Gps, first.Reason);
            Assert.AreEqual(100.0, first.HeadingDifferenceDegrees, 1.5, "Teología lies at 0°");
            CollectionAssert.AreEqual(new[] { "Teologia", "EIC", "Teologia", "X1", "Teologia", null },
                NextWith(new PaseoSearchPlan(Campus), 6, SouthOfTeologia, PaseoHeading.Of(100.0)));
        }

        [Test]
        public void WithoutAUsableHeadingTheOrderIsTheSameAsBefore()
        {
            var expected = new[] { "Teologia", "EIC", "Teologia", "X1", "Teologia", null, "Teologia" };
            CollectionAssert.AreEqual(expected, Next(new PaseoSearchPlan(Campus), 7, SouthOfTeologia));
            CollectionAssert.AreEqual(expected, NextWith(new PaseoSearchPlan(Campus), 7, SouthOfTeologia, PaseoHeading.None));
            var search = new PaseoSearchPlan(Campus).Next(SouthOfTeologia, PaseoHeading.None, null);
            Assert.AreEqual(PaseoSearchReason.Gps, search.Reason);
            Assert.IsTrue(double.IsNaN(search.HeadingDegrees));
            Assert.IsTrue(double.IsNaN(search.HeadingDifferenceDegrees));
        }

        [Test]
        public void WithTheCompassRankingOffTheOrderIgnoresTheHeadingButTheLogKeepsIt()
        {
            var expected = new[] { "Teologia", "EIC", "Teologia", "X1", "Teologia", null, "Teologia" };
            CollectionAssert.AreEqual(expected, NextWith(new PaseoSearchPlan(Campus), 7, SouthOfTeologia, PaseoHeading.Of(312.0), rankByHeading: false));
            var search = new PaseoSearchPlan(Campus).Next(SouthOfTeologia, PaseoHeading.Of(312.0), null, rankByHeading: false);
            Assert.AreEqual("Teologia", search.BuildingId);
            Assert.AreEqual(PaseoSearchReason.Gps, search.Reason);
            Assert.AreEqual(312.0, search.HeadingDegrees, 1e-9);
            Assert.AreEqual(48.0, search.HeadingDifferenceDegrees, 1.5, "the nearest building lies 48° off the heading");
        }

        [Test]
        public void ABuildingAlreadyInViewStaysFirstWhateverTheHeadingSays()
        {
            var first = new PaseoSearchPlan(Campus).Next(SouthOfTeologia, PaseoHeading.Of(312.0), "Teologia");
            Assert.AreEqual("Teologia", first.BuildingId);
            Assert.AreEqual(PaseoSearchReason.InView, first.Reason);
            CollectionAssert.AreEqual(new[] { "Teologia", "X1", "Teologia", "EIC", "Teologia", null },
                NextWith(new PaseoSearchPlan(Campus), 6, SouthOfTeologia, PaseoHeading.Of(312.0), "Teologia"));
            var plan = new PaseoSearchPlan(Campus);
            plan.Next(SouthOfTeologia, PaseoHeading.Of(312.0), "Teologia");
            Assert.AreEqual(PaseoSearchReason.Compass, plan.Next(SouthOfTeologia, PaseoHeading.Of(312.0), "Teologia").Reason,
                "the next one in line is the building the camera faces");
        }

        [Test]
        public void WithoutAGpsFixTheHeadingChangesNothingButIsKeptForTheLog()
        {
            var search = new PaseoSearchPlan(Campus).Next(PaseoGpsFix.None, PaseoHeading.Of(312.0), null);
            Assert.IsNull(search.BuildingId);
            Assert.AreEqual(PaseoSearchReason.All, search.Reason);
            Assert.AreEqual(312.0, search.HeadingDegrees, 1e-9);
            Assert.IsTrue(double.IsNaN(search.HeadingDifferenceDegrees));
            // A building in view and no fix: the others keep the configuration order.
            CollectionAssert.AreEqual(new[] { "EIC", "CienciasBasicas", "EIC", "Teologia", "EIC", null },
                NextWith(new PaseoSearchPlan(Campus), 6, PaseoGpsFix.None, PaseoHeading.Of(312.0), "EIC"));
        }

        [Test]
        public void ABuildingUnderThePhoneCountsAsInFrontWhateverTheHeading()
        {
            // At Teología's centre the bearing to it is noise. Facing EIC (199°): Teología, then EIC, then the others by distance.
            CollectionAssert.AreEqual(new[] { "Teologia", "EIC", "Teologia", "X1", "Teologia", null },
                NextWith(new PaseoSearchPlan(Campus), 6, At(Teologia), PaseoHeading.Of(199.4)));
            Assert.AreEqual(PaseoSearchReason.Compass, new PaseoSearchPlan(Campus).Next(At(Teologia), PaseoHeading.Of(199.4), null).Reason);
        }

        [Test]
        public void ABuildingWithoutGpsStaysAfterTheOnesWithIt()
        {
            var unknown = new PaseoSearchBuilding("Nuevo", "Nuevo", new[] { 1 });
            var plan = new PaseoSearchPlan(new[] { unknown, Teologia, X1 });
            CollectionAssert.AreEqual(new[] { "X1", "Teologia", "X1", "Nuevo" }, NextWith(plan, 4, SouthOfTeologia, PaseoHeading.Of(312.0)));
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

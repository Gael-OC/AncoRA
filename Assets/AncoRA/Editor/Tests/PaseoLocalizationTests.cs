using System.Collections.Generic;
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoLocalizationTests
    {
        static readonly HashSet<int> Loaded = new() { 152259, 152198, 152200 };

        static bool InPlugin(int id) => Loaded.Contains(id);

        static PaseoSearch Building(string id, params int[] maps) => new(id, id, maps, PaseoSearchReason.Gps, 20.0);

        [Test]
        public void OnlyMapsTheSdkConfiguredAndThePluginLoadedAreAskedFor()
        {
            var configured = new HashSet<int> { 152259, 152198 };
            CollectionAssert.AreEqual(new[] { 152198 }, PaseoLocalization.Searchable(new[] { 152198, 152200 }, configured, InPlugin));
        }

        [Test]
        public void ABuildingWithoutUsableMapsFallsBackToEveryMap()
        {
            // The plugin fails the whole attempt when one id has no handle; nothing to ask for means every loaded map.
            var configured = new HashSet<int> { 152259 };
            Assert.IsNull(PaseoLocalization.Searchable(new[] { 152242 }, configured, InPlugin));
        }

        [Test]
        public void ASearchOverEveryMapAsksThePluginForAllOfThem()
        {
            var configured = new HashSet<int> { 152259, 152198, 152200 };
            Assert.IsNull(PaseoLocalization.Searchable(new[] { 152259, 152198, 152200 }, configured, InPlugin));
        }

        [Test]
        public void ABuildingSearchAsksThePluginForItsMapsOnly()
        {
            var configured = new HashSet<int> { 152259, 152198, 152200 };
            var search = PaseoLocalization.Resolve(Building("X1", 152198, 152200), configured, InPlugin, out int[] pluginIds);
            CollectionAssert.AreEqual(new[] { 152198, 152200 }, pluginIds);
            Assert.AreEqual("X1", search.BuildingId);
            CollectionAssert.AreEqual(new[] { 152198, 152200 }, search.MapIds);
            Assert.AreEqual(PaseoSearchReason.Gps, search.Reason);
            Assert.AreEqual(20.0, search.GpsDistanceMeters);
        }

        [Test]
        public void ABuildingWhoseMapsDidNotLoadIsSearchedAsEverything()
        {
            // EIC's map is not in the plugin: the attempt searches every loaded map, and its log line must say so.
            var configured = new HashSet<int> { 152259, 152198, 152242 };
            var search = PaseoLocalization.Resolve(Building("EIC", 152242), configured, InPlugin, out int[] pluginIds);
            Assert.IsNull(pluginIds);
            Assert.IsNull(search.BuildingId);
            Assert.AreEqual(PaseoSearchReason.All, search.Reason);
            CollectionAssert.AreEquivalent(new[] { 152259, 152198 }, search.MapIds);
            Assert.IsTrue(double.IsNaN(search.GpsDistanceMeters));
        }

        [Test]
        public void TheOnlyBuildingWithMapsKeepsItsNameThoughItsMapsAreAllOfThem()
        {
            var configured = new HashSet<int> { 152198, 152200 };
            var search = PaseoLocalization.Resolve(Building("X1", 152198, 152200), configured, InPlugin, out int[] pluginIds);
            Assert.IsNull(pluginIds, "every loaded map: the plugin's n = 0");
            Assert.AreEqual("X1", search.BuildingId);
            CollectionAssert.AreEqual(new[] { 152198, 152200 }, search.MapIds);
        }

        [Test]
        public void TheHeadingOfTheSearchReachesTheLog()
        {
            var configured = new HashSet<int> { 152259, 152198, 152200 };
            var wanted = new PaseoSearch("X1", "X1", new[] { 152198, 152200 }, PaseoSearchReason.Compass, 26.0, 312.0, 0.4);
            var search = PaseoLocalization.Resolve(wanted, configured, InPlugin, out _);
            Assert.AreEqual(PaseoSearchReason.Compass, search.Reason);
            Assert.AreEqual(312.0, search.HeadingDegrees);
            Assert.AreEqual(0.4, search.HeadingDifferenceDegrees);

            // A building whose maps did not load is searched as everything: the heading stays, the difference to a building does not.
            var missing = new PaseoSearch("EIC", "EIC", new[] { 152242 }, PaseoSearchReason.Compass, 23.0, 200.0, 3.0);
            var everything = PaseoLocalization.Resolve(missing, configured, InPlugin, out _);
            Assert.AreEqual(PaseoSearchReason.All, everything.Reason);
            Assert.AreEqual(200.0, everything.HeadingDegrees);
            Assert.IsTrue(double.IsNaN(everything.HeadingDifferenceDegrees));
        }

        [Test]
        public void ASearchOverEverythingListsTheLoadedMaps()
        {
            var configured = new HashSet<int> { 152259, 152198, 152242 };
            var everything = new PaseoSearch(null, null, new[] { 152259, 152198, 152242 }, PaseoSearchReason.All, double.NaN);
            var search = PaseoLocalization.Resolve(everything, configured, InPlugin, out int[] pluginIds);
            Assert.IsNull(pluginIds);
            Assert.IsNull(search.BuildingId);
            CollectionAssert.AreEquivalent(new[] { 152259, 152198 }, search.MapIds);
        }

        [Test]
        public void TheLocatedMapOfTheSearchedBuildingIsSearchedAlongWhenThePriorTestIsOn()
        {
            var configured = new HashSet<int> { 152259, 152198, 152200 };
            var search = Building("X1", 152198, 152200);
            Assert.AreEqual(152200, PaseoLocalization.PriorMap(true, search, 152200, false, configured, InPlugin));
            Assert.AreEqual(152198, PaseoLocalization.PriorMap(true, search, 152198, false, configured, InPlugin));
        }

        [Test]
        public void WithThePriorTestOffOrNothingLocatedTheSearchIsNormal()
        {
            var configured = new HashSet<int> { 152259, 152198, 152200 };
            var search = Building("X1", 152198, 152200);
            Assert.AreEqual(0, PaseoLocalization.PriorMap(false, search, 152198, false, configured, InPlugin));
            Assert.AreEqual(0, PaseoLocalization.PriorMap(true, search, 0, false, configured, InPlugin));
            Assert.AreEqual(0, PaseoLocalization.PriorMap(true, search, -1, false, configured, InPlugin));
        }

        [Test]
        public void AFailedPriorAttemptLetsTheNextOneOfThatBuildingSearchNormally()
        {
            var configured = new HashSet<int> { 152259, 152198, 152200 };
            var search = Building("X1", 152198, 152200);
            Assert.AreEqual(0, PaseoLocalization.PriorMap(true, search, 152198, skipOnce: true, configured, InPlugin));
            Assert.AreEqual(152198, PaseoLocalization.PriorMap(true, search, 152198, skipOnce: false, configured, InPlugin));
        }

        [Test]
        public void AMapOfAnotherBuildingOrOneThePluginDoesNotHaveIsNeverUsedAsAPrior()
        {
            var configured = new HashSet<int> { 152259, 152198, 152200 };
            var search = Building("X1", 152198, 152200);
            Assert.AreEqual(0, PaseoLocalization.PriorMap(true, search, 152259, false, configured, InPlugin), "Teología's map is not X1's");
            Assert.AreEqual(0, PaseoLocalization.PriorMap(true, Building("EIC", 152242), 152242, false, new HashSet<int> { 152242 }, InPlugin),
                "the plugin did not load it");
            Assert.AreEqual(0, PaseoLocalization.PriorMap(true, search, 152198, false, new HashSet<int> { 152259 }, InPlugin),
                "the SDK did not configure it");
        }

        [Test]
        public void ASearchOverEveryMapNeverUsesAPrior()
        {
            var configured = new HashSet<int> { 152259, 152198 };
            var everything = PaseoLocalization.Resolve(new PaseoSearch(null, null, new[] { 152259, 152198 }, PaseoSearchReason.All, double.NaN),
                configured, InPlugin, out _);
            Assert.AreEqual(0, PaseoLocalization.PriorMap(true, everything, 152259, false, configured, InPlugin));
        }

        [Test]
        public void ThePriorSearchNarrowsTheLoggedSearchToItsOneMap()
        {
            var wanted = new PaseoSearch("X1", "X1", new[] { 152198, 152200 }, PaseoSearchReason.Compass, 26.0, 312.0, 0.4);
            var narrowed = PaseoLocalization.OnlyMap(wanted, 152200);
            CollectionAssert.AreEqual(new[] { 152200 }, narrowed.MapIds);
            Assert.AreEqual("X1", narrowed.BuildingId);
            Assert.AreEqual("X1", narrowed.BuildingName);
            Assert.AreEqual(PaseoSearchReason.Compass, narrowed.Reason);
            Assert.AreEqual(26.0, narrowed.GpsDistanceMeters);
            Assert.AreEqual(312.0, narrowed.HeadingDegrees);
            Assert.AreEqual(0.4, narrowed.HeadingDifferenceDegrees);
        }
    }
}

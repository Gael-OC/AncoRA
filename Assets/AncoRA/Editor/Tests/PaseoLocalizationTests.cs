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
        public void ASearchOverEverythingListsTheLoadedMaps()
        {
            var configured = new HashSet<int> { 152259, 152198, 152242 };
            var everything = new PaseoSearch(null, null, new[] { 152259, 152198, 152242 }, PaseoSearchReason.All, double.NaN);
            var search = PaseoLocalization.Resolve(everything, configured, InPlugin, out int[] pluginIds);
            Assert.IsNull(pluginIds);
            Assert.IsNull(search.BuildingId);
            CollectionAssert.AreEquivalent(new[] { 152259, 152198 }, search.MapIds);
        }
    }
}

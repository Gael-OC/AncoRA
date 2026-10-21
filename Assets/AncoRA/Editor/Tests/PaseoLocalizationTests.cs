using System.Collections.Generic;
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoLocalizationTests
    {
        static readonly HashSet<int> Loaded = new() { 152259, 152198, 152200 };

        static bool InPlugin(int id) => Loaded.Contains(id);

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
    }
}

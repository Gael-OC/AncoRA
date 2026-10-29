using System;
using System.Collections.Generic;
using System.Threading;
using AncorRA.AR;
using Immersal.XR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoLocalizationTests
    {
        static readonly HashSet<int> Loaded = new() { 152259, 152198, 152200 };

        static bool InPlugin(int id) => Loaded.Contains(id);

        static PaseoSearch Building(string id, params int[] maps) => new(id, id, maps, PaseoSearchReason.Gps, 20.0);

        [Test]
        public void PitchIsNegativeLookingDownAndIgnoresHeadingAndRoll()
        {
            Assert.AreEqual(0f, PaseoLocalization.PitchDegrees(Quaternion.Euler(0f, 123f, 0f)), 0.01f);
            Assert.AreEqual(-40f, PaseoLocalization.PitchDegrees(Quaternion.Euler(40f, 200f, 25f)), 0.01f);
            Assert.AreEqual(20f, PaseoLocalization.PitchDegrees(Quaternion.Euler(-20f, 0f, 0f)), 0.01f);
        }

        sealed class CountingImage : ImageData
        {
            public int Released;
            public override IntPtr UnmanagedDataPointer => IntPtr.Zero;
            public override byte[] ManagedBytes => null;
            public override void DisposeData() => Released++;
        }

        [Test]
        public void ASkippedFloorAttemptStillReleasesTheCameraImage()
        {
            // Regression of the first floor filter: it returned before releasing the SDK's CPU image, ARCore ran out of
            // images and every later attempt failed with "Could not acquire camera image".
            var image = new CountingImage();
            var camera = new CameraData(image)
            {
                Width = 1920, Height = 1080, Channels = 1,
                CameraRotationOnCapture = Quaternion.Euler(70f, 0f, 0f), ScreenOrientation = Quaternion.identity
            };
            var go = new GameObject("PaseoLocalizationTest");
            try
            {
                var localization = go.AddComponent<PaseoLocalization>();
                localization.SetMaxPixels = _ => Assert.Fail("a skipped attempt never touches the plugin");
                var result = localization.Localize(camera, CancellationToken.None).GetAwaiter().GetResult();
                Assert.IsFalse(result.Success);
                Assert.AreEqual(1, image.Released);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OnlyFloorViewsAreSkipped()
        {
            Assert.GreaterOrEqual(PaseoLocalization.PitchDegrees(Quaternion.Euler(21f, 0f, 0f)), PaseoLocalization.MinPitchDegrees);
            Assert.Less(PaseoLocalization.PitchDegrees(Quaternion.Euler(45f, 0f, 0f)), PaseoLocalization.MinPitchDegrees);
        }

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

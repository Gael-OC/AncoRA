using System.Reflection;
using AncorRA.AR;
using Immersal;
using Immersal.XR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace AncorRA.Tests
{
    public class PaseoDemoMapTests
    {
        const string Key = PaseoBoxStore.DefaultPrefix + "MapaDemo";
        GameObject host;
        PaseoTour tour;
        UnityEvent<int> previousLoaded;
        bool hadSaved;
        int saved;

        [SetUp]
        public void SetUp()
        {
            previousLoaded = MapManager.MapRegisteredAndLoaded;
            hadSaved = PlayerPrefs.HasKey(Key);
            saved = PlayerPrefs.GetInt(Key);
            PlayerPrefs.DeleteKey(Key);
            host = new GameObject("Campus de prueba");
            var space = host.AddComponent<XRSpace>();
            PaseoMapContent Content(string id, bool search, params int[] maps)
            {
                var go = new GameObject(id);
                go.transform.SetParent(space.transform);
                var box = go.AddComponent<EdificioFacadeFrame>();
                var content = go.AddComponent<PaseoMapContent>();
                content.Configure(maps, 0, id, id, "", box);
                content.SetSearchable(search);
                return content;
            }
            var teo = Content("Teologia", true, 152259, 152693);
            var x1 = Content("X1", false, 152198);
            tour = host.AddComponent<PaseoTour>();
            tour.Configure(null, null, new[] { teo, x1 });
        }

        void Awake() => typeof(PaseoTour).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tour, null);
        PaseoSearch Next() => (PaseoSearch)typeof(PaseoTour).GetMethod("NextSearch", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tour, null);

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            MapManager.MapRegisteredAndLoaded = previousLoaded;
            if (hadSaved) PlayerPrefs.SetInt(Key, saved);
            else PlayerPrefs.DeleteKey(Key);
        }

        [TestCase(0, 152259)]
        [TestCase(152693, 152693)]
        [TestCase(999, 152259)]
        public void SearchUsesOnlyTheChosenMapAndFallsBackFromStalePreferences(int preference, int expected)
        {
            if (preference != 0) PlayerPrefs.SetInt(Key, preference);
            Awake();
            CollectionAssert.AreEqual(new[] { 152259, 152693 }, tour.DemoMapIds);
            Assert.AreEqual(expected, tour.DemoMapId);
            for (int i = 0; i < 5; i++)
                CollectionAssert.AreEqual(new[] { expected }, Next().MapIds);
            Assert.IsFalse(tour.ChangeDemoMap(999).GetAwaiter().GetResult());
            Assert.IsFalse(tour.ChangeDemoMap(expected).GetAwaiter().GetResult());
        }

        [Test]
        public void ManualAdjustmentKeepsItsBuildingAndStopsWhenThatBuildingIsHidden()
        {
            Awake();
            var x1 = tour.DemoBuildings[1];
            x1.SetVisible(true);
            tour.SelectEditBuilding("X1");
            Assert.AreSame(x1, tour.EditableContent);
            tour.SelectEditBuilding("unknown");
            Assert.AreSame(x1, tour.EditableContent);
            x1.SetVisible(false);
            Assert.IsNull(tour.EditableContent);
            x1.SetVisible(true);
            Assert.AreSame(x1, tour.EditableContent);
            x1.SetBoxHidden(true);
            Assert.IsNull(tour.EditableContent);
            tour.SelectEditBuilding(null);
            Assert.IsNull(tour.EditBuildingId);
        }

        [Test]
        public void OpaqueModelsDoNotOfferTheTransparentBoxControl()
        {
            var sheet = new PaseoTeamSheet();
            sheet.SetTarget("Teología", opaqueModel: true);
            Assert.IsTrue(PaseoUiKit.IsHidden(sheet.Root.Q("equipo-relleno")));
            sheet.SetTarget("Caja de diagnóstico");
            Assert.IsFalse(PaseoUiKit.IsHidden(sheet.Root.Q("equipo-relleno")));
        }

        [Test]
        public void TeamSheetMarksTheActiveMapAndRequestsTheOther()
        {
            var sheet = new PaseoTeamSheet();
            sheet.AttachDemoMaps(new[] { 152259, 152693 }, 152259);
            var first = sheet.Root.Q<Button>("demo-mapa-152259");
            var second = sheet.Root.Q<Button>("demo-mapa-152693");
            Assert.IsTrue(first.ClassListContains(PaseoRecordPanel.OnClass));
            Assert.IsFalse(second.ClassListContains(PaseoRecordPanel.OnClass));
            int requested = 0;
            sheet.DemoMapRequested += id => requested = id;
            Assert.IsTrue(sheet.PickDemoMap(152693));
            Assert.AreEqual(152693, requested);
            sheet.SetDemoMapChanging(true, 152259);
            Assert.IsFalse(second.enabledInHierarchy);
            Assert.IsFalse(sheet.PickDemoMap(152259));
        }
    }
}

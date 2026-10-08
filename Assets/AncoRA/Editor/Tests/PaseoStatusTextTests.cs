using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoStatusTextTests
    {
        static PaseoStatusInputs Ready(params PaseoBuildingStatus[] buildings) => new()
        {
            SdkReady = true, Tracking = true, SecondsSinceStart = 3f, Buildings = buildings
        };

        static string Describe(PaseoStatusInputs s, out PaseoStatusLevel level) => PaseoStatusText.Describe(s, out level);

        [Test]
        public void ErrorWinsOverEverything()
        {
            var s = Ready();
            s.Error = "SDK no quedó listo";
            Assert.AreEqual("Error: SDK no quedó listo", Describe(s, out var level));
            Assert.AreEqual(PaseoStatusLevel.Error, level);
        }

        [Test]
        public void UnsupportedPhone()
        {
            var s = Ready();
            s.ArUnsupported = true;
            StringAssert.Contains("no es compatible", Describe(s, out var level));
            Assert.AreEqual(PaseoStatusLevel.Error, level);
        }

        [Test]
        public void StartingThenSlowStart()
        {
            var s = new PaseoStatusInputs { SdkReady = false, SecondsSinceStart = 2f };
            Assert.AreEqual("Iniciando...", Describe(s, out var level));
            Assert.AreEqual(PaseoStatusLevel.Waiting, level);
            s.SecondsSinceStart = 16f;
            StringAssert.Contains("no arrancó", Describe(s, out level));
            Assert.AreEqual(PaseoStatusLevel.Error, level);
        }

        [Test]
        public void LoadingMap()
        {
            var s = Ready();
            s.MapStillLoading = "152192 (Ciencias Básicas)";
            Assert.AreEqual("Cargando mapa 152192 (Ciencias Básicas)...", Describe(s, out _));
        }

        [Test]
        public void NotTrackingAsksToMoveSlowly()
        {
            var s = Ready();
            s.Tracking = false;
            StringAssert.Contains("Mueve el teléfono despacio", Describe(s, out _));
        }

        [Test]
        public void NothingLocatedYet()
        {
            var s = Ready(new PaseoBuildingStatus("X1", PaseoBuildingState.Searching));
            Assert.AreEqual("Apunta a un edificio del paseo.", Describe(s, out var level));
            Assert.AreEqual(PaseoStatusLevel.Waiting, level);
            s.SecondsSinceStart = 20f;
            Assert.AreEqual("Acércate a un edificio del paseo y muévete despacio.", Describe(s, out _));
        }

        [Test]
        public void ListsEveryBuildingOnceOneIsLocated()
        {
            var s = Ready(
                new PaseoBuildingStatus("Ciencias Básicas", PaseoBuildingState.Located),
                new PaseoBuildingStatus("X1", PaseoBuildingState.Held),
                new PaseoBuildingStatus("EIC", PaseoBuildingState.Searching),
                new PaseoBuildingStatus("Teología", PaseoBuildingState.MapFailed));
            Assert.AreEqual(
                "Ciencias Básicas: ubicado · X1: ubicado (mantenido) · EIC: buscando... · Teología: el mapa no cargó",
                Describe(s, out var level));
            Assert.AreEqual(PaseoStatusLevel.Ok, level);
        }

        [Test]
        public void NullBuildingListIsTreatedAsEmpty()
        {
            var s = Ready();
            s.Buildings = null;
            Assert.AreEqual("Apunta a un edificio del paseo.", Describe(s, out _));
        }
    }
}

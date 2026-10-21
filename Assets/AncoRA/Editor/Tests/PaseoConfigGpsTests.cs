using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoConfigGpsTests
    {
        const string WithGps = "{\"nombre\":\"Teología\",\"descripcion\":\"Algo.\",\"gps\":{\"latitud\":-29.964054,\"longitud\":-71.349144},\"mapas\":[]}";

        [Test]
        public void TheBuildingCentreIsRead()
        {
            var config = PaseoConfig.Parse(WithGps);
            Assert.IsTrue(PaseoConfig.HasGps(config));
            Assert.AreEqual(-29.964054, config.gps.latitud, 1e-9);
            Assert.AreEqual(-71.349144, config.gps.longitud, 1e-9);
        }

        [Test]
        public void AMissingCentreIsWarnedNotAnError()
        {
            var config = PaseoConfig.Parse("{\"nombre\":\"X1\",\"descripcion\":\"Algo.\",\"mapas\":[]}");
            Assert.IsFalse(PaseoConfig.HasGps(config));
            var validation = PaseoConfig.Validate("X1", config, new string[0], true);
            Assert.IsTrue(validation.Ok);
            Assert.IsTrue(validation.Warnings.Exists(w => w.Contains("gps")));
        }

        [Test]
        public void ACentreOutsideTheEarthIsAnError()
        {
            var config = PaseoConfig.Parse("{\"nombre\":\"X1\",\"gps\":{\"latitud\":-129.9,\"longitud\":-71.3},\"mapas\":[]}");
            Assert.IsFalse(PaseoConfig.Validate("X1", config, new string[0], true).Ok);
        }

        [Test]
        public void ApplyingAFieldAdjustmentKeepsTheCentre()
        {
            var again = PaseoConfig.Parse(PaseoConfig.Serialize(PaseoConfig.Parse(WithGps)));
            Assert.AreEqual(-29.964054, again.gps.latitud, 1e-9);
            Assert.AreEqual(-71.349144, again.gps.longitud, 1e-9);
        }
    }
}

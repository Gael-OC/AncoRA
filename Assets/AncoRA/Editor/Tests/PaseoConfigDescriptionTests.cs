using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoConfigDescriptionTests
    {
        [Test]
        public void TheDescriptionIsRead()
        {
            var config = PaseoConfig.Parse("{\"nombre\":\"X1\",\"descripcion\":\"Edificio de laboratorios.\",\"mapas\":[]}");
            Assert.AreEqual("Edificio de laboratorios.", config.descripcion);
        }

        [Test]
        public void AMissingDescriptionIsEmptyAndWarned()
        {
            var config = PaseoConfig.Parse("{\"nombre\":\"X1\",\"mapas\":[]}");
            Assert.AreEqual("", config.descripcion);
            var validation = PaseoConfig.Validate("X1", config, new string[0], true);
            Assert.IsTrue(validation.Ok);
            Assert.IsTrue(validation.Warnings.Exists(w => w.Contains("descripcion")));
        }

        [Test]
        public void ADescriptionIsNotWarned()
        {
            var config = PaseoConfig.Parse("{\"nombre\":\"X1\",\"descripcion\":\"Algo.\",\"mapas\":[]}");
            Assert.IsFalse(PaseoConfig.Validate("X1", config, new string[0], true).Warnings.Exists(w => w.Contains("descripcion")));
        }

        [Test]
        public void ApplyingAFieldAdjustmentKeepsTheDescription()
        {
            var config = PaseoConfig.Parse("{\"nombre\":\"X1\",\"descripcion\":\"Algo.\",\"mapas\":[]}");
            Assert.AreEqual("Algo.", PaseoConfig.Parse(PaseoConfig.Serialize(config)).descripcion);
        }
    }
}

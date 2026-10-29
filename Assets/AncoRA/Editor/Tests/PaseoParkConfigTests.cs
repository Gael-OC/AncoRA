using System.Collections.Generic;
using System.Linq;
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoParkConfigTests
    {
        static readonly string[] Files = { "152198-x1gael.bytes", "edificio.json" };

        static string Json(string park) => @"{ ""nombre"": ""X1"", " + park +
            @"""mapas"": [ { ""id"": 152198, ""archivo"": ""152198-x1gael.bytes"" } ] }";

        static PaseoValidation Check(string park) => PaseoConfig.Validate("X1", PaseoConfig.Parse(Json(park)), Files, false);

        [Test]
        public void Parse_ReadsPark()
        {
            var c = PaseoConfig.Parse(Json(@"""parque"": { ""tema"": ""pista"", ""escala"": 2, ""frente"": ""-x"" },"));
            Assert.IsTrue(PaseoConfig.HasPark(c));
            Assert.AreEqual("pista", c.parque.tema);
            Assert.AreEqual(2f, c.parque.escala);
            Assert.AreEqual("-x", c.parque.frente);
        }

        [Test]
        public void Parse_WithoutPark_HasNoPark()
        {
            var c = PaseoConfig.Parse(Json(""));
            Assert.IsFalse(PaseoConfig.HasPark(c));
            Assert.IsTrue(PaseoConfig.Validate("X1", c, Files, false).Ok);
        }

        [Test]
        public void Parse_ParkWithDefaults()
        {
            var c = PaseoConfig.Parse(Json(@"""parque"": { ""tema"": ""baile"" },"));
            Assert.AreEqual(1.5f, c.parque.escala);
            Assert.AreEqual("+z", c.parque.frente);
            Assert.IsTrue(PaseoConfig.Validate("X1", c, Files, false).Ok);
        }

        [Test]
        public void Serialize_WithoutPark_DoesNotAddOne()
        {
            Assert.IsFalse(PaseoConfig.Serialize(PaseoConfig.Parse(Json(""))).Contains("parque"));
        }

        [Test]
        public void Serialize_KeepsPark()
        {
            var again = PaseoConfig.Parse(PaseoConfig.Serialize(PaseoConfig.Parse(Json(@"""parque"": { ""tema"": ""saludo"", ""escala"": 3 },"))));
            Assert.AreEqual("saludo", again.parque.tema);
            Assert.AreEqual(3f, again.parque.escala);
        }

        [Test]
        public void Validate_UnknownThemeIsError()
        {
            var r = Check(@"""parque"": { ""tema"": ""circo"" },");
            Assert.IsTrue(r.Errors.Any(e => e.Contains("parque.tema")));
        }

        [TestCase(0.4f)]
        [TestCase(4.5f)]
        [TestCase(float.NaN)]
        public void Validate_ScaleOutOfRangeIsError(float scale)
        {
            var c = PaseoConfig.Parse(Json(@"""parque"": { ""tema"": ""baile"" },"));
            c.parque.escala = scale;
            Assert.IsTrue(PaseoConfig.Validate("X1", c, Files, false).Errors.Any(e => e.Contains("parque.escala")));
        }

        [Test]
        public void Validate_BadFrontIsError()
        {
            var r = Check(@"""parque"": { ""tema"": ""baile"", ""frente"": ""norte"" },");
            Assert.IsTrue(r.Errors.Any(e => e.Contains("parque.frente")));
        }

        [TestCase("ciencias")]
        [TestCase("pista")]
        [TestCase("saludo")]
        [TestCase("baile")]
        public void Validate_KnownThemesPass(string tema)
        {
            Assert.IsTrue(Check(@"""parque"": { ""tema"": """ + tema + @""", ""escala"": 0.5, ""frente"": ""+x"" },").Ok);
        }
    }
}

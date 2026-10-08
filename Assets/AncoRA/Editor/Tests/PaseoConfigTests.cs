using System;
using System.Collections.Generic;
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoConfigTests
    {
        const string Full = @"{
  ""nombre"": ""Ciencias Básicas"",
  ""tamano"": [30, 10, 15],
  ""solido"": true,
  ""mapas"": [
    { ""id"": 152192, ""archivo"": ""152192-csbasicasgael.bytes"",
      ""caja"": { ""posicion"": [1, 2, 3], ""giro"": 45, ""colocada"": true } },
    { ""id"": 152196, ""archivo"": ""152196-csbasicasgael2.bytes"" }
  ]
}";

        static readonly string[] Files = { "152192-csbasicasgael.bytes", "152196-csbasicasgael2.bytes", "edificio.json" };

        [Test]
        public void Parse_ReadsAllFields()
        {
            var c = PaseoConfig.Parse(Full);
            Assert.AreEqual("Ciencias Básicas", c.nombre);
            CollectionAssert.AreEqual(new[] { 30f, 10f, 15f }, c.tamano);
            Assert.IsTrue(c.solido);
            Assert.AreEqual(2, c.mapas.Count);
            Assert.AreEqual(152192, c.mapas[0].id);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, c.mapas[0].caja.posicion);
            Assert.AreEqual(45f, c.mapas[0].caja.giro);
            Assert.IsTrue(c.mapas[0].caja.colocada);
        }

        [Test]
        public void Parse_FillsDefaultsForMissingSizeAndBox()
        {
            var c = PaseoConfig.Parse(@"{ ""nombre"": ""X1"", ""mapas"": [ { ""id"": 152198, ""archivo"": ""152198-x1gael.bytes"" } ] }");
            CollectionAssert.AreEqual(PaseoConfig.DefaultSize, c.tamano);
            Assert.IsNotNull(c.mapas[0].caja);
            Assert.AreEqual(3, c.mapas[0].caja.posicion.Length);
            Assert.IsFalse(c.mapas[0].caja.colocada);
        }

        [Test]
        public void Parse_MissingMapsGivesEmptyList()
        {
            var c = PaseoConfig.Parse(@"{ ""nombre"": ""Teología"" }");
            Assert.IsNotNull(c.mapas);
            Assert.AreEqual(0, c.mapas.Count);
        }

        [Test]
        public void Parse_MalformedJsonThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => PaseoConfig.Parse("{ nombre: "));
            Assert.Throws<FormatException>(() => PaseoConfig.Parse("   "));
        }

        [Test]
        public void SerializeThenParse_KeepsAccentsAndValues()
        {
            var c = PaseoConfig.Parse(Full);
            var again = PaseoConfig.Parse(PaseoConfig.Serialize(c));
            Assert.AreEqual("Ciencias Básicas", again.nombre);
            Assert.AreEqual(152196, again.mapas[1].id);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, again.mapas[0].caja.posicion);
        }

        [Test]
        public void IdFromFileName_ParsesLeadingNumber()
        {
            Assert.AreEqual(152192, PaseoConfig.IdFromFileName("152192-csbasicasgael.bytes"));
            Assert.IsNull(PaseoConfig.IdFromFileName("csbasicas.bytes"));
            Assert.IsNull(PaseoConfig.IdFromFileName("152192-csbasicasgael.json"));
        }

        [Test]
        public void Validate_AcceptsAGoodBuilding()
        {
            var v = PaseoConfig.Validate("CienciasBasicas", PaseoConfig.Parse(Full), Files, rejectSampleIds: true);
            Assert.IsTrue(v.Ok, string.Join("\n", v.Errors));
            Assert.AreEqual(0, v.Warnings.Count, string.Join("\n", v.Warnings));
        }

        [Test]
        public void Validate_ReportsMissingFile()
        {
            var v = PaseoConfig.Validate("CienciasBasicas", PaseoConfig.Parse(Full), new[] { "152192-csbasicasgael.bytes" }, true);
            Assert.IsFalse(v.Ok);
            StringAssert.Contains("152196-csbasicasgael2.bytes", string.Join("\n", v.Errors));
        }

        [Test]
        public void Validate_ReportsIdThatDoesNotMatchFile()
        {
            var c = PaseoConfig.Parse(Full);
            c.mapas[1].id = 999;
            var v = PaseoConfig.Validate("CienciasBasicas", c, Files, true);
            StringAssert.Contains("es del mapa 152196", string.Join("\n", v.Errors));
        }

        [Test]
        public void Validate_RejectsSdkSampleIdsOnlyWhenAsked()
        {
            var c = PaseoConfig.Parse(@"{ ""nombre"": ""A"", ""mapas"": [ { ""id"": 90687, ""archivo"": ""90687-SampleMapA.bytes"" } ] }");
            var files = new[] { "90687-SampleMapA.bytes" };
            Assert.IsFalse(PaseoConfig.Validate("A", c, files, rejectSampleIds: true).Ok);
            Assert.IsTrue(PaseoConfig.Validate("A", c, files, rejectSampleIds: false).Ok);
        }

        [Test]
        public void Validate_ReportsBadSizeAndDuplicateMap()
        {
            var c = PaseoConfig.Parse(Full);
            c.tamano = new[] { 20f, 0f };
            c.mapas[1].id = 152192;
            c.mapas[1].archivo = "152192-csbasicasgael.bytes";
            var errors = string.Join("\n", PaseoConfig.Validate("CienciasBasicas", c, Files, true).Errors);
            StringAssert.Contains("tamano", errors);
            StringAssert.Contains("repetido", errors);
        }

        [Test]
        public void Validate_WarnsAboutUnlistedBytesAndEmptyBuilding()
        {
            var c = PaseoConfig.Parse(@"{ ""nombre"": ""Teología"" }");
            var v = PaseoConfig.Validate("Teologia", c, new[] { "152195-Teologianicowo.bytes" }, true);
            Assert.IsTrue(v.Ok);
            var warnings = string.Join("\n", v.Warnings);
            StringAssert.Contains("no tiene mapas", warnings);
            StringAssert.Contains("152195-Teologianicowo.bytes", warnings);
        }

        [Test]
        public void ValidateTour_ReportsMapUsedByTwoBuildings()
        {
            var a = PaseoConfig.Parse(@"{ ""nombre"": ""A"", ""mapas"": [ { ""id"": 1, ""archivo"": ""1-a.bytes"" } ] }");
            var b = PaseoConfig.Parse(@"{ ""nombre"": ""B"", ""mapas"": [ { ""id"": 1, ""archivo"": ""1-a.bytes"" } ] }");
            var errors = PaseoConfig.ValidateTour(new Dictionary<string, PaseoBuildingConfig> { ["A"] = a, ["B"] = b });
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("mapa 1", errors[0]);
        }
    }
}

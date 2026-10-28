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
  ""descripcion"": ""Edificio de ciencias básicas."",
  ""gps"": { ""latitud"": -29.963828, ""longitud"": -71.349024 },
  ""tamano"": [30, 10, 15],
  ""solido"": true,
  ""mapas"": [
    { ""id"": 152192, ""archivo"": ""152192-csbasicasgael.bytes"",
      ""caja"": { ""posicion"": [1, 2, 3], ""giro"": 45, ""colocada"": true } },
    { ""id"": 152196, ""archivo"": ""152196-csbasicasgael2.bytes"",
      ""caja"": { ""posicion"": [4, 0, -2], ""giro"": 10, ""colocada"": true } }
  ]
}";

        const string Campus = @"{
  ""nombre"": ""X1"",
  ""campus"": { ""posicion"": [-10.40, 2.29, 0.54], ""giro"": 112.5, ""nube"": ""Campus-X1.ply"" },
  ""mapas"": [
    { ""id"": 152198, ""archivo"": ""152198-x1gael.bytes"",
      ""caja"": { ""posicion"": [-13.64, 0.74, -4.75], ""giro"": 120.67, ""colocada"": true } },
    { ""id"": 152200, ""archivo"": ""152200-x1johan.bytes"",
      ""caja"": { ""posicion"": [-9.63, 0.73, -3.43], ""giro"": 15.25, ""colocada"": true } }
  ]
}";

        static readonly string[] Files = { "152192-csbasicasgael.bytes", "152196-csbasicasgael2.bytes", "edificio.json" };
        static readonly string[] CampusFiles = { "152198-x1gael.bytes", "152200-x1johan.bytes", "edificio.json" };

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
        public void Validate_RefusesABuildingThatMixesPlacedAndUnplacedBoxes()
        {
            var c = PaseoConfig.Parse(Full);
            c.mapas[1].caja.colocada = false;
            var v = PaseoConfig.Validate("CienciasBasicas", c, Files, true);
            Assert.IsFalse(v.Ok);
            StringAssert.Contains("mezcla", string.Join("\n", v.Errors));
        }

        [Test]
        public void Validate_RefusesSeveralMapsWhenNoBoxIsPlaced()
        {
            var c = PaseoConfig.Parse(Full);
            c.mapas[0].caja.colocada = false;
            c.mapas[1].caja.colocada = false;
            var v = PaseoConfig.Validate("CienciasBasicas", c, Files, true);
            Assert.IsFalse(v.Ok);
            StringAssert.Contains("sin colocar", string.Join("\n", v.Errors));
        }

        [Test]
        public void Validate_AcceptsASingleMapWithItsBoxUnplaced()
        {
            var c = PaseoConfig.Parse(@"{ ""nombre"": ""X1"", ""mapas"": [ { ""id"": 152198, ""archivo"": ""152198-x1gael.bytes"" } ] }");
            Assert.IsTrue(PaseoConfig.Validate("X1", c, new[] { "152198-x1gael.bytes" }, true).Ok);
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
        public void Parse_ReadsTheCampusPose()
        {
            var c = PaseoConfig.Parse(Campus);
            Assert.IsTrue(PaseoConfig.HasCampus(c));
            CollectionAssert.AreEqual(new[] { -10.40f, 2.29f, 0.54f }, c.campus.posicion);
            Assert.AreEqual(112.5f, c.campus.giro);
            Assert.AreEqual("Campus-X1.ply", c.campus.nube);
        }

        [Test]
        public void Parse_WithoutCampusIsNotCampus()
        {
            Assert.IsFalse(PaseoConfig.HasCampus(PaseoConfig.Parse(Full)));
            Assert.IsFalse(PaseoConfig.HasCampus(PaseoConfig.Parse(@"{ ""nombre"": ""A"", ""campus"": { ""giro"": 5 } }")));
        }

        [Test]
        public void SerializeThenParse_KeepsTheCampusPoseAndItsCloud()
        {
            var again = PaseoConfig.Parse(PaseoConfig.Serialize(PaseoConfig.Parse(Campus)));
            Assert.IsTrue(PaseoConfig.HasCampus(again));
            CollectionAssert.AreEqual(new[] { -10.40f, 2.29f, 0.54f }, again.campus.posicion);
            Assert.AreEqual(112.5f, again.campus.giro);
            Assert.AreEqual("Campus-X1.ply", again.campus.nube);
            Assert.AreEqual(2, again.mapas.Count);
        }

        [Test]
        public void Serialize_WithoutCampusDoesNotWriteAnEmptyOne()
        {
            // JsonUtility writes absent nested objects as empty ones: rewriting a per-building file must not change it.
            string json = PaseoConfig.Serialize(PaseoConfig.Parse(Full));
            StringAssert.DoesNotContain("campus", json);
            var again = PaseoConfig.Parse(json);
            Assert.IsFalse(PaseoConfig.HasCampus(again));
            Assert.AreEqual(2, again.mapas.Count);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, again.mapas[0].caja.posicion);
        }

        [Test]
        public void Validate_AcceptsACampusBuilding()
        {
            var v = PaseoConfig.Validate("X1", PaseoConfig.Parse(Campus), CampusFiles, rejectSampleIds: true);
            Assert.IsTrue(v.Ok, string.Join("\n", v.Errors));
        }

        [Test]
        public void Validate_RefusesACampusBuildingWithABoxNotPlaced()
        {
            var c = PaseoConfig.Parse(Campus);
            c.mapas[1].caja.colocada = false;
            var v = PaseoConfig.Validate("X1", c, CampusFiles, true);
            Assert.IsFalse(v.Ok);
            StringAssert.Contains("campus", string.Join("\n", v.Errors));
        }

        [Test]
        public void Validate_RefusesACampusSingleMapBuildingWithItsBoxNotPlaced()
        {
            var c = PaseoConfig.Parse(Campus);
            c.mapas.RemoveAt(1);
            c.mapas[0].caja.colocada = false;
            Assert.IsFalse(PaseoConfig.Validate("X1", c, CampusFiles, true).Ok);
        }

        [Test]
        public void Validate_RefusesABadCampusPose()
        {
            var c = PaseoConfig.Parse(Campus);
            c.campus.giro = float.NaN;
            StringAssert.Contains("campus.posicion", string.Join("\n", PaseoConfig.Validate("X1", c, CampusFiles, true).Errors));

            c = PaseoConfig.Parse(Campus);
            c.campus.posicion = new[] { 1f, float.PositiveInfinity, 3f };
            Assert.IsFalse(PaseoConfig.Validate("X1", c, CampusFiles, true).Ok);

            c = PaseoConfig.Parse(Campus);
            c.campus.posicion = new[] { 1f, 2f };
            var errors = string.Join("\n", PaseoConfig.Validate("X1", c, CampusFiles, true).Errors);
            StringAssert.Contains("tres números", errors);
        }

        [Test]
        public void IsCampusTour_NeedsEveryBuildingWithMapsToCarryIt()
        {
            var x1 = PaseoConfig.Parse(Campus);
            var teologia = PaseoConfig.Parse(Campus);
            var empty = PaseoConfig.Parse(@"{ ""nombre"": ""EIC"" }");
            var perBuilding = PaseoConfig.Parse(Full);

            Assert.IsTrue(PaseoConfig.IsCampusTour(new[] { x1, teologia }));
            Assert.IsTrue(PaseoConfig.IsCampusTour(new[] { x1, empty }), "a building without maps takes no part");
            Assert.IsFalse(PaseoConfig.IsCampusTour(new[] { x1, perBuilding }));
            Assert.IsFalse(PaseoConfig.IsCampusTour(new[] { perBuilding }));
            Assert.IsFalse(PaseoConfig.IsCampusTour(new[] { empty }));
            Assert.IsFalse(PaseoConfig.IsCampusTour(new PaseoBuildingConfig[0]));
        }

        [Test]
        public void ValidateTour_RefusesCampusInSomeBuildingsOnly()
        {
            var tour = new Dictionary<string, PaseoBuildingConfig>
            {
                ["X1"] = PaseoConfig.Parse(Campus),
                ["CienciasBasicas"] = PaseoConfig.Parse(Full)
            };
            var errors = PaseoConfig.ValidateTour(tour);
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("X1", errors[0]);
            StringAssert.Contains("CienciasBasicas", errors[0]);
            StringAssert.Contains("campus", errors[0]);
        }

        [Test]
        public void ValidateTour_AcceptsCampusInAllOrNoneAndIgnoresBuildingsWithoutMaps()
        {
            var other = PaseoConfig.Parse(Campus.Replace("152198", "152298").Replace("152200", "152300"));
            var empty = PaseoConfig.Parse(@"{ ""nombre"": ""EIC"" }");
            Assert.AreEqual(0, PaseoConfig.ValidateTour(new Dictionary<string, PaseoBuildingConfig> { ["X1"] = PaseoConfig.Parse(Campus), ["Otro"] = other, ["EIC"] = empty }).Count);
            Assert.AreEqual(0, PaseoConfig.ValidateTour(new Dictionary<string, PaseoBuildingConfig> { ["A"] = PaseoConfig.Parse(Full), ["EIC"] = empty }).Count);
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

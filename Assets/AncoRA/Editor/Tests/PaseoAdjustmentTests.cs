using System.Collections.Generic;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoAdjustmentTests
    {
        static Dictionary<string, PaseoBuildingConfig> Tour() => new()
        {
            ["CienciasBasicas"] = PaseoConfig.Parse(@"{ ""nombre"": ""Ciencias Básicas"", ""mapas"": [
                { ""id"": 152192, ""archivo"": ""152192-csbasicasgael.bytes"" },
                { ""id"": 152196, ""archivo"": ""152196-csbasicasgael2.bytes"" } ] }"),
            ["X1"] = PaseoConfig.Parse(@"{ ""nombre"": ""X1"", ""mapas"": [ { ""id"": 152198, ""archivo"": ""152198-x1gael.bytes"" } ] }")
        };

        static PaseoAdjustment Adjustment(string building, int mapId, bool placed) => new()
        {
            generado = "2026-10-08T12:00:00",
            edificios = new List<PaseoAdjustmentBuilding>
            {
                new()
                {
                    id = building, tamano = new[] { 30f, 10f, 15f }, solido = true,
                    mapas = new List<PaseoAdjustmentMap> { new() { id = mapId, posicion = new[] { 1f, 2f, 3f }, giro = 45f, colocada = placed } }
                }
            }
        };

        [Test]
        public void ApplyTo_UpdatesPlacedMapAndBuildingSize()
        {
            var tour = Tour();
            var problems = Adjustment("CienciasBasicas", 152196, placed: true).ApplyTo(tour);
            Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
            var building = tour["CienciasBasicas"];
            CollectionAssert.AreEqual(new[] { 30f, 10f, 15f }, building.tamano);
            Assert.IsTrue(building.solido);
            var box = building.mapas[1].caja;
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, box.posicion);
            Assert.AreEqual(45f, box.giro);
            Assert.IsTrue(box.colocada);
            Assert.IsFalse(building.mapas[0].caja.colocada, "the other map of the building was not adjusted");
        }

        [Test]
        public void ApplyTo_IgnoresPoseOfMapsTheTeamDidNotPlace()
        {
            var tour = Tour();
            Adjustment("X1", 152198, placed: false).ApplyTo(tour);
            var box = tour["X1"].mapas[0].caja;
            Assert.IsFalse(box.colocada);
            CollectionAssert.AreEqual(new[] { 0f, 0f, 12f }, box.posicion);
            CollectionAssert.AreEqual(new[] { 30f, 10f, 15f }, tour["X1"].tamano, "size is still applied");
        }

        [Test]
        public void ApplyTo_UnknownMapChangesNothing()
        {
            var tour = Tour();
            var adjustment = Adjustment("X1", 152198, true);
            adjustment.edificios[0].mapas.Add(new PaseoAdjustmentMap { id = 777, posicion = new[] { 0f, 0f, 0f }, colocada = true });
            var problems = adjustment.ApplyTo(tour);
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("777", problems[0]);
            Assert.IsFalse(tour["X1"].mapas[0].caja.colocada, "nothing is applied when there are problems");
            CollectionAssert.AreEqual(PaseoConfig.DefaultSize, tour["X1"].tamano);
        }

        [Test]
        public void ApplyTo_UnknownBuildingIsAProblem()
        {
            var problems = Adjustment("Teologia", 152195, true).ApplyTo(Tour());
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("Teologia", problems[0]);
        }

        [Test]
        public void ApplyTo_RejectsBadPositionAndSize()
        {
            var adjustment = Adjustment("X1", 152198, true);
            adjustment.edificios[0].mapas[0].posicion = new[] { 1f, float.NaN, 3f };
            adjustment.edificios[0].tamano = new[] { 30f, 10f };
            var problems = adjustment.ApplyTo(Tour());
            Assert.AreEqual(2, problems.Count, string.Join("\n", problems));
        }

        [Test]
        public void JsonRoundTrip_KeepsValues()
        {
            string json = JsonUtility.ToJson(Adjustment("X1", 152198, true));
            var back = JsonUtility.FromJson<PaseoAdjustment>(json);
            Assert.AreEqual("X1", back.edificios[0].id);
            Assert.AreEqual(152198, back.edificios[0].mapas[0].id);
            Assert.AreEqual(45f, back.edificios[0].mapas[0].giro);
        }
    }
}

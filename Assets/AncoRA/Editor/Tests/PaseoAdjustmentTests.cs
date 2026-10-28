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
                { ""id"": 152192, ""archivo"": ""152192-csbasicasgael.bytes"",
                  ""caja"": { ""posicion"": [-11.64, 1.6, 3.59], ""giro"": 79.1, ""colocada"": true } },
                { ""id"": 152196, ""archivo"": ""152196-csbasicasgael2.bytes"",
                  ""caja"": { ""posicion"": [-17.42, 0.19, -0.49], ""giro"": 45.89, ""colocada"": true } } ] }"),
            ["X1"] = PaseoConfig.Parse(@"{ ""nombre"": ""X1"", ""mapas"": [ { ""id"": 152198, ""archivo"": ""152198-x1gael.bytes"" } ] }")
        };

        static PaseoAdjustment Adjustment(string building, int reference, bool placed) => new()
        {
            generado = "2026-10-08T12:00:00",
            edificios = new List<PaseoAdjustmentBuilding>
            {
                new()
                {
                    id = building, referencia = reference, tamano = new[] { 30f, 10f, 15f }, solido = true,
                    posicion = new[] { 1f, 2f, 3f }, giro = 45f, colocada = placed
                }
            }
        };

        static void AssertPose(Vector3 position, float yaw, Matrix4x4 actual)
        {
            PaseoBuildingFrame.Decompose(actual, out var p, out float y);
            Assert.AreEqual(0f, Vector3.Distance(position, p), 1e-3f, $"position {p}");
            Assert.AreEqual(0f, Mathf.DeltaAngle(yaw, y), 1e-3f, $"yaw {y}");
        }

        [Test]
        public void ApplyTo_WritesThePoseIntoTheReferenceMapAndTheBuildingSize()
        {
            var tour = Tour();
            var problems = Adjustment("CienciasBasicas", 152192, placed: true).ApplyTo(tour);
            Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
            var building = tour["CienciasBasicas"];
            CollectionAssert.AreEqual(new[] { 30f, 10f, 15f }, building.tamano);
            Assert.IsTrue(building.solido);
            var box = building.mapas[0].caja;
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, box.posicion);
            Assert.AreEqual(45f, box.giro);
            Assert.IsTrue(box.colocada);
        }

        [Test]
        public void ApplyTo_CarriesThePoseToTheBoxOfEveryOtherMap()
        {
            var tour = Tour();
            var building = tour["CienciasBasicas"];
            var before = PaseoBuildingFrame.MapTransforms(building);

            Adjustment("CienciasBasicas", 152192, placed: true).ApplyTo(tour);

            // B_M = T_M⁻¹ · B_R': seen through the (unchanged) transform of its map, the box of map 152196 is the new box.
            var second = building.mapas[1].caja;
            Assert.IsTrue(second.colocada);
            var seen = before[1] * PaseoBuildingFrame.BoxMatrix(new Vector3(second.posicion[0], second.posicion[1], second.posicion[2]), second.giro);
            AssertPose(new Vector3(1f, 2f, 3f), 45f, seen);
            Assert.IsTrue(second.giro >= 0f && second.giro < 360f);
        }

        [Test]
        public void ApplyTo_LeavesTheMapsWhereTheyWereInsideTheBuilding()
        {
            var tour = Tour();
            var before = PaseoBuildingFrame.MapTransforms(tour["CienciasBasicas"]);
            Adjustment("CienciasBasicas", 152192, placed: true).ApplyTo(tour);
            var after = PaseoBuildingFrame.MapTransforms(tour["CienciasBasicas"]);
            for (int i = 0; i < before.Length; i++)
            {
                PaseoBuildingFrame.Decompose(before[i], out var p, out float y);
                AssertPose(p, y, after[i]);
            }
        }

        [Test]
        public void ApplyTo_PlacesTheBoxOfASingleMapBuilding()
        {
            var tour = Tour();
            var problems = Adjustment("X1", 152198, placed: true).ApplyTo(tour);
            Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
            var box = tour["X1"].mapas[0].caja;
            Assert.IsTrue(box.colocada);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, box.posicion);
            Assert.AreEqual(45f, box.giro);
        }

        [Test]
        public void ApplyTo_IgnoresPoseOfBuildingsTheTeamDidNotPlace()
        {
            var tour = Tour();
            Adjustment("X1", 152198, placed: false).ApplyTo(tour);
            var box = tour["X1"].mapas[0].caja;
            Assert.IsFalse(box.colocada);
            CollectionAssert.AreEqual(new[] { 0f, 0f, 12f }, box.posicion);
            CollectionAssert.AreEqual(new[] { 30f, 10f, 15f }, tour["X1"].tamano, "size is still applied");
        }

        [Test]
        public void ApplyTo_ADifferentReferenceMapChangesNothing()
        {
            var tour = Tour();
            var problems = Adjustment("CienciasBasicas", 152196, placed: true).ApplyTo(tour);
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("152196", problems[0]);
            StringAssert.Contains("152192", problems[0]);
            CollectionAssert.AreEqual(new[] { -11.64f, 1.6f, 3.59f }, tour["CienciasBasicas"].mapas[0].caja.posicion);
            CollectionAssert.AreEqual(PaseoConfig.DefaultSize, tour["CienciasBasicas"].tamano, "nothing is applied when there are problems");
        }

        [Test]
        public void ApplyTo_AnAdjustmentWithoutReferenceIsNotChecked()
        {
            var tour = Tour();
            Assert.AreEqual(0, Adjustment("CienciasBasicas", 0, placed: true).ApplyTo(tour).Count);
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
            adjustment.edificios[0].posicion = new[] { 1f, float.NaN, 3f };
            adjustment.edificios[0].tamano = new[] { 30f, 10f };
            var problems = adjustment.ApplyTo(Tour());
            Assert.AreEqual(2, problems.Count, string.Join("\n", problems));
        }

        static Dictionary<string, PaseoBuildingConfig> CampusTour() => new()
        {
            ["CienciasBasicas"] = PaseoConfig.Parse(@"{ ""nombre"": ""Ciencias Básicas"",
                ""campus"": { ""posicion"": [-10.4, 2.29, 0.54], ""giro"": 112.5, ""nube"": ""Campus-CienciasBasicas.ply"" },
                ""mapas"": [
                { ""id"": 152192, ""archivo"": ""152192-csbasicasgael.bytes"",
                  ""caja"": { ""posicion"": [-11.64, 1.6, 3.59], ""giro"": 79.1, ""colocada"": true } },
                { ""id"": 152196, ""archivo"": ""152196-csbasicasgael2.bytes"",
                  ""caja"": { ""posicion"": [-17.42, 0.19, -0.49], ""giro"": 45.89, ""colocada"": true } } ] }"),
            ["X1"] = PaseoConfig.Parse(@"{ ""nombre"": ""X1"",
                ""campus"": { ""posicion"": [20, 0.5, 8], ""giro"": 30, ""nube"": ""Campus-X1.ply"" },
                ""mapas"": [ { ""id"": 152198, ""archivo"": ""152198-x1gael.bytes"",
                  ""caja"": { ""posicion"": [-13.64, 0.74, -4.75], ""giro"": 120.67, ""colocada"": true } } ] }")
        };

        [Test]
        public void ApplyTo_InCampusModeWritesTheCampusPoseAndKeepsEveryMapWhereItWas()
        {
            var tour = CampusTour();
            var building = tour["CienciasBasicas"];
            var before = PaseoBuildingFrame.CampusMapTransforms(building);

            var problems = Adjustment("CienciasBasicas", 0, placed: true).ApplyTo(tour);

            Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, building.campus.posicion);
            Assert.AreEqual(45f, building.campus.giro);
            Assert.AreEqual("Campus-CienciasBasicas.ply", building.campus.nube);
            CollectionAssert.AreEqual(new[] { 30f, 10f, 15f }, building.tamano);
            Assert.IsTrue(building.solido);
            var after = PaseoBuildingFrame.CampusMapTransforms(building);
            for (int i = 0; i < before.Length; i++)
            {
                PaseoBuildingFrame.Decompose(before[i], out var p, out float y);
                AssertPose(p, y, after[i]);
                var box = building.mapas[i].caja;
                AssertPose(new Vector3(1f, 2f, 3f), 45f,
                    after[i] * PaseoBuildingFrame.BoxMatrix(new Vector3(box.posicion[0], box.posicion[1], box.posicion[2]), box.giro));
            }
        }

        [Test]
        public void ApplyTo_InCampusModeLeavesOtherBuildingsAlone()
        {
            var tour = CampusTour();
            Adjustment("CienciasBasicas", 0, placed: true).ApplyTo(tour);
            CollectionAssert.AreEqual(new[] { 20f, 0.5f, 8f }, tour["X1"].campus.posicion);
            CollectionAssert.AreEqual(new[] { -13.64f, 0.74f, -4.75f }, tour["X1"].mapas[0].caja.posicion);
        }

        [Test]
        public void ApplyTo_InCampusModeRefusesAPoseFromTheFrameOfAMap()
        {
            // The phone says «referencia» 0 in campus mode; a map id means the pose is in that map's building frame.
            var tour = CampusTour();
            var problems = Adjustment("CienciasBasicas", 152192, placed: true).ApplyTo(tour);
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("campus", problems[0]);
            CollectionAssert.AreEqual(new[] { -10.4f, 2.29f, 0.54f }, tour["CienciasBasicas"].campus.posicion, "nothing changed");
        }

        [Test]
        public void ApplyTo_InCampusModeIgnoresTheSizeOnlyAdjustmentOfABuildingNotPlaced()
        {
            var tour = CampusTour();
            Adjustment("X1", 0, placed: false).ApplyTo(tour);
            CollectionAssert.AreEqual(new[] { 20f, 0.5f, 8f }, tour["X1"].campus.posicion);
            CollectionAssert.AreEqual(new[] { 30f, 10f, 15f }, tour["X1"].tamano);
        }

        [Test]
        public void JsonRoundTrip_KeepsValues()
        {
            string json = JsonUtility.ToJson(Adjustment("X1", 152198, true));
            var back = JsonUtility.FromJson<PaseoAdjustment>(json);
            Assert.AreEqual("X1", back.edificios[0].id);
            Assert.AreEqual(152198, back.edificios[0].referencia);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, back.edificios[0].posicion);
            Assert.AreEqual(45f, back.edificios[0].giro);
            Assert.IsTrue(back.edificios[0].colocada);
        }
    }
}

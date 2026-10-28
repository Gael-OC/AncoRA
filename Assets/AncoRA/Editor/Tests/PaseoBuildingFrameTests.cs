using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoBuildingFrameTests
    {
        static PaseoMapConfig Map(int id, Vector3 position, float yaw, bool placed = true) => new()
        {
            id = id,
            archivo = $"{id}-mapa.bytes",
            caja = new PaseoBoxPose { posicion = new[] { position.x, position.y, position.z }, giro = yaw, colocada = placed }
        };

        static PaseoBuildingConfig Building(params PaseoMapConfig[] maps)
        {
            var building = new PaseoBuildingConfig { nombre = "Prueba" };
            building.mapas.AddRange(maps);
            return building;
        }

        static void AssertPose(Vector3 position, float yaw, Matrix4x4 actual, string what = "")
        {
            PaseoBuildingFrame.Decompose(actual, out var p, out float y);
            Assert.AreEqual(0f, Vector3.Distance(position, p), 1e-3f, $"{what} position {p}");
            Assert.AreEqual(0f, Mathf.DeltaAngle(yaw, y), 1e-3f, $"{what} yaw {y}");
        }

        [Test]
        public void TheReferenceMapIsAtTheIdentity()
        {
            var building = Building(Map(1, new Vector3(-9.2f, 1f, -9.1f), 66.7f), Map(2, new Vector3(-11.7f, 1.5f, -12.9f), 330.8f));
            var transforms = PaseoBuildingFrame.MapTransforms(building);
            Assert.AreEqual(Matrix4x4.identity, transforms[0]);
        }

        [Test]
        public void KnownAnswer()
        {
            // Reference box at the origin facing +Z. Map 2 sees the same box 10 m along its X, turned 90°: its frame must
            // be turned -90° (270°) and moved so that the box lands on the reference box.
            var building = Building(Map(1, Vector3.zero, 0f), Map(2, new Vector3(10f, 0f, 0f), 90f));
            var transforms = PaseoBuildingFrame.MapTransforms(building);
            AssertPose(new Vector3(0f, 0f, -10f), 270f, transforms[1]);
        }

        [Test]
        public void TheBoxSeenFromAnyMapThroughItsTransformLandsOnTheReferenceBox()
        {
            var building = Building(
                Map(1, new Vector3(-9.21f, 0.97f, -9.09f), 66.66f),
                Map(2, new Vector3(-11.702f, 1.554f, -12.926f), 330.779f),
                Map(3, new Vector3(25f, -0.4f, 7f), 10f));
            var transforms = PaseoBuildingFrame.MapTransforms(building);
            PaseoBuildingFrame.ReferenceBox(building, out var referencePosition, out float referenceYaw, out bool placed);
            Assert.IsTrue(placed);
            for (int i = 0; i < building.mapas.Count; i++)
            {
                var box = building.mapas[i].caja;
                var seen = transforms[i] * PaseoBuildingFrame.BoxMatrix(new Vector3(box.posicion[0], box.posicion[1], box.posicion[2]), box.giro);
                AssertPose(referencePosition, referenceYaw, seen, $"map {building.mapas[i].id}");
            }
        }

        [Test]
        public void ATransformIsAYawAndAMoveOnly()
        {
            var building = Building(Map(1, new Vector3(3f, 0.5f, -2f), 20f), Map(2, new Vector3(-8f, 1.5f, 6f), 200f));
            var t = PaseoBuildingFrame.MapTransforms(building)[1];
            // The vertical axis stays vertical and nothing tilts: the map frame and ARCore are aligned with gravity.
            Assert.AreEqual(1f, t.m11, 1e-5f);
            foreach (float zero in new[] { t.m01, t.m10, t.m12, t.m21, t.m30, t.m31, t.m32 })
                Assert.AreEqual(0f, zero, 1e-5f);
            Assert.AreEqual(1f, t.m33, 1e-6f);
            Assert.AreEqual(1f, t.lossyScale.x, 1e-5f);
            // The height difference between the two boxes carries over unchanged.
            Assert.AreEqual(0.5f - 1.5f, t.m13, 1e-4f);
        }

        [Test]
        public void YawsThatWrapAroundStillWork()
        {
            var building = Building(Map(1, new Vector3(4f, 0f, 1f), 10f), Map(2, new Vector3(-3f, 0f, 5f), 350f));
            var t = PaseoBuildingFrame.MapTransforms(building)[1];
            PaseoBuildingFrame.Decompose(t, out _, out float yaw);
            Assert.AreEqual(20f, yaw, 1e-3f);
            AssertPose(new Vector3(4f, 0f, 1f), 10f, t * PaseoBuildingFrame.BoxMatrix(new Vector3(-3f, 0f, 5f), 350f));
        }

        [Test]
        public void TheReferenceIsTheFirstMapWithAPlacedBox()
        {
            var building = Building(Map(1, Vector3.zero, 0f, placed: false), Map(2, Vector3.one, 5f));
            Assert.AreEqual(1, PaseoBuildingFrame.ReferenceIndex(building));
            Assert.AreEqual(2, PaseoBuildingFrame.ReferenceMapId(building));
        }

        [Test]
        public void WithoutAPlacedBoxTheFirstMapIsTheReferenceAndEverythingIsAtTheIdentity()
        {
            var building = Building(Map(1, new Vector3(0f, 0f, 12f), 0f, placed: false));
            Assert.AreEqual(0, PaseoBuildingFrame.ReferenceIndex(building));
            Assert.AreEqual(1, PaseoBuildingFrame.ReferenceMapId(building));
            PaseoBuildingFrame.ReferenceBox(building, out var position, out float yaw, out bool placed);
            Assert.IsFalse(placed);
            Assert.AreEqual(new Vector3(0f, 0f, 12f), position);
            Assert.AreEqual(Matrix4x4.identity, PaseoBuildingFrame.MapTransforms(building)[0]);
        }

        [Test]
        public void ABuildingWithoutMapsHasNoReferenceNorTransforms()
        {
            var building = Building();
            Assert.AreEqual(0, PaseoBuildingFrame.ReferenceMapId(building));
            Assert.AreEqual(0, PaseoBuildingFrame.MapTransforms(building).Length);
        }

        [Test]
        public void AppliedBoxPoseGoesToEveryMapAndKeepsTheMapsWhereTheyWere()
        {
            var building = Building(Map(1, new Vector3(-9.21f, 0.97f, -9.09f), 66.66f), Map(2, new Vector3(-11.702f, 1.554f, -12.926f), 330.779f));
            var before = PaseoBuildingFrame.MapTransforms(building);

            PaseoBuildingFrame.ApplyBoxPose(building, new Vector3(2f, 1f, -3f), 120f);

            var reference = building.mapas[0].caja;
            CollectionAssert.AreEqual(new[] { 2f, 1f, -3f }, reference.posicion);
            Assert.AreEqual(120f, reference.giro);
            var after = PaseoBuildingFrame.MapTransforms(building);
            for (int i = 0; i < before.Length; i++)
            {
                PaseoBuildingFrame.Decompose(before[i], out var p, out float y);
                AssertPose(p, y, after[i], $"map {i}");
                Assert.IsTrue(building.mapas[i].caja.colocada);
            }
            // And the second map's box, seen through its transform, is the new building box.
            var second = building.mapas[1].caja;
            AssertPose(new Vector3(2f, 1f, -3f), 120f,
                after[1] * PaseoBuildingFrame.BoxMatrix(new Vector3(second.posicion[0], second.posicion[1], second.posicion[2]), second.giro));
        }

        static PaseoBuildingConfig CampusBuilding(Vector3 position, float yaw, params PaseoMapConfig[] maps)
        {
            var building = Building(maps);
            building.campus = new PaseoCampusPose { posicion = new[] { position.x, position.y, position.z }, giro = yaw, nube = "Campus-Prueba.ply" };
            return building;
        }

        [Test]
        public void ABuildingIsCampusOnlyWhenItCarriesAPosition()
        {
            Assert.IsFalse(PaseoBuildingFrame.IsCampus(Building(Map(1, Vector3.zero, 0f))));
            Assert.IsTrue(PaseoBuildingFrame.IsCampus(CampusBuilding(new Vector3(1f, 2f, 3f), 4f, Map(1, Vector3.zero, 0f))));
        }

        [Test]
        public void CampusKnownAnswer()
        {
            // The map sees the box at its origin facing +Z: the map frame must be the campus pose of the box itself.
            var building = CampusBuilding(new Vector3(10f, 0f, 5f), 90f, Map(1, Vector3.zero, 0f));
            AssertPose(new Vector3(10f, 0f, 5f), 90f, PaseoBuildingFrame.CampusMapTransforms(building)[0]);
        }

        [Test]
        public void NoCampusMapIsTheReferenceSoNoneGetsTheIdentity()
        {
            var building = CampusBuilding(new Vector3(-10.4f, 2.29f, 0.54f), 112.5f,
                Map(1, new Vector3(-9.21f, 0.97f, -9.09f), 66.66f), Map(2, new Vector3(-11.702f, 1.554f, -12.926f), 330.779f));
            foreach (var transform in PaseoBuildingFrame.CampusMapTransforms(building))
                Assert.AreNotEqual(Matrix4x4.identity, transform);
        }

        [Test]
        public void TheBoxSeenFromAnyMapThroughItsCampusTransformLandsOnTheCampusPose()
        {
            var building = CampusBuilding(new Vector3(-10.4f, 2.29f, 0.54f), 112.5f,
                Map(1, new Vector3(-9.21f, 0.97f, -9.09f), 66.66f),
                Map(2, new Vector3(-11.702f, 1.554f, -12.926f), 330.779f),
                Map(3, new Vector3(25f, -0.4f, 7f), 10f));
            var transforms = PaseoBuildingFrame.CampusMapTransforms(building);
            Assert.AreEqual(3, transforms.Length);
            PaseoBuildingFrame.CampusBox(building, out var campusPosition, out float campusYaw);
            for (int i = 0; i < building.mapas.Count; i++)
            {
                var box = building.mapas[i].caja;
                var seen = transforms[i] * PaseoBuildingFrame.BoxMatrix(new Vector3(box.posicion[0], box.posicion[1], box.posicion[2]), box.giro);
                AssertPose(campusPosition, campusYaw, seen, $"map {building.mapas[i].id}");
            }
        }

        [Test]
        public void ACampusTransformIsAYawAndAMoveOnly()
        {
            var building = CampusBuilding(new Vector3(8f, 1f, -3f), 200f, Map(1, new Vector3(3f, 0.5f, -2f), 20f));
            var t = PaseoBuildingFrame.CampusMapTransforms(building)[0];
            Assert.AreEqual(1f, t.m11, 1e-5f);
            foreach (float zero in new[] { t.m01, t.m10, t.m12, t.m21, t.m30, t.m31, t.m32 })
                Assert.AreEqual(0f, zero, 1e-5f);
            // The height difference between the campus pose and the box in the map carries over unchanged.
            Assert.AreEqual(1f - 0.5f, t.m13, 1e-4f);
        }

        [Test]
        public void ABuildingWithoutMapsHasNoCampusTransforms()
        {
            Assert.AreEqual(0, PaseoBuildingFrame.CampusMapTransforms(CampusBuilding(Vector3.zero, 0f)).Length);
        }

        [Test]
        public void CampusBoxNormalizesTheYaw()
        {
            var building = CampusBuilding(new Vector3(1f, 2f, 3f), -29.221f, Map(1, Vector3.zero, 0f));
            PaseoBuildingFrame.CampusBox(building, out var position, out float yaw);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), position);
            Assert.AreEqual(330.779f, yaw, 1e-3f);
        }

        [Test]
        public void AppliedCampusBoxPoseKeepsEveryMapWhereItWasAndMovesTheBox()
        {
            var building = CampusBuilding(new Vector3(-10.4f, 2.29f, 0.54f), 112.5f,
                Map(1, new Vector3(-9.21f, 0.97f, -9.09f), 66.66f), Map(2, new Vector3(-11.702f, 1.554f, -12.926f), 330.779f));
            var before = PaseoBuildingFrame.CampusMapTransforms(building);

            PaseoBuildingFrame.ApplyCampusBoxPose(building, new Vector3(2f, 1f, -3f), 120f);

            CollectionAssert.AreEqual(new[] { 2f, 1f, -3f }, building.campus.posicion);
            Assert.AreEqual(120f, building.campus.giro);
            Assert.AreEqual("Campus-Prueba.ply", building.campus.nube, "the cloud name is not touched");
            var after = PaseoBuildingFrame.CampusMapTransforms(building);
            for (int i = 0; i < before.Length; i++)
            {
                PaseoBuildingFrame.Decompose(before[i], out var p, out float y);
                AssertPose(p, y, after[i], $"map {i}");
                Assert.IsTrue(building.mapas[i].caja.colocada);
                // And every map's box, seen through its unchanged transform, is the new campus pose.
                var box = building.mapas[i].caja;
                AssertPose(new Vector3(2f, 1f, -3f), 120f,
                    after[i] * PaseoBuildingFrame.BoxMatrix(new Vector3(box.posicion[0], box.posicion[1], box.posicion[2]), box.giro), $"box {i}");
                Assert.IsTrue(box.giro >= 0f && box.giro < 360f);
            }
        }

        [Test]
        public void NormalizeDegreesStaysInZeroTo360()
        {
            Assert.AreEqual(330.779f, PaseoBuildingFrame.NormalizeDegrees(-29.221f), 1e-3f);
            Assert.AreEqual(0f, PaseoBuildingFrame.NormalizeDegrees(360f));
            Assert.AreEqual(0f, PaseoBuildingFrame.NormalizeDegrees(-1e-9f));
            Assert.AreEqual(10f, PaseoBuildingFrame.NormalizeDegrees(730f), 1e-3f);
        }
    }
}

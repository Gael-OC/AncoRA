using System;
using System.Collections.Generic;
using System.Linq;
using AncorRA.AR;
using AncorRA.Editor;
using Immersal.XR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoSimulationTests
    {
        static PaseoSimInput Box(string id, Vector3 centre, float yaw, Vector3 size, string front = null) =>
            new(id, id, centre, yaw, size, front);

        static PaseoSimBuilding Single(Vector3 centre, float yaw, Vector3 size, string front = null) =>
            PaseoSimulationGeometry.Build(new[] { Box("A", centre, yaw, size, front) })[0];

        static void AssertVector(Vector3 expected, Vector3 actual, float tolerance = 1e-3f) =>
            Assert.AreEqual(0f, Vector3.Distance(expected, actual), tolerance, $"expected {expected}, was {actual}");

        // ---------------------------------------------------------------- viewpoints

        [Test]
        public void TheViewpointStandsInFrontOfTheDeclaredFaceAtTheDistanceFromIt()
        {
            // Face +z of a 10 m deep box centred at the origin is at z = 5; 25 m from it is z = 30.
            var building = Single(Vector3.zero, 0f, new Vector3(20f, 8f, 10f), "+z");
            var pose = PaseoSimulationGeometry.Viewpoint(building, 25f, groundY: -1f);
            AssertVector(new Vector3(0f, -1f + PaseoSimulationGeometry.EyeHeightMeters, 30f), pose.position);
            var forward = pose.rotation * Vector3.forward;
            Assert.Greater(Vector3.Dot(new Vector3(forward.x, 0f, forward.z).normalized, Vector3.back), 0.999f, "looks at the building");
        }

        [Test]
        public void TheFrontTurnsWithTheBox()
        {
            // Turned 90° around the vertical the box's +z points along world +x.
            var building = Single(Vector3.zero, 90f, new Vector3(20f, 8f, 10f), "+z");
            AssertVector(Vector3.right, building.Front);
            var pose = PaseoSimulationGeometry.Viewpoint(building, 25f, 0f);
            Assert.AreEqual(30f, pose.position.x, 1e-3f);
            Assert.AreEqual(0f, pose.position.z, 1e-3f);
        }

        [Test]
        public void AFrontAlongTheWidthUsesHalfTheWidth()
        {
            var building = Single(Vector3.zero, 0f, new Vector3(20f, 8f, 10f), "-x");
            AssertVector(Vector3.left, building.Front);
            Assert.AreEqual(10f, building.FrontHalfDepth, 1e-4f);
            Assert.AreEqual(-35f, PaseoSimulationGeometry.Viewpoint(building, 25f, 0f).position.x, 1e-3f);
        }

        [Test]
        public void WithoutAParkTheLongFaceLooksAwayFromTheOtherBuildings()
        {
            // Wider than deep: the long faces are ±z. The others are to the south, so the front is the north face.
            var buildings = PaseoSimulationGeometry.Build(new[]
            {
                Box("A", Vector3.zero, 0f, new Vector3(20f, 8f, 10f)),
                Box("B", new Vector3(0f, 0f, -50f), 0f, new Vector3(10f, 8f, 10f))
            });
            AssertVector(Vector3.forward, buildings[0].Front);
            Assert.IsFalse(buildings[0].FrontDeclared);
            AssertVector(Vector3.back, buildings[1].Front);
        }

        [Test]
        public void WithoutAParkADeeperThanWideBoxUsesItsXFaces()
        {
            var buildings = PaseoSimulationGeometry.Build(new[]
            {
                Box("A", Vector3.zero, 0f, new Vector3(8f, 8f, 20f)),
                Box("B", new Vector3(-50f, 0f, 0f), 0f, new Vector3(10f, 8f, 10f))
            });
            AssertVector(Vector3.right, buildings[0].Front);
            Assert.AreEqual(4f, buildings[0].FrontHalfDepth, 1e-4f);
        }

        [Test]
        public void ADistanceKeyPicksTheDistance()
        {
            Assert.AreEqual(25f, PaseoSimulationGeometry.DistanceFor(false, false));
            Assert.AreEqual(50f, PaseoSimulationGeometry.DistanceFor(true, false));
            Assert.AreEqual(15f, PaseoSimulationGeometry.DistanceFor(false, true));
            Assert.AreEqual(15f, PaseoSimulationGeometry.DistanceFor(true, true));
        }

        [Test]
        public void TheGroundIsTheLowestBoxBottom()
        {
            var buildings = PaseoSimulationGeometry.Build(new[]
            {
                Box("A", new Vector3(0f, 3f, 0f), 0f, new Vector3(10f, 8f, 10f)),
                Box("B", new Vector3(30f, 4f, 0f), 0f, new Vector3(10f, 4f, 10f))
            });
            Assert.AreEqual(-1f, PaseoSimulationGeometry.GroundY(buildings), 1e-4f);
        }

        // ---------------------------------------------------------------- the injected lock

        GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
                UnityEngine.Object.DestroyImmediate(host);
        }

        PaseoPoseProcessor NewProcessor()
        {
            host = new GameObject("XR Space de prueba");
            return host.AddComponent<PaseoPoseProcessor>();
        }

        [Test]
        public void TheSimulatedLockLeavesTheSpaceExactlyAtItsAuthoredPoseAndConfirmed()
        {
            var processor = NewProcessor();
            var authored = new Pose(new Vector3(3f, 1f, -2f), Quaternion.Euler(0f, 40f, 0f));

            processor.SimulateLock(authored, new Vector3(10f, 1.6f, 25f));

            Assert.IsTrue(processor.HasPose);
            Assert.IsTrue(processor.Confirmed);
            Assert.AreEqual(2, processor.Results);
            Assert.AreEqual(2, processor.UsedResults);
            var output = processor.ProcessData(null, DataProcessorTrigger.Update).Result;
            Assert.IsFalse(output.Ignore);
            AssertVector(authored.position, output.Pose.GetPosition());
            Assert.AreEqual(0f, Quaternion.Angle(authored.rotation, output.Pose.rotation), 1e-2f);
        }

        [Test]
        public void TheSimulatedLockReplacesWhatTheFilterHadBefore()
        {
            var processor = NewProcessor();
            processor.SimulateLock(new Pose(new Vector3(50f, 0f, 0f), Quaternion.identity), Vector3.zero);

            var authored = new Pose(Vector3.zero, Quaternion.identity);
            processor.SimulateLock(authored, Vector3.zero);

            var output = processor.ProcessData(null, DataProcessorTrigger.Update).Result;
            AssertVector(Vector3.zero, output.Pose.GetPosition());
        }

        [Test]
        public void TheDeliberateErrorOffsetsTheSpaceByTheRequestedAmount()
        {
            var authored = new Pose(new Vector3(3f, 1f, -2f), Quaternion.Euler(0f, 40f, 0f));
            // Pivoting on the space itself, the turn does not move it: only the offset does.
            var moved = PaseoSimulationGeometry.WithError(authored, authored.position, Vector3.right + Vector3.up, 1.5f, 3f);
            Assert.AreEqual(1.5f, Vector3.Distance(authored.position, moved.position), 1e-4f);
            Assert.AreEqual(authored.position.y, moved.position.y, 1e-4f, "the offset is along the ground");
            Assert.AreEqual(3f, Mathf.Abs(Mathf.DeltaAngle(authored.rotation.eulerAngles.y, moved.rotation.eulerAngles.y)), 1e-3f);
        }

        [Test]
        public void TheDeliberateErrorTurnsAroundThePivot()
        {
            var authored = new Pose(new Vector3(0f, 0f, 10f), Quaternion.identity);
            var moved = PaseoSimulationGeometry.WithError(authored, Vector3.zero, Vector3.zero, 0f, 90f);
            AssertVector(new Vector3(10f, 0f, 0f), moved.position);
        }

        [Test]
        public void TheProcessorHoldsTheErrorPoseWhenInjected()
        {
            var processor = NewProcessor();
            var authored = new Pose(Vector3.zero, Quaternion.identity);
            var wrong = PaseoSimulationGeometry.WithError(authored, authored.position, Vector3.right,
                PaseoSimulationGeometry.ErrorMeters, PaseoSimulationGeometry.ErrorDegrees);

            processor.SimulateLock(wrong, Vector3.zero);

            var output = processor.ProcessData(null, DataProcessorTrigger.Update).Result;
            Assert.AreEqual(PaseoSimulationGeometry.ErrorMeters, output.Pose.GetPosition().magnitude, 1e-3f);
            Assert.IsTrue(processor.Confirmed);
        }

        // ---------------------------------------------------------------- the generated environment

        [Test]
        public void TheEnvironmentHoldsOneBlockPerBuildingAtItsPoseAndSize()
        {
            var buildings = PaseoSimulationGeometry.Build(new[]
            {
                Box("A", new Vector3(5f, 3f, -6f), 303f, new Vector3(22f, 8f, 10f), "+z"),
                Box("B", new Vector3(-14f, 3.4f, 2.7f), 123f, new Vector3(27f, 4.7f, 9.4f))
            });
            float ground = PaseoSimulationGeometry.GroundY(buildings);
            var root = PaseoSimulationSetup.BuildEnvironmentObject(buildings, ground, PaseoSimulationSetup.StartPose(buildings, ground),
                PaseoSimulationSetup.MovementBounds(buildings, ground), null, null);
            try
            {
                var blocks = root.transform.Cast<Transform>().Where(t => t.name.StartsWith(PaseoSimulationSetup.BlockPrefix)).ToList();
                Assert.AreEqual(buildings.Count, blocks.Count);
                foreach (var building in buildings)
                {
                    var block = blocks.Single(t => t.name == PaseoSimulationSetup.BlockPrefix + building.Id);
                    AssertVector(building.Center, block.localPosition);
                    AssertVector(building.Size * PaseoSimulationSetup.BlockShrink, block.localScale);
                    Assert.AreEqual(0f, Mathf.DeltaAngle(building.Yaw, block.localEulerAngles.y), 1e-2f);
                }
                Assert.IsNotNull(root.transform.Find("Suelo"));
                var type = Type.GetType("UnityEngine.XR.Simulation.SimulationEnvironment, Unity.XR.Simulation");
                Assert.IsNotNull(type, "AR Foundation's SimulationEnvironment type");
                Assert.IsNotNull(root.GetComponent(type));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EveryHotkeyViewpointLiesInsideTheCameraBounds()
        {
            var buildings = PaseoSimulationGeometry.Build(new[]
            {
                Box("A", new Vector3(5f, 3f, -6f), 303f, new Vector3(22f, 8f, 10f), "+z"),
                Box("B", new Vector3(-14f, 3.4f, 2.7f), 123f, new Vector3(27f, 4.7f, 9.4f)),
                Box("C", new Vector3(22f, 3f, 17f), 216f, new Vector3(20f, 7f, 11f))
            });
            float ground = PaseoSimulationGeometry.GroundY(buildings);
            var bounds = PaseoSimulationSetup.MovementBounds(buildings, ground);
            foreach (var building in buildings)
                foreach (float distance in new[] { 15f, 25f, 50f })
                    Assert.IsTrue(bounds.Contains(PaseoSimulationGeometry.Viewpoint(building, distance, ground).position),
                        $"{building.Id} a {distance} m");
        }

        [Test]
        public void TheRealCampusDataGivesAViewpointInFrontOfEachBuilding()
        {
            var buildings = PaseoSimulationGeometry.LoadBuildings(PaseoSimulationGeometry.DataRoot);
            Assert.GreaterOrEqual(buildings.Count, 4);
            var teologia = buildings.Single(b => b.Id == "Teologia");
            Assert.IsTrue(teologia.FrontDeclared, "Teología declares «parque.frente»");
            float ground = PaseoSimulationGeometry.GroundY(buildings);
            foreach (var building in buildings)
            {
                var pose = PaseoSimulationGeometry.Viewpoint(building, 25f, ground);
                var toBox = building.Center - pose.position;
                toBox.y = 0f;
                Assert.AreEqual(25f + building.FrontHalfDepth, toBox.magnitude, 1e-2f, building.Id);
            }
        }

        [Test]
        public void ARealViewpointNeverStandsInsideOrBehindAnotherBuilding()
        {
            var buildings = PaseoSimulationGeometry.LoadBuildings(PaseoSimulationGeometry.DataRoot);
            float ground = PaseoSimulationGeometry.GroundY(buildings);
            var teologia = buildings.Single(b => b.Id == "Teologia");
            var x1 = buildings.Single(b => b.Id == "X1");
            // Teología's front faces X1 about 13 m away: at 25 m X1 stands between the viewer and the face.
            float teologiaClear = PaseoSimulationGeometry.ClearDistance(teologia, 25f, buildings);
            Assert.Less(teologiaClear, 25f);
            var face = teologia.Center + teologia.Front * teologia.FrontHalfDepth;
            var blocked = Enumerable.Range(0, 51).Select(i => Vector3.Lerp(face, face + teologia.Front * 25f, i / 50f));
            Assert.IsTrue(blocked.Any(p => PaseoSimulationGeometry.InsideFootprint(x1, p, 0f)), "X1 corta la línea a 25 m");
            foreach (var building in buildings)
                foreach (float requested in new[] { 15f, 25f, 50f })
                {
                    float clear = PaseoSimulationGeometry.ClearDistance(building, requested, buildings);
                    Assert.LessOrEqual(clear, requested);
                    Assert.GreaterOrEqual(clear, PaseoSimulationGeometry.MinDistanceMeters);
                    var position = PaseoSimulationGeometry.Viewpoint(building, clear, ground).position;
                    foreach (var other in buildings.Where(o => o.Id != building.Id))
                        Assert.IsFalse(PaseoSimulationGeometry.InsideFootprint(other, position, 0f), $"{building.Id} a {clear} m dentro de {other.Id}");
                }
        }
    }
}

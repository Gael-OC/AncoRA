using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoMultiSetRelationTests
    {
        const string Prefix = "AncoRA.Test.MultiSet.";
        static readonly Vector3 Size = new(20f, 8f, 10f);

        static void AssertVector(Vector3 expected, Vector3 actual, float tolerance = 1e-3f) =>
            Assert.AreEqual(0f, Vector3.Distance(expected, actual), tolerance, $"expected {expected}, was {actual}");

        [Test]
        public void TheBoxInTheFrameOfAnUntransformedSpaceIsItsWorldPose()
        {
            var relation = PaseoMultiSetGeometry.FromWorld(Vector3.zero, Quaternion.identity, new Vector3(0f, 1f, 10f), Quaternion.Euler(0f, 30f, 0f), Size);
            AssertVector(new Vector3(0f, 1f, 10f), relation.Position);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.Euler(0f, 30f, 0f), relation.Rotation), 1e-3f);
            Assert.AreEqual(Size, relation.Size);
        }

        [Test]
        public void ARelationTurnsWithItsSpace()
        {
            // The box is 10 m along the space's own +Z. Turn the space 90 degrees and put it at x = 5: the box is 10 m along world +X from there.
            var relation = new PaseoMultiSetRelation(new Vector3(0f, 0f, 10f), Quaternion.identity, Size);
            PaseoMultiSetGeometry.ToWorld(relation, new Vector3(5f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), out var position, out var rotation);
            AssertVector(new Vector3(15f, 0f, 0f), position);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), rotation), 1e-3f);
        }

        [Test]
        public void FromWorldAndToWorldAreInverses()
        {
            var spacePosition = new Vector3(-3f, 0.4f, 7f);
            var spaceRotation = Quaternion.Euler(1f, 143f, -2f);
            var boxPosition = new Vector3(12f, 1.5f, -4f);
            var boxRotation = Quaternion.Euler(0f, 77f, 0f);
            var relation = PaseoMultiSetGeometry.FromWorld(spacePosition, spaceRotation, boxPosition, boxRotation, Size);
            PaseoMultiSetGeometry.ToWorld(relation, spacePosition, spaceRotation, out var position, out var rotation);
            AssertVector(boxPosition, position);
            Assert.AreEqual(0f, Quaternion.Angle(boxRotation, rotation), 1e-2f);
        }

        [Test]
        public void ARelationFollowsTheSpaceWhenMultiSetLocalizesAgain()
        {
            // First fix: the space is here and Immersal's box is there. Second fix: MultiSet moved the space; the saved relation puts the box
            // exactly where the same real box is seen from the new space pose.
            var relation = PaseoMultiSetGeometry.FromWorld(new Vector3(1f, 0f, 2f), Quaternion.Euler(0f, 20f, 0f), new Vector3(8f, 1f, 15f), Quaternion.Euler(0f, 50f, 0f), Size);
            var movedPosition = new Vector3(1.4f, 0f, 2.2f);
            var movedRotation = Quaternion.Euler(0f, 22f, 0f);
            PaseoMultiSetGeometry.ToWorld(relation, movedPosition, movedRotation, out var position, out var rotation);
            // The new space is 0.4 m and 2 degrees off the old one: the box is off by about that, plus the lever arm of its distance.
            float move = PaseoMultiSetGeometry.MaxCornerMove(position, rotation, new Vector3(8f, 1f, 15f), Quaternion.Euler(0f, 50f, 0f), Size);
            Assert.Greater(move, 0.3f);
            Assert.Less(move, 3f);
        }

        [Test]
        public void ATranslationMovesEveryCornerByTheSameDistance() =>
            Assert.AreEqual(1f, PaseoMultiSetGeometry.MaxCornerMove(Vector3.zero, Quaternion.identity, new Vector3(1f, 0f, 0f), Quaternion.identity, Size), 1e-4f);

        [Test]
        public void NoMoveIsZero() =>
            Assert.AreEqual(0f, PaseoMultiSetGeometry.MaxCornerMove(new Vector3(3f, 1f, 2f), Quaternion.Euler(0f, 10f, 0f), new Vector3(3f, 1f, 2f), Quaternion.Euler(0f, 10f, 0f), Size), 1e-5f);

        [Test]
        public void ATurnMovesTheFarthestCornerByItsChord()
        {
            // A 2 x 2 x 2 box turned 90 degrees about its own vertical: a corner sits sqrt(2) m from the axis, so it travels a chord of 2 m.
            float move = PaseoMultiSetGeometry.MaxCornerMove(Vector3.zero, Quaternion.identity, Vector3.zero, Quaternion.Euler(0f, 90f, 0f), new Vector3(2f, 2f, 2f));
            Assert.AreEqual(2f, move, 1e-4f);
        }

        [Test]
        public void ATurnOfAWideBoxMovesItsEndsMost()
        {
            // 20 m wide, 1 m deep: turning 10 degrees about the centre moves an end corner by 2 * r * sin(5 deg), r = sqrt(10^2 + 0.5^2).
            float radius = Mathf.Sqrt(10f * 10f + 0.5f * 0.5f);
            float move = PaseoMultiSetGeometry.MaxCornerMove(Vector3.zero, Quaternion.identity, Vector3.zero, Quaternion.Euler(0f, 10f, 0f), new Vector3(20f, 4f, 1f));
            Assert.AreEqual(2f * radius * Mathf.Sin(5f * Mathf.Deg2Rad), move, 1e-3f);
        }

        [Test]
        public void WithoutASavedRelationASolidImmersalBoxSavesOne()
        {
            var evaluation = PaseoMultiSetGeometry.Evaluate(false, default, true, new Vector3(8f, 1f, 15f), Quaternion.Euler(0f, 50f, 0f), Size,
                new Vector3(1f, 0f, 2f), Quaternion.Euler(0f, 20f, 0f));
            Assert.AreEqual(PaseoMultiSetRelationState.New, evaluation.State);
            Assert.IsTrue(evaluation.HasRelation);
            Assert.IsNaN(evaluation.DifferenceMeters, "saved from Immersal's own box: nothing to compare yet");
            PaseoMultiSetGeometry.ToWorld(evaluation.Relation, new Vector3(1f, 0f, 2f), Quaternion.Euler(0f, 20f, 0f), out var position, out _);
            AssertVector(new Vector3(8f, 1f, 15f), position);
        }

        [Test]
        public void WithoutARelationNorASolidImmersalBoxThereIsNothingToDo()
        {
            var evaluation = PaseoMultiSetGeometry.Evaluate(false, default, false, default, Quaternion.identity, Size, Vector3.zero, Quaternion.identity);
            Assert.AreEqual(PaseoMultiSetRelationState.NoImmersal, evaluation.State);
            Assert.IsFalse(evaluation.HasRelation);
            Assert.IsNaN(evaluation.DifferenceMeters);
        }

        [Test]
        public void ASavedRelationWithoutImmersalPlacesTheBoxButComparesNothing()
        {
            var saved = new PaseoMultiSetRelation(new Vector3(0f, 0f, 10f), Quaternion.identity, Size);
            var evaluation = PaseoMultiSetGeometry.Evaluate(true, saved, false, default, Quaternion.identity, Size, Vector3.zero, Quaternion.identity);
            Assert.AreEqual(PaseoMultiSetRelationState.Saved, evaluation.State);
            Assert.IsTrue(evaluation.HasRelation);
            Assert.IsNaN(evaluation.DifferenceMeters);
        }

        [Test]
        public void ASavedRelationAgainstAnImmersalBoxGivesTheirDifference()
        {
            // MultiSet's space sits at the origin; the relation puts the box 10 m ahead. Immersal has it 10.5 m ahead: half a metre apart.
            var saved = new PaseoMultiSetRelation(new Vector3(0f, 0f, 10f), Quaternion.identity, Size);
            var evaluation = PaseoMultiSetGeometry.Evaluate(true, saved, true, new Vector3(0f, 0f, 10.5f), Quaternion.identity, Size,
                Vector3.zero, Quaternion.identity);
            Assert.AreEqual(PaseoMultiSetRelationState.Saved, evaluation.State);
            Assert.AreEqual(0.5f, evaluation.DifferenceMeters, 1e-4f);
        }

        [Test]
        public void ASavedRelationThatMultiSetReproducesExactlyDiffersByZero()
        {
            var spacePosition = new Vector3(2f, 0f, -1f);
            var spaceRotation = Quaternion.Euler(0f, 40f, 0f);
            var saved = PaseoMultiSetGeometry.FromWorld(spacePosition, spaceRotation, new Vector3(9f, 1f, 12f), Quaternion.Euler(0f, 65f, 0f), Size);
            var evaluation = PaseoMultiSetGeometry.Evaluate(true, saved, true, new Vector3(9f, 1f, 12f), Quaternion.Euler(0f, 65f, 0f), Size,
                spacePosition, spaceRotation);
            Assert.AreEqual(0f, evaluation.DifferenceMeters, 1e-3f);
        }

        // ---------------------------------------------------------------- store

        PaseoMultiSetStore store;

        [SetUp]
        public void SetUp() => store = new PaseoMultiSetStore(Prefix);

        [TearDown]
        public void TearDown() => store.ClearAll();

        [Test]
        public void ARelationRoundTrips()
        {
            Assert.IsFalse(store.TryLoad("MAP_A", "Teologia", out _));
            var relation = new PaseoMultiSetRelation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 45f, 0f), new Vector3(22f, 8f, 10f));
            store.Save("MAP_A", "Teologia", relation);
            Assert.IsTrue(store.Has("MAP_A", "Teologia"));
            Assert.IsTrue(store.TryLoad("MAP_A", "Teologia", out var loaded));
            AssertVector(relation.Position, loaded.Position, 1e-5f);
            Assert.AreEqual(0f, Quaternion.Angle(relation.Rotation, loaded.Rotation), 1e-3f);
            AssertVector(relation.Size, loaded.Size, 1e-5f);
        }

        [Test]
        public void RelationsAreKeptPerMapCodeAndBuilding()
        {
            store.Save("MAP_A", "Teologia", new PaseoMultiSetRelation(Vector3.one, Quaternion.identity, Size));
            Assert.IsFalse(store.Has("MAP_B", "Teologia"));
            Assert.IsFalse(store.Has("MAP_A", "X1"));
            store.Save("MAP_A", "X1", new PaseoMultiSetRelation(Vector3.zero, Quaternion.identity, Size));
            Assert.AreEqual(2, store.Count);
        }

        [Test]
        public void SavingTheSamePairTwiceKeepsOneEntryAndTheNewValue()
        {
            store.Save("MAP_A", "Teologia", new PaseoMultiSetRelation(Vector3.one, Quaternion.identity, Size));
            store.Save("MAP_A", "Teologia", new PaseoMultiSetRelation(Vector3.one * 2f, Quaternion.identity, Size));
            Assert.AreEqual(1, store.Count);
            store.TryLoad("MAP_A", "Teologia", out var loaded);
            AssertVector(Vector3.one * 2f, loaded.Position, 1e-5f);
        }

        [Test]
        public void ForgettingClearsEveryRelationAndSaysHowMany()
        {
            store.Save("MAP_A", "Teologia", new PaseoMultiSetRelation(Vector3.one, Quaternion.identity, Size));
            store.Save("MAP_B", "X1", new PaseoMultiSetRelation(Vector3.one, Quaternion.identity, Size));
            Assert.AreEqual(2, store.ClearAll());
            Assert.IsFalse(store.Has("MAP_A", "Teologia"));
            Assert.IsFalse(store.Has("MAP_B", "X1"));
            Assert.AreEqual(0, store.Count);
            Assert.AreEqual(0, store.ClearAll());
        }

        [Test]
        public void TheDefaultPrefixIsVersioned() => StringAssert.StartsWith("AncoRA.Paseo.MultiSet.v1.", PaseoMultiSetStore.DefaultPrefix);
    }
}

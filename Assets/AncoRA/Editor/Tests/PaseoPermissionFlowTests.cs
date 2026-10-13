using System.Collections.Generic;
using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoPermissionFlowTests
    {
        sealed class FakeSystem : IPaseoPermissionSystem
        {
            public readonly HashSet<string> Granted = new();
            public readonly List<string> Requested = new();
            public bool Has(string permission) => Granted.Contains(permission);
            public void Request(string permission) => Requested.Add(permission);
        }

        [Test]
        public void AlreadyGrantedPermissionsFinishAtOnce()
        {
            var system = new FakeSystem { Granted = { PaseoPermissionFlow.Camera, PaseoPermissionFlow.Location } };
            var flow = new PaseoPermissionFlow(system);
            flow.Begin(0f);
            Assert.IsTrue(flow.Done);
            Assert.IsTrue(flow.CameraGranted && flow.LocationGranted);
            Assert.IsEmpty(system.Requested);
        }

        [Test]
        public void CameraThenLocationOneDialogAtATime()
        {
            var system = new FakeSystem();
            var flow = new PaseoPermissionFlow(system);
            flow.Begin(0f);
            CollectionAssert.AreEqual(new[] { PaseoPermissionFlow.Camera }, system.Requested);
            flow.Update(0.2f, focused: false); // the dialog took the focus
            system.Granted.Add(PaseoPermissionFlow.Camera);
            flow.Update(0.4f, focused: true);
            CollectionAssert.AreEqual(new[] { PaseoPermissionFlow.Camera, PaseoPermissionFlow.Location }, system.Requested);
            Assert.IsFalse(flow.Done);
            flow.Update(0.6f, focused: false);
            system.Granted.Add(PaseoPermissionFlow.Location);
            flow.Update(0.8f, focused: true);
            Assert.IsTrue(flow.Done);
            Assert.IsTrue(flow.CameraGranted && flow.LocationGranted);
        }

        [Test]
        public void WithoutTheCameraTheLocationIsNotAsked()
        {
            var system = new FakeSystem();
            var flow = new PaseoPermissionFlow(system);
            flow.Begin(0f);
            flow.Update(0.2f, focused: false);
            flow.Update(0.4f, focused: true); // denied
            Assert.IsTrue(flow.Done);
            Assert.IsFalse(flow.CameraGranted);
            CollectionAssert.AreEqual(new[] { PaseoPermissionFlow.Camera }, system.Requested);
        }

        [Test]
        public void ADeniedLocationStillFinishes()
        {
            var system = new FakeSystem { Granted = { PaseoPermissionFlow.Camera } };
            var flow = new PaseoPermissionFlow(system);
            flow.Begin(0f);
            flow.Update(0.2f, focused: false);
            flow.Update(0.4f, focused: true);
            Assert.IsTrue(flow.Done);
            Assert.IsTrue(flow.CameraGranted);
            Assert.IsFalse(flow.LocationGranted);
        }

        [Test]
        public void ARequestThatShowsNoDialogDoesNotHang()
        {
            // Android shows nothing for a permission denied for good: the focus never leaves.
            var system = new FakeSystem();
            var flow = new PaseoPermissionFlow(system);
            flow.Begin(0f);
            flow.Update(PaseoPermissionFlow.NoDialogSeconds * 0.5f, focused: true);
            Assert.IsFalse(flow.Done);
            flow.Update(PaseoPermissionFlow.NoDialogSeconds, focused: true);
            Assert.IsTrue(flow.Done);
            Assert.IsFalse(flow.CameraGranted);
        }
    }
}

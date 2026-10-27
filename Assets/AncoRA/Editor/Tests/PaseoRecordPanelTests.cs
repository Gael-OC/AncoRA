using System.Collections.Generic;
using System.IO;
using System.Linq;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace AncorRA.Tests
{
    public class PaseoRecordPanelTests
    {
        PaseoRecordSelection selection;
        PaseoRecordPanel panel;
        readonly List<bool> toggles = new();
        readonly List<string> notices = new();
        int deletes;

        [SetUp]
        public void SetUp()
        {
            toggles.Clear();
            notices.Clear();
            deletes = 0;
            selection = new PaseoRecordSelection();
            panel = new PaseoRecordPanel(selection);
            panel.SetBuildings(new[] { "CienciasBasicas", "EIC", "Teologia", "X1" });
            panel.ToggleRequested += toggles.Add;
            panel.DeleteRequested += () => deletes++;
            panel.Notice += notices.Add;
        }

        static Button ButtonNamed(VisualElement root, string text) => root.Query<Button>().Where(b => b.text == text).First();

        [Test]
        public void TheBuildingsAreOfferedByShortCode()
        {
            var row = panel.Root.Q("grabar-edificios");
            CollectionAssert.AreEqual(new[] { "CB", "EIC", "TEO", "X1" }, row.Query<Button>().ToList().ConvertAll(b => b.text));
        }

        [Test]
        public void PickingBuildingPointAndLightNamesThePointAndMarksTheButtons()
        {
            Assert.IsTrue(panel.Pick("TEO"));
            Assert.IsTrue(panel.Pick("D"));
            Assert.IsTrue(panel.Pick("nublado"));
            Assert.AreEqual("TEO-D-30m", selection.Label);
            Assert.AreEqual("TEO-D-30m · nublado", panel.Root.Q<Label>("grabar-punto").text);
            Assert.IsTrue(ButtonNamed(panel.Root, "TEO").ClassListContains(PaseoRecordPanel.OnClass));
            Assert.IsFalse(ButtonNamed(panel.Root, "X1").ClassListContains(PaseoRecordPanel.OnClass));
            Assert.IsTrue(ButtonNamed(panel.Root, "30 m").ClassListContains(PaseoRecordPanel.OnClass), "the usual distance of D");
            Assert.IsFalse(ButtonNamed(panel.Root, "15 m").ClassListContains(PaseoRecordPanel.OnClass));
        }

        [Test]
        public void ADistancePickedAfterThePointOverridesTheUsualOne()
        {
            panel.Pick("A");
            panel.Pick("50 m");
            Assert.AreEqual(50, selection.Meters);
        }

        [Test]
        public void TheSwitchAsksForTheOppositeOfItsCurrentState()
        {
            panel.PressToggle();
            panel.SetState(true, "3 imágenes");
            panel.PressToggle();
            CollectionAssert.AreEqual(new[] { true, false }, toggles);
            Assert.AreEqual("3 imágenes", panel.Root.Q<Label>("grabar-estado").text);
            Assert.IsTrue(panel.Root.Q<Button>("grabar-interruptor").ClassListContains("paseo-button--recording"));
        }

        [Test]
        public void DeletingNeedsASecondTapWithinAFewSeconds()
        {
            panel.PressDelete(10f);
            Assert.AreEqual(0, deletes);
            Assert.AreEqual(1, notices.Count);
            panel.PressDelete(10f + PaseoRecordPanel.DeleteConfirmSeconds + 1f);
            Assert.AreEqual(0, deletes, "too late: it asks again");
            panel.PressDelete(10f + PaseoRecordPanel.DeleteConfirmSeconds + 2f);
            Assert.AreEqual(1, deletes);
        }

        [Test]
        public void TheSheetAddsTheSectionOnceAndAboveTheState()
        {
            var sheet = new PaseoTeamSheet();
            Assert.IsNull(sheet.Record);
            var first = sheet.AttachRecorder(selection);
            Assert.AreSame(first, sheet.AttachRecorder(selection));
            var scroll = sheet.Root.Q<ScrollView>();
            Assert.AreSame(first.Root, scroll.contentContainer[0]);
        }
    }

    public class PaseoRecordingsTests
    {
        string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "ancora-grabaciones-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "20261028-090000"));
            Directory.CreateDirectory(Path.Combine(root, "20261028-140000"));
            File.WriteAllBytes(Path.Combine(root, "20261028-090000", "0001-X1-A-15m.png"), new byte[1000]);
            File.WriteAllText(Path.Combine(root, "20261028-090000", PaseoRecorder.MetaFile), "{}\n");
            File.WriteAllBytes(Path.Combine(root, "20261028-140000", "0001-EIC-B-15m.png"), new byte[500]);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public void ItAddsUpTheSessions()
        {
            Assert.AreEqual(2, PaseoRecordings.Sessions(root).Count);
            Assert.AreEqual(1503, PaseoRecordings.TotalBytes(root));
            Assert.AreEqual(0, PaseoRecordings.Sessions(Path.Combine(root, "nada")).Count);
        }

        [Test]
        public void ASecondCopySkipsWhatWasCopiedButTakesTheMetadataThatGrew()
        {
            string session = Path.Combine(root, "20261028-090000");
            var done = new HashSet<string>();
            Assert.AreEqual(2, new List<string>(PaseoRecordings.FilesToCopy(session, done)).Count);
            foreach (string file in PaseoRecordings.FilesToCopy(session, done).ToArray())
                done.Add(PaseoRecordings.CopyKey(file));
            Assert.AreEqual(0, new List<string>(PaseoRecordings.FilesToCopy(session, done)).Count);
            File.AppendAllText(Path.Combine(session, PaseoRecorder.MetaFile), "{\"n\":2}\n");
            var again = new List<string>(PaseoRecordings.FilesToCopy(session, done));
            Assert.AreEqual(1, again.Count);
            Assert.AreEqual(PaseoRecorder.MetaFile, Path.GetFileName(again[0]));
        }

        [Test]
        public void TheListOfCopiedNamesIsNeverCopied()
        {
            string session = Path.Combine(root, "20261028-090000");
            File.WriteAllText(Path.Combine(session, PaseoRecordings.CopiedFile), "x");
            foreach (string file in PaseoRecordings.FilesToCopy(session, new HashSet<string>()))
                Assert.AreNotEqual(PaseoRecordings.CopiedFile, Path.GetFileName(file));
        }

        [Test]
        public void DeletingRemovesEverySessionAndReportsTheBytes()
        {
            Assert.AreEqual(1503, PaseoRecordings.DeleteAll(root));
            Assert.AreEqual(0, PaseoRecordings.Sessions(root).Count);
        }

        [Test]
        public void SizesReadInMegabytesOrGigabytes()
        {
            Assert.AreEqual("12 MB", PaseoRecordings.SizeText(12L * 1024 * 1024 + 5));
            Assert.AreEqual("1.5 GB", PaseoRecordings.SizeText(1610612736L));
        }
    }
}

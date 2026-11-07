using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AncorRA.AR;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    /// <summary>The closing of a recording folder: final count after the last write, manifest, rows right after images, copy check, walk.</summary>
    public class PaseoRecorderManifestTests
    {
        string folder;
        PaseoRecorder recorder;
        readonly ConcurrentQueue<string> logs = new();

        void OnLog(string message, string stack, LogType type) => logs.Enqueue(message);

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "ancora-manifest-" + Guid.NewGuid().ToString("N"));
            recorder = new PaseoRecorder(folder, () => 10L * 1024 * 1024 * 1024);
            recorder.Selection.SetBuilding("TEO");
            recorder.Selection.SetPoint("B");
            recorder.Selection.SetMeters(10);
            recorder.Selection.SetLight("nublado");
            logs.Clear();
            Application.logMessageReceivedThreaded += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        static PaseoFrame Frame(int seed)
        {
            var pixels = new byte[64 * 48];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = (byte)(i * 31 + seed * 17 + i / 64);
            return new PaseoFrame { Pixels = pixels, Width = 64, Height = 48, Channels = 1, CameraRotation = Quaternion.identity, ScreenOrientation = Quaternion.identity };
        }

        static PaseoRecordMeta Meta(int n) => new()
        {
            Attempt = n, File = PaseoRecorder.FileName(n, "TEO-B-10m"), Time = new DateTime(2026, 11, 5, 10, 0, 0), Session = "barrido-test",
            Point = "TEO-B-10m", Building = "TEO", Light = "nublado", Width = 64, Height = 48, Channels = 1, Rmse = double.NaN, GpsDistance = double.NaN
        };

        string[] Lines() => File.ReadAllLines(Path.Combine(folder, PaseoRecorder.MetaFile));

        static void WaitUntil(Func<bool> condition, string what)
        {
            var until = DateTime.UtcNow.AddSeconds(10);
            while (!condition() && DateTime.UtcNow < until)
                Thread.Sleep(5);
            Assert.IsTrue(condition(), what);
        }

        bool Logged(string fragment) => logs.Any(l => l.Contains(fragment));

        void Record(int count, string kind = "barrido-arcore")
        {
            recorder.CaptureKind = kind;
            Assert.IsTrue(recorder.TryEnable(0f, out string reason), reason);
            for (int i = 1; i <= count; i++)
                recorder.Save(Frame(i), Meta(i)).GetAwaiter().GetResult();
        }

        // ---- the closing waits for the writes ----

        [Test]
        public void TheClosingLinesWaitForTheLastPendingWritesAndCountThem()
        {
            using var release = new ManualResetEventSlim(false);
            recorder.BeforeWrite = () => release.Wait(10000);
            Assert.IsTrue(recorder.TryEnable(0f, out _));
            var tasks = Enumerable.Range(1, 3).Select(i => recorder.Save(Frame(i), Meta(i))).ToArray();
            recorder.Disable();
            Assert.IsFalse(recorder.Enabled);
            Assert.IsTrue(recorder.Writing);
            Assert.IsFalse(Logged("GRABACION_FIN"), "nothing is final while writes are pending");
            Assert.IsFalse(File.Exists(Path.Combine(folder, PaseoRecordManifest.FileName)));
            release.Set();
            System.Threading.Tasks.Task.WaitAll(tasks);
            WaitUntil(() => recorder.Finishes == 1, "the closing runs once the writes end");
            Assert.IsTrue(Logged("GRABACION_FIN imagenes=3 "), string.Join("\n", logs));
            Assert.IsTrue(Logged("GRABACION_COMPLETA imagenes=3 omitidas=0 bytes="));
            Assert.AreEqual(3, (int)JObject.Parse(File.ReadAllText(Path.Combine(folder, PaseoRecordManifest.FileName)))["imagenes"]);
        }

        [Test]
        public void DisablingWithNothingPendingClosesAtOnce()
        {
            Record(2);
            recorder.Disable();
            Assert.AreEqual(1, recorder.Finishes);
            Assert.IsTrue(Logged("GRABACION_FIN imagenes=2 "));
            Assert.IsTrue(Logged("GRABACION_COMPLETA imagenes=2 omitidas=0"));
        }

        [Test]
        public void ItClosesOncePerStop()
        {
            Record(1);
            recorder.Disable();
            recorder.Disable();
            Assert.AreEqual(1, recorder.Finishes);
            Assert.AreEqual(1, logs.Count(l => l.Contains("GRABACION_FIN")));
        }

        // ---- the manifest ----

        [Test]
        public void TheManifestListsEveryFileWithSizeAndSha256()
        {
            Record(3);
            recorder.Disable();
            var json = JObject.Parse(File.ReadAllText(Path.Combine(folder, PaseoRecordManifest.FileName)));
            Assert.AreEqual(Path.GetFileName(folder), (string)json["carpeta"]);
            Assert.AreEqual("barrido-arcore", (string)json["tipo_captura"]);
            Assert.AreEqual(3, (int)json["imagenes"]);
            Assert.AreEqual(3, (int)json["filas_intentos"]);
            Assert.AreEqual(0, (int)json["omitidas"]);
            Assert.IsTrue((bool)json["completo"]);
            Assert.AreEqual("", (string)json["motivo"]);
            Assert.IsNull(json["recorrido_m"], "a sweep does not measure the distance walked");
            var files = (JArray)json["archivos"];
            Assert.AreEqual(3, files.Count);
            foreach (var entry in files)
            {
                string path = Path.Combine(folder, (string)entry["archivo"]);
                Assert.AreEqual(new FileInfo(path).Length, (long)entry["bytes"]);
                Assert.AreEqual(PaseoRecordManifest.Sha256HexOfFile(path), (string)entry["sha256"]);
                Assert.AreEqual(64, ((string)entry["sha256"]).Length);
            }
            var rows = json["intentos"];
            Assert.AreEqual(PaseoRecorder.MetaFile, (string)rows["archivo"]);
            Assert.AreEqual(new FileInfo(Path.Combine(folder, PaseoRecorder.MetaFile)).Length, (long)rows["bytes"]);
            Assert.AreEqual(PaseoRecordManifest.Sha256HexOfFile(Path.Combine(folder, PaseoRecorder.MetaFile)), (string)rows["sha256"]);
        }

        [Test]
        public void TheSha256IsTheKnownOneForAKnownInput()
        {
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                PaseoRecordManifest.Sha256Hex(System.Text.Encoding.ASCII.GetBytes("abc")));
        }

        [Test]
        public void AStopWithANoticeWritesAnIncompleteManifestWithTheReason()
        {
            Record(2);
            recorder.Disable("Se perdió el seguimiento; repetir el barrido.");
            var json = JObject.Parse(File.ReadAllText(Path.Combine(folder, PaseoRecordManifest.FileName)));
            Assert.IsFalse((bool)json["completo"]);
            StringAssert.Contains("seguimiento", (string)json["motivo"]);
            Assert.AreEqual(2, (int)json["imagenes"]);
            Assert.AreEqual(PaseoManifestVerdict.Incomplete, PaseoRecordManifest.Verify(folder).Verdict);
        }

        [Test]
        public void TheManifestCarriesTheWalkedDistanceAndFlagsAShortWalk()
        {
            Record(2, "recorrido-arcore");
            recorder.SetTravel(3.25, true);
            recorder.Disable();
            var json = JObject.Parse(File.ReadAllText(Path.Combine(folder, PaseoRecordManifest.FileName)));
            Assert.AreEqual("recorrido-arcore", (string)json["tipo_captura"]);
            Assert.AreEqual(3.25, (double)json["recorrido_m"], 1e-6);
            Assert.IsTrue((bool)json["recorrido_corto"]);
            Assert.IsTrue((bool)json["completo"], "a short walk was saved whole; it is the notice that tells it is useless");
        }

        [Test]
        public void ALongWalkIsNotShort()
        {
            Record(1, "recorrido-arcore");
            recorder.SetTravel(18.5, false);
            recorder.Disable();
            var json = JObject.Parse(File.ReadAllText(Path.Combine(folder, PaseoRecordManifest.FileName)));
            Assert.IsFalse((bool)json["recorrido_corto"]);
        }

        [Test]
        public void DroppedFramesAreCountedButDoNotMakeTheFolderIncomplete()
        {
            using var release = new ManualResetEventSlim(false);
            recorder.BeforeWrite = () => release.Wait(10000);
            Assert.IsTrue(recorder.TryEnable(0f, out _));
            var tasks = Enumerable.Range(1, PaseoRecorder.MaxPending).Select(i => recorder.Save(Frame(i), Meta(i))).ToArray();
            Assert.IsFalse(recorder.WantsFrame(1f));
            recorder.Disable();
            release.Set();
            System.Threading.Tasks.Task.WaitAll(tasks);
            WaitUntil(() => recorder.Finishes == 1, "closing");
            var json = JObject.Parse(File.ReadAllText(Path.Combine(folder, PaseoRecordManifest.FileName)));
            Assert.AreEqual(1, (int)json["omitidas"]);
            Assert.IsTrue((bool)json["completo"]);
            Assert.IsTrue(Logged("omitidas=1"));
        }

        [Test]
        public void TheManifestIsWrittenToATemporaryFileAndRenamed()
        {
            Record(1);
            recorder.Disable();
            Assert.IsFalse(File.Exists(Path.Combine(folder, PaseoRecordManifest.FileName + PaseoRecordManifest.TemporarySuffix)));
            // Writing again over an existing manifest also leaves no temporary file and the new content.
            var data = new PaseoManifestData { Folder = "x", CaptureKind = "k", Complete = true, Rows = new PaseoManifestFile(PaseoRecorder.MetaFile, 0, "") };
            PaseoRecordManifest.Write(folder, data);
            Assert.IsFalse(File.Exists(Path.Combine(folder, PaseoRecordManifest.FileName + PaseoRecordManifest.TemporarySuffix)));
            Assert.AreEqual("k", (string)JObject.Parse(File.ReadAllText(Path.Combine(folder, PaseoRecordManifest.FileName)))["tipo_captura"]);
        }

        [Test]
        public void AStopWithoutImagesWritesNoManifest()
        {
            Assert.IsTrue(recorder.TryEnable(0f, out _));
            recorder.Disable();
            Assert.IsFalse(File.Exists(Path.Combine(folder, PaseoRecordManifest.FileName)));
            Assert.IsTrue(Logged("GRABACION_COMPLETA imagenes=0"));
        }

        [Test]
        public void ResettingForgetsTheListedFiles()
        {
            Record(2);
            recorder.Disable();
            recorder.ResetCounts();
            Directory.Delete(folder, true);
            Record(1);
            recorder.Disable();
            Assert.AreEqual(1, (int)JObject.Parse(File.ReadAllText(Path.Combine(folder, PaseoRecordManifest.FileName)))["imagenes"]);
        }

        // ---- rows right after their image ----

        [Test]
        public void EachRowIsOnDiskRightAfterItsImageAndNeverTheOtherWayAround()
        {
            var seen = new List<(bool Image, int Rows)>();
            int index = 0;
            recorder.AfterImage = () =>
            {
                index++;
                string image = Path.Combine(folder, PaseoRecorder.FileName(index, "TEO-B-10m"));
                string rows = Path.Combine(folder, PaseoRecorder.MetaFile);
                // The image is complete on disk and its row is not there yet: rows never run ahead of images.
                seen.Add((File.Exists(image), File.Exists(rows) ? File.ReadAllLines(rows).Length : 0));
            };
            Record(4);
            CollectionAssert.AreEqual(new[] { (true, 0), (true, 1), (true, 2), (true, 3) }, seen);
            var lines = Lines();
            Assert.AreEqual(4, lines.Length);
            Assert.AreEqual(4, Directory.GetFiles(folder, "*.png").Length);
            for (int i = 0; i < lines.Length; i++)
            {
                var row = JObject.Parse(lines[i]);
                Assert.AreEqual(i + 1, (int)row["n"]);
                Assert.IsTrue(File.Exists(Path.Combine(folder, (string)row["archivo"])), "every row has its image");
            }
        }

        [Test]
        public void AnImageWhoseRowCannotBeWrittenIsRemoved()
        {
            Directory.CreateDirectory(folder);
            // A directory where the metadata file should be: the append fails after the image was written.
            Directory.CreateDirectory(Path.Combine(folder, PaseoRecorder.MetaFile));
            Assert.IsTrue(recorder.TryEnable(0f, out _));
            Assert.DoesNotThrow(() => recorder.Save(Frame(1), Meta(1)).GetAwaiter().GetResult());
            Assert.AreEqual(0, Directory.GetFiles(folder, "*.png").Length, "no image without a row");
            Assert.AreEqual(0, recorder.Saved);
        }

        // ---- verifying a folder against its manifest ----

        [Test]
        public void ACompleteFolderVerifies()
        {
            Record(3);
            recorder.Disable();
            Assert.AreEqual(PaseoManifestVerdict.Complete, PaseoRecordManifest.Verify(folder).Verdict);
        }

        [Test]
        public void AFolderWithoutManifestIsReportedAsSuch()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "0001-X.png"), new byte[10]);
            Assert.AreEqual(PaseoManifestVerdict.NoManifest, PaseoRecordManifest.Verify(folder).Verdict);
        }

        [Test]
        public void AMissingImageMakesItIncomplete()
        {
            Record(3);
            recorder.Disable();
            File.Delete(Path.Combine(folder, PaseoRecorder.FileName(2, "TEO-B-10m")));
            var check = PaseoRecordManifest.Verify(folder);
            Assert.AreEqual(PaseoManifestVerdict.Incomplete, check.Verdict);
            StringAssert.Contains("falta 0002", check.Problems);
        }

        [Test]
        public void ATruncatedImageMakesItIncomplete()
        {
            Record(2);
            recorder.Disable();
            string path = Path.Combine(folder, PaseoRecorder.FileName(1, "TEO-B-10m"));
            File.WriteAllBytes(path, File.ReadAllBytes(path).Take(20).ToArray());
            Assert.AreEqual(PaseoManifestVerdict.Incomplete, PaseoRecordManifest.Verify(folder).Verdict);
        }

        [Test]
        public void ACorruptedImageOfTheSameSizeIsOnlyCaughtByTheHash()
        {
            Record(2);
            recorder.Disable();
            string path = Path.Combine(folder, PaseoRecorder.FileName(1, "TEO-B-10m"));
            var bytes = File.ReadAllBytes(path);
            bytes[bytes.Length / 2] ^= 0xFF;
            File.WriteAllBytes(path, bytes);
            Assert.AreEqual(PaseoManifestVerdict.Complete, PaseoRecordManifest.Verify(folder, hash: false).Verdict, "same size");
            var check = PaseoRecordManifest.Verify(folder);
            Assert.AreEqual(PaseoManifestVerdict.Incomplete, check.Verdict);
            StringAssert.Contains("SHA-256", check.Problems);
        }

        [Test]
        public void AnImageNotInTheManifestMakesItIncomplete()
        {
            Record(1);
            recorder.Disable();
            File.WriteAllBytes(Path.Combine(folder, "0099-extra.png"), new byte[5]);
            StringAssert.Contains("0099-extra.png", PaseoRecordManifest.Verify(folder).Problems);
        }

        [Test]
        public void MissingRowsMakeItIncomplete()
        {
            Record(3);
            recorder.Disable();
            string rows = Path.Combine(folder, PaseoRecorder.MetaFile);
            File.WriteAllLines(rows, File.ReadAllLines(rows).Take(2));
            var check = PaseoRecordManifest.Verify(folder, hash: false);
            Assert.AreEqual(PaseoManifestVerdict.Incomplete, check.Verdict);
        }

        [Test]
        public void ALostMetadataFileMakesItIncomplete()
        {
            Record(2);
            recorder.Disable();
            File.Delete(Path.Combine(folder, PaseoRecorder.MetaFile));
            StringAssert.Contains(PaseoRecorder.MetaFile, PaseoRecordManifest.Verify(folder).Problems);
        }

        [Test]
        public void AnUnreadableManifestIsIncompleteNotMissing()
        {
            Record(1);
            recorder.Disable();
            File.WriteAllText(Path.Combine(folder, PaseoRecordManifest.FileName), "{ no es json");
            Assert.AreEqual(PaseoManifestVerdict.Incomplete, PaseoRecordManifest.Verify(folder).Verdict);
        }

        [Test]
        public void TheCopyBookkeepingAndTemporaryFilesAreNotExtraFiles()
        {
            Record(1);
            recorder.Disable();
            File.WriteAllText(Path.Combine(folder, PaseoRecordings.CopiedFile), "x");
            File.WriteAllText(Path.Combine(folder, PaseoRecordManifest.FileName + PaseoRecordManifest.TemporarySuffix), "x");
            Assert.AreEqual(PaseoManifestVerdict.Complete, PaseoRecordManifest.Verify(folder).Verdict);
            CollectionAssert.DoesNotContain(PaseoRecordings.FilesToCopy(folder, new HashSet<string>()).Select(Path.GetFileName), PaseoRecordings.CopiedFile);
            CollectionAssert.DoesNotContain(PaseoRecordings.FilesToCopy(folder, new HashSet<string>()).Select(Path.GetFileName),
                PaseoRecordManifest.FileName + PaseoRecordManifest.TemporarySuffix);
            CollectionAssert.Contains(PaseoRecordings.FilesToCopy(folder, new HashSet<string>()).Select(Path.GetFileName), PaseoRecordManifest.FileName);
        }

        // ---- the check after copying ----

        [Test]
        public void TheCopyReportCountsCompleteIncompleteAndWithoutManifest()
        {
            Record(2);
            recorder.Disable();
            string root = Path.Combine(Path.GetTempPath(), "ancora-copia-" + Guid.NewGuid().ToString("N"));
            try
            {
                string complete = Path.Combine(root, "barrido-1");
                string incomplete = Path.Combine(root, "barrido-2");
                string none = Path.Combine(root, "viejo-3");
                CopyDirectory(folder, complete);
                CopyDirectory(folder, incomplete);
                File.Delete(Path.Combine(incomplete, PaseoRecorder.FileName(1, "TEO-B-10m")));
                Directory.CreateDirectory(none);
                File.WriteAllBytes(Path.Combine(none, "0001.png"), new byte[3]);
                var report = PaseoRecordManifest.CompareFolders(PaseoRecordings.Sessions(root));
                Assert.AreEqual(3, report.Folders);
                Assert.AreEqual(1, report.Complete);
                Assert.AreEqual(1, report.Incomplete);
                Assert.AreEqual(1, report.NoManifest);
                Assert.AreEqual("COPIA_GRABACIONES carpetas=3 completas=1 incompletas=1 sin_manifiesto=1", report.LogLine());
                StringAssert.Contains("barrido-2", report.Notice());
                StringAssert.Contains("1 incompletas", report.Notice());
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void AFolderWhoseFilesDidNotAllReachDescargasCountsAsIncomplete()
        {
            Record(1);
            recorder.Disable();
            var report = PaseoRecordManifest.CompareFolders(new[] { folder }, new HashSet<string> { folder });
            Assert.AreEqual(0, report.Complete);
            Assert.AreEqual(1, report.Incomplete);
        }

        [Test]
        public void TheCopyCheckRunsOffTheMainThreadAndGivesTheSameReport()
        {
            Record(2);
            recorder.Disable();
            var report = PaseoRecordings.CompareWithManifestsAsync(new[] { folder }).GetAwaiter().GetResult();
            Assert.AreEqual("COPIA_GRABACIONES carpetas=1 completas=1 incompletas=0 sin_manifiesto=0", report.LogLine());
        }

        [Test]
        public void CopyingOutsideThePhoneStillReportsTheCheck()
        {
            Record(1);
            recorder.Disable();
            string root = Path.Combine(Path.GetTempPath(), "ancora-copia-" + Guid.NewGuid().ToString("N"));
            try
            {
                CopyDirectory(folder, Path.Combine(root, "barrido-1"));
                // Off the main thread: Unity's synchronization context would never run the continuation of a blocked wait.
                string text = System.Threading.Tasks.Task.Run(() => PaseoRecordings.CopyToDownloadsAsync(root, null)).GetAwaiter().GetResult();
                StringAssert.Contains("1 carpetas: 1 completas, 0 incompletas, 0 sin manifiesto", text);
                Assert.IsTrue(Logged("COPIA_GRABACIONES carpetas=1 completas=1 incompletas=0 sin_manifiesto=0"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            StringAssert.Contains("No hay grabaciones", System.Threading.Tasks.Task.Run(() => PaseoRecordings.CopyToDownloadsAsync(root, null)).GetAwaiter().GetResult());
        }

        static void CopyDirectory(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        // ---- the walk ----

        [Test]
        public void TheRowsOfAWalkCarryTheDistanceWalkedSoFar()
        {
            var meta = Meta(1);
            meta.CaptureKind = "recorrido-arcore";
            meta.TravelMeters = 4.5;
            Assert.AreEqual(4.5, (double)JObject.Parse(PaseoRecorder.MetaLine(meta))["recorrido_m"], 1e-9);
            meta.TravelMeters = null;
            Assert.IsNull(JObject.Parse(PaseoRecorder.MetaLine(meta))["recorrido_m"]);
        }

        [Test]
        public void TheWalkIsSixtySecondsAtTwoImagesPerSecondAndTheSweepStaysTheSame()
        {
            Assert.AreEqual("recorrido-arcore", PaseoCaptureMode.Walk.Kind);
            Assert.AreEqual(60f, PaseoCaptureMode.Walk.Duration);
            Assert.AreEqual(0.5f, PaseoCaptureMode.Walk.Interval);
            Assert.IsTrue(PaseoCaptureMode.Walk.TracksTravel);
            Assert.AreEqual("barrido-arcore", PaseoCaptureMode.Sweep.Kind);
            Assert.AreEqual(PaseoSequenceCapture.Duration, PaseoCaptureMode.Sweep.Duration);
            Assert.AreEqual(PaseoSequenceCapture.Interval, PaseoCaptureMode.Sweep.Interval);
            Assert.IsFalse(PaseoCaptureMode.Sweep.TracksTravel);
        }

        [Test]
        public void TheWalkKeepsTheTenToTwentyFiveMeterRange()
        {
            var host = new GameObject("Recorrido de prueba");
            try
            {
                var capture = host.AddComponent<PaseoSequenceCapture>();
                var selection = new PaseoRecordSelection();
                selection.SetBuilding("TEO");
                selection.SetPoint("A");
                selection.SetLight("sol");
                selection.SetMeters(30);
                Assert.IsFalse(capture.Begin(selection, PaseoCaptureMode.Walk, out string reason));
                StringAssert.Contains("recorrido", reason);
                StringAssert.Contains("10 y 25", reason);
                Assert.IsFalse(capture.Busy);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void TheTravelTrackerAddsTheStepsAndSkipsTrackingJumps()
        {
            var tracker = new PaseoTravelTracker();
            tracker.Add(Vector3.zero);
            tracker.Add(new Vector3(0, 0, 1.5f));
            tracker.Add(new Vector3(0, 0, 3f));
            Assert.AreEqual(3.0, tracker.Meters, 1e-6);
            Assert.IsTrue(tracker.Short);
            tracker.Add(new Vector3(0, 0, 30f));
            Assert.AreEqual(3.0, tracker.Meters, 1e-6, "a 27 m step is a relocalization jump, not walking");
            Assert.AreEqual(1, tracker.Jumps);
            tracker.Add(new Vector3(0, 0, 31.5f));
            tracker.Add(new Vector3(0, 0, 33f));
            Assert.AreEqual(6.0, tracker.Meters, 1e-6);
            Assert.IsFalse(tracker.Short, "6 m is enough for a construction walk");
        }

        [Test]
        public void FiveMetersExactlyIsEnoughAndTheTrackerCanBeReset()
        {
            var tracker = new PaseoTravelTracker();
            tracker.Add(Vector3.zero);
            for (int i = 1; i <= 5; i++)
                tracker.Add(new Vector3(i, 0, 0));
            Assert.AreEqual(5.0, tracker.Meters, 1e-6);
            Assert.IsFalse(tracker.Short);
            tracker.Reset();
            Assert.AreEqual(0.0, tracker.Meters);
            Assert.AreEqual(0, tracker.Jumps);
            tracker.Add(new Vector3(100, 0, 0));
            Assert.AreEqual(0.0, tracker.Meters, "the first position after a reset is only a starting point");
        }
    }
}

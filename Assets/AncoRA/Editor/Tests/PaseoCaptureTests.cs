using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace AncorRA.Tests
{
    public class PaseoCaptureTests
    {
        string root;

        [SetUp]
        public void SetUp() => Directory.CreateDirectory(root = Path.Combine(Path.GetTempPath(), "ancora-paquete-" + Guid.NewGuid().ToString("N")));

        [TearDown]
        public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

        [Test]
        public void GuidedRecorderRequiresLightAndLocksInputsDuringCaptureExportAndPlayback()
        {
            var selection = new PaseoRecordSelection();
            var view = new PaseoCaptureView(selection);
            Assert.AreEqual("TEO", selection.Building);
            Assert.IsTrue(view.WithVideo);
            view.SetState(false, false, true);
            Assert.IsFalse(view.Root.Q<Button>("captura-grabar").enabledSelf);
            selection.SetLight("sol");
            view.SetState(false, false, true);
            Assert.IsTrue(view.Root.Q<Button>("captura-grabar").enabledSelf);
            view.SetState(true, false, true);
            Assert.IsFalse(view.Root.Q<Button>("captura-copiar").enabledSelf);
            view.SetState(false, true, true);
            Assert.IsFalse(view.Root.Q<Button>("captura-grabar").enabledSelf);
            Assert.IsFalse(view.Root.Q<Button>("captura-copiar").enabledSelf);
            view.Select(Array.FindIndex(PaseoCapturePlan.Shots, s => s.Id == "08-consulta-25"));
            Assert.AreEqual(25, selection.Meters);
            Assert.AreEqual("consulta", view.Shot.Role);
            Assert.AreEqual(PaseoCapturePlan.Shots.Length, PaseoCapturePlan.Shots.Select(s => s.Id).Distinct().Count());
        }

        [Test]
        public void SessionWithMissingOrUnclosedVideoCannotBeCountedCompleteAndAllFilesTravelTogether()
        {
            var session = new PaseoCaptureSession { estado = "completa", fotos_guardadas = 3, video_solicitado = true };
            Assert.IsFalse(session.Complete);
            session.video_cerrado = true;
            session.Save(root);
            Assert.IsTrue(JsonUtility.FromJson<PaseoCaptureSession>(File.ReadAllText(Path.Combine(root, "sesion.json"))).Complete);
            File.WriteAllBytes(Path.Combine(root, PaseoSessionVideo.FileName), new byte[] { 1, 2, 3 });
            File.WriteAllText(Path.Combine(root, PaseoRecorder.MetaFile), "{\"n\":1}\n");
            File.WriteAllText(Path.Combine(root, "0001.png"), "png");
            File.WriteAllText(Path.Combine(root, "registro.log"), "log");
            File.WriteAllText(Path.Combine(root, "sesion.json.tmp"), "partial");
            File.WriteAllText(Path.Combine(root, PaseoRecordings.CopiedFile), "receipt");
            PaseoRecordings.WriteChecksums(root);
            var files = PaseoRecordings.FilesToCopy(root, new System.Collections.Generic.HashSet<string>()).Select(Path.GetFileName).ToArray();
            CollectionAssert.AreEquivalent(new[] { "sesion.json", "sesion-arcore.mp4", "intentos.jsonl", "0001.png", "registro.log", "sha256.txt" }, files);
            using var sha = SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(new byte[] { 1, 2, 3 })).Replace("-", "").ToLowerInvariant();
            StringAssert.Contains(hash + "  sesion-arcore.mp4", File.ReadAllText(Path.Combine(root, "sha256.txt")));
            Assert.IsFalse(File.ReadAllText(Path.Combine(root, "sha256.txt")).Contains(".tmp"));
        }

        [Test]
        public void CopyBundlesLogsFromPreviousLaunchesWithoutRemovingSources()
        {
            string logs = Path.Combine(root, "logs");
            string recordings = Path.Combine(root, "grabaciones");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "paseo-anterior.log"), "previous launch");
            File.WriteAllText(Path.Combine(logs, "paseo-actual.log"), "current launch");
            PaseoRecordings.AttachLogsAsync(recordings, logs, "ahora", "phone").GetAwaiter().GetResult();
            string bundle = Path.Combine(recordings, "registros-ahora");
            Assert.AreEqual("previous launch", File.ReadAllText(Path.Combine(bundle, "paseo-anterior.log")));
            Assert.IsTrue(File.Exists(Path.Combine(bundle, "telefono.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(bundle, "LEEME.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(logs, "paseo-anterior.log")));
        }

        [Test]
        public void InterruptedTakeIsPreservedButNotOfferedAsCompleted()
        {
            string take = Path.Combine(root, "toma");
            Directory.CreateDirectory(take);
            new PaseoCaptureSession { estado = "interrumpida", toma = PaseoCapturePlan.Shots[0].Id,
                edificio = "TEO", fotos_guardadas = 10 }.Save(take);
            var view = new PaseoCaptureView(new PaseoRecordSelection());
            view.RefreshSaved(root);
            Assert.IsTrue(File.Exists(Path.Combine(take, "sesion.json")));
            Assert.IsFalse(view.Root.Query<Label>().ToList().Any(l => l.text.Contains("· guardada")));
        }

        [Test]
        public void FinishingARealImageWriteClosesTheManifestAndReportsSuccess()
        {
            var host = new GameObject("Cierre de paquete");
            host.SetActive(false);
            try
            {
                var recorder = new PaseoRecorder(root);
                recorder.Selection.SetBuilding("TEO"); recorder.Selection.SetPoint("A"); recorder.Selection.SetLight("sol");
                Assert.IsTrue(recorder.TryEnable(0, out _));
                Assert.IsTrue(recorder.Save(new PaseoFrame { Pixels = new byte[4], Width = 2, Height = 2, Channels = 1 },
                    new PaseoRecordMeta { Attempt = 1, File = "0001.png", Time = DateTime.Now }).Wait(5000));
                var capture = host.AddComponent<PaseoSequenceCapture>();
                const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(PaseoSequenceCapture).GetField("recorder", fields).SetValue(capture, recorder);
                typeof(PaseoSequenceCapture).GetField("session", fields).SetValue(capture, new PaseoCaptureSession { estado = "grabando" });
                typeof(PaseoSequenceCapture).GetField("sessionOpen", fields).SetValue(capture, true);
                Assert.IsFalse(capture.Successful);
                capture.Stop();
                Assert.IsTrue(capture.Successful);
                Assert.IsFalse(capture.Busy);
                var session = JsonUtility.FromJson<PaseoCaptureSession>(File.ReadAllText(Path.Combine(root, "sesion.json")));
                Assert.IsTrue(session.Complete);
                Assert.AreEqual(1, session.fotos_guardadas);
                PaseoRecordings.WriteChecksums(root);
                Assert.AreEqual(PaseoManifestVerdict.Complete, PaseoRecordManifest.Verify(root).Verdict,
                    "The image manifest accepts the session bundle's separate metadata and checksum inventory.");
                File.WriteAllText(Path.Combine(root, "unexpected.png"), "unlisted image");
                Assert.AreEqual(PaseoManifestVerdict.Incomplete, PaseoRecordManifest.Verify(root).Verdict,
                    "Auxiliary bundle files must not disable detection of unexpected images.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void SameSizeMetadataChangesAreExportedAgain()
        {
            string file = Path.Combine(root, "sesion.json");
            File.WriteAllText(file, "abc");
            string before = PaseoRecordings.CopyKey(file);
            File.WriteAllText(file, "xyz");
            Assert.AreNotEqual(before, PaseoRecordings.CopyKey(file));
        }
    }
}

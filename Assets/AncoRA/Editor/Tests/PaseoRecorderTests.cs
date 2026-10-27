using System;
using System.IO;
using System.Linq;
using AncorRA.AR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoRecorderTests
    {
        string folder;
        long free;
        PaseoRecorder recorder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "ancora-recorder-" + Guid.NewGuid().ToString("N"));
            free = 10L * 1024 * 1024 * 1024;
            recorder = New();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        PaseoRecorder New(long max = PaseoRecorder.DefaultMaxBytes) => new(folder, () => free, max);

        static void Choose(PaseoRecorder r)
        {
            r.Selection.SetBuilding("X1");
            r.Selection.SetPoint("C");
            r.Selection.SetLight("sol");
        }

        static PaseoFrame Frame(int width, int height, int channels = 1)
        {
            var pixels = new byte[width * height * channels];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = (byte)(i * 31 + i / width);
            return new PaseoFrame
            {
                Pixels = pixels, Width = width, Height = height, Channels = channels,
                Intrinsics = new Vector4(1500f, 1490f, 960f, 540f), CameraRotation = Quaternion.identity, ScreenOrientation = Quaternion.identity
            };
        }

        static PaseoRecordMeta Meta(int attempt, string label = "X1-C-30m") => new()
        {
            Attempt = attempt, File = PaseoRecorder.FileName(attempt, label), Time = new DateTime(2026, 10, 28, 9, 30, 0), Session = "20261028-093000",
            Point = label, Building = "X1", Light = "sol", Width = 64, Height = 48, Channels = 1, Intrinsics = new Vector4(1500f, 1490f, 960f, 540f),
            Latitude = -23.6805, Longitude = -70.4108, GpsRunning = true, GpsAccuracy = 4f, GpsAge = 1.5, SearchedBuilding = "X1",
            SearchedMaps = new[] { 152198, 152200 }, SearchReason = "gps", GpsDistance = 22, Rmse = double.NaN, Phone = "Xiaomi \"25069PTEBG\"", Version = "1.3.9"
        };

        [Test]
        public void ItIsOffUntilTheTeamTurnsItOnAndWantsNothingWhileOff()
        {
            Choose(recorder);
            Assert.IsFalse(recorder.Enabled);
            Assert.IsFalse(recorder.WantsFrame(1f));
        }

        [Test]
        public void ItWillNotStartWithoutAChosenPoint()
        {
            Assert.IsFalse(recorder.TryEnable(0f, out string reason));
            StringAssert.Contains("Elige", reason);
            Assert.IsFalse(recorder.Enabled);
        }

        [Test]
        public void ItStartsOnceThePointIsChosenAndAsksForFrames()
        {
            Choose(recorder);
            Assert.IsTrue(recorder.TryEnable(0f, out _));
            Assert.IsTrue(recorder.WantsFrame(1f));
        }

        [Test]
        public void ItWillNotStartOnAnAlmostFullPhone()
        {
            Choose(recorder);
            free = 100L * 1024 * 1024;
            Assert.IsFalse(recorder.TryEnable(0f, out string reason));
            StringAssert.Contains("poco espacio", reason);
        }

        [Test]
        public void ItStopsByItselfWhenThePhoneRunsOutOfSpace()
        {
            Choose(recorder);
            Assert.IsTrue(recorder.TryEnable(0f, out _));
            free = 50L * 1024 * 1024;
            Assert.IsTrue(recorder.WantsFrame(5f), "the free space is only checked every few seconds");
            Assert.IsFalse(recorder.WantsFrame(20f));
            Assert.IsFalse(recorder.Enabled);
            StringAssert.Contains("poco espacio", recorder.Notice);
        }

        [Test]
        public void ItStopsByItselfAtTheSessionCap()
        {
            recorder = New(max: 500);
            Choose(recorder);
            Assert.IsTrue(recorder.TryEnable(0f, out _));
            recorder.Save(Frame(64, 48), Meta(1)).GetAwaiter().GetResult();
            Assert.GreaterOrEqual(recorder.Bytes, 500);
            Assert.IsFalse(recorder.WantsFrame(1f));
            Assert.IsFalse(recorder.Enabled);
            StringAssert.Contains("tope", recorder.Notice);
            Assert.IsFalse(recorder.TryEnable(2f, out string reason));
            StringAssert.Contains("tope", reason);
        }

        [Test]
        public void ASaveWritesTheImageAndOneJsonLine()
        {
            Choose(recorder);
            recorder.TryEnable(0f, out _);
            recorder.Save(Frame(64, 48), Meta(7)).GetAwaiter().GetResult();
            recorder.Save(Frame(64, 48), Meta(8)).GetAwaiter().GetResult();
            Assert.IsTrue(File.Exists(Path.Combine(folder, "0007-X1-C-30m.png")));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "0008-X1-C-30m.png")));
            Assert.AreEqual(2, recorder.Saved);
            var lines = File.ReadAllLines(Path.Combine(folder, PaseoRecorder.MetaFile));
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual(8, (int)JObject.Parse(lines[1])["n"]);
            Assert.AreEqual(new FileInfo(Path.Combine(folder, "0007-X1-C-30m.png")).Length + new FileInfo(Path.Combine(folder, "0008-X1-C-30m.png")).Length +
                            lines.Sum(l => l.Length + 1), recorder.Bytes);
        }

        [Test]
        public void ADiskThatFailsTurnsRecordingOffWithoutRaising()
        {
            // A file where the folder should be: every write fails.
            File.WriteAllText(folder, "x");
            Choose(recorder);
            recorder.TryEnable(0f, out _);
            Assert.DoesNotThrow(() => recorder.Save(Frame(64, 48), Meta(1)).GetAwaiter().GetResult());
            Assert.AreEqual(0, recorder.Saved);
            Assert.IsFalse(recorder.WantsFrame(1f));
            Assert.IsFalse(recorder.Enabled);
            StringAssert.Contains("se detuvo", recorder.Notice);
            File.Delete(folder);
        }

        [Test]
        public void SlowWritesDropFramesInsteadOfPilingUp()
        {
            Choose(recorder);
            recorder.TryEnable(0f, out _);
            using var release = new System.Threading.ManualResetEventSlim(false);
            recorder.BeforeWrite = () => release.Wait(5000);
            var tasks = new System.Threading.Tasks.Task[PaseoRecorder.MaxPending];
            for (int i = 0; i < tasks.Length; i++)
                tasks[i] = recorder.Save(Frame(64, 48), Meta(i + 1));
            Assert.IsFalse(recorder.WantsFrame(1f));
            Assert.AreEqual(1, recorder.Dropped);
            Assert.IsTrue(recorder.Enabled, "dropping is not an error");
            release.Set();
            System.Threading.Tasks.Task.WaitAll(tasks);
            Assert.IsTrue(recorder.WantsFrame(2f));
            Assert.AreEqual(PaseoRecorder.MaxPending, recorder.Saved);
        }

        [Test]
        public void ChangingThePointRestartsTheCountOfThePoint()
        {
            Choose(recorder);
            recorder.TryEnable(0f, out _);
            Assert.IsTrue(recorder.WantsFrame(10f));
            recorder.Save(Frame(64, 48), Meta(1)).GetAwaiter().GetResult();
            Assert.AreEqual(1, recorder.PointImages);
            recorder.Selection.SetPoint("D");
            Assert.IsTrue(recorder.WantsFrame(20f));
            Assert.AreEqual(0, recorder.PointImages);
            Assert.AreEqual(0f, recorder.PointSeconds(20f), 1e-4f);
        }

        [Test]
        public void ResettingForgetsTheCountsAndTheFailure()
        {
            File.WriteAllText(folder, "x");
            Choose(recorder);
            recorder.TryEnable(0f, out _);
            recorder.Save(Frame(64, 48), Meta(1)).GetAwaiter().GetResult();
            recorder.WantsFrame(1f);
            recorder.ResetCounts();
            Assert.AreEqual("", recorder.Notice);
            File.Delete(folder);
            Assert.IsTrue(recorder.TryEnable(2f, out _));
        }

        // ---- the image ----

        [Test]
        public void ThePngHoldsExactlyThePixelsThePluginGot()
        {
            var frame = Frame(64, 48);
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(texture.LoadImage(PaseoRecorder.EncodePng(frame.Pixels, 64, 48, 1)));
                Assert.AreEqual(64, texture.width);
                Assert.AreEqual(48, texture.height);
                var read = texture.GetPixels32();
                for (int y = 0; y < 48; y++)
                for (int x = 0; x < 64; x++)
                    Assert.AreEqual(frame.Pixels[y * 64 + x], read[(47 - y) * 64 + x].r, $"pixel {x},{y}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void AColourPngKeepsEveryChannel()
        {
            var frame = Frame(16, 8, 3);
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(texture.LoadImage(PaseoRecorder.EncodePng(frame.Pixels, 16, 8, 3)));
                var read = texture.GetPixels32();
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 16; x++)
                {
                    var p = read[(7 - y) * 16 + x];
                    int i = (y * 16 + x) * 3;
                    Assert.AreEqual(frame.Pixels[i], p.r);
                    Assert.AreEqual(frame.Pixels[i + 1], p.g);
                    Assert.AreEqual(frame.Pixels[i + 2], p.b);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void AFullHdFrameEncodesLosslesslyAndSmallerThanItsRawSize()
        {
            // A smooth gradient with noise, a stand-in for a photo: the PNG must not be larger than the raw buffer.
            var rng = new System.Random(3);
            var pixels = new byte[1920 * 1080];
            for (int y = 0; y < 1080; y++)
            for (int x = 0; x < 1920; x++)
                pixels[y * 1920 + x] = (byte)Mathf.Clamp(x / 12 + y / 20 + rng.Next(-3, 4), 0, 255);
            var png = PaseoRecorder.EncodePng(pixels, 1920, 1080, 1);
            Debug.Log($"[test] PNG 1920x1080 gris: {png.Length / 1024} KB de {pixels.Length / 1024} KB");
            Assert.Less(png.Length, pixels.Length);
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(texture.LoadImage(png));
                var read = texture.GetPixels32();
                for (int i = 0; i < pixels.Length; i += 997)
                    Assert.AreEqual(pixels[i], read[(1079 - i / 1920) * 1920 + i % 1920].r);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void FlippingTheRowsTwiceGivesTheImageBack()
        {
            var pixels = Enumerable.Range(0, 12).Select(i => (byte)i).ToArray();
            var flipped = PaseoRecorder.FlipRows(pixels, 3, 4);
            CollectionAssert.AreEqual(new byte[] { 9, 10, 11, 6, 7, 8, 3, 4, 5, 0, 1, 2 }, flipped);
            CollectionAssert.AreEqual(pixels, PaseoRecorder.FlipRows(flipped, 3, 4));
        }

        [Test]
        public void ThePgmFallbackIsTheRawBufferWithAHeader()
        {
            var pgm = PaseoRecorder.EncodePnm(new byte[] { 1, 2, 3, 4, 5, 6 }, 3, 2, 1);
            string header = "P5\n3 2\n255\n";
            Assert.AreEqual(header, System.Text.Encoding.ASCII.GetString(pgm, 0, header.Length));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6 }, pgm.Skip(header.Length).ToArray());
        }

        [Test]
        public void ThePpmFallbackDropsTheAlphaChannel()
        {
            var ppm = PaseoRecorder.EncodePnm(new byte[] { 1, 2, 3, 255, 4, 5, 6, 255 }, 2, 1, 4);
            string header = "P6\n2 1\n255\n";
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6 }, ppm.Skip(header.Length).ToArray());
        }

        // ---- the metadata ----

        [Test]
        public void TheMetadataLineIsOneValidJsonObjectWithWhatTheReplayNeeds()
        {
            string line = PaseoRecorder.MetaLine(Meta(12));
            Assert.IsFalse(line.Contains('\n'));
            var json = JsonConvert.DeserializeObject<JObject>(line, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
            Assert.AreEqual(12, (int)json["n"]);
            Assert.AreEqual("0012-X1-C-30m.png", (string)json["archivo"]);
            Assert.AreEqual("X1-C-30m", (string)json["punto"]);
            Assert.AreEqual("sol", (string)json["luz"]);
            Assert.AreEqual(64, (int)json["ancho"]);
            Assert.AreEqual(48, (int)json["alto"]);
            Assert.AreEqual(1, (int)json["canales"]);
            Assert.AreEqual(1500f, (float)json["intrinsecos"]["fx"], 1e-3f);
            Assert.AreEqual(540f, (float)json["intrinsecos"]["cy"], 1e-3f);
            Assert.AreEqual(4, ((JArray)json["rot_camara"]).Count);
            Assert.AreEqual(3, ((JArray)json["pos_camara"]).Count);
            Assert.AreEqual("X1", (string)json["busca"]["edificio"]);
            CollectionAssert.AreEqual(new[] { 152198, 152200 }, json["busca"]["mapas"].Select(t => (int)t).ToArray());
            Assert.AreEqual(JTokenType.Null, json["resultado"]["mapa"].Type);
            Assert.AreEqual(JTokenType.Null, json["resultado"]["rmse"].Type, "NaN is written as null");
            Assert.AreEqual(-23.6805, (double)json["gps"]["lat"], 1e-6);
            Assert.AreEqual("Xiaomi \"25069PTEBG\"", (string)json["telefono"], "quotes in the phone name are escaped");
            Assert.AreEqual("2026-10-28", ((string)json["hora"]).Substring(0, 10));
        }

        [Test]
        public void AMatchCarriesTheMapTheConfidenceAndTheRmse()
        {
            var meta = Meta(3);
            meta.MatchedMap = 152198;
            meta.Confidence = 87;
            meta.Rmse = 0.0421;
            var json = JObject.Parse(PaseoRecorder.MetaLine(meta));
            Assert.AreEqual(152198, (int)json["resultado"]["mapa"]);
            Assert.AreEqual(87, (int)json["resultado"]["conf"]);
            Assert.AreEqual(0.0421, (double)json["resultado"]["rmse"], 1e-9);
        }

        [Test]
        public void WithoutGpsTheFixIsNullNotZero()
        {
            var meta = Meta(3);
            meta.GpsRunning = false;
            var json = JObject.Parse(PaseoRecorder.MetaLine(meta));
            Assert.AreEqual(JTokenType.Null, json["gps"]["lat"].Type);
            Assert.AreEqual(false, (bool)json["gps"]["activo"]);
        }

        [Test]
        public void TheFileNameCarriesTheAttemptAndThePoint()
        {
            Assert.AreEqual("0012-X1-C-30m.png", PaseoRecorder.FileName(12, "X1-C-30m"));
        }
    }
}

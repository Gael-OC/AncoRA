using System;
using System.Collections.Generic;
using System.IO;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoMultiSetConfigTests
    {
        const string Full = @"{
            ""clientId"": ""id-de-prueba"", ""clientSecret"": ""secreto-de-prueba"", ""codigo"": ""MAP_AAAA"", ""tipo"": ""mapset"",
            ""edificios"": { ""MAP_AAAA"": ""Teologia"", ""MAP_BBBB"": ""X1"" },
            ""pistaGps"": true, ""radioGps"": 40, ""maxConsultas"": 50, ""segundosTrasUbicar"": 4.5 }";

        string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "ancora-multiset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(folder, true);

        void Write(string json) => File.WriteAllText(Path.Combine(folder, PaseoMultiSetConfig.FileName), json);

        [Test]
        public void ADocumentWithEveryValueIsRead()
        {
            var config = PaseoMultiSetConfig.Parse(Full);
            Assert.AreEqual("id-de-prueba", config.ClientId);
            Assert.AreEqual("secreto-de-prueba", config.ClientSecret);
            Assert.AreEqual("MAP_AAAA", config.Code);
            Assert.AreEqual(PaseoMultiSetKind.MapSet, config.Kind);
            Assert.AreEqual("Teologia", config.BuildingFor("MAP_AAAA"));
            Assert.AreEqual("X1", config.BuildingFor("MAP_BBBB"));
            Assert.IsNull(config.BuildingFor("MAP_CCCC"), "a code no building names");
            Assert.IsNull(config.BuildingFor(null));
            Assert.IsTrue(config.GpsHint);
            Assert.AreEqual(40f, config.GpsRadiusMeters);
            Assert.AreEqual(50, config.MaxQueries);
            Assert.AreEqual(4.5f, config.SecondsAfterLocated);
        }

        [Test]
        public void TheOptionalValuesHaveTheirDefaults()
        {
            var config = PaseoMultiSetConfig.Parse(@"{ ""clientId"": ""a"", ""clientSecret"": ""b"", ""codigo"": ""MAP_X"" }");
            Assert.AreEqual(PaseoMultiSetKind.Map, config.Kind);
            Assert.IsFalse(config.GpsHint, "the GPS hint only helps a georeferenced map: off unless asked");
            Assert.AreEqual(25f, config.GpsRadiusMeters);
            Assert.AreEqual(120, config.MaxQueries);
            Assert.AreEqual(10f, config.SecondsAfterLocated);
            Assert.AreEqual(0, config.Buildings.Count);
        }

        [Test]
        public void TheGpsRadiusStaysInTheSdksRange()
        {
            Assert.AreEqual(5f, PaseoMultiSetConfig.Parse(@"{ ""radioGps"": 1 }").GpsRadiusMeters);
            Assert.AreEqual(100f, PaseoMultiSetConfig.Parse(@"{ ""radioGps"": 5000 }").GpsRadiusMeters);
        }

        [Test]
        public void AnUnknownKindOrANonsensicalValueIsReportedNotGuessed()
        {
            Assert.Throws<FormatException>(() => PaseoMultiSetConfig.Parse(@"{ ""tipo"": ""otro"" }"));
            Assert.Throws<FormatException>(() => PaseoMultiSetConfig.Parse(@"{ ""maxConsultas"": 0 }"));
            Assert.Throws<FormatException>(() => PaseoMultiSetConfig.Parse(@"{ ""pistaGps"": ""si"" }"));
            Assert.Throws<FormatException>(() => PaseoMultiSetConfig.Parse(@"{ ""clientId"": 5 }"));
            Assert.Throws<FormatException>(() => PaseoMultiSetConfig.Parse(@"{ ""edificios"": { ""MAP_A"": 3 } }"));
            Assert.Throws<FormatException>(() => PaseoMultiSetConfig.Parse(@"[1, 2]"));
        }

        [Test]
        public void TheKindIsCaseInsensitive() =>
            Assert.AreEqual(PaseoMultiSetKind.MapSet, PaseoMultiSetConfig.Parse(@"{ ""tipo"": ""MapSet"" }").Kind);

        [Test]
        public void WithoutTheFileMultiSetStaysOffAndSaysWhere()
        {
            var status = PaseoMultiSetConfig.Load(folder, out var config, out string message);
            Assert.AreEqual(PaseoMultiSetConfigStatus.Missing, status);
            Assert.IsNull(config);
            Assert.AreEqual($"MultiSet apagado: falta multiset.json en {folder}", message);
        }

        [Test]
        public void AnEmptyOrBrokenFileLeavesItOffWithoutQuotingAnything()
        {
            Write("");
            Assert.AreEqual(PaseoMultiSetConfigStatus.Invalid, PaseoMultiSetConfig.Load(folder, out var config, out string message));
            Assert.IsNull(config);
            StringAssert.StartsWith("MultiSet apagado:", message);

            Write(@"{ ""clientId"": ""no-debe-salir"", ""clientSecret"": ");
            Assert.AreEqual(PaseoMultiSetConfigStatus.Invalid, PaseoMultiSetConfig.Load(folder, out config, out message));
            StringAssert.DoesNotContain("no-debe-salir", message);
        }

        [Test]
        public void EmptyCredentialsLeaveItOff()
        {
            Write(@"{ ""codigo"": ""MAP_X"" }");
            Assert.AreEqual(PaseoMultiSetConfigStatus.NoCredentials, PaseoMultiSetConfig.Load(folder, out var config, out string message));
            Assert.IsNull(config);
            StringAssert.Contains("clientId", message);

            Write(@"{ ""clientId"": ""a"", ""clientSecret"": ""  "", ""codigo"": ""MAP_X"" }");
            Assert.AreEqual(PaseoMultiSetConfigStatus.NoCredentials, PaseoMultiSetConfig.Load(folder, out config, out _));
        }

        [Test]
        public void WithCredentialsButNoMapCodeItStaysOff()
        {
            Write(@"{ ""clientId"": ""a"", ""clientSecret"": ""b"" }");
            Assert.AreEqual(PaseoMultiSetConfigStatus.NoCode, PaseoMultiSetConfig.Load(folder, out _, out string message));
            StringAssert.Contains("codigo", message);
        }

        [Test]
        public void ACompleteFileIsReady()
        {
            Write(Full);
            Assert.AreEqual(PaseoMultiSetConfigStatus.Ready, PaseoMultiSetConfig.Load(folder, out var config, out string message));
            Assert.AreEqual("", message);
            Assert.AreEqual("MAP_AAAA", config.Code);
        }

        [Test]
        public void ABomFromAWindowsEditorIsTolerated()
        {
            Write("﻿" + Full);
            Assert.AreEqual(PaseoMultiSetConfigStatus.Ready, PaseoMultiSetConfig.Load(folder, out _, out _));
        }

        [Test]
        public void TheExampleInTheRepoParsesAndTurnsTheGpsHintOff()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "multiset.ejemplo.json"));
            Assert.IsTrue(File.Exists(path), path);
            var config = PaseoMultiSetConfig.Parse(File.ReadAllText(path));
            Assert.AreEqual(PaseoMultiSetConfigStatus.Ready, PaseoMultiSetConfig.Check(config, out _), "placeholders, but complete");
            Assert.IsFalse(config.GpsHint);
            Assert.Greater(config.Buildings.Count, 0);
        }

        // ---------------------------------------------------------------- PaseoMiniJson

        [Test]
        public void MiniJsonReadsNestedValuesAndEscapes()
        {
            var root = (Dictionary<string, object>)PaseoMiniJson.Parse(
                @"{ ""a"": [1, 2.5, -3e2, true, false, null, ""x\nñ""], ""b"": { ""c"": ""d\""e"" }, ""v"": {} }");
            var list = (List<object>)root["a"];
            Assert.AreEqual(1d, list[0]);
            Assert.AreEqual(2.5d, list[1]);
            Assert.AreEqual(-300d, list[2]);
            Assert.AreEqual(true, list[3]);
            Assert.AreEqual(false, list[4]);
            Assert.IsNull(list[5]);
            Assert.AreEqual("x\nñ", list[6]);
            Assert.AreEqual("d\"e", ((Dictionary<string, object>)root["b"])["c"]);
            Assert.AreEqual(0, ((Dictionary<string, object>)root["v"]).Count);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{")]
        [TestCase("{ \"a\": }")]
        [TestCase("{ \"a\": 1, }")]
        [TestCase("{ \"a\" 1 }")]
        [TestCase("{ a: 1 }")]
        [TestCase("{ \"a\": 1 } basura")]
        [TestCase("{ \"a\": \"sin cerrar }")]
        [TestCase("[1, 2")]
        [TestCase("{ \"a\": tru }")]
        public void MiniJsonRejectsMalformedText(string text) => Assert.Throws<FormatException>(() => PaseoMiniJson.Parse(text));

        [Test]
        public void MiniJsonRejectsNull() => Assert.Throws<FormatException>(() => PaseoMiniJson.Parse(null));
    }
}

using System;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace AncorRA.Tests
{
    public class PaseoMultiSetLogTests
    {
        static readonly DateTime At = new(2026, 10, 23, 15, 4, 9, 321);

        [TearDown]
        public void TearDown() => PaseoLogFormat.ForgetSecrets();

        [Test]
        public void TheStateLineNamesTheStateTheQueriesAndTheClock()
        {
            Assert.AreEqual("[AncoRA Paseo] MULTISET_ESTADO estado=autenticado consultas=14 t=63.5", PaseoLogFormat.MultiSetState("autenticado", 14, 63.5f));
            Assert.AreEqual("[AncoRA Paseo] MULTISET_ESTADO estado=tope consultas=120 t=900.0", PaseoLogFormat.MultiSetState("tope", 120, 900f));
        }

        [Test]
        public void EveryStateHasItsLogName()
        {
            Assert.AreEqual("sin-config", PaseoMultiSetStates.LogName(PaseoMultiSetState.NoConfig));
            Assert.AreEqual("esperando", PaseoMultiSetStates.LogName(PaseoMultiSetState.Waiting));
            Assert.AreEqual("autenticando", PaseoMultiSetStates.LogName(PaseoMultiSetState.Authenticating));
            Assert.AreEqual("autenticado", PaseoMultiSetStates.LogName(PaseoMultiSetState.Authenticated));
            Assert.AreEqual("error", PaseoMultiSetStates.LogName(PaseoMultiSetState.Error));
            Assert.AreEqual("apagado", PaseoMultiSetStates.LogName(PaseoMultiSetState.Paused));
            Assert.AreEqual("tope", PaseoMultiSetStates.LogName(PaseoMultiSetState.CapReached));
        }

        [Test]
        public void AnAcceptedQueryLineHasEverythingTheAnalysisNeeds()
        {
            var attempt = new PaseoMultiSetAttempt(7, 1.8f, "MAP_AAAA", 0.71f, true, "Teologia", true, 41.2f);
            Assert.AreEqual("[AncoRA Paseo] INTENTO_MULTISET n=7 seg=1.80 resultado=MAP_AAAA conf=0.71 aceptada=1 edificio=Teologia gps=1 t=41.2",
                PaseoLogFormat.MultiSetAttempt(attempt));
        }

        [Test]
        public void AQueryWithoutAMatchSaysNingunoAndNoBuilding()
        {
            var attempt = new PaseoMultiSetAttempt(8, 2.345f, null, 0f, false, null, false, 43.0f);
            Assert.AreEqual("[AncoRA Paseo] INTENTO_MULTISET n=8 seg=2.35 resultado=ninguno conf=0.00 aceptada=0 edificio=- gps=0 t=43.0",
                PaseoLogFormat.MultiSetAttempt(attempt));
        }

        [Test]
        public void AMatchBelowTheThresholdIsLoggedButNotAccepted()
        {
            var attempt = new PaseoMultiSetAttempt(9, 1.1f, "MAP_AAAA", 0.18f, false, "Teologia", false, 50f);
            StringAssert.Contains("resultado=MAP_AAAA conf=0.18 aceptada=0", PaseoLogFormat.MultiSetAttempt(attempt));
        }

        [Test]
        public void ALocationLineSaysWhichRelationWasUsedAndTheDifference()
        {
            Assert.AreEqual("[AncoRA Paseo] UBICACION_MULTISET edificio=Teologia mapa=MAP_AAAA conf=0.71 relacion=guardada dif_caja=0.62 dist=18.4 t=41.2",
                PaseoLogFormat.MultiSetLocation("Teologia", "MAP_AAAA", 0.71f, PaseoMultiSetRelationState.Saved, 0.62f, 18.4f, 41.2f));
            Assert.AreEqual("[AncoRA Paseo] UBICACION_MULTISET edificio=X1 mapa=MAP_B conf=0.40 relacion=nueva dif_caja=- dist=12.0 t=70.0",
                PaseoLogFormat.MultiSetLocation("X1", "MAP_B", 0.4f, PaseoMultiSetRelationState.New, float.NaN, 12f, 70f));
            Assert.AreEqual("[AncoRA Paseo] UBICACION_MULTISET edificio=- mapa=MAP_C conf=0.90 relacion=sin-immersal dif_caja=- dist=- t=5.0",
                PaseoLogFormat.MultiSetLocation(null, "MAP_C", 0.9f, PaseoMultiSetRelationState.NoImmersal, float.NaN, float.NaN, 5f));
        }

        [Test]
        public void TheFirstLocationLineNamesTheSystem()
        {
            Assert.AreEqual("[AncoRA Paseo] PRIMERA_UBICACION sistema=immersal edificio=Teologia t=12.3", PaseoLogFormat.FirstLocation("immersal", "Teologia", 12.34f));
            Assert.AreEqual("[AncoRA Paseo] PRIMERA_UBICACION sistema=multiset edificio=X1 t=8.0", PaseoLogFormat.FirstLocation("multiset", "X1", 8f));
        }

        [Test]
        public void TheLinesSurviveTheSessionLogFilter()
        {
            var attempt = new PaseoMultiSetAttempt(1, 1f, "MAP_A", 0.5f, true, "X1", false, 1f);
            Assert.IsTrue(PaseoLogFormat.Keep(PaseoLogFormat.MultiSetAttempt(attempt), LogType.Log));
            Assert.IsTrue(PaseoLogFormat.Keep(PaseoLogFormat.MultiSetState("error", 0, 1f), LogType.Log));
            Assert.IsTrue(PaseoLogFormat.Keep(PaseoLogFormat.FirstLocation("multiset", "X1", 1f), LogType.Log));
        }

        [Test]
        public void ARegisteredCredentialNeverReachesTheFile()
        {
            PaseoLogFormat.RegisterSecret("s3creto-largo");
            PaseoLogFormat.RegisterSecret("id-cliente-123");
            Assert.AreEqual("12:40:05.123 I auth con ***",
                PaseoLogFormat.Line(new DateTime(2026, 10, 23, 12, 40, 5, 123), LogType.Log, "auth con id-cliente-123", ""));
            string line = PaseoLogFormat.Line(At, LogType.Error, "falló con s3creto-largo", "Llamada(s3creto-largo)\nOtra()");
            StringAssert.DoesNotContain("s3creto-largo", line);
            StringAssert.Contains("falló con ***", line);
            StringAssert.Contains("@ Llamada(***)", line);
        }

        [Test]
        public void WithoutRegisteredSecretsTextIsUntouchedAndBlankOnesAreIgnored()
        {
            PaseoLogFormat.RegisterSecret("  ");
            PaseoLogFormat.RegisterSecret(null);
            Assert.AreEqual("hola mundo", PaseoLogFormat.Redact("hola mundo"));
            Assert.IsNull(PaseoLogFormat.Redact(null));
        }

        // ---------------------------------------------------------------- status row

        [Test]
        public void TheStatusRowReadsLikeTheBrief()
        {
            var last = new PaseoMultiSetAttempt(14, 1.8f, "MAP_AAAA", 0.71f, true, "Teologia", false, 30f);
            Assert.AreEqual("MultiSet: autenticado · 14 consultas · última 1.8 s, conf 0.71 (Teología)",
                PaseoMultiSetStates.Describe(PaseoMultiSetState.Authenticated, 14, last, "Teología", null));
        }

        [Test]
        public void TheStatusRowHandlesNoQueriesOneQueryAndNoMatch()
        {
            Assert.AreEqual("MultiSet: autenticado · 0 consultas", PaseoMultiSetStates.Describe(PaseoMultiSetState.Authenticated, 0, null, null, null));
            var miss = new PaseoMultiSetAttempt(1, 2.5f, null, 0f, false, null, false, 5f);
            Assert.AreEqual("MultiSet: autenticado · 1 consulta · última 2.5 s, conf 0.00 (sin coincidencia)",
                PaseoMultiSetStates.Describe(PaseoMultiSetState.Authenticated, 1, miss, null, null));
            var unnamed = new PaseoMultiSetAttempt(2, 1f, "MAP_Z", 0.6f, true, null, false, 6f);
            StringAssert.EndsWith("(MAP_Z)", PaseoMultiSetStates.Describe(PaseoMultiSetState.Authenticated, 2, unnamed, null, null));
        }

        [Test]
        public void TheStatusRowSaysWhyItIsOffOrFailed()
        {
            Assert.AreEqual("MultiSet: apagado: falta multiset.json en /x",
                PaseoMultiSetStates.Describe(PaseoMultiSetState.NoConfig, 0, null, null, "MultiSet apagado: falta multiset.json en /x"));
            Assert.AreEqual("MultiSet: apagado (sin multiset.json)", PaseoMultiSetStates.Describe(PaseoMultiSetState.NoConfig, 0, null, null, null));
            Assert.AreEqual("MultiSet: autenticando…", PaseoMultiSetStates.Describe(PaseoMultiSetState.Authenticating, 0, null, null, null));
            StringAssert.Contains("comience", PaseoMultiSetStates.Describe(PaseoMultiSetState.Waiting, 0, null, null, null));
            Assert.AreEqual("MultiSet: error · sin internet", PaseoMultiSetStates.Describe(PaseoMultiSetState.Error, 0, null, null, "sin internet"));
            StringAssert.StartsWith("MultiSet: en pausa · 3 consultas", PaseoMultiSetStates.Describe(PaseoMultiSetState.Paused, 3, null, null, null));
            StringAssert.StartsWith("MultiSet: tope alcanzado · 120 consultas", PaseoMultiSetStates.Describe(PaseoMultiSetState.CapReached, 120, null, null, null));
        }

        // ---------------------------------------------------------------- team sheet

        [Test]
        public void TheSheetHasTheMultiSetRowAndItsTwoButtons()
        {
            var sheet = new PaseoTeamSheet();
            Assert.IsNotNull(sheet.Root.Q<Label>("equipo-multiset"));
            Assert.IsNotNull(sheet.Root.Q<Button>("equipo-multiset-pausa"));
            Assert.IsNotNull(sheet.Root.Q<Button>("equipo-multiset-olvidar"));
            Assert.AreEqual("Olvidar relación MultiSet", sheet.Root.Q<Button>("equipo-multiset-olvidar").text);
            Assert.IsFalse(PaseoUiKit.IsHidden(sheet.Root.Q("equipo-multiset-seccion")), "MultiSet is the point of this branch: visible in any build");
        }

        [Test]
        public void TheSheetShowsTheStatusAndWhetherTheQueriesAreOn()
        {
            var sheet = new PaseoTeamSheet();
            sheet.SetMultiSet("MultiSet: autenticado · 2 consultas", queriesOn: true);
            Assert.AreEqual("MultiSet: autenticado · 2 consultas", sheet.Root.Q<Label>("equipo-multiset").text);
            Assert.AreEqual("MultiSet: sí", sheet.Root.Q<Button>("equipo-multiset-pausa").text);
            sheet.SetMultiSet("MultiSet: en pausa", queriesOn: false);
            Assert.AreEqual("MultiSet: no", sheet.Root.Q<Button>("equipo-multiset-pausa").text);
        }

        [Test]
        public void TheToggleButtonAsksToPauseOrResumeWithoutABuilding()
        {
            var sheet = new PaseoTeamSheet();
            int toggles = 0, edits = 0;
            sheet.MultiSetToggleRequested += () => toggles++;
            sheet.EditRequested += _ => edits++;
            sheet.SetTarget(null);
            sheet.Press(PaseoSheetAction.ToggleMultiSet, 0f);
            Assert.AreEqual(1, toggles);
            Assert.AreEqual(0, edits);
        }

        [Test]
        public void ForgettingTheRelationsNeedsASecondTapWithinThreeSeconds()
        {
            var sheet = new PaseoTeamSheet();
            int forgets = 0;
            sheet.MultiSetForgetRequested += () => forgets++;
            sheet.Press(PaseoSheetAction.ForgetMultiSet, 0f);
            Assert.AreEqual(0, forgets);
            StringAssert.Contains("otra vez", sheet.Root.Q<Label>("equipo-aviso").text);
            sheet.Press(PaseoSheetAction.ForgetMultiSet, 1f);
            Assert.AreEqual(1, forgets);
            sheet.Press(PaseoSheetAction.ForgetMultiSet, 10f);
            sheet.Press(PaseoSheetAction.ForgetMultiSet, 10f + PaseoTeamSheet.ForgetConfirmSeconds + 0.1f);
            Assert.AreEqual(1, forgets, "the second tap came too late: it only arms again");
        }

        [Test]
        public void TheNewButtonsReportTheirPressForTheDebugLog()
        {
            var sheet = new PaseoTeamSheet();
            var pressed = new System.Collections.Generic.List<PaseoSheetAction>();
            sheet.Pressed += pressed.Add;
            sheet.Press(PaseoSheetAction.ToggleMultiSet, 0f);
            sheet.Press(PaseoSheetAction.ForgetMultiSet, 0f);
            CollectionAssert.AreEqual(new[] { PaseoSheetAction.ToggleMultiSet, PaseoSheetAction.ForgetMultiSet }, pressed);
        }
    }
}

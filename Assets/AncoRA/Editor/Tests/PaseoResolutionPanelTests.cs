using System;
using System.Collections.Generic;
using System.Linq;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace AncorRA.Tests
{
    public class PaseoResolutionPanelTests
    {
        PaseoResolutionPanel panel;
        readonly List<int> picks = new();
        readonly List<string> notices = new();
        int tests;

        [SetUp]
        public void SetUp()
        {
            picks.Clear();
            notices.Clear();
            tests = 0;
            panel = new PaseoResolutionPanel(PaseoResolutionChoice.Default);
            panel.PickRequested += picks.Add;
            panel.TestRequested += () => tests++;
            panel.Notice += notices.Add;
        }

        static Button ButtonNamed(VisualElement root, string text) => root.Query<Button>().Where(b => b.text == text).First();

        static bool On(Button button) => button.ClassListContains(PaseoResolutionPanel.OnClass);

        [Test]
        public void ItOffersTheThreeSizesAndMarksTheDefault()
        {
            var row = panel.Root.Q("resolucion-opciones");
            CollectionAssert.AreEqual(new[] { "960×720", "1440×810", "Completa" }, row.Query<Button>().ToList().Select(b => b.text));
            Assert.IsTrue(On(ButtonNamed(panel.Root, "1440×810")));
            Assert.IsFalse(On(ButtonNamed(panel.Root, "960×720")));
            Assert.AreEqual("Resolución de la imagen", panel.Root.Q<Label>(className: "paseo-section__title").text);
            Assert.AreEqual("Ahora: 1440×810 · por defecto, sin probar en este teléfono", panel.Root.Q<Label>("resolucion-actual").text);
        }

        [Test]
        public void PickingASizeMarksItRaisesTheEventAndSaysItWasChosenByHand()
        {
            Assert.IsTrue(panel.Pick("Completa"));
            CollectionAssert.AreEqual(new[] { 0 }, picks);
            Assert.IsTrue(On(ButtonNamed(panel.Root, "Completa")));
            Assert.IsFalse(On(ButtonNamed(panel.Root, "1440×810")));
            Assert.AreEqual("Ahora: Completa · elegida a mano", panel.Root.Q<Label>("resolucion-actual").text);

            panel.Pick("960×720");
            CollectionAssert.AreEqual(new[] { 0, 691200 }, picks);
            Assert.IsFalse(panel.Pick("2000×2000"));
        }

        [Test]
        public void TheOwnerCanSetWhatIsInUseAndWhy()
        {
            panel.SetChoice(new PaseoResolutionChoice(PaseoResolution.Reduced, PaseoResolution.OriginTest, new DateTime(2026, 10, 29)));
            Assert.AreEqual("Ahora: 960×720 · elegida por la prueba (29-10)", panel.Root.Q<Label>("resolucion-actual").text);
            Assert.IsTrue(On(ButtonNamed(panel.Root, "960×720")));
            Assert.IsFalse(On(ButtonNamed(panel.Root, "1440×810")));
        }

        [Test]
        public void TheTestButtonRaisesItsEventOnceWhileTheTestRuns()
        {
            var button = panel.Root.Q<Button>("resolucion-probar");
            Assert.AreEqual("Probar este teléfono (~30 s)", button.text);
            panel.PressTest();
            Assert.AreEqual(1, tests);
            Assert.IsTrue(panel.Running, "the panel is busy from the tap, before the owner starts the test");
            panel.PressTest();
            panel.PressTest();
            Assert.AreEqual(1, tests, "taps are ignored while it runs");

            panel.SetRunning(true, "Probando 960×720: imagen 2 de 4…");
            Assert.AreEqual("Probando 960×720: imagen 2 de 4…", button.text);
            panel.PressTest();
            Assert.AreEqual(1, tests);

            panel.SetRunning(false);
            Assert.AreEqual("Probar este teléfono (~30 s)", button.text);
            panel.PressTest();
            Assert.AreEqual(2, tests);
        }

        [Test]
        public void ASizePickedWhileTheTestRunsIsIgnoredAndSaysWhy()
        {
            panel.PressTest();
            panel.Pick("Completa");
            Assert.IsEmpty(picks);
            Assert.IsFalse(On(ButtonNamed(panel.Root, "Completa")));
            Assert.AreEqual("Espera a que termine la prueba.", notices.Last());
        }

        [Test]
        public void TheResultIsOneLinePerSizeThenTheChoiceWithTheSpanishDecimalComma()
        {
            var result = new PaseoPhoneTestResult { Chosen = PaseoResolution.Medium, TotalSeconds = 24.9 };
            result.Steps.Add(new PaseoPhoneTestStep { MaxPixels = PaseoResolution.Reduced, Measured = true, MeanSeconds = 2.083, Images = 4, Located = 2, AsExpected = 4 });
            result.Steps.Add(new PaseoPhoneTestStep { MaxPixels = PaseoResolution.Medium, Measured = true, MeanSeconds = 4.14, Images = 4, Located = 2, AsExpected = 4 });
            result.Steps.Add(new PaseoPhoneTestStep { MaxPixels = PaseoResolution.Full, PredictedSeconds = 9.108 });
            var text = panel.Root.Q<Label>("resolucion-resultado");
            Assert.IsTrue(PaseoUiKit.IsHidden(text), "nothing to show before a test");

            panel.SetResult(result);
            Assert.IsFalse(PaseoUiKit.IsHidden(text));
            Assert.AreEqual("960×720: 2,1 s · ubica 2/4\n1440×810: 4,1 s · ubica 2/4\nCompleta: no se probó (≈ 9,1 s)\nElegida: 1440×810", text.text);
            Assert.AreEqual("Prueba lista: se usa 1440×810 (25 s).", notices.Last());
        }

        [Test]
        public void AWarningFromTheTestShowsAfterTheChoice()
        {
            var result = new PaseoPhoneTestResult { Chosen = PaseoResolution.Reduced };
            result.Steps.Add(new PaseoPhoneTestStep { MaxPixels = PaseoResolution.Reduced, Measured = true, MeanSeconds = 3.0, Images = 4, Located = 3 });
            result.Warnings.Add("Aviso: a 960×720 la imagen 3 ubicó en el mapa 152192 y no debía ubicar.");
            panel.SetResult(result);
            var lines = panel.Root.Q<Label>("resolucion-resultado").text.Split('\n');
            Assert.AreEqual("Elegida: 960×720", lines[lines.Length - 2]);
            StringAssert.StartsWith("Aviso:", lines[lines.Length - 1]);
        }

        [Test]
        public void ATestThatCouldNotRunShowsWhyAndNoChoice()
        {
            panel.SetResult(new PaseoPhoneTestResult { Failure = PaseoPhoneTest.WaitForMaps });
            Assert.AreEqual("Espera a que carguen los mapas y vuelve a probar.", panel.Root.Q<Label>("resolucion-resultado").text);
            Assert.AreEqual("Espera a que carguen los mapas y vuelve a probar.", notices.Last());
        }

        [Test]
        public void ACancelledTestLeavesTheResultAlone()
        {
            panel.SetResult(new PaseoPhoneTestResult { Cancelled = true });
            Assert.IsTrue(PaseoUiKit.IsHidden(panel.Root.Q<Label>("resolucion-resultado")));
            Assert.AreEqual("Prueba cancelada.", notices.Last());
        }

        [Test]
        public void TheSheetAddsTheSectionOnceAtTheTopEvenAboveTheRecordSection()
        {
            var sheet = new PaseoTeamSheet();
            Assert.IsNull(sheet.Resolution);
            var record = sheet.AttachRecorder(new PaseoRecordSelection());
            var first = sheet.AttachResolution(PaseoResolutionChoice.Default);
            Assert.AreSame(first, sheet.AttachResolution(PaseoResolutionChoice.Default));
            var scroll = sheet.Root.Q<ScrollView>();
            Assert.AreSame(first.Root, scroll.contentContainer[0]);
            Assert.AreSame(record.Root, scroll.contentContainer[1]);
        }

        [Test]
        public void ThePanelsNoticesReachTheSheetsNoticeLine()
        {
            var sheet = new PaseoTeamSheet();
            var attached = sheet.AttachResolution(PaseoResolutionChoice.Default);
            attached.SetResult(new PaseoPhoneTestResult { Failure = "No se pudo pausar la búsqueda; vuelve a probar." });
            Assert.AreEqual("No se pudo pausar la búsqueda; vuelve a probar.", sheet.Root.Q<Label>("equipo-aviso").text);
        }
    }
}

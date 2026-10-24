using System.Collections.Generic;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace AncorRA.Tests
{
    public class PaseoTeamSheetTests
    {
        PaseoTeamSheet sheet;
        readonly List<PaseoBoxEdit> edits = new();

        [SetUp]
        public void SetUp()
        {
            edits.Clear();
            sheet = new PaseoTeamSheet();
            sheet.EditRequested += edits.Add;
        }

        [Test]
        public void ItStartsClosedAndToggles()
        {
            Assert.IsFalse(sheet.IsOpen);
            sheet.Toggle();
            Assert.IsTrue(sheet.IsOpen);
            sheet.Press(PaseoSheetAction.Close, 0f);
            Assert.IsFalse(sheet.IsOpen);
        }

        [Test]
        public void WithoutALocatedBuildingTheAdjustmentSaysSoAndDoesNothing()
        {
            sheet.SetTarget(null);
            sheet.Press(PaseoSheetAction.Right, 0f);
            Assert.IsEmpty(edits);
            Assert.IsTrue(PaseoUiKit.IsHidden(sheet.Root.Q("equipo-controles")));
            Assert.IsFalse(PaseoUiKit.IsHidden(sheet.Root.Q("equipo-sin-objetivo")));
        }

        [Test]
        public void RightAtTheMediumStepMovesAQuarterMetre()
        {
            sheet.SetTarget("Teología · mapa 152259");
            Assert.AreEqual("medio", sheet.StepName);
            sheet.Press(PaseoSheetAction.Right, 0f);
            Assert.AreEqual(1, edits.Count);
            Assert.AreEqual(0.25f, edits[0].Right, 1e-5f);
            Assert.AreEqual(0f, edits[0].Closer);
        }

        [Test]
        public void TheStepCyclesMediumFineCoarse()
        {
            sheet.SetTarget("X1 · mapa 152198");
            sheet.Press(PaseoSheetAction.NextStep, 0f);
            Assert.AreEqual("fino", sheet.StepName);
            sheet.Press(PaseoSheetAction.TurnLeft, 0f);
            Assert.AreEqual(-0.2f, edits[0].Turn, 1e-5f);
            sheet.Press(PaseoSheetAction.NextStep, 0f);
            Assert.AreEqual("grueso", sheet.StepName);
            sheet.Press(PaseoSheetAction.Wider, 0f);
            Assert.AreEqual(2f, edits[1].Width, 1e-5f);
            sheet.Press(PaseoSheetAction.Farther, 0f);
            Assert.AreEqual(-1f, edits[2].Closer, 1e-5f);
        }

        [Test]
        public void RestoreNeedsASecondTapWithinThreeSeconds()
        {
            int restores = 0;
            sheet.RestoreRequested += () => restores++;
            sheet.Press(PaseoSheetAction.Restore, 0f);
            Assert.AreEqual(0, restores);
            sheet.Press(PaseoSheetAction.Restore, 1f);
            Assert.AreEqual(1, restores);
            sheet.Press(PaseoSheetAction.Restore, 10f);
            sheet.Press(PaseoSheetAction.Restore, 10f + PaseoTeamSheet.RestoreConfirmSeconds + 0.1f);
            Assert.AreEqual(1, restores, "the second tap came too late: it only arms again");
        }

        [Test]
        public void CopyIsAsked()
        {
            bool copied = false;
            sheet.CopyRequested += () => copied = true;
            sheet.Press(PaseoSheetAction.Copy, 0f);
            Assert.IsTrue(copied);
        }

        [Test]
        public void TheDetectionTestsAskToSwitchTheImageAndTheSearchWithoutABuilding()
        {
            int image = 0, search = 0;
            sheet.ImageToggleRequested += () => image++;
            sheet.SearchToggleRequested += () => search++;
            sheet.SetTarget(null);
            sheet.Press(PaseoSheetAction.ToggleImage, 0f);
            sheet.Press(PaseoSheetAction.ToggleSearch, 0f);
            Assert.AreEqual(1, image);
            Assert.AreEqual(1, search);
            Assert.IsEmpty(edits);
        }

        [Test]
        public void SharingTheLogIsAskedWithoutABuilding()
        {
            int shares = 0;
            sheet.ShareRequested += () => shares++;
            sheet.SetTarget(null);
            sheet.Press(PaseoSheetAction.Share, 0f);
            Assert.AreEqual(1, shares);
            Assert.IsNotNull(sheet.Root.Q<Button>("equipo-compartir"));
        }

        [Test]
        public void TheDetectionTestsSayWhatIsOn()
        {
            sheet.SetDetectionTests(reducedImage: true, everyMap: false);
            Assert.AreEqual("Imagen: reducida a 960 × 720", sheet.Root.Q<Button>("equipo-imagen").text);
            Assert.AreEqual("Búsqueda: un edificio por intento", sheet.Root.Q<Button>("equipo-busqueda").text);
            sheet.SetDetectionTests(reducedImage: false, everyMap: true);
            Assert.AreEqual("Imagen: completa", sheet.Root.Q<Button>("equipo-imagen").text);
            Assert.AreEqual("Búsqueda: todos los mapas", sheet.Root.Q<Button>("equipo-busqueda").text);
        }

        [Test]
        public void TheCompassTestAsksToSwitchWithoutABuildingAndSaysWhatIsOn()
        {
            int compass = 0;
            sheet.CompassToggleRequested += () => compass++;
            sheet.SetTarget(null);
            sheet.Press(PaseoSheetAction.ToggleCompass, 0f);
            Assert.AreEqual(1, compass);
            Assert.IsEmpty(edits);

            var button = sheet.Root.Q<Button>("equipo-brujula");
            Assert.IsNotNull(button);
            Assert.IsFalse(PaseoUiKit.IsHidden(button), "it lives in the detection tests: shown in development builds");
            Assert.AreEqual("Brújula: sí", button.text, "on by default");
            sheet.SetDetectionTests(reducedImage: true, everyMap: false, compass: false);
            Assert.AreEqual("Brújula: no", button.text);
            sheet.SetDetectionTests(reducedImage: true, everyMap: false, compass: true);
            Assert.AreEqual("Brújula: sí", button.text);
            Assert.IsNotNull(sheet.Root.Q("equipo-pruebas").Q<Button>("equipo-brujula"), "inside the section that release builds hide");
        }

        [Test]
        public void TheOcclusionTestAsksToSwitchWithoutABuildingAndSaysWhatIsOn()
        {
            int occlusion = 0;
            sheet.OcclusionToggleRequested += () => occlusion++;
            sheet.SetTarget(null);
            sheet.Press(PaseoSheetAction.ToggleOcclusion, 0f);
            Assert.AreEqual(1, occlusion);
            Assert.IsEmpty(edits);

            var button = sheet.Root.Q<Button>("equipo-oclusion");
            Assert.IsNotNull(button);
            Assert.IsFalse(PaseoUiKit.IsHidden(button), "it lives in the detection tests: shown in development builds");
            sheet.SetDetectionTests(reducedImage: true, everyMap: false, occlusion: true);
            Assert.AreEqual("Oclusión: sí", button.text);
            sheet.SetDetectionTests(reducedImage: true, everyMap: false, occlusion: false);
            Assert.AreEqual("Oclusión: no", button.text);
            sheet.SetDetectionTests(reducedImage: true, everyMap: false, occlusion: true, occlusionUnsupported: true);
            StringAssert.StartsWith("Oclusión: no", button.text);
            StringAssert.Contains("teléfono", button.text);
        }

        [Test]
        public void TheContrastTestAsksToCycleWithoutABuildingAndSaysWhatIsOn()
        {
            int cycles = 0;
            sheet.ContrastCycleRequested += () => cycles++;
            sheet.SetTarget(null);
            sheet.Press(PaseoSheetAction.CycleContrast, 0f);
            Assert.AreEqual(1, cycles);
            Assert.IsEmpty(edits);

            var button = sheet.Root.Q<Button>("equipo-contraste");
            Assert.IsNotNull(button);
            Assert.IsFalse(PaseoUiKit.IsHidden(button), "it lives in the detection tests: shown in development builds");
            Assert.AreEqual("Contraste: normal", button.text, "normal by default");
            sheet.SetDetectionTests(reducedImage: true, everyMap: false, contrast: PaseoAbMode.Variant);
            Assert.AreEqual("Contraste: realzado", button.text);
            sheet.SetDetectionTests(reducedImage: true, everyMap: false, contrast: PaseoAbMode.Alternate);
            Assert.AreEqual("Contraste: alterno", button.text);
            Assert.IsNotNull(sheet.Root.Q("equipo-pruebas").Q<Button>("equipo-contraste"), "inside the section that release builds hide");
        }

        [Test]
        public void TheSolverTestAsksToCycleWithoutABuildingAndSaysWhatIsOn()
        {
            int cycles = 0;
            sheet.SolverCycleRequested += () => cycles++;
            sheet.SetTarget(null);
            sheet.Press(PaseoSheetAction.CycleSolver, 0f);
            Assert.AreEqual(1, cycles);
            Assert.IsEmpty(edits);

            var button = sheet.Root.Q<Button>("equipo-solver");
            Assert.IsNotNull(button);
            Assert.IsFalse(PaseoUiKit.IsHidden(button), "it lives in the detection tests: shown in development builds");
            Assert.AreEqual("Solver: normal", button.text, "normal by default");
            sheet.SetDetectionTests(reducedImage: true, everyMap: false, solver: PaseoAbMode.Variant);
            Assert.AreEqual("Solver: gravedad", button.text);
            sheet.SetDetectionTests(reducedImage: true, everyMap: false, solver: PaseoAbMode.Alternate);
            Assert.AreEqual("Solver: alterno", button.text);
            Assert.IsNotNull(sheet.Root.Q("equipo-pruebas").Q<Button>("equipo-solver"), "inside the section that release builds hide");
        }

        [Test]
        public void ThePriorTestAsksToSwitchWithoutABuildingAndSaysWhatIsOn()
        {
            int toggles = 0;
            sheet.PriorToggleRequested += () => toggles++;
            sheet.SetTarget(null);
            sheet.Press(PaseoSheetAction.TogglePrior, 0f);
            Assert.AreEqual(1, toggles);
            Assert.IsEmpty(edits);

            var button = sheet.Root.Q<Button>("equipo-prior");
            Assert.IsNotNull(button);
            Assert.IsFalse(PaseoUiKit.IsHidden(button), "it lives in the detection tests: shown in development builds");
            Assert.AreEqual("Prior: no", button.text, "off by default");
            sheet.SetDetectionTests(reducedImage: true, everyMap: false, prior: true);
            Assert.AreEqual("Prior: sí", button.text);
            sheet.SetDetectionTests(reducedImage: true, everyMap: false, prior: false);
            Assert.AreEqual("Prior: no", button.text);
            Assert.IsNotNull(sheet.Root.Q("equipo-pruebas").Q<Button>("equipo-prior"), "inside the section that release builds hide");
        }

        [Test]
        public void TheNewTestsDoNotDisturbTheOthersAndHideWithThem()
        {
            // Changing one test's text leaves the other buttons as they were.
            sheet.SetDetectionTests(reducedImage: false, everyMap: true, occlusion: false, compass: false,
                contrast: PaseoAbMode.Alternate, solver: PaseoAbMode.Variant, prior: true);
            Assert.AreEqual("Imagen: completa", sheet.Root.Q<Button>("equipo-imagen").text);
            Assert.AreEqual("Búsqueda: todos los mapas", sheet.Root.Q<Button>("equipo-busqueda").text);
            Assert.AreEqual("Oclusión: no", sheet.Root.Q<Button>("equipo-oclusion").text);
            Assert.AreEqual("Brújula: no", sheet.Root.Q<Button>("equipo-brujula").text);
            sheet.ShowDetectionTests(false);
            Assert.IsTrue(PaseoUiKit.IsHidden(sheet.Root.Q("equipo-pruebas")), "release builds hide the section with the three new buttons");
        }

        [Test]
        public void TheOcclusionTestHidesWithTheDetectionTests()
        {
            sheet.ShowDetectionTests(false);
            Assert.IsTrue(PaseoUiKit.IsHidden(sheet.Root.Q("equipo-pruebas")));
            Assert.IsNotNull(sheet.Root.Q("equipo-pruebas").Q<Button>("equipo-oclusion"), "inside the section that release builds hide");
        }

        [Test]
        public void TheDetectionTestsHideOutsideDevelopmentBuilds()
        {
            sheet.ShowDetectionTests(false);
            Assert.IsTrue(PaseoUiKit.IsHidden(sheet.Root.Q("equipo-pruebas")));
            sheet.ShowDetectionTests(true);
            Assert.IsFalse(PaseoUiKit.IsHidden(sheet.Root.Q("equipo-pruebas")));
        }

        [Test]
        public void OneRowPerMapWithItsTone()
        {
            bool created = sheet.SetStatus("v1.2.0", new[]
            {
                new PaseoMapRow("152259 Teología", "visible", PaseoTone.Ok),
                new PaseoMapRow("152242 EIC", "NO CARGÓ", PaseoTone.Error)
            });
            Assert.IsTrue(created);
            var rows = sheet.Root.Q("equipo-mapas");
            Assert.AreEqual(2, rows.childCount);
            Assert.IsTrue(rows[1][1].ClassListContains(PaseoTeamSheet.ToneClass(PaseoTone.Error)));
            Assert.IsFalse(rows[1][1].ClassListContains(PaseoTeamSheet.ToneClass(PaseoTone.Ok)));

            Assert.IsFalse(sheet.SetStatus("v1.2.0", new[] { new PaseoMapRow("152259 Teología", "visible", PaseoTone.Ok) }));
            Assert.IsTrue(PaseoUiKit.IsHidden(rows[1]));
        }
    }
}

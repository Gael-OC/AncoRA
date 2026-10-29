using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace AncorRA.AR
{
    /// <summary>
    /// The «Resolución de la imagen» section of the team sheet (every build): the size Immersal works at on each camera image,
    /// as three buttons, and the phone test that times this phone and picks one (PaseoPhoneTest). The panel only draws and
    /// raises events; the tour applies, saves and runs (PaseoUi wires them).
    /// </summary>
    public sealed class PaseoResolutionPanel
    {
        public const string OnClass = PaseoRecordPanel.OnClass;
        public const string TestText = "Probar este teléfono (~30 s)";

        readonly Label current;
        readonly Button testButton;
        readonly Label resultText;
        readonly List<(Button Button, int MaxPixels)> picks = new();
        bool running;

        /// <summary>A size was picked by hand: its max pixels. The owner applies it and saves it.</summary>
        public event Action<int> PickRequested;
        /// <summary>The test button was pressed while no test was running.</summary>
        public event Action TestRequested;
        /// <summary>A message for the sheet's notice line.</summary>
        public event Action<string> Notice;

        public VisualElement Root { get; }
        public bool Running => running;

        public PaseoResolutionPanel(PaseoResolutionChoice choice)
        {
            Root = new VisualElement { name = "equipo-resolucion" };
            Root.AddToClassList("paseo-section");
            Root.Add(PaseoUiKit.Text("Resolución de la imagen", "paseo-section__title"));

            current = PaseoUiKit.Text("", "paseo-sheet__target", strong: true);
            current.name = "resolucion-actual";
            Root.Add(current);

            var row = new VisualElement { name = "resolucion-opciones" };
            row.AddToClassList("paseo-picks");
            foreach (int maxPixels in PaseoResolution.Options)
            {
                var button = new Button { text = PaseoResolution.ShortName(maxPixels) };
                button.clicked += () => Choose(maxPixels);
                button.AddToClassList("paseo-pick");
                button.AddToClassList(PaseoUiKit.StrongClass);
                row.Add(button);
                picks.Add((button, maxPixels));
            }
            Root.Add(row);
            Root.Add(PaseoUiKit.Text("Más píxeles ubican mejor, pero cada intento tarda más.", "paseo-sheet__hint"));

            testButton = new Button(PressTest) { name = "resolucion-probar", text = TestText };
            testButton.AddToClassList("paseo-button");
            testButton.AddToClassList(PaseoUiKit.StrongClass);
            Root.Add(testButton);

            resultText = PaseoUiKit.Text("", "paseo-sheet__hint");
            resultText.name = "resolucion-resultado";
            PaseoUiKit.SetHidden(resultText, true);
            Root.Add(resultText);

            SetChoice(choice);
        }

        /// <summary>What is in use and why; marks its button.</summary>
        public void SetChoice(PaseoResolutionChoice value)
        {
            current.text = PaseoResolution.Describe(value);
            foreach (var (button, maxPixels) in picks)
                button.EnableInClassList(OnClass, maxPixels == value.MaxPixels);
        }

        /// <summary>The test button's look: its progress while a test runs, its label otherwise.</summary>
        public void SetRunning(bool on, string progress = "")
        {
            running = on;
            testButton.text = on ? (string.IsNullOrEmpty(progress) ? "Probando…" : progress) : TestText;
        }

        /// <summary>Shows what the test found, one line per size, and tells the sheet's notice line how it ended.</summary>
        public void SetResult(PaseoPhoneTestResult result)
        {
            if (result.Cancelled)
            {
                Notice?.Invoke("Prueba cancelada.");
                return;
            }
            var lines = result.Completed ? result.Lines() : new List<string> { result.Failure };
            resultText.text = string.Join("\n", lines);
            PaseoUiKit.SetHidden(resultText, false);
            Notice?.Invoke(result.Completed
                ? $"Prueba lista: se usa {PaseoResolution.ShortName(result.Chosen)} ({PaseoPhoneTestResult.Comma(result.TotalSeconds, 0)} s)."
                : result.Failure);
        }

        /// <summary>Pressing a size button by its text, as a tap does (tests).</summary>
        public bool Pick(string text)
        {
            var pick = picks.FirstOrDefault(p => p.Button.text == text);
            if (pick.Button == null)
                return false;
            Choose(pick.MaxPixels);
            return true;
        }

        /// <summary>A tap on the test button: ignored while a test runs.</summary>
        public void PressTest()
        {
            if (running)
                return;
            SetRunning(true, "Preparando la prueba…");
            TestRequested?.Invoke();
        }

        void Choose(int maxPixels)
        {
            if (running)
            {
                Notice?.Invoke("Espera a que termine la prueba.");
                return;
            }
            // Marked at once; the owner confirms with SetChoice from what it saved.
            SetChoice(new PaseoResolutionChoice(maxPixels, PaseoResolution.OriginChosen, DateTime.Now.Date));
            PickRequested?.Invoke(maxPixels);
        }
    }
}

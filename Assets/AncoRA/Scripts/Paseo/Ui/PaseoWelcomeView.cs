using System;
using UnityEngine.UIElements;

namespace AncorRA.AR
{
    /// <summary>
    /// Welcome screen: what the tour is, three steps, and the button that asks for the camera and the location. Built in
    /// code (no UXML) so EditMode tests can build and read it without a panel.
    /// </summary>
    public sealed class PaseoWelcomeView
    {
        public const string DefaultNote = "Usa la cámara y tu ubicación.";

        static readonly string[] Steps =
        {
            "Camina al punto que te indicamos.",
            "Apunta al edificio, sin apuro.",
            "Míralo en realidad aumentada."
        };

        readonly Label note;

        public event Action Started;

        public VisualElement Root { get; }
        public bool Visible => !PaseoUiKit.IsHidden(Root);

        public PaseoWelcomeView()
        {
            Root = new VisualElement { name = "bienvenida" };
            Root.AddToClassList("paseo-welcome");
            Root.Add(PaseoUiKit.Text("AncoRA", "paseo-welcome__title", strong: true));
            Root.Add(PaseoUiKit.Text("Paseo por la Escuela de Ingeniería", "paseo-welcome__subtitle"));
            for (int i = 0; i < Steps.Length; i++)
            {
                var step = new VisualElement();
                step.AddToClassList("paseo-step");
                step.Add(PaseoUiKit.Text((i + 1).ToString(), "paseo-step__badge", strong: true));
                step.Add(PaseoUiKit.Text(Steps[i], "paseo-step__text"));
                Root.Add(step);
            }
            var spacer = new VisualElement();
            spacer.AddToClassList("paseo-welcome__spacer");
            Root.Add(spacer);
            note = PaseoUiKit.Text(DefaultNote, "paseo-welcome__note");
            note.name = "bienvenida-nota";
            Root.Add(note);
            var start = new Button(PressStart) { name = "comenzar", text = "Comenzar" };
            start.AddToClassList("paseo-button");
            start.AddToClassList("paseo-button--primary");
            start.AddToClassList(PaseoUiKit.StrongClass);
            Root.Add(start);
        }

        public void PressStart() => Started?.Invoke();

        public void Show()
        {
            note.text = DefaultNote;
            PaseoUiKit.SetHidden(Root, false);
        }

        public void Hide() => PaseoUiKit.SetHidden(Root, true);

        public void ShowMessage(string message) => note.text = message;
    }
}

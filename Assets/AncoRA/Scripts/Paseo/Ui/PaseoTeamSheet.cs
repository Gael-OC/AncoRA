using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AncorRA.AR
{
    public enum PaseoSheetAction
    {
        Left, Right, Closer, Farther, Down, Up, TurnLeft, TurnRight,
        Narrower, Wider, Lower, Taller, Shallower, Deeper, ToggleSolid,
        NextStep, Copy, Restore, Close
    }

    /// <summary>A change the team asked for, already multiplied by the step: metres and degrees.</summary>
    public readonly struct PaseoBoxEdit
    {
        public readonly float Right;
        public readonly float Closer;
        public readonly float Up;
        public readonly float Turn;
        public readonly float Width;
        public readonly float Height;
        public readonly float Depth;
        public readonly bool ToggleSolid;

        public PaseoBoxEdit(float right = 0f, float closer = 0f, float up = 0f, float turn = 0f, float width = 0f,
            float height = 0f, float depth = 0f, bool toggleSolid = false)
        {
            Right = right;
            Closer = closer;
            Up = up;
            Turn = turn;
            Width = width;
            Height = height;
            Depth = depth;
            ToggleSolid = toggleSolid;
        }
    }

    /// <summary>
    /// Team sheet: slides up from the bottom with the state of every map and the box adjustment, moved in the box's own
    /// axes (PaseoBoxNudge). Buttons call Press, so tests drive it without a panel.
    /// </summary>
    public sealed class PaseoTeamSheet
    {
        public const string ClosedClass = "paseo-sheet--closed";
        public const float RestoreConfirmSeconds = 3f;

        static readonly float[] PositionSteps = { 1f, 0.25f, 0.05f };
        static readonly float[] AngleSteps = { 5f, 1f, 0.2f };
        static readonly float[] SizeSteps = { 2f, 0.5f, 0.1f };
        static readonly string[] StepNames = { "grueso", "medio", "fino" };

        readonly Label summary;
        readonly VisualElement rows;
        readonly Label target;
        readonly Label noTarget;
        readonly VisualElement controls;
        readonly Button stepButton;
        readonly Label toast;
        int stepIndex = 1;
        bool hasTarget;
        float restoreArmedUntil = float.NegativeInfinity;

        public event Action<PaseoBoxEdit> EditRequested;
        public event Action CopyRequested;
        public event Action RestoreRequested;

        public VisualElement Root { get; }
        public bool IsOpen => !Root.ClassListContains(ClosedClass);
        public string StepName => StepNames[stepIndex];

        public PaseoTeamSheet()
        {
            Root = new VisualElement { name = "equipo" };
            Root.AddToClassList("paseo-sheet");
            Root.AddToClassList(ClosedClass);
            var grabber = new VisualElement();
            grabber.AddToClassList("paseo-sheet__grabber");
            Root.Add(grabber);
            var header = new VisualElement();
            header.AddToClassList("paseo-sheet__header");
            header.Add(PaseoUiKit.Text("Equipo", "paseo-sheet__title", strong: true));
            header.Add(ActionButton("Cerrar", PaseoSheetAction.Close, "paseo-link"));
            Root.Add(header);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("paseo-sheet__scroll");
            Root.Add(scroll);

            var state = Section(scroll, "Estado");
            summary = PaseoUiKit.Text("", "paseo-sheet__summary");
            summary.name = "equipo-resumen";
            state.Add(summary);
            rows = new VisualElement { name = "equipo-mapas" };
            state.Add(rows);

            var adjust = Section(scroll, "Ajuste de caja");
            target = PaseoUiKit.Text("", "paseo-sheet__target", strong: true);
            target.name = "equipo-objetivo";
            adjust.Add(target);
            noTarget = PaseoUiKit.Text("Ubícate con un edificio para ajustar su caja.", "paseo-sheet__hint");
            noTarget.name = "equipo-sin-objetivo";
            adjust.Add(noTarget);
            controls = new VisualElement { name = "equipo-controles" };
            adjust.Add(controls);
            stepButton = ActionButton("", PaseoSheetAction.NextStep, "paseo-nudge");
            stepButton.name = "equipo-paso";
            stepButton.AddToClassList("paseo-nudge--wide");
            controls.Add(stepButton);
            var grid = new VisualElement();
            grid.AddToClassList("paseo-nudges");
            controls.Add(grid);
            foreach (var (text, action) in new[]
                     {
                         ("Izquierda", PaseoSheetAction.Left), ("Derecha", PaseoSheetAction.Right),
                         ("Más lejos", PaseoSheetAction.Farther), ("Más cerca", PaseoSheetAction.Closer),
                         ("Bajar", PaseoSheetAction.Down), ("Subir", PaseoSheetAction.Up),
                         ("Girar a la izquierda", PaseoSheetAction.TurnLeft), ("Girar a la derecha", PaseoSheetAction.TurnRight),
                         ("Más angosta", PaseoSheetAction.Narrower), ("Más ancha", PaseoSheetAction.Wider),
                         ("Más baja", PaseoSheetAction.Lower), ("Más alta", PaseoSheetAction.Taller),
                         ("Menos fondo", PaseoSheetAction.Shallower), ("Más fondo", PaseoSheetAction.Deeper)
                     })
                grid.Add(ActionButton(text, action, "paseo-nudge"));
            var solid = ActionButton("Relleno: sólido o transparente", PaseoSheetAction.ToggleSolid, "paseo-nudge");
            solid.AddToClassList("paseo-nudge--wide");
            controls.Add(solid);

            var footer = new VisualElement();
            footer.AddToClassList("paseo-sheet__footer");
            scroll.Add(footer);
            footer.Add(ActionButton("Copiar valores", PaseoSheetAction.Copy, "paseo-button"));
            footer.Add(ActionButton("Restaurar escena", PaseoSheetAction.Restore, "paseo-button"));
            toast = PaseoUiKit.Text("", "paseo-toast");
            toast.name = "equipo-aviso";
            scroll.Add(toast);

            SetTarget(null);
            UpdateStepButton();
        }

        public void Toggle() => Root.EnableInClassList(ClosedClass, IsOpen);

        public void Close() => Root.AddToClassList(ClosedClass);

        public void ShowToast(string message) => toast.text = message;

        public static string ToneClass(PaseoTone tone) => "tone-" + tone.ToString().ToLowerInvariant();

        /// <returns>True when a row was created, so the caller applies the fonts to it.</returns>
        public bool SetStatus(string text, IReadOnlyList<PaseoMapRow> mapRows)
        {
            summary.text = text;
            bool created = false;
            while (rows.childCount < mapRows.Count)
            {
                var row = new VisualElement();
                row.AddToClassList("paseo-row");
                row.Add(PaseoUiKit.Text("", "paseo-row__title", strong: true));
                row.Add(PaseoUiKit.Text("", "paseo-row__detail"));
                rows.Add(row);
                created = true;
            }
            for (int i = 0; i < rows.childCount; i++)
            {
                var row = rows[i];
                bool used = i < mapRows.Count;
                PaseoUiKit.SetHidden(row, !used);
                if (!used)
                    continue;
                ((Label)row[0]).text = mapRows[i].Title;
                var detail = (Label)row[1];
                detail.text = mapRows[i].Detail;
                foreach (PaseoTone tone in Enum.GetValues(typeof(PaseoTone)))
                    detail.EnableInClassList(ToneClass(tone), tone == mapRows[i].Tone);
            }
            return created;
        }

        public void SetTarget(string description)
        {
            hasTarget = !string.IsNullOrEmpty(description);
            target.text = description ?? "";
            PaseoUiKit.SetHidden(target, !hasTarget);
            PaseoUiKit.SetHidden(controls, !hasTarget);
            PaseoUiKit.SetHidden(noTarget, hasTarget);
        }

        public void Press(PaseoSheetAction action, float now)
        {
            switch (action)
            {
                case PaseoSheetAction.Close:
                    Close();
                    return;
                case PaseoSheetAction.NextStep:
                    stepIndex = (stepIndex + 1) % StepNames.Length;
                    UpdateStepButton();
                    return;
                case PaseoSheetAction.Copy:
                    CopyRequested?.Invoke();
                    return;
                case PaseoSheetAction.Restore:
                    if (now < restoreArmedUntil)
                    {
                        restoreArmedUntil = float.NegativeInfinity;
                        RestoreRequested?.Invoke();
                    }
                    else
                    {
                        restoreArmedUntil = now + RestoreConfirmSeconds;
                        ShowToast("Toca otra vez para borrar lo guardado en este teléfono.");
                    }
                    return;
            }
            if (!hasTarget)
                return;
            float p = PositionSteps[stepIndex], a = AngleSteps[stepIndex], s = SizeSteps[stepIndex];
            EditRequested?.Invoke(action switch
            {
                PaseoSheetAction.Left => new PaseoBoxEdit(right: -p),
                PaseoSheetAction.Right => new PaseoBoxEdit(right: p),
                PaseoSheetAction.Closer => new PaseoBoxEdit(closer: p),
                PaseoSheetAction.Farther => new PaseoBoxEdit(closer: -p),
                PaseoSheetAction.Down => new PaseoBoxEdit(up: -p),
                PaseoSheetAction.Up => new PaseoBoxEdit(up: p),
                PaseoSheetAction.TurnLeft => new PaseoBoxEdit(turn: -a),
                PaseoSheetAction.TurnRight => new PaseoBoxEdit(turn: a),
                PaseoSheetAction.Narrower => new PaseoBoxEdit(width: -s),
                PaseoSheetAction.Wider => new PaseoBoxEdit(width: s),
                PaseoSheetAction.Lower => new PaseoBoxEdit(height: -s),
                PaseoSheetAction.Taller => new PaseoBoxEdit(height: s),
                PaseoSheetAction.Shallower => new PaseoBoxEdit(depth: -s),
                PaseoSheetAction.Deeper => new PaseoBoxEdit(depth: s),
                PaseoSheetAction.ToggleSolid => new PaseoBoxEdit(toggleSolid: true),
                _ => default
            });
        }

        Button ActionButton(string text, PaseoSheetAction action, string className)
        {
            var button = new Button(() => Press(action, Time.realtimeSinceStartup)) { text = text };
            button.AddToClassList(className);
            button.AddToClassList(PaseoUiKit.StrongClass);
            return button;
        }

        static VisualElement Section(VisualElement parent, string title)
        {
            var section = new VisualElement();
            section.AddToClassList("paseo-section");
            section.Add(PaseoUiKit.Text(title, "paseo-section__title"));
            parent.Add(section);
            return section;
        }

        void UpdateStepButton() => stepButton.text = $"Paso: {StepNames[stepIndex]}";
    }
}

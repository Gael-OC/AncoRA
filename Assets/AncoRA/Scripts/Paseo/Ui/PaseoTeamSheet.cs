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
        NextStep, Copy, Restore, Close, Share
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
        readonly Button solidButton;
        readonly Label toast;
        readonly ScrollView scroll;
        VisualElement demoMapPicks;
        Label demoMapStatus;
        Label occlusionStatus;
        VisualElement occlusionPicks;
        VisualElement rangePicks;
        VisualElement buildingPicks;
        Button diagnosticBoxes;
        int stepIndex = 1;
        bool hasTarget;
        float restoreArmedUntil = float.NegativeInfinity;

        public event Action<PaseoBoxEdit> EditRequested;
        public event Action CopyRequested;
        public event Action RestoreRequested;
        // Zip the session logs and photos and open Android's share sheet (PaseoShare).
        public event Action ShareRequested;
        public event Action<int> DemoMapRequested;
        public event Action<PaseoOcclusionMode> OcclusionRequested;
        public event Action<float> OcclusionRangeRequested;
        public event Action<string> EditBuildingRequested;
        public event Action DiagnosticBoxesRequested;
        // Every button pressed, for the debug log.
        public event Action<PaseoSheetAction> Pressed;

        public VisualElement Root { get; }
        /// <summary>Record mode section; null until <see cref="AttachRecorder"/> (development builds only).</summary>
        public PaseoRecordPanel Record { get; private set; }
        /// <summary>Image size section; null until <see cref="AttachResolution"/>.</summary>
        public PaseoResolutionPanel Resolution { get; private set; }
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
            scroll = new ScrollView(ScrollViewMode.Vertical);
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
            solidButton = ActionButton("Relleno: sólido o transparente", PaseoSheetAction.ToggleSolid, "paseo-nudge");
            solidButton.name = "equipo-relleno";
            solidButton.AddToClassList("paseo-nudge--wide");
            controls.Add(solidButton);

            var footer = new VisualElement();
            footer.AddToClassList("paseo-sheet__footer");
            scroll.Add(footer);
            footer.Add(ActionButton("Copiar valores", PaseoSheetAction.Copy, "paseo-button"));
            footer.Add(ActionButton("Restaurar escena", PaseoSheetAction.Restore, "paseo-button"));
            var share = ActionButton("Compartir registro", PaseoSheetAction.Share, "paseo-button");
            share.name = "equipo-compartir";
            footer.Add(share);
            toast = PaseoUiKit.Text("", "paseo-toast");
            toast.name = "equipo-aviso";
            scroll.Add(toast);

            SetTarget(null);
            UpdateStepButton();
        }

        /// <summary>Adds the «Grabar» section above the state of the maps; its notices go to the sheet's notice line.</summary>
        public PaseoRecordPanel AttachRecorder(PaseoRecordSelection selection)
        {
            if (Record != null)
                return Record;
            Record = new PaseoRecordPanel(selection);
            Record.Notice += ShowToast;
            scroll.Insert(0, Record.Root);
            return Record;
        }

        /// <summary>Adds the «Resolución de la imagen» section at the top of the sheet; its notices go to the sheet's notice line.</summary>
        public PaseoResolutionPanel AttachResolution(PaseoResolutionChoice choice)
        {
            if (Resolution != null)
                return Resolution;
            Resolution = new PaseoResolutionPanel(choice);
            Resolution.Notice += ShowToast;
            scroll.Insert(0, Resolution.Root);
            return Resolution;
        }

        public void AttachDemoMaps(IReadOnlyList<int> mapIds, int selected)
        {
            if (demoMapPicks != null || mapIds.Count < 2)
                return;
            var section = new VisualElement { name = "equipo-mapa-demo" };
            section.AddToClassList("paseo-section");
            section.Add(PaseoUiKit.Text("Mapa de Teología · demo", "paseo-section__title"));
            demoMapStatus = PaseoUiKit.Text($"En uso: {selected}", "paseo-sheet__target", strong: true);
            section.Add(demoMapStatus);
            demoMapPicks = new VisualElement();
            demoMapPicks.AddToClassList("paseo-picks");
            foreach (int id in mapIds)
            {
                var button = new Button(() => PickDemoMap(id)) { text = id.ToString(), name = $"demo-mapa-{id}" };
                button.AddToClassList("paseo-pick");
                button.AddToClassList(PaseoUiKit.StrongClass);
                button.EnableInClassList(PaseoRecordPanel.OnClass, id == selected);
                demoMapPicks.Add(button);
            }
            section.Add(demoMapPicks);
            section.Add(PaseoUiKit.Text("Cambiar reinicia la ubicación. Vuelve a tocar Comenzar y apunta al mismo lugar para comparar.", "paseo-sheet__hint"));
            scroll.Insert(0, section);
        }

        public void SetDemoMapChanging(bool changing, int selected)
        {
            if (demoMapPicks == null)
                return;
            demoMapPicks.SetEnabled(!changing);
            demoMapStatus.text = changing ? "Esperando el intento actual para cambiar…" : $"En uso: {selected}";
        }

        public bool PickDemoMap(int mapId)
        {
            var button = demoMapPicks?.Q<Button>($"demo-mapa-{mapId}");
            if (button == null || !button.enabledInHierarchy)
                return false;
            DemoMapRequested?.Invoke(mapId);
            return true;
        }

        public void AttachOcclusion()
        {
            if (occlusionPicks != null) return;
            var section = Section(scroll, "Oclusión · prueba");
            occlusionStatus = PaseoUiKit.Text("", "paseo-sheet__target", strong: true);
            section.Add(occlusionStatus);
            occlusionPicks = new VisualElement();
            occlusionPicks.AddToClassList("paseo-picks");
            foreach (var (text, mode) in new[] { ("Sin", PaseoOcclusionMode.Off), ("Cercana", PaseoOcclusionMode.Near), ("Normal", PaseoOcclusionMode.Normal) })
                occlusionPicks.Add(PickButton(text, $"oclusion-{(int)mode}", () => OcclusionRequested?.Invoke(mode)));
            section.Add(occlusionPicks);
            rangePicks = new VisualElement();
            rangePicks.AddToClassList("paseo-picks");
            foreach (int meters in new[] { 3, 5, 8 })
                rangePicks.Add(PickButton($"{meters} m", $"oclusion-distancia-{meters}", () => OcclusionRangeRequested?.Invoke(meters)));
            section.Add(rangePicks);
            section.Add(PaseoUiKit.Text("Cercana conserva solo obstáculos próximos con profundidad confiable. Compara los modos sin cambiar de lugar.", "paseo-sheet__hint"));
            scroll.Remove(section);
            scroll.Insert(0, section);
        }

        public void SetOcclusion(PaseoOcclusionMode mode, float range, string status)
        {
            if (occlusionPicks == null) return;
            occlusionStatus.text = status;
            foreach (var child in occlusionPicks.Children())
                child.EnableInClassList(PaseoRecordPanel.OnClass, child.name == $"oclusion-{(int)mode}");
            rangePicks.SetEnabled(mode == PaseoOcclusionMode.Near);
            foreach (var child in rangePicks.Children())
                child.EnableInClassList(PaseoRecordPanel.OnClass, child.name == $"oclusion-distancia-{range:F0}");
        }

        public void AttachDemoBoxes(IReadOnlyList<PaseoMapContent> buildings)
        {
            if (buildingPicks != null) return;
            var section = Section(scroll, "Cajas para la demo");
            diagnosticBoxes = PickButton("Mostrar cajas", "demo-cajas", () => DiagnosticBoxesRequested?.Invoke());
            section.Add(diagnosticBoxes);
            buildingPicks = new VisualElement();
            buildingPicks.AddToClassList("paseo-picks");
            buildingPicks.Add(PickButton("Automático", "ajustar-auto", () => EditBuildingRequested?.Invoke(null)));
            foreach (var building in buildings)
            {
                if (building.BoxHidden) continue;
                buildingPicks.Add(PickButton(building.BuildingName, "ajustar-" + building.BuildingId,
                    () => EditBuildingRequested?.Invoke(building.BuildingId)));
            }
            section.Add(buildingPicks);
            section.Add(PaseoUiKit.Text("Elige un edificio y usa Ajuste de caja para mover, girar y cambiar su tamaño. Se guarda en este teléfono. Las cajas muestran solo bordes sobre los modelos.", "paseo-sheet__hint"));
            scroll.Remove(section);
            scroll.Insert(0, section);
        }

        public void SetDemoBoxes(bool shown, string buildingId)
        {
            if (buildingPicks == null) return;
            diagnosticBoxes.text = shown ? "Ocultar cajas" : "Mostrar cajas";
            diagnosticBoxes.EnableInClassList(PaseoRecordPanel.OnClass, shown);
            foreach (var child in buildingPicks.Children())
                child.EnableInClassList(PaseoRecordPanel.OnClass, child.name == "ajustar-" + (buildingId ?? "auto"));
        }

        static Button PickButton(string text, string name, Action action)
        {
            var button = new Button(action) { text = text, name = name };
            button.AddToClassList("paseo-pick");
            button.AddToClassList(PaseoUiKit.StrongClass);
            return button;
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

        public void SetTarget(string description, bool opaqueModel = false)
        {
            hasTarget = !string.IsNullOrEmpty(description);
            target.text = description ?? "";
            PaseoUiKit.SetHidden(target, !hasTarget);
            PaseoUiKit.SetHidden(controls, !hasTarget);
            PaseoUiKit.SetHidden(noTarget, hasTarget);
            PaseoUiKit.SetHidden(solidButton, opaqueModel);
        }

        public void Press(PaseoSheetAction action, float now)
        {
            Pressed?.Invoke(action);
            switch (action)
            {
                case PaseoSheetAction.Share:
                    ShareRequested?.Invoke();
                    return;
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

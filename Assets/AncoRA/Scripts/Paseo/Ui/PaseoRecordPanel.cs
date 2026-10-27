using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace AncorRA.AR
{
    /// <summary>
    /// The «Grabar» section of the team sheet: the switch, the recording point (building, point, distance) and the light as
    /// rows of buttons, so nothing needs a keyboard, and the two housekeeping buttons for the recordings on the phone.
    /// The choice lives in the recorder's PaseoRecordSelection; the buttons only change it and mark what is chosen.
    /// </summary>
    public sealed class PaseoRecordPanel
    {
        public const string OnClass = "paseo-pick--on";

        readonly PaseoRecordSelection selection;
        readonly Button toggle;
        readonly Label chosen;
        readonly Label state;
        readonly VisualElement buildingRow;
        readonly List<(Button Button, Action Choose, Func<bool> Chosen)> picks = new();
        readonly List<string> buildingCodes = new();
        bool enabled;
        float deleteArmedUntil = float.NegativeInfinity;

        public const float DeleteConfirmSeconds = 3f;

        /// <summary>The switch was pressed: the state the team wants.</summary>
        public event Action<bool> ToggleRequested;
        public event Action CopyRequested;
        public event Action DeleteRequested;
        /// <summary>A message for the sheet's notice line.</summary>
        public event Action<string> Notice;

        public VisualElement Root { get; }

        public PaseoRecordPanel(PaseoRecordSelection selection)
        {
            this.selection = selection;
            Root = new VisualElement { name = "equipo-grabar" };
            Root.AddToClassList("paseo-section");
            Root.Add(PaseoUiKit.Text("Grabar (banco de pruebas)", "paseo-section__title"));

            toggle = new Button(() => ToggleRequested?.Invoke(!enabled)) { name = "grabar-interruptor" };
            toggle.AddToClassList("paseo-button");
            toggle.AddToClassList(PaseoUiKit.StrongClass);
            Root.Add(toggle);

            chosen = PaseoUiKit.Text("", "paseo-sheet__target", strong: true);
            chosen.name = "grabar-punto";
            Root.Add(chosen);

            Root.Add(PaseoUiKit.Text("Edificio", "paseo-sheet__hint"));
            buildingRow = Picks("grabar-edificios");
            Root.Add(buildingRow);
            Root.Add(PaseoUiKit.Text("Punto", "paseo-sheet__hint"));
            var points = Picks("grabar-puntos");
            foreach (string point in PaseoRecordSelection.Points)
                AddPick(points, point, () => selection.SetPoint(point), () => selection.Point == point);
            Root.Add(points);
            Root.Add(PaseoUiKit.Text("Distancia", "paseo-sheet__hint"));
            var distances = Picks("grabar-distancias");
            foreach (int meters in PaseoRecordSelection.Distances)
                AddPick(distances, $"{meters} m", () => selection.SetMeters(meters), () => selection.Meters == meters);
            Root.Add(distances);
            Root.Add(PaseoUiKit.Text("Luz", "paseo-sheet__hint"));
            var lights = Picks("grabar-luz");
            foreach (string light in PaseoRecordSelection.Lights)
                AddPick(lights, light, () => selection.SetLight(light), () => selection.Light == light);
            Root.Add(lights);

            state = PaseoUiKit.Text("", "paseo-sheet__hint");
            state.name = "grabar-estado";
            Root.Add(state);

            var copy = new Button(() => CopyRequested?.Invoke()) { name = "grabar-copiar", text = "Copiar grabaciones a Descargas" };
            copy.AddToClassList("paseo-button");
            copy.AddToClassList(PaseoUiKit.StrongClass);
            Root.Add(copy);
            var delete = new Button(PressDelete) { name = "grabar-borrar", text = "Borrar grabaciones del teléfono" };
            delete.AddToClassList("paseo-button");
            delete.AddToClassList(PaseoUiKit.StrongClass);
            Root.Add(delete);

            SetState(false, "");
            Refresh();
        }

        /// <summary>The buildings to pick from, by id; the label uses their short code (PaseoRecordSelection.BuildingCode).</summary>
        public void SetBuildings(IEnumerable<string> buildingIds)
        {
            var codes = buildingIds.Select(PaseoRecordSelection.BuildingCode).ToList();
            if (codes.SequenceEqual(buildingCodes))
                return;
            buildingCodes.Clear();
            buildingCodes.AddRange(codes);
            picks.RemoveAll(p => p.Button.parent == buildingRow);
            buildingRow.Clear();
            foreach (string code in codes)
                AddPick(buildingRow, code, () => selection.SetBuilding(code), () => selection.Building == code);
            Refresh();
        }

        /// <summary>The switch's look and the line under the pickers (counts, free space, why it stopped).</summary>
        public void SetState(bool on, string summary)
        {
            enabled = on;
            toggle.text = on ? "Grabando: toca para parar" : "Grabar: apagado, toca para empezar";
            toggle.EnableInClassList("paseo-button--recording", on);
            state.text = summary;
            chosen.text = selection.Describe();
        }

        /// <summary>Marks the chosen button of each row; called after any change of the selection.</summary>
        public void Refresh()
        {
            foreach (var (button, _, isChosen) in picks)
                button.EnableInClassList(OnClass, isChosen());
            chosen.text = selection.Describe();
        }

        /// <summary>Pressing a picker button by its text, as a tap does (tests).</summary>
        public bool Pick(string text)
        {
            var pick = picks.FirstOrDefault(p => p.Button.text == text);
            if (pick.Button == null)
                return false;
            Choose(pick.Choose);
            return true;
        }

        public void PressToggle() => ToggleRequested?.Invoke(!enabled);

        public void PressDelete() => PressDelete(UnityEngine.Time.realtimeSinceStartup);

        /// <summary>Deleting needs a second tap within <see cref="DeleteConfirmSeconds"/>.</summary>
        public void PressDelete(float now)
        {
            if (now < deleteArmedUntil)
            {
                deleteArmedUntil = float.NegativeInfinity;
                DeleteRequested?.Invoke();
                return;
            }
            deleteArmedUntil = now + DeleteConfirmSeconds;
            Notice?.Invoke("Toca otra vez para borrar las grabaciones guardadas en este teléfono (no las de Descargas).");
        }

        static VisualElement Picks(string name)
        {
            var row = new VisualElement { name = name };
            row.AddToClassList("paseo-picks");
            return row;
        }

        void AddPick(VisualElement row, string text, Action choose, Func<bool> isChosen)
        {
            var button = new Button { text = text };
            button.clicked += () => Choose(choose);
            button.AddToClassList("paseo-pick");
            button.AddToClassList(PaseoUiKit.StrongClass);
            row.Add(button);
            picks.Add((button, choose, isChosen));
        }

        void Choose(Action choose)
        {
            choose();
            Refresh();
        }
    }
}

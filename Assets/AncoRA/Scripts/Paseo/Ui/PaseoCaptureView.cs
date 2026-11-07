using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AncorRA.AR
{
    /// <summary>A dedicated field recorder: choose a take, record, then export everything in one place.</summary>
    public sealed class PaseoCaptureView
    {
        readonly PaseoRecordSelection selection;
        readonly DropdownField shotPicker, lightPicker, buildingPicker, distancePicker, playbackPicker;
        readonly Label instructions, status, inventory;
        readonly TextField notes;
        readonly Toggle withVideo;
        readonly Button record, copy, play, next;
        readonly List<string> playbackFolders = new();
        readonly HashSet<string> completed = new();
        readonly VisualElement choices;
        int index;
        public VisualElement Root { get; }
        public PaseoCaptureShot Shot => PaseoCapturePlan.Shots[index];
        public bool WithVideo => withVideo.value;
        public string Notes => notes.value ?? "";
        public string PlaybackFolder => playbackPicker.index >= 0 && playbackPicker.index < playbackFolders.Count ? playbackFolders[playbackPicker.index] : "";
        public event Action RecordRequested, CopyRequested, PlayRequested, CloseRequested;

        public PaseoCaptureView(PaseoRecordSelection selection)
        {
            this.selection = selection;
            Root = new VisualElement { name = "capturas-campus" };
            Root.AddToClassList("paseo-sheet");
            Root.AddToClassList("paseo-capture");
            Root.style.maxHeight = Length.Percent(88);
            var scroll = new ScrollView { name = "capturas-scroll" };
            scroll.AddToClassList("paseo-sheet__scroll");
            Root.Add(scroll);
            scroll.Add(PaseoUiKit.Text("Capturas para trabajar desde casa", "paseo-sheet__title", true));
            scroll.Add(PaseoUiKit.Text("1. Elige toma y luz.  2. Graba y espera el guardado.  3. Copia todo a Descargas al terminar.", "paseo-sheet__hint"));
            choices = new VisualElement();
            scroll.Add(choices);
            buildingPicker = new DropdownField("Edificio", new List<string> { "Teología", "Ciencias Básicas", "X1", "EIC" }, 0);
            buildingPicker.RegisterValueChangedCallback(_ =>
            {
                selection.SetBuilding(new[] { "TEO", "CB", "X1", "EIC" }[buildingPicker.index]);
                completed.Clear();
                UpdateInstructions();
            });
            choices.Add(buildingPicker);
            shotPicker = new DropdownField("Toma", PaseoCapturePlan.Shots.Select(s => s.Title).ToList(), 0);
            shotPicker.RegisterValueChangedCallback(_ => Select(shotPicker.index));
            choices.Add(shotPicker);
            instructions = PaseoUiKit.Text("", "paseo-sheet__target");
            choices.Add(instructions);
            lightPicker = new DropdownField("Luz real", new List<string> { "Elegir luz…", "sol", "nublado", "sombra", "parcial" }, 0);
            lightPicker.RegisterValueChangedCallback(_ => selection.SetLight(lightPicker.index == 0 ? "" : lightPicker.value));
            choices.Add(lightPicker);
            distancePicker = new DropdownField("Distancia aproximada · solo contexto", new List<string> { "3 m", "5 m", "10 m", "15 m", "20 m", "25 m" }, 2);
            distancePicker.RegisterValueChangedCallback(_ => selection.SetMeters(int.Parse(distancePicker.value.Split(' ')[0])));
            choices.Add(distancePicker);
            withVideo = new Toggle("Video ARCore + fotos y metadatos") { value = true, name = "captura-video" };
            choices.Add(withVideo);
            choices.Add(PaseoUiKit.Text("Desmarca solo si el teléfono no logra grabar video. Las fotos originales y sus metadatos se guardan igualmente.", "paseo-sheet__hint"));
            notes = new TextField("Notas / medidas / punto de inicio") { multiline = true, name = "captura-notas" };
            choices.Add(notes);
            record = AddButton(scroll, "Grabar esta toma", "captura-grabar", () => RecordRequested?.Invoke(), true);
            status = PaseoUiKit.Text("Primero haz la prueba en casa y reprodúcela antes de salir.", "paseo-sheet__target");
            status.name = "captura-estado";
            scroll.Add(status);
            next = AddButton(scroll, "Siguiente toma", "captura-siguiente", Next);
            inventory = PaseoUiKit.Text("", "paseo-sheet__hint");
            scroll.Add(inventory);
            copy = AddButton(scroll, "Copiar TODO a Descargas", "captura-copiar", () => CopyRequested?.Invoke(), true);
            scroll.Add(PaseoUiKit.Text("Incluye video, PNG originales, metadatos, notas y registros. Carpeta: Descargas/AncoRA/grabaciones. Espera el mensaje de copia terminada. Conserva las sesiones en el teléfono.", "paseo-sheet__hint"));
            var replay = new Foldout { text = "Comprobar / reproducir una sesión", value = false };
            scroll.Add(replay);
            playbackPicker = new DropdownField("Video guardado", new List<string> { "Sin videos todavía" }, 0);
            replay.Add(playbackPicker);
            play = AddButton(replay, "Reproducir sesión", "captura-reproducir", () => PlayRequested?.Invoke());
            replay.Add(PaseoUiKit.Text("Reproduce cámara y sensores en Android; puede cambiar el resultado de ARCore entre ejecuciones. Durante la reproducción no se graba material nuevo.", "paseo-sheet__hint"));
            AddButton(scroll, "Ver cámara", "captura-cerrar", () => CloseRequested?.Invoke());
            selection.SetBuilding("TEO");
            Select(0);
        }

        public void Select(int shot)
        {
            index = Mathf.Clamp(shot, 0, PaseoCapturePlan.Shots.Length - 1);
            shotPicker.SetValueWithoutNotify(shotPicker.choices[index]);
            selection.SetPoint(index % 2 == 0 ? "A" : "B");
            selection.SetMeters(Shot.Meters);
            distancePicker.SetValueWithoutNotify(Shot.Meters + " m");
            notes.SetValueWithoutNotify("");
            UpdateInstructions();
        }

        public void Next() => Select((index + 1) % PaseoCapturePlan.Shots.Length);
        void UpdateInstructions() => instructions.text = $"{index + 1}/{PaseoCapturePlan.Shots.Length} · {Shot.Seconds} s · {Shot.Role}" +
            (completed.Contains(Shot.Id) ? " · guardada" : "") + "\n" + Shot.Instructions +
            (selection.Building == "TEO" ? "" : "\nLas referencias a ventanas corresponden a Teología. En este edificio usa recorridos y consultas equivalentes.");

        public void SetState(bool busy, bool playing, bool ready)
        {
            choices.SetEnabled(!busy && !playing);
            record.SetEnabled(!busy && !playing && ready && selection.Complete);
            copy.SetEnabled(!busy && !playing);
            next.SetEnabled(!busy && !playing);
            playbackPicker.SetEnabled(!busy && !playing);
            play.SetEnabled(!busy && playbackFolders.Count > 0);
            play.text = playing ? "Volver a cámara en vivo" : "Reproducir sesión";
        }

        public void ShowMessage(string text) => status.text = text;
        public void Show(bool visible) => PaseoUiKit.SetHidden(Root, !visible);
        public bool Visible => !PaseoUiKit.IsHidden(Root);

        public void RefreshSaved(string root)
        {
            completed.Clear();
            playbackFolders.Clear();
            int sessions = 0, interrupted = 0;
            foreach (string folder in PaseoRecordings.Sessions(root).Reverse())
            {
                string meta = Path.Combine(folder, "sesion.json");
                if (!File.Exists(meta)) continue;
                try
                {
                    var session = JsonUtility.FromJson<PaseoCaptureSession>(File.ReadAllText(meta));
                    if (session == null) continue;
                    sessions++;
                    if (session.Complete && session.edificio == selection.Building) completed.Add(session.toma);
                    if (!session.Complete) interrupted++;
                    if (session.video_cerrado && File.Exists(Path.Combine(folder, PaseoSessionVideo.FileName))) playbackFolders.Add(folder);
                }
                catch (Exception) { interrupted++; }
            }
            playbackPicker.choices = playbackFolders.Count == 0 ? new List<string> { "Sin videos todavía" }
                : playbackFolders.Select(Path.GetFileName).ToList();
            playbackPicker.index = 0;
            inventory.text = $"{sessions} sesiones · {completed.Count}/{PaseoCapturePlan.Shots.Length} tomas completas de {selection.Building}" +
                (interrupted > 0 ? $" · {interrupted} incompletas (se conservan; revisar o repetir)" : "");
            UpdateInstructions();
        }

        static Button AddButton(VisualElement parent, string text, string name, Action action, bool primary = false)
        {
            var button = new Button(action) { text = text, name = name };
            button.AddToClassList("paseo-button");
            button.AddToClassList(PaseoUiKit.StrongClass);
            if (primary) button.AddToClassList("paseo-button--primary");
            parent.Add(button);
            return button;
        }
    }
}

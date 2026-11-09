using System;
using UnityEngine.UIElements;

namespace AncorRA.AR
{
    /// <summary>
    /// The overlay on the camera: status pill on top with the search activity under it, the card at the bottom (a hint,
    /// or the building that was recognised) a help button that brings the welcome back, the switch of the animated park and
    /// the era selector under the card, with the year shown large for a moment after each change.
    /// </summary>
    public sealed class PaseoCameraView
    {
        public const string EnteringClass = "paseo-card--entering";
        public const string PopClass = "paseo-activity--pop";

        readonly VisualElement pill;
        readonly Label status;
        readonly VisualElement activity;
        readonly Label activityText;
        readonly Label recording;
        readonly VisualElement card;
        readonly Label title;
        readonly Label chip;
        readonly Label subtitle;
        readonly Button stopCapture;
        bool capturing;
        PaseoActivityLine shownActivity;

        public const string ParkOffClass = "paseo-park--off";

        readonly Button park;
        public const string EpochOnClass = "paseo-epoch--on";
        public const string EpochFlashShownClass = "paseo-epoch-flash--shown";
        readonly Button[] epochButtons = new Button[PaseoEpochs.Count];
        readonly VisualElement epochFlash;
        readonly Label epochFlashYear;
        IVisualElementScheduledItem epochFlashHide;

        public event Action HelpRequested;
        public event Action ParkToggled;
        public event Action<PaseoEpoch> EpochRequested;
        public event Action StopCaptureRequested;

        public VisualElement Root { get; }
        public PaseoCard Card { get; private set; } = PaseoCard.None;

        public PaseoCameraView()
        {
            Root = new VisualElement { name = "camara", pickingMode = PickingMode.Ignore };
            Root.AddToClassList("paseo-camera");

            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.AddToClassList("paseo-top");
            pill = new VisualElement { name = "estado" };
            pill.AddToClassList("paseo-pill");
            var dot = new VisualElement();
            dot.AddToClassList("paseo-pill__dot");
            pill.Add(dot);
            status = PaseoUiKit.Text("", "paseo-pill__text");
            status.name = "estado-texto";
            pill.Add(status);
            top.Add(pill);
            activity = new VisualElement { name = "actividad", pickingMode = PickingMode.Ignore };
            activity.AddToClassList("paseo-activity");
            var activityDot = new VisualElement();
            activityDot.AddToClassList("paseo-activity__dot");
            activity.Add(activityDot);
            activityText = PaseoUiKit.Text("", "paseo-activity__text");
            activityText.name = "actividad-texto";
            activity.Add(activityText);
            top.Add(activity);
            PaseoUiKit.SetHidden(activity, true);
            recording = PaseoUiKit.Text("", "paseo-recording", strong: true);
            recording.name = "grabando";
            top.Add(recording);
            PaseoUiKit.SetHidden(recording, true);
            Root.Add(top);

            var help = new Button(PressHelp) { name = "ayuda", text = "?" };
            help.AddToClassList("paseo-help");
            help.AddToClassList(PaseoUiKit.StrongClass);
            Root.Add(help);

            park = new Button(PressPark) { name = "parque", text = "Parque" };
            park.AddToClassList("paseo-park");
            park.AddToClassList(PaseoUiKit.StrongClass);
            Root.Add(park);

            var bottom = new VisualElement { pickingMode = PickingMode.Ignore };
            bottom.AddToClassList("paseo-bottom");
            stopCapture = new Button(() => StopCaptureRequested?.Invoke()) { name = "detener-captura", text = "Detener captura y guardar" };
            stopCapture.AddToClassList("paseo-button");
            bottom.Add(stopCapture);
            PaseoUiKit.SetHidden(stopCapture, true);
            card = new VisualElement { name = "tarjeta" };
            card.AddToClassList("paseo-card");
            var row = new VisualElement();
            row.AddToClassList("paseo-card__row");
            title = PaseoUiKit.Text("", "paseo-card__title", strong: true);
            title.name = "tarjeta-titulo";
            chip = PaseoUiKit.Text("Ubicado", "paseo-chip", strong: true);
            chip.name = "tarjeta-chip";
            row.Add(title);
            row.Add(chip);
            card.Add(row);
            subtitle = PaseoUiKit.Text("", "paseo-card__subtitle");
            subtitle.name = "tarjeta-texto";
            card.Add(subtitle);
            bottom.Add(card);
            var epochs = new VisualElement { name = "epocas" };
            epochs.AddToClassList("paseo-epochs");
            foreach (var epoch in PaseoEpochs.All)
            {
                var button = new Button(() => EpochRequested?.Invoke(epoch)) { name = $"epoca-{PaseoEpochs.Key(epoch)}", text = PaseoEpochs.Label(epoch) };
                button.AddToClassList("paseo-epoch");
                button.AddToClassList(PaseoUiKit.StrongClass);
                epochButtons[(int)epoch] = button;
                epochs.Add(button);
            }
            bottom.Add(epochs);
            Root.Add(bottom);

            // The year, large in the middle of the screen, for a moment after each change. These are illustrative models.
            epochFlash = new VisualElement { name = "epoca-anuncio", pickingMode = PickingMode.Ignore };
            epochFlash.AddToClassList("paseo-epoch-flash");
            epochFlashYear = PaseoUiKit.Text("", "paseo-epoch-flash__year", strong: true);
            epochFlash.Add(epochFlashYear);
            var note = PaseoUiKit.Text("Recreación ilustrativa", "paseo-epoch-flash__note");
            note.name = "epoca-nota";
            epochFlash.Add(note);
            Root.Add(epochFlash);
            PaseoUiKit.SetHidden(card, true);
        }

        public void PressHelp() => HelpRequested?.Invoke();

        public void PressPark() => ParkToggled?.Invoke();

        /// <summary>Reflects whether the animated park is on: the button greys out when it is off.</summary>
        public void SetPark(bool on) => park.EnableInClassList(ParkOffClass, !on);

        /// <summary>Marks the era on show in the selector.</summary>
        public void SetEpoch(PaseoEpoch epoch)
        {
            foreach (var value in PaseoEpochs.All)
                epochButtons[(int)value].EnableInClassList(EpochOnClass, value == epoch);
        }

        /// <summary>Shows the year large for a moment; «Actualidad» says so without the illustrative note.</summary>
        public void FlashEpoch(PaseoEpoch epoch)
        {
            epochFlashYear.text = PaseoEpochs.Label(epoch);
            PaseoUiKit.SetHidden(epochFlash.Q("epoca-nota"), epoch == PaseoEpoch.Actualidad);
            epochFlash.AddToClassList(EpochFlashShownClass);
            epochFlashHide?.Pause();
            epochFlashHide = epochFlash.schedule.Execute(() => epochFlash.RemoveFromClassList(EpochFlashShownClass));
            epochFlashHide.ExecuteLater((long)(PaseoEpochs.TotalSeconds * 1000f));
        }

        public void SetStatus(string text, PaseoStatusLevel level)
        {
            status.text = text;
            pill.EnableInClassList("paseo-pill--ok", level == PaseoStatusLevel.Ok);
            pill.EnableInClassList("paseo-pill--error", level == PaseoStatusLevel.Error);
        }

        /// <summary>What the search is doing (PaseoActivity). A new match or discard pops so the eye catches it.</summary>
        public void SetActivity(PaseoActivityLine line)
        {
            if (line.Tone == shownActivity.Tone && line.Text == shownActivity.Text)
                return;
            shownActivity = line;
            PaseoUiKit.SetHidden(activity, line.Tone == PaseoActivityTone.None);
            if (line.Tone == PaseoActivityTone.None)
                return;
            activityText.text = line.Text;
            activity.EnableInClassList("paseo-activity--match", line.Tone == PaseoActivityTone.Match);
            activity.EnableInClassList("paseo-activity--discarded", line.Tone == PaseoActivityTone.Discarded);
            if (line.Tone == PaseoActivityTone.Searching)
                return;
            activity.AddToClassList(PopClass);
            activity.schedule.Execute(() => activity.RemoveFromClassList(PopClass)).ExecuteLater(30);
        }

        /// <summary>Record mode badge («● X1-C-30m · 12 s · 9 imágenes»); null or empty hides it. <paramref name="warning"/> turns it amber.</summary>
        public void SetRecording(string text, bool warning = false)
        {
            PaseoUiKit.SetHidden(recording, string.IsNullOrEmpty(text));
            if (string.IsNullOrEmpty(text))
                return;
            recording.text = text;
            recording.EnableInClassList("paseo-recording--warning", warning);
        }

        public void SetCaptureState(bool busy, bool recording)
        {
            capturing = busy;
            PaseoUiKit.SetHidden(stopCapture, !busy);
            stopCapture.SetEnabled(recording);
            stopCapture.text = recording ? "Detener captura y guardar" : "Preparando o guardando…";
            PaseoUiKit.SetHidden(card, busy || Card.Kind == PaseoCardKind.None);
        }

        public void SetCard(PaseoCard next)
        {
            if (next.Equals(Card))
                return;
            bool newContent = next.Kind != Card.Kind || next.Title != Card.Title;
            Card = next;
            PaseoUiKit.SetHidden(card, capturing || next.Kind == PaseoCardKind.None);
            if (next.Kind == PaseoCardKind.None)
                return;
            title.text = next.Title;
            subtitle.text = next.Subtitle ?? "";
            PaseoUiKit.SetHidden(subtitle, string.IsNullOrEmpty(next.Subtitle));
            PaseoUiKit.SetHidden(chip, next.Kind != PaseoCardKind.Building);
            chip.text = next.Confirmed ? "Ubicado" : "Confirmando";
            chip.EnableInClassList("paseo-chip--pending", !next.Confirmed);
            if (!newContent)
                return;
            // Start below and transparent; the USS transition brings it up once the class goes on the next frames.
            card.AddToClassList(EnteringClass);
            card.schedule.Execute(() => card.RemoveFromClassList(EnteringClass)).ExecuteLater(30);
        }
    }
}

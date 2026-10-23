using System.Collections.Generic;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace AncorRA.Tests
{
    public class PaseoViewsTests
    {
        [Test]
        public void TheWelcomeOffersStartAndExplainsThePermissions()
        {
            var view = new PaseoWelcomeView();
            Assert.AreEqual("Comenzar", view.Root.Q<Button>("comenzar").text);
            Assert.AreEqual(PaseoWelcomeView.DefaultNote, view.Root.Q<Label>("bienvenida-nota").text);
            bool started = false;
            view.Started += () => started = true;
            view.PressStart();
            Assert.IsTrue(started);
        }

        [Test]
        public void TheWelcomeHidesAndComesBackWithItsNote()
        {
            var view = new PaseoWelcomeView();
            view.ShowMessage("Sin la cámara la app no puede funcionar.");
            view.Hide();
            Assert.IsFalse(view.Visible);
            view.Show();
            Assert.IsTrue(view.Visible);
            Assert.AreEqual(PaseoWelcomeView.DefaultNote, view.Root.Q<Label>("bienvenida-nota").text);
        }

        [Test]
        public void ThePillShowsTheStatusAndItsTone()
        {
            var view = new PaseoCameraView();
            view.SetStatus("Teología: ubicado", PaseoStatusLevel.Ok);
            Assert.AreEqual("Teología: ubicado", view.Root.Q<Label>("estado-texto").text);
            Assert.IsTrue(view.Root.Q("estado").ClassListContains("paseo-pill--ok"));
            view.SetStatus("Error: algo", PaseoStatusLevel.Error);
            Assert.IsFalse(view.Root.Q("estado").ClassListContains("paseo-pill--ok"));
            Assert.IsTrue(view.Root.Q("estado").ClassListContains("paseo-pill--error"));
        }

        [Test]
        public void NoCardMeansTheCardIsHidden()
        {
            var view = new PaseoCameraView();
            view.SetCard(PaseoCard.None);
            Assert.IsTrue(PaseoUiKit.IsHidden(view.Root.Q("tarjeta")));
        }

        [Test]
        public void ABuildingCardShowsNameChipAndDescription()
        {
            var view = new PaseoCameraView();
            view.SetCard(PaseoCard.Building("Teología", "Facultad de Teología."));
            Assert.IsFalse(PaseoUiKit.IsHidden(view.Root.Q("tarjeta")));
            Assert.AreEqual("Teología", view.Root.Q<Label>("tarjeta-titulo").text);
            Assert.AreEqual("Facultad de Teología.", view.Root.Q<Label>("tarjeta-texto").text);
            Assert.IsFalse(PaseoUiKit.IsHidden(view.Root.Q("tarjeta-chip")));
            Assert.AreEqual("Ubicado", view.Root.Q<Label>("tarjeta-chip").text);
        }

        [Test]
        public void AnUnconfirmedBuildingSaysSoOnItsChip()
        {
            var view = new PaseoCameraView();
            view.SetCard(PaseoCard.Building("Teología", "", confirmed: false));
            var chip = view.Root.Q<Label>("tarjeta-chip");
            Assert.AreEqual("Confirmando", chip.text);
            Assert.IsTrue(chip.ClassListContains("paseo-chip--pending"));
            view.SetCard(PaseoCard.Building("Teología", "", confirmed: true));
            Assert.AreEqual("Ubicado", chip.text);
            Assert.IsFalse(chip.ClassListContains("paseo-chip--pending"));
        }

        [Test]
        public void TheActivityLineShowsWhatTheSearchIsDoing()
        {
            var view = new PaseoCameraView();
            var line = view.Root.Q("actividad");
            view.SetActivity(new PaseoActivityLine("Buscando Teología · 3 imágenes sin coincidencia", PaseoActivityTone.Searching));
            Assert.IsFalse(PaseoUiKit.IsHidden(line));
            Assert.AreEqual("Buscando Teología · 3 imágenes sin coincidencia", view.Root.Q<Label>("actividad-texto").text);
            view.SetActivity(new PaseoActivityLine("Coincidencia con Teología", PaseoActivityTone.Match));
            Assert.IsTrue(line.ClassListContains("paseo-activity--match"));
            Assert.IsTrue(line.ClassListContains(PaseoCameraView.PopClass), "a new match pops");
            view.SetActivity(new PaseoActivityLine("Coincidencia con Teología descartada: imagen inclinada 7°", PaseoActivityTone.Discarded));
            Assert.IsFalse(line.ClassListContains("paseo-activity--match"));
            Assert.IsTrue(line.ClassListContains("paseo-activity--discarded"));
            view.SetActivity(PaseoActivityLine.None);
            Assert.IsTrue(PaseoUiKit.IsHidden(line));
        }

        [Test]
        public void AHintHasNoChipAndAnEmptyDescriptionIsHidden()
        {
            var view = new PaseoCameraView();
            view.SetCard(PaseoCard.Hint(PaseoCard.HintTitle, ""));
            Assert.IsTrue(PaseoUiKit.IsHidden(view.Root.Q("tarjeta-chip")));
            Assert.IsTrue(PaseoUiKit.IsHidden(view.Root.Q("tarjeta-texto")));
        }

        [Test]
        public void ANewCardSlidesInButTheSameCardDoesNotReplay()
        {
            var view = new PaseoCameraView();
            var card = view.Root.Q("tarjeta");
            view.SetCard(PaseoCard.Building("X1", ""));
            Assert.IsTrue(card.ClassListContains(PaseoCameraView.EnteringClass));
            card.RemoveFromClassList(PaseoCameraView.EnteringClass); // what the scheduled step does on device
            view.SetCard(PaseoCard.Building("X1", ""));
            Assert.IsFalse(card.ClassListContains(PaseoCameraView.EnteringClass));
            view.SetCard(PaseoCard.Building("EIC", ""));
            Assert.IsTrue(card.ClassListContains(PaseoCameraView.EnteringClass));
        }

        [Test]
        public void HelpBringsTheWelcomeBack()
        {
            var view = new PaseoCameraView();
            bool asked = false;
            view.HelpRequested += () => asked = true;
            view.PressHelp();
            Assert.IsTrue(asked);
        }

        [Test]
        public void TheViewButtonIsHiddenUntilAskedAndStartsOnTheBox()
        {
            var view = new PaseoCameraView();
            var button = view.Root.Q<Button>("vista");
            Assert.IsNotNull(button);
            Assert.IsTrue(PaseoUiKit.IsHidden(button), "release builds never show it");
            Assert.AreEqual("Vista: Caja", button.text);

            view.ShowViewButton(true);
            Assert.IsFalse(PaseoUiKit.IsHidden(button));
            view.ShowViewButton(false);
            Assert.IsTrue(PaseoUiKit.IsHidden(button));
        }

        [Test]
        public void TheViewButtonSaysTheModeAndRaisesAnEventWhenPressed()
        {
            var view = new PaseoCameraView();
            var button = view.Root.Q<Button>("vista");
            int presses = 0;
            view.ViewRequested += () => presses++;
            view.PressView();
            Assert.AreEqual(1, presses);
            Assert.AreEqual("Vista: Caja", button.text, "pressing only asks: the owner says the new mode");

            view.SetViewMode(PaseoViewMode.Background);
            Assert.AreEqual("Vista: Fondo", button.text);
            view.SetViewMode(PaseoViewMode.Object);
            Assert.AreEqual("Vista: Objeto", button.text);
            view.SetViewMode(PaseoViewMode.Object);
            Assert.AreEqual("Vista: Objeto", button.text);
            view.SetViewMode(PaseoViewMode.Box);
            Assert.AreEqual("Vista: Caja", button.text);
        }

        [Test]
        public void NamePillsFollowTheirBoxesAndHideWithoutAPosition()
        {
            var view = new PaseoLabelsView();
            bool created = view.Set(new List<PaseoLabelItem>
            {
                new("Teología", new Vector2(120f, 300f)),
                new("X1", null)
            });
            Assert.IsTrue(created);
            Assert.AreEqual(2, view.Pills.Count);
            Assert.AreEqual("Teología", view.Pills[0].text);
            Assert.AreEqual(120f, view.Pills[0].style.left.value.value, 1e-3f);
            Assert.AreEqual(300f, view.Pills[0].style.top.value.value, 1e-3f);
            Assert.IsFalse(PaseoUiKit.IsHidden(view.Pills[0]));
            Assert.IsTrue(PaseoUiKit.IsHidden(view.Pills[1]));

            Assert.IsFalse(view.Set(new List<PaseoLabelItem> { new("Teología", new Vector2(1f, 2f)) }));
            Assert.IsTrue(PaseoUiKit.IsHidden(view.Pills[1]), "a pill without an item hides");
        }
    }
}

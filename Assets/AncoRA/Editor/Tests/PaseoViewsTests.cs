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

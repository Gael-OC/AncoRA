using System;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoResolutionTests
    {
        const string Prefix = "AncoRA.Test.Resolucion.";
        PaseoResolutionStore store;

        [SetUp]
        public void SetUp()
        {
            store = new PaseoResolutionStore(Prefix);
            store.Clear();
        }

        [TearDown]
        public void TearDown() => store.Clear();

        [Test]
        public void TheOptionsAreTheThreeSizesAndTheDefaultIsTheMiddleOne()
        {
            CollectionAssert.AreEqual(new[] { 691200, 1166400, 0 }, PaseoResolution.Options);
            Assert.AreEqual(1166400, PaseoResolution.Default);
            Assert.IsTrue(PaseoResolution.IsKnown(0));
            Assert.IsFalse(PaseoResolution.IsKnown(921600), "the 1.4.2 sheet offers three sizes only");
            Assert.IsFalse(PaseoResolution.IsKnown(-5));
        }

        [Test]
        public void TheFullImageHasTheMostPixelsOfAll()
        {
            Assert.Greater(PaseoResolution.PixelRank(PaseoResolution.Full), PaseoResolution.PixelRank(PaseoResolution.Medium));
            Assert.Greater(PaseoResolution.PixelRank(PaseoResolution.Medium), PaseoResolution.PixelRank(PaseoResolution.Reduced));
        }

        [Test]
        public void ThePluginShrinksAFullHdImageKeepingItsShape()
        {
            Assert.AreEqual((1109, 624), PaseoResolution.ReducedSize(PaseoResolution.Reduced, 1920, 1080));
            Assert.AreEqual((1440, 810), PaseoResolution.ReducedSize(PaseoResolution.Medium, 1920, 1080));
            Assert.AreEqual((1920, 1080), PaseoResolution.ReducedSize(PaseoResolution.Full, 1920, 1080));
            Assert.AreEqual("1109x624", PaseoResolution.SizeText(PaseoResolution.Reduced));
            Assert.AreEqual("1440x810", PaseoResolution.SizeText(PaseoResolution.Medium));
            Assert.AreEqual("1920x1080", PaseoResolution.SizeText(PaseoResolution.Full));
        }

        [Test]
        public void AnImageSmallerThanTheLimitIsLeftAlone()
        {
            Assert.AreEqual((640, 480), PaseoResolution.ReducedSize(PaseoResolution.Reduced, 640, 480));
            Assert.AreEqual((0, 0), PaseoResolution.ReducedSize(PaseoResolution.Reduced, 0, 0));
        }

        [Test]
        public void TheNamesAreTheOnesOnTheButtons()
        {
            Assert.AreEqual("960×720", PaseoResolution.ShortName(PaseoResolution.Reduced));
            Assert.AreEqual("1440×810", PaseoResolution.ShortName(PaseoResolution.Medium));
            Assert.AreEqual("Completa", PaseoResolution.ShortName(PaseoResolution.Full));
            Assert.AreEqual("Completa (1920×1080)", PaseoResolution.Name(PaseoResolution.Full));
            Assert.AreEqual("1440×810", PaseoResolution.Name(PaseoResolution.Medium));
        }

        // ---- the saved choice ----

        [Test]
        public void NothingSavedIsTheDefaultSizeBecauseNobodyChoseIt()
        {
            var choice = store.Load();
            Assert.AreEqual(PaseoResolution.Default, choice.MaxPixels);
            Assert.AreEqual(PaseoResolution.OriginDefault, choice.Origin);
            Assert.IsFalse(choice.HasDate);
        }

        [Test]
        public void AChoiceMadeByHandRoundTrips()
        {
            store.Save(PaseoResolution.Reduced, PaseoResolution.OriginChosen, new DateTime(2026, 10, 29, 14, 5, 0));
            var choice = store.Load();
            Assert.AreEqual(PaseoResolution.Reduced, choice.MaxPixels);
            Assert.AreEqual(PaseoResolution.OriginChosen, choice.Origin);
            Assert.AreEqual(new DateTime(2026, 10, 29), choice.Date);
        }

        [Test]
        public void AChoiceOfThePhoneTestRoundTripsAndFullIsNotConfusedWithNothingSaved()
        {
            store.Save(PaseoResolution.Full, PaseoResolution.OriginTest, new DateTime(2026, 10, 29));
            var choice = store.Load();
            Assert.AreEqual(0, choice.MaxPixels);
            Assert.AreEqual(PaseoResolution.OriginTest, choice.Origin);
        }

        [Test]
        public void ASizeThisVersionDoesNotOfferIsTheDefault()
        {
            PlayerPrefs.SetInt(Prefix + "Resolucion", 921600);
            PlayerPrefs.SetString(Prefix + "ResolucionOrigen", PaseoResolution.OriginTest);
            var choice = store.Load();
            Assert.AreEqual(PaseoResolution.Default, choice.MaxPixels);
            Assert.AreEqual(PaseoResolution.OriginDefault, choice.Origin);
        }

        [Test]
        public void AnUnknownOriginReadsAsChosenByHandAndABadDateAsNone()
        {
            PlayerPrefs.SetInt(Prefix + "Resolucion", PaseoResolution.Reduced);
            PlayerPrefs.SetString(Prefix + "ResolucionOrigen", "otra-cosa");
            PlayerPrefs.SetString(Prefix + "ResolucionFecha", "ayer");
            var choice = store.Load();
            Assert.AreEqual(PaseoResolution.OriginChosen, choice.Origin);
            Assert.IsFalse(choice.HasDate);
        }

        [Test]
        public void TheKeysAreUnderTheSamePrefixAsTheBoxValues()
        {
            Assert.AreEqual("AncoRA.Paseo.v3.", PaseoBoxStore.DefaultPrefix);
            store.Save(PaseoResolution.Full, PaseoResolution.OriginChosen, DateTime.Now);
            Assert.IsTrue(PlayerPrefs.HasKey(Prefix + "Resolucion"));
            Assert.IsTrue(PlayerPrefs.HasKey(Prefix + "ResolucionOrigen"));
            Assert.IsTrue(PlayerPrefs.HasKey(Prefix + "ResolucionFecha"));
        }

        [Test]
        public void ClearingForgetsTheChoice()
        {
            store.Save(PaseoResolution.Reduced, PaseoResolution.OriginChosen, DateTime.Now);
            store.Clear();
            Assert.AreEqual(PaseoResolution.OriginDefault, store.Load().Origin);
        }

        // ---- how it reads ----

        [Test]
        public void TheSheetLineSaysWhatIsInUseAndWhy()
        {
            Assert.AreEqual("Ahora: 1440×810 · por defecto, sin probar en este teléfono",
                PaseoResolution.Describe(PaseoResolutionChoice.Default));
            Assert.AreEqual("Ahora: 1440×810 · elegida por la prueba (29-10)",
                PaseoResolution.Describe(new PaseoResolutionChoice(PaseoResolution.Medium, PaseoResolution.OriginTest, new DateTime(2026, 10, 29))));
            Assert.AreEqual("Ahora: 960×720 · elegida a mano",
                PaseoResolution.Describe(new PaseoResolutionChoice(PaseoResolution.Reduced, PaseoResolution.OriginChosen, new DateTime(2026, 10, 29))));
            Assert.AreEqual("Ahora: Completa · elegida por la prueba",
                PaseoResolution.Describe(new PaseoResolutionChoice(PaseoResolution.Full, PaseoResolution.OriginTest)));
        }

        [Test]
        public void TheOriginInALogLineHasNoSpace()
        {
            Assert.AreEqual("por-defecto", PaseoResolution.OriginToken(PaseoResolution.OriginDefault));
            Assert.AreEqual("elegida", PaseoResolution.OriginToken(PaseoResolution.OriginChosen));
            Assert.AreEqual("prueba", PaseoResolution.OriginToken(PaseoResolution.OriginTest));
        }
    }
}

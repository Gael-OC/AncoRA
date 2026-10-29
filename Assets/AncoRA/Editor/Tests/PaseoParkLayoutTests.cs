using System.Linq;
using AncorRA.AR;
using NUnit.Framework;
using UnityEngine;

namespace AncorRA.Tests
{
    public class PaseoParkLayoutTests
    {
        static readonly Vector3[] Sizes = { new(20f, 8f, 12f), new(27.2f, 4.7f, 9.4f), new(4f, 4f, 4f), new(30f, 10f, 15f), new(2f, 3f, 2f) };
        static readonly string[] Themes = { "ciencias", "pista", "saludo", "baile" };

        static PaseoParkPlan Plan(string tema, string frente = "+z", Vector3? size = null, string id = "X1") =>
            PaseoParkLayout.Build(new PaseoParkConfig { tema = tema, escala = 1.5f, frente = frente }, size ?? new Vector3(20f, 8f, 12f), id);

        static int Count(PaseoParkPlan p, PaseoSurface s, bool walks) => p.Actors.Count(a => a.Surface == s && a.Walks == walks);

        static bool Only(PaseoActorSpot a, PaseoMove move) => a.Repertoire.Length == 1 && a.Repertoire[0] == move;

        [Test]
        public void Ciencias()
        {
            var p = Plan("ciencias");
            Assert.AreEqual(3, Count(p, PaseoSurface.Techo, false));
            Assert.AreEqual(0, Count(p, PaseoSurface.Techo, true));
            Assert.AreEqual(2, Count(p, PaseoSurface.Suelo, true));
            Assert.AreEqual(5, p.Actors.Count);
            Assert.AreEqual(1, p.Props.Count(x => x.Kind == PaseoPropKind.Atomo && x.Surface == PaseoSurface.Techo));
            Assert.AreEqual(2, p.Props.Count(x => x.Kind == PaseoPropKind.Arbol && x.Surface == PaseoSurface.Suelo));
            Assert.AreEqual(3, p.Props.Count);
            foreach (var a in p.Actors.Where(a => !a.Walks))
                CollectionAssert.AreEquivalent(new[] { PaseoMove.Bailar, PaseoMove.Saltar, PaseoMove.Saludar }, a.Repertoire);
        }

        [Test]
        public void Pista()
        {
            var p = Plan("pista");
            Assert.AreEqual(2, p.Actors.Count(a => a.Walks && a.Surface == PaseoSurface.Techo && Only(a, PaseoMove.Trotar)));
            Assert.AreEqual(2, p.Actors.Count(a => !a.Walks && a.Surface == PaseoSurface.Techo && Only(a, PaseoMove.Saltar)));
            Assert.AreEqual(1, Count(p, PaseoSurface.Suelo, true));
            Assert.AreEqual(5, p.Actors.Count);
            Assert.AreEqual(1, p.Props.Count(x => x.Kind == PaseoPropKind.Bandera && x.Surface == PaseoSurface.Techo));
            Assert.AreEqual(2, p.Props.Count(x => x.Kind == PaseoPropKind.Arbol));
        }

        [Test]
        public void Saludo()
        {
            var p = Plan("saludo");
            Assert.AreEqual(4, p.Actors.Count(a => !a.Walks && a.Surface == PaseoSurface.Techo && Only(a, PaseoMove.Saludar)));
            Assert.AreEqual(1, p.Actors.Count(a => !a.Walks && a.Surface == PaseoSurface.Techo && Only(a, PaseoMove.Bailar)));
            Assert.AreEqual(1, Count(p, PaseoSurface.Suelo, true));
            Assert.AreEqual(6, p.Actors.Count);
            Assert.AreEqual(2, p.Props.Count(x => x.Kind == PaseoPropKind.Arbol));
        }

        [Test]
        public void Baile()
        {
            var p = Plan("baile");
            Assert.AreEqual(5, p.Actors.Count(a => !a.Walks && a.Surface == PaseoSurface.Techo && Only(a, PaseoMove.Bailar)));
            Assert.AreEqual(1, Count(p, PaseoSurface.Suelo, true));
            Assert.AreEqual(6, p.Actors.Count);
            Assert.AreEqual(1, p.Props.Count(x => x.Kind == PaseoPropKind.BolaDisco && x.Surface == PaseoSurface.Techo));
            Assert.AreEqual(1, p.Props.Count(x => x.Kind == PaseoPropKind.Arbol));
        }

        [Test]
        public void RingFacesTheCentre()
        {
            var size = new Vector3(20f, 8f, 12f);
            foreach (var a in Plan("baile").Actors.Where(a => !a.Walks))
            {
                var pos = PaseoParkLayout.Resolve(a.Normalized, a.Surface, size);
                var facing = new Vector3(Mathf.Sin(a.Yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(a.Yaw * Mathf.Deg2Rad));
                Assert.Greater(Vector3.Dot(facing, new Vector3(-pos.x, 0f, -pos.z).normalized), 0.99f);
            }
        }

        [Test]
        public void Scale_FollowsConfig()
        {
            var plan = PaseoParkLayout.Build(new PaseoParkConfig { tema = "baile", escala = 2.5f }, new Vector3(20f, 8f, 12f), "EIC");
            Assert.AreEqual(2.5f, plan.Scale);
            Assert.AreEqual("baile", plan.Theme);
        }

        [Test]
        public void IsDeterministic()
        {
            foreach (var tema in Themes)
            {
                var a = Plan(tema);
                var b = Plan(tema);
                Assert.AreEqual(a.Actors.Count, b.Actors.Count);
                for (int i = 0; i < a.Actors.Count; i++)
                {
                    Assert.AreEqual(a.Actors[i].ColorIndex, b.Actors[i].ColorIndex);
                    Assert.AreEqual(a.Actors[i].Phase01, b.Actors[i].Phase01);
                    Assert.AreEqual(a.Actors[i].Normalized, b.Actors[i].Normalized);
                    Assert.AreEqual(a.Actors[i].Yaw, b.Actors[i].Yaw);
                }
            }
        }

        [Test]
        public void ColorsAreInPaletteAndDependOnBuilding()
        {
            var a = Plan("saludo", id: "Teologia");
            var b = Plan("saludo", id: "Ciencias Basicas");
            Assert.IsTrue(a.Actors.All(x => x.ColorIndex >= 0 && x.ColorIndex < PaseoParkLayout.ColorCount));
            Assert.IsFalse(a.Actors.Select(x => x.ColorIndex).SequenceEqual(b.Actors.Select(x => x.ColorIndex)));
        }

        [Test]
        public void RoofSpotsKeepMargin()
        {
            foreach (var size in Sizes)
                foreach (var tema in Themes)
                    foreach (var frente in PaseoParkConfig.Fronts)
                    {
                        var plan = Plan(tema, frente, size);
                        float mx = Mathf.Max(0f, size.x / 2f - 0.8f) + 1e-3f, mz = Mathf.Max(0f, size.z / 2f - 0.8f) + 1e-3f;
                        var spots = plan.Actors.Where(a => !a.Walks && a.Surface == PaseoSurface.Techo).Select(a => a.Normalized)
                            .Concat(plan.Props.Where(p => p.Surface == PaseoSurface.Techo).Select(p => p.Normalized));
                        foreach (var n in spots)
                        {
                            var pos = PaseoParkLayout.Resolve(n, PaseoSurface.Techo, size);
                            Assert.LessOrEqual(Mathf.Abs(pos.x), mx, $"{tema} {frente} {size}");
                            Assert.LessOrEqual(Mathf.Abs(pos.z), mz, $"{tema} {frente} {size}");
                        }
                    }
        }

        [Test]
        public void GroundSpotsAreOutsideWithClearance()
        {
            foreach (var size in Sizes)
                foreach (var tema in Themes)
                    foreach (var frente in PaseoParkConfig.Fronts)
                    {
                        var plan = Plan(tema, frente, size);
                        var spots = plan.Actors.Where(a => !a.Walks && a.Surface == PaseoSurface.Suelo).Select(a => a.Normalized)
                            .Concat(plan.Props.Where(p => p.Surface == PaseoSurface.Suelo).Select(p => p.Normalized));
                        foreach (var n in spots)
                        {
                            var pos = PaseoParkLayout.Resolve(n, PaseoSurface.Suelo, size);
                            bool outside = Mathf.Abs(pos.x) >= size.x / 2f + 1.5f - 1e-3f || Mathf.Abs(pos.z) >= size.z / 2f + 1.5f - 1e-3f;
                            Assert.IsTrue(outside, $"{tema} {frente} {size} -> {pos}");
                        }
                        foreach (var w in plan.Actors.Where(a => a.Walks && a.Surface == PaseoSurface.Suelo))
                            Assert.GreaterOrEqual(w.PathMargin, 1.5f);
                    }
        }

        [Test]
        public void FrontRowFollowsFrente()
        {
            var size = new Vector3(20f, 8f, 12f);
            var expected = new (string frente, Vector3 dir, float yaw)[]
            {
                ("+z", Vector3.forward, 0f), ("-z", Vector3.back, 180f), ("+x", Vector3.right, 90f), ("-x", Vector3.left, 270f),
            };
            foreach (var (frente, dir, yaw) in expected)
            {
                var row = Plan("saludo", frente, size).Actors.Where(a => !a.Walks && Only(a, PaseoMove.Saludar)).ToList();
                Assert.AreEqual(4, row.Count);
                foreach (var a in row)
                {
                    var pos = PaseoParkLayout.Resolve(a.Normalized, a.Surface, size);
                    float half = Mathf.Abs(dir.x) > 0f ? size.x / 2f : size.z / 2f;
                    Assert.Greater(Vector3.Dot(pos, dir), half - 3f, frente);
                    Assert.AreEqual(yaw, a.Yaw, 1e-3f, frente);
                }
            }
        }

        [Test]
        public void ResolveHeights()
        {
            var size = new Vector3(20f, 8f, 12f);
            Assert.AreEqual(new Vector3(5f, 4f, -3f), PaseoParkLayout.Resolve(new Vector2(0.25f, -0.25f), PaseoSurface.Techo, size));
            Assert.AreEqual(new Vector3(-10f, -4f, 6f), PaseoParkLayout.Resolve(new Vector2(-0.5f, 0.5f), PaseoSurface.Suelo, size));
        }

        [Test]
        public void ConstantsAndErrors()
        {
            Assert.AreEqual(1.8f, PaseoParkLayout.ActorHeightMeters);
            Assert.AreEqual(10, PaseoParkLayout.ColorCount);
            Assert.Throws<System.ArgumentException>(() => PaseoParkLayout.Build(null, Vector3.one * 10f, "X1"));
            Assert.Throws<System.ArgumentException>(() => PaseoParkLayout.Build(new PaseoParkConfig { tema = "circo" }, Vector3.one * 10f, "X1"));
        }
    }
}

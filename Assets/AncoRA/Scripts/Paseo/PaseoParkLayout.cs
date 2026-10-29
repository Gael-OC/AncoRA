using System;
using System.Collections.Generic;
using UnityEngine;

namespace AncorRA.AR
{
    public enum PaseoSurface { Techo, Suelo }

    public enum PaseoPropKind { Arbol, Atomo, Bandera, BolaDisco }

    /// <summary>One block character. Stationary actors use «normalized»; walkers follow a PaseoParkPath instead.</summary>
    public sealed class PaseoActorSpot
    {
        public PaseoSurface Surface;
        /// <summary>x, z as a fraction of the box width and depth (box centred at the origin).</summary>
        public Vector2 Normalized;
        /// <summary>Local yaw in degrees, 0 = facing +Z of the box.</summary>
        public float Yaw;
        public PaseoMove[] Repertoire;
        public bool Walks;
        /// <summary>Roof inset or ground outset of the loop, metres (walkers only).</summary>
        public float PathMargin;
        public float PathStart01;
        public int ColorIndex;
        public float Phase01;
    }

    public sealed class PaseoPropSpot
    {
        public PaseoPropKind Kind;
        public PaseoSurface Surface;
        public Vector2 Normalized;
        public float Scale = 1f;
        /// <summary>Metres above the surface at scale 1 (floating props such as the atom and the disco ball).</summary>
        public float HeightAbove;
    }

    public sealed class PaseoParkPlan
    {
        public string Theme;
        public float Scale;
        public List<PaseoActorSpot> Actors = new();
        public List<PaseoPropSpot> Props = new();
    }

    /// <summary>What goes where in each park theme. Pure and deterministic: the same inputs give the same plan.</summary>
    public static class PaseoParkLayout
    {
        /// <summary>Height of a character at scale 1; the displayed height is this times the plan scale.</summary>
        public const float ActorHeightMeters = 1.8f;
        public const int ColorCount = 10;
        public const float RoofMarginMeters = 0.8f;
        public const float RoofPathMarginMeters = 1.2f;
        public const float GroundPathOutsetMeters = 2.0f;
        public const float GroundClearanceMeters = 1.5f;
        const float TreeOutsetMeters = 3.5f;

        /// <summary>Local position in the box frame (box centred at the origin: roof y = +h/2, ground y = -h/2).</summary>
        public static Vector3 Resolve(Vector2 normalized, PaseoSurface surface, Vector3 boxSize)
        {
            float y = surface == PaseoSurface.Techo ? boxSize.y / 2f : -boxSize.y / 2f;
            return new Vector3(normalized.x * boxSize.x, y, normalized.y * boxSize.z);
        }

        public static PaseoParkPlan Build(PaseoParkConfig park, Vector3 boxSize, string buildingId)
        {
            if (park == null || string.IsNullOrWhiteSpace(park.tema))
                throw new ArgumentException("El edificio no tiene parque.");
            int quarter = Array.IndexOf(PaseoParkConfig.Fronts, park.frente ?? "+z");
            if (quarter < 0)
                throw new ArgumentException($"Frente del parque inválido: {park.frente}.");
            var plan = new PaseoParkPlan { Theme = park.tema, Scale = park.escala };
            var b = new Builder(plan, boxSize, quarter, new System.Random(unchecked((int)PaseoStableHash.Of(buildingId))));
            switch (park.tema)
            {
                case "ciencias": b.Ciencias(); break;
                case "pista": b.Pista(); break;
                case "saludo": b.Saludo(); break;
                case "baile": b.Baile(); break;
                default: throw new ArgumentException($"Tema de parque desconocido: {park.tema}.");
            }
            return plan;
        }

        // Designs are written for a «front frame» where the front is +Z and then rotated a quarter turn at a time.
        // Fronts order: +z, -z, +x, -x, so the quarter index is mapped below to the rotation.
        sealed class Builder
        {
            readonly PaseoParkPlan plan;
            readonly Vector3 size;
            readonly int rotation; // 0, 180, 90 or 270 degrees about Y
            readonly System.Random rng;
            readonly float fw, fd; // box width and depth seen from the front frame

            public Builder(PaseoParkPlan plan, Vector3 size, int frontIndex, System.Random rng)
            {
                this.plan = plan;
                this.size = size;
                this.rng = rng;
                rotation = frontIndex switch { 0 => 0, 1 => 180, 2 => 90, _ => 270 };
                bool turned = rotation == 90 || rotation == 270;
                fw = turned ? size.z : size.x;
                fd = turned ? size.x : size.z;
            }

            // Metres in the front frame to a normalized point of the real box frame.
            Vector2 Norm(float x, float z)
            {
                Vector2 p = rotation switch
                {
                    0 => new Vector2(x, z),
                    180 => new Vector2(-x, -z),
                    90 => new Vector2(z, -x),
                    _ => new Vector2(-z, x),
                };
                return new Vector2(p.x / size.x, p.y / size.z);
            }

            float Yaw(float yaw) => Mathf.Repeat(yaw + rotation, 360f);

            // Point on the roof as a fraction (-1..1) of the usable half extents, so it keeps the margin on any box.
            Vector2 Roof(float fx, float fz)
            {
                float ux = Mathf.Max(0f, fw / 2f - RoofMarginMeters);
                float uz = Mathf.Max(0f, fd / 2f - RoofMarginMeters);
                return Norm(fx * ux, fz * uz);
            }

            // Point outside the footprint, on the side of the front frame given by sign (-1 left, +1 right).
            Vector2 Ground(float side, float alongFraction) =>
                Norm(side * (fw / 2f + TreeOutsetMeters), alongFraction * fd / 2f);

            float FacingCentre(float fx, float fz) => Mathf.Atan2(-fx, -fz) * Mathf.Rad2Deg;

            void Actor(PaseoSurface surface, Vector2 normalized, float yaw, params PaseoMove[] repertoire)
            {
                plan.Actors.Add(new PaseoActorSpot
                {
                    Surface = surface,
                    Normalized = normalized,
                    Yaw = yaw,
                    Repertoire = repertoire,
                    ColorIndex = rng.Next(ColorCount),
                    Phase01 = (float)rng.NextDouble(),
                });
            }

            void Walker(PaseoSurface surface, PaseoMove move, float start01)
            {
                plan.Actors.Add(new PaseoActorSpot
                {
                    Surface = surface,
                    Repertoire = new[] { move },
                    Walks = true,
                    PathMargin = surface == PaseoSurface.Techo ? RoofPathMarginMeters : GroundPathOutsetMeters,
                    PathStart01 = start01,
                    ColorIndex = rng.Next(ColorCount),
                    Phase01 = (float)rng.NextDouble(),
                });
            }

            void Prop(PaseoPropKind kind, PaseoSurface surface, Vector2 normalized, float height = 0f)
            {
                plan.Props.Add(new PaseoPropSpot { Kind = kind, Surface = surface, Normalized = normalized, HeightAbove = height });
            }

            public void Ciencias()
            {
                var all = new[] { PaseoMove.Bailar, PaseoMove.Saltar, PaseoMove.Saludar };
                Actor(PaseoSurface.Techo, Roof(-0.6f, -0.4f), Yaw(0f), all);
                Actor(PaseoSurface.Techo, Roof(0.6f, -0.4f), Yaw(0f), PaseoMove.Saltar, PaseoMove.Saludar, PaseoMove.Bailar);
                Actor(PaseoSurface.Techo, Roof(0f, 0.6f), Yaw(0f), PaseoMove.Saludar, PaseoMove.Bailar, PaseoMove.Saltar);
                Walker(PaseoSurface.Suelo, PaseoMove.Caminar, 0f);
                Walker(PaseoSurface.Suelo, PaseoMove.Caminar, 0.5f);
                Prop(PaseoPropKind.Atomo, PaseoSurface.Techo, Roof(0f, 0f), 2.5f);
                Prop(PaseoPropKind.Arbol, PaseoSurface.Suelo, Ground(-1f, 0.4f));
                Prop(PaseoPropKind.Arbol, PaseoSurface.Suelo, Ground(1f, -0.4f));
            }

            public void Pista()
            {
                Walker(PaseoSurface.Techo, PaseoMove.Trotar, 0f);
                Walker(PaseoSurface.Techo, PaseoMove.Trotar, 0.5f);
                Actor(PaseoSurface.Techo, Roof(-0.3f, 0f), Yaw(0f), PaseoMove.Saltar);
                Actor(PaseoSurface.Techo, Roof(0.3f, 0f), Yaw(0f), PaseoMove.Saltar);
                Prop(PaseoPropKind.Bandera, PaseoSurface.Techo, Roof(0.7f, 0f));
                Walker(PaseoSurface.Suelo, PaseoMove.Caminar, 0f);
                Prop(PaseoPropKind.Arbol, PaseoSurface.Suelo, Ground(-1f, 0.4f));
                Prop(PaseoPropKind.Arbol, PaseoSurface.Suelo, Ground(1f, -0.4f));
            }

            public void Saludo()
            {
                // Row along the front edge, facing outward (front = +Z of the front frame).
                foreach (float x in new[] { -0.75f, -0.25f, 0.25f, 0.75f })
                    Actor(PaseoSurface.Techo, Roof(x, 0.85f), Yaw(0f), PaseoMove.Saludar);
                Actor(PaseoSurface.Techo, Roof(0f, -0.3f), Yaw(0f), PaseoMove.Bailar);
                Walker(PaseoSurface.Suelo, PaseoMove.Caminar, 0f);
                Prop(PaseoPropKind.Arbol, PaseoSurface.Suelo, Ground(-1f, 0.4f));
                Prop(PaseoPropKind.Arbol, PaseoSurface.Suelo, Ground(1f, -0.4f));
            }

            public void Baile()
            {
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * 2f * Mathf.PI / 5f;
                    float fx = 0.6f * Mathf.Sin(angle), fz = 0.6f * Mathf.Cos(angle);
                    // Facing the centre of the real roof: an ellipse in fractions is a ring only for a square roof, so
                    // the yaw is taken from the metres.
                    float ux = Mathf.Max(0f, fw / 2f - RoofMarginMeters), uz = Mathf.Max(0f, fd / 2f - RoofMarginMeters);
                    Actor(PaseoSurface.Techo, Roof(fx, fz), Yaw(FacingCentre(fx * ux, fz * uz)), PaseoMove.Bailar);
                }
                Prop(PaseoPropKind.BolaDisco, PaseoSurface.Techo, Roof(0f, 0f), 3.5f);
                Walker(PaseoSurface.Suelo, PaseoMove.Caminar, 0f);
                Prop(PaseoPropKind.Arbol, PaseoSurface.Suelo, Ground(1f, 0.4f));
            }
        }
    }
}

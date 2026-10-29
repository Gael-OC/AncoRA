using System;
using UnityEngine;

namespace AncorRA.AR
{
    public enum PaseoMove { Caminar, Saltar, Saludar, Bailar, Trotar }

    /// <summary>
    /// Joint angles of one block character (degrees). Shoulder and hip values are pitch about local X, positive = forward.
    /// Rolls turn about Z (arm raised sideways/up). bodyLift is metres above standing at scale 1.
    /// </summary>
    public struct PaseoBlockPose
    {
        public float leftShoulder, rightShoulder, leftHip, rightHip;
        public float leftShoulderRoll, rightShoulderRoll;
        public float headYaw;
        public float bodyLift;
        public float bodyYaw;
        public float bodySway;
    }

    /// <summary>Pure animation curves of the block characters: no scene access, so EditMode tests cover them.</summary>
    public static class PaseoBlockMotion
    {
        public const float JumpHeight = 0.6f;
        public const float WalkSpeed = 1.4f;
        public const float JogSpeed = 2.6f;

        /// <summary>Seconds after which the move repeats exactly.</summary>
        public static float Period(PaseoMove move) => move switch
        {
            PaseoMove.Caminar => 1.0f,
            PaseoMove.Trotar => 0.7f,
            PaseoMove.Saltar => 1.2f,
            PaseoMove.Saludar => 1.0f,
            PaseoMove.Bailar => 2.0f,
            _ => 1.0f,
        };

        /// <summary>Travel speed in m/s: only the walking moves travel.</summary>
        public static float Speed(PaseoMove move) => move switch
        {
            PaseoMove.Caminar => WalkSpeed,
            PaseoMove.Trotar => JogSpeed,
            _ => 0f,
        };

        /// <summary>Pose at «timeSeconds»; phase01 shifts the character by a fraction of the period.</summary>
        public static PaseoBlockPose Evaluate(PaseoMove move, float timeSeconds, float phase01)
        {
            float period = Period(move);
            float u = Mathf.Repeat(timeSeconds / period + phase01, 1f);
            float angle = u * 2f * Mathf.PI;
            float s = Mathf.Sin(angle);
            var pose = new PaseoBlockPose();
            switch (move)
            {
                case PaseoMove.Caminar:
                    pose.rightHip = 35f * s;
                    pose.leftHip = -pose.rightHip;
                    pose.leftShoulder = 30f * s;
                    pose.rightShoulder = -pose.leftShoulder;
                    break;
                case PaseoMove.Trotar:
                    pose.rightHip = 55f * s;
                    pose.leftHip = -pose.rightHip;
                    pose.leftShoulder = 60f * s;
                    pose.rightShoulder = -pose.leftShoulder;
                    pose.bodyLift = 0.06f * Mathf.Abs(Mathf.Sin(angle));
                    break;
                case PaseoMove.Saltar:
                    EvaluateJump(u, ref pose);
                    break;
                case PaseoMove.Saludar:
                    pose.rightShoulderRoll = 150f + 25f * s;
                    pose.headYaw = 8f * s;
                    break;
                case PaseoMove.Bailar:
                    pose.bodySway = 10f * s;
                    pose.bodyYaw = 45f * Mathf.Sin(angle + 0.5f * Mathf.PI);
                    pose.bodyLift = 0.08f * Mathf.Abs(Mathf.Sin(angle * 2f));
                    pose.leftShoulderRoll = 100f * Mathf.Max(0f, s);
                    pose.rightShoulderRoll = 100f * Mathf.Max(0f, -s);
                    pose.leftShoulder = 40f * Mathf.Sin(angle * 2f);
                    pose.rightShoulder = -pose.leftShoulder;
                    pose.leftHip = 15f * s;
                    pose.rightHip = -pose.leftHip;
                    break;
            }
            return pose;
        }

        // Crouch (no lift) for the first quarter, then a parabola up and down; the arms go up in the air.
        static void EvaluateJump(float u, ref PaseoBlockPose pose)
        {
            const float crouch = 0.25f;
            if (u < crouch)
            {
                float k = Mathf.Sin(u / crouch * Mathf.PI);
                pose.leftHip = pose.rightHip = 40f * k;
                pose.leftShoulder = pose.rightShoulder = -30f * k;
                return;
            }
            float x = (u - crouch) / (1f - crouch);
            pose.bodyLift = JumpHeight * 4f * x * (1f - x);
            float air = Mathf.Sin(x * Mathf.PI);
            pose.leftShoulder = pose.rightShoulder = 150f * air;
            pose.leftHip = pose.rightHip = 20f * air;
        }
    }
}

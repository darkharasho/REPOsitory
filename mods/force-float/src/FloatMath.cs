using System;

namespace ForceFloat
{
    /// <summary>A Unity-free 3D vector for the testable drift math.</summary>
    public readonly struct V3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public V3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float SqrMagnitude => (X * X) + (Y * Y) + (Z * Z);
    }

    /// <summary>
    /// Pure drift math mirroring the game's SemiAffectZeroGravity.FixedUpdate: the world-space
    /// push direction is camForward*inputZ + camRight*inputX, normalized only when it would
    /// exceed unit length (so partial stick/key input stays proportional).
    /// </summary>
    public static class FloatMath
    {
        public static V3 DriftDirection(V3 camForward, V3 camRight, float inputX, float inputZ)
        {
            float x = (camForward.X * inputZ) + (camRight.X * inputX);
            float y = (camForward.Y * inputZ) + (camRight.Y * inputX);
            float z = (camForward.Z * inputZ) + (camRight.Z * inputX);
            var v = new V3(x, y, z);

            float sqr = v.SqrMagnitude;
            if (sqr > 1f)
            {
                float inv = 1f / (float)Math.Sqrt(sqr);
                return new V3(x * inv, y * inv, z * inv);
            }
            return v;
        }
    }
}

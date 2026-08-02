using System.Numerics;

namespace Osussist.src.cheat.aimbot
{
    internal static class SpinnerPath
    {
        private const float RotationsPerSecond = 8f;

        public static Vector2 PositionAt(Vector2 center, float radius, int startTime, int currentTime)
        {
            float elapsedSeconds = System.Math.Max(0f, currentTime - startTime) / 1000f;
            float angle = elapsedSeconds * (float)(2d * System.Math.PI * RotationsPerSecond);
            return center + new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle)) * radius;
        }
    }
}

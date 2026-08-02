using System.Numerics;

namespace Osussist.src.cheat.aimbot
{
    internal static class LinearMovement
    {
        public static Vector2 CalculateDelta(Vector2 currentPosition, Vector2 destinationPosition, float strength)
        {
            strength = System.Math.Max(0f, System.Math.Min(1f, strength));
            return (destinationPosition - currentPosition) * strength;
        }
    }
}

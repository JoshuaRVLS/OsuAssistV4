using System.Numerics;

namespace Osussist.src.cheat.aimbot
{
    internal sealed class SliderCursorTracker
    {
        public Vector2 CurrentPosition { get; private set; }

        public void Reset(Vector2 position)
        {
            CurrentPosition = position;
        }

        public Vector2 MoveTowards(Vector2 target)
        {
            Vector2 movement = target - CurrentPosition;
            int x = (int)System.Math.Truncate(movement.X);
            int y = (int)System.Math.Truncate(movement.Y);
            Vector2 delta = new Vector2(x, y);
            CurrentPosition += delta;
            return delta;
        }
    }
}

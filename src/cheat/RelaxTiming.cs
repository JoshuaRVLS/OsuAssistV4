namespace Osussist.src.cheat
{
    internal static class RelaxTiming
    {
        public static int NextCenteredHitOffset(Random random, int hitWindow300)
        {
            int jitter = System.Math.Max(1, hitWindow300 / 4);
            return random.Next(-jitter, jitter + 1);
        }
    }
}

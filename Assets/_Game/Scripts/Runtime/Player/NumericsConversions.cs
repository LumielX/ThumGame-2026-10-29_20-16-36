namespace SpiritMovers.Player
{
    /// <summary>Core uses System.Numerics so it stays engine-free; these convert at the boundary.</summary>
    public static class NumericsConversions
    {
        public static System.Numerics.Vector3 ToNumerics(this UnityEngine.Vector3 v) => new(v.x, v.y, v.z);

        public static UnityEngine.Vector3 ToUnity(this System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
    }
}

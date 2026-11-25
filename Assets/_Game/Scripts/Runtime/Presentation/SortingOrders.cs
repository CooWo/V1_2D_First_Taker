using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>2D 정렬 순서 규칙 (y가 낮을수록 앞).</summary>
    public static class SortingOrders
    {
        public const int Floor = -2000;
        public const int Wall = -1900;
        public const int Zone = -1000;
        public const int GroundShadow = -900;
        public const int Projectile = 3000;
        public const int Vfx = 3500;

        public static int ForY(float y) => 1000 - Mathf.RoundToInt(y * 40f);
    }
}

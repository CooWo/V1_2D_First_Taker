using System;

namespace ShadowRPG.Core
{
    /// <summary>난수 추상화. 테스트에서 고정값으로 바꿔 끼울 수 있도록 분리합니다 (DIP).</summary>
    public interface IRandom
    {
        /// <summary>[0, 1) 범위 실수.</summary>
        float Value();
        float Range(float minInclusive, float maxInclusive);
        int Range(int minInclusive, int maxExclusive);
    }

    public sealed class SystemRandom : IRandom
    {
        readonly Random _random;

        public SystemRandom(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public float Value() => (float)_random.NextDouble();
        public float Range(float minInclusive, float maxInclusive) => minInclusive + (maxInclusive - minInclusive) * Value();
        public int Range(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    }

    public static class RandomExtensions
    {
        public static bool Chance(this IRandom random, float percent) => random.Value() * 100f < percent;

        public static T Pick<T>(this IRandom random, System.Collections.Generic.IReadOnlyList<T> list)
            => list[random.Range(0, list.Count)];
    }
}

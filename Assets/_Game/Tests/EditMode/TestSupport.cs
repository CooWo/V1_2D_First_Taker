using ShadowRPG.Core;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Stats;
using UnityEngine;

namespace ShadowRPG.Tests
{
    /// <summary>항상 같은 값을 내는 난수 (치명타·회피·막기를 끄거나 켜기 위해).</summary>
    public sealed class FixedRandom : IRandom
    {
        readonly float _value;
        public FixedRandom(float value) => _value = value;
        public float Value() => _value;
        public float Range(float minInclusive, float maxInclusive) => Mathf.Lerp(minInclusive, maxInclusive, _value);
        public int Range(int minInclusive, int maxExclusive) => Mathf.Clamp(minInclusive + (int)((maxExclusive - minInclusive) * _value), minInclusive, maxExclusive - 1);
    }

    public static class TestFactory
    {
        public static readonly Rect Arena = new Rect(-20f, -20f, 40f, 40f);

        /// <summary>치명타·회피·막기가 절대 일어나지 않는 세션.</summary>
        public static GameSession Session(float randomValue = 0.999f)
            => new GameSession(GameContent.CreateDefault(), new BalanceSettings(), new FixedRandom(randomValue), Arena);

        public static Combatant Dummy(GameSession session, Vector2 position, float life = 100000f)
        {
            var dummy = new Combatant("허수아비", Team.Enemy) { Position = position };
            dummy.Stats.SetBase(StatType.MaxLife, life);
            dummy.Health.ResetToFull();
            session.World.Add(dummy);
            return dummy;
        }
    }
}

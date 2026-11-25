using System;
using ShadowRPG.Core.Character;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Pets;
using ShadowRPG.Core.Skills;
using ShadowRPG.Core.Skills.Behaviors;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Game
{
    /// <summary>던전·레이드가 공통으로 쓰는 전투 월드 조립 (피해 규칙·밸런스 규칙 등록).</summary>
    public static class CombatSetup
    {
        public static CombatWorld CreateWorld(BalanceSettings balance, IRandom random, Rect arena)
        {
            var damage = new DamageService(balance,
                new IOutgoingDamageRule[]
                {
                    new TagScalingRule(), new RangeAsymmetryRule(balance), new TagMultiplierRule(),
                    new BossDamageRule(), new CriticalStrikeRule(balance, random)
                },
                new IIncomingDamageRule[]
                {
                    new EvasionRule(balance, random), new BlockRule(balance, random), new ArmorRule(),
                    new ResistanceRule(balance), new EliteDamageReductionRule(), new StatusModifierRule()
                });

            var world = new CombatWorld(balance, random, damage, arena);
            world.Executor = new SkillExecutor(world, new ISkillModifierSource[] { new StatSkillModifierSource() });

            var fortify = new ShadowFortifyRule(balance);
            damage.AddListener(fortify);
            damage.AddListener(new LifeOnHitRule(balance));
            damage.AddListener(new MarkTransferListener(world, world));
            damage.AddListener(new DamageVfxReporter(world));
            world.Executor.AddListener(fortify);
            return world;
        }
    }

    /// <summary>전투 세션 공통 계약 (던전 / 레이드). 표현 계층은 이것만 보고 그립니다.</summary>
    public interface ICombatSession : IDisposable
    {
        CombatWorld World { get; }
        PlayerCharacter Player { get; }
        PetCompanion Pet { get; }
        bool IsPlayerDead { get; }
        float RespawnRemaining { get; }
        event Action<string> Log;
        void Tick(float dt);
    }
}

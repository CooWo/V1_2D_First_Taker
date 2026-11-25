using System;
using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Economy;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Status;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Pets
{
    /// <summary>펫 스킬 효과. 아주 작지만 꼭 필요한 보조 (물리 피감, 보호막, 신속 등).</summary>
    public interface IPetSkillEffect
    {
        string Describe(float power);
        void Apply(Combatant owner, IEffectSpawner effects, float power);
    }

    public sealed class PetSkillDefinition
    {
        public string Id;
        public string Name;
        public int UnlockGrade = 1;
        public float Cooldown = 12f;
        public IPetSkillEffect Effect;
    }

    public sealed class PetDefinition
    {
        public string Id;
        public string Name;
        public string VisualKey;
        public List<PetSkillDefinition> Skills = new List<PetSkillDefinition>();
    }

    /// <summary>보유 펫의 성장 상태 (저장 대상).</summary>
    [Serializable]
    public sealed class PetState
    {
        public string PetId;
        public int Level = 1;
        public int Grade = 1;
    }

    // ---------------- 효과 ----------------

    public sealed class BarrierPetEffect : IPetSkillEffect
    {
        readonly float _fraction;
        public BarrierPetEffect(float fraction) => _fraction = fraction;
        public string Describe(float power) => $"최대 생명력 {_fraction * power * 100f:0.#}% 보호막 (5초)";

        public void Apply(Combatant owner, IEffectSpawner effects, float power)
        {
            owner.Health.AddBarrier(owner.Health.MaxLife * _fraction * power, 5f);
            effects.Emit(VfxEvent.At(VfxKind.Shield, owner.Position, 0.9f, DamageType.Light, 0.8f));
        }
    }

    public sealed class HealPetEffect : IPetSkillEffect
    {
        readonly float _fraction;
        public HealPetEffect(float fraction) => _fraction = fraction;
        public string Describe(float power) => $"생명력 {_fraction * power * 100f:0.#}% 회복";

        public void Apply(Combatant owner, IEffectSpawner effects, float power)
        {
            owner.Health.Heal(owner.Health.MaxLife * _fraction * power);
            effects.Emit(VfxEvent.At(VfxKind.Nova, owner.Position, 1f, DamageType.Light, 0.4f));
        }
    }

    /// <summary>지속시간 동안 능력치 버프 (신속, 물리 피감, 원소 수호, 고무).</summary>
    public sealed class BuffPetEffect : IPetSkillEffect
    {
        readonly string _id;
        readonly string _label;
        readonly float _duration;
        readonly (StatType stat, float value)[] _stats;

        public BuffPetEffect(string id, string label, float duration, params (StatType, float)[] stats)
        {
            _id = id;
            _label = label;
            _duration = duration;
            _stats = stats;
        }

        public string Describe(float power) => $"{_label} {string.Join(", ", _stats.Select(s => $"+{s.value * power:0.#}"))} ({_duration:0}초)";

        public void Apply(Combatant owner, IEffectSpawner effects, float power)
        {
            var modifiers = _stats.Select(s => new StatModifier(s.stat, s.value * power)).ToArray();
            owner.Statuses.Remove(_id);
            owner.Statuses.Apply(new StatBuffStatus(_id, _duration, modifiers));
            effects.Emit(VfxEvent.At(VfxKind.Warcry, owner.Position, 0.8f, DamageType.Light, 0.35f));
        }
    }

    public interface IPetCatalog
    {
        IReadOnlyList<PetDefinition> All { get; }
        PetDefinition Get(string id);
    }

    public sealed class PetCatalog : IPetCatalog
    {
        public PetCatalog(IEnumerable<PetDefinition> pets) => All = pets.ToList();
        public IReadOnlyList<PetDefinition> All { get; }
        public PetDefinition Get(string id) => All.FirstOrDefault(p => p.Id == id);

        public static PetCatalog CreateDefault()
        {
            var haste = new BuffPetEffect("pet_haste", "신속: 이동/공격 속도", 4f, (StatType.MoveSpeedPct, 12f), (StatType.AttackSpeedPct, 8f));
            var guard = new BuffPetEffect("pet_guard", "물리 피감: 방어도/효율", 5f, (StatType.Armor, 60f), (StatType.ArmorEffectPct, 15f));
            var ward = new BuffPetEffect("pet_ward", "원소 수호: 모든 저항", 6f, (StatType.AllResistPct, 10f));
            var inspire = new BuffPetEffect("pet_inspire", "고무: 피해", 5f, (StatType.AllDamagePct, 8f));

            return new PetCatalog(new[]
            {
                new PetDefinition
                {
                    Id = "spirit", Name = "빛의 정령", VisualKey = "angel",
                    Skills =
                    {
                        new PetSkillDefinition { Id = "spirit_shield", Name = "보호막 걸기", UnlockGrade = 1, Cooldown = 12f, Effect = new BarrierPetEffect(0.06f) },
                        new PetSkillDefinition { Id = "spirit_heal", Name = "치유의 빛", UnlockGrade = 3, Cooldown = 15f, Effect = new HealPetEffect(0.05f) },
                        new PetSkillDefinition { Id = "spirit_ward", Name = "원소 수호", UnlockGrade = 5, Cooldown = 18f, Effect = ward }
                    }
                },
                new PetDefinition
                {
                    Id = "pumpkin", Name = "호박 도깨비", VisualKey = "pumpkin_dude",
                    Skills =
                    {
                        new PetSkillDefinition { Id = "pumpkin_haste", Name = "신속", UnlockGrade = 1, Cooldown = 14f, Effect = haste },
                        new PetSkillDefinition { Id = "pumpkin_inspire", Name = "고무", UnlockGrade = 2, Cooldown = 16f, Effect = inspire },
                        new PetSkillDefinition { Id = "pumpkin_shield", Name = "보호막 걸기", UnlockGrade = 4, Cooldown = 18f, Effect = new BarrierPetEffect(0.05f) }
                    }
                },
                new PetDefinition
                {
                    Id = "slug", Name = "단단한 민달팽이", VisualKey = "tiny_slug",
                    Skills =
                    {
                        new PetSkillDefinition { Id = "slug_guard", Name = "물리 피감", UnlockGrade = 1, Cooldown = 15f, Effect = guard },
                        new PetSkillDefinition { Id = "slug_heal", Name = "점액 재생", UnlockGrade = 3, Cooldown = 16f, Effect = new HealPetEffect(0.04f) },
                        new PetSkillDefinition { Id = "slug_ward", Name = "원소 수호", UnlockGrade = 5, Cooldown = 20f, Effect = ward }
                    }
                }
            });
        }
    }

    /// <summary>펫 성장: 레벨업·등급업 모두 골드(폐지 판매 수익)로. 등급마다 최대 레벨과 스킬이 열립니다.</summary>
    public sealed class PetService
    {
        public const int MaxGrade = 5;

        public static int MaxLevel(PetState state) => state.Grade * 10;

        public long LevelUpCost(PetState state) => (long)Mathf.Round(60f * Mathf.Pow(1.2f, state.Level - 1) * state.Grade);

        public long GradeUpCost(PetState state) => (long)Mathf.Round(800f * Mathf.Pow(5f, state.Grade - 1));

        public bool TryLevelUp(PetState state, Wallet wallet)
        {
            if (state.Level >= MaxLevel(state)) return false;
            if (!wallet.TrySpend(LevelUpCost(state))) return false;
            state.Level++;
            return true;
        }

        /// <summary>등급업은 현재 등급의 최대 레벨을 채워야 가능합니다.</summary>
        public bool TryGradeUp(PetState state, Wallet wallet)
        {
            if (state.Grade >= MaxGrade || state.Level < MaxLevel(state)) return false;
            if (!wallet.TrySpend(GradeUpCost(state))) return false;
            state.Grade++;
            return true;
        }

        /// <summary>펫 효과 배율: 레벨 +3%, 등급 +25%, 트리의 펫 능력 %.</summary>
        public static float Power(PetState state, IStatReader ownerStats)
            => (1f + 0.03f * (state.Level - 1) + 0.25f * (state.Grade - 1)) * (1f + ownerStats.Get(StatType.PetPowerPct) / 100f);
    }

    /// <summary>전투 중 펫: 주인을 따라다니며 열린 스킬을 쿨타임마다 사용합니다.</summary>
    public sealed class PetCompanion
    {
        readonly Combatant _owner;
        readonly IEffectSpawner _effects;
        readonly float[] _timers;

        public PetCompanion(PetDefinition definition, PetState state, Combatant owner, IEffectSpawner effects)
        {
            Definition = definition;
            State = state;
            _owner = owner;
            _effects = effects;
            Position = owner.Position + new Vector2(-0.8f, 0.4f);
            _timers = definition.Skills.Select(s => s.Cooldown * 0.3f).ToArray();
        }

        public PetDefinition Definition { get; }
        public PetState State { get; }
        public Vector2 Position { get; private set; }

        public event Action<PetSkillDefinition> SkillUsed;

        /// <summary>층 이동 등 순간이동 시 주인 곁으로.</summary>
        public void SnapTo(Vector2 ownerPosition) => Position = ownerPosition + new Vector2(-0.8f, 0.5f);

        public IEnumerable<PetSkillDefinition> UnlockedSkills => Definition.Skills.Where(s => s.UnlockGrade <= State.Grade);

        public void Tick(float dt)
        {
            var target = _owner.Position + new Vector2(_owner.Facing.x >= 0f ? -0.8f : 0.8f, 0.5f);
            Position = Vector2.Lerp(Position, target, Mathf.Clamp01(dt * 4f));
            if (!_owner.IsAlive) return;

            float cooldownScale = 1f - Mathf.Clamp(_owner.Stats.Get(StatType.PetCooldownPct), 0f, 50f) / 100f;
            float power = PetService.Power(State, _owner.Stats);
            for (int i = 0; i < Definition.Skills.Count; i++)
            {
                var skill = Definition.Skills[i];
                if (skill.UnlockGrade > State.Grade) continue;
                _timers[i] -= dt;
                if (_timers[i] > 0f) continue;
                _timers[i] = skill.Cooldown * cooldownScale;
                skill.Effect.Apply(_owner, _effects, power);
                SkillUsed?.Invoke(skill);
            }
        }
    }
}

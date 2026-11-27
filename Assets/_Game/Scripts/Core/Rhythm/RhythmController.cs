using System;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Tuning;
using UnityEngine;

namespace ShadowRPG.Core.Rhythm
{
    public enum RhythmJudgement
    {
        Miss,
        Good,
        Perfect
    }

    /// <summary>리듬 무기 정보 (장비가 정함). 박자는 무기마다 다르고 배경음악과 무관한 단순 반복.</summary>
    public interface IRhythmSource
    {
        /// <summary>리듬 무기를 들고 있으면 박자 간격(초), 아니면 0.</summary>
        float BeatSeconds { get; }
    }

    /// <summary>
    /// 리듬 입력 (방향성 v1 §7): 바깥 원이 줄어들어 판정 원에 맞을 때 터치.
    /// 성공 → 즉시 추가 타격 + 공격 속도 중첩(짧게 유지). 실패 → 불이익 없음, 보너스만 없음.
    /// 시간은 Tick으로만 흐르므로 배속·히트스톱과 자연스럽게 맞습니다.
    /// </summary>
    public sealed class RhythmController
    {
        readonly Combatant _owner;
        readonly IRhythmSource _source;
        readonly RhythmTuning _tuning;
        readonly Func<Combatant> _targetProvider;
        readonly DamageService _damage;
        readonly object _buffSource = new object();
        float _time;
        int _judgedBeat = -1;
        float _stackTimer;

        public RhythmController(Combatant owner, IRhythmSource source, RhythmTuning tuning, DamageService damage, Func<Combatant> targetProvider)
        {
            _owner = owner;
            _source = source;
            _tuning = tuning;
            _damage = damage;
            _targetProvider = targetProvider;
        }

        public bool IsActive => _source != null && _source.BeatSeconds > 0.05f;
        public float BeatSeconds => IsActive ? _source.BeatSeconds : 0f;
        /// <summary>다음 박까지 진행도 0→1 (1이 판정 순간). UI가 원 크기를 그릴 때 사용.</summary>
        public float Phase => IsActive ? (_time % BeatSeconds) / BeatSeconds : 0f;
        public int Stacks { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public RhythmJudgement LastJudgement { get; private set; }

        public event Action<RhythmJudgement, int> Judged;
        /// <summary>박마다 (UI 박자음·원 초기화용).</summary>
        public event Action Beat;

        public void Tick(float dt)
        {
            if (!IsActive)
            {
                if (Stacks > 0) ClearStacks();
                return;
            }
            int before = Mathf.FloorToInt(_time / BeatSeconds);
            _time += dt;
            int after = Mathf.FloorToInt(_time / BeatSeconds);
            if (after != before)
            {
                Beat?.Invoke();
                // 판정 창이 지나도록 누르지 않은 박 = 콤보 끊김 (불이익은 없음)
                if (_judgedBeat < before) Combo = 0;
            }

            if (Stacks > 0)
            {
                _stackTimer -= dt;
                if (_stackTimer <= 0f) ClearStacks();
            }
        }

        /// <summary>화면 터치. 가장 가까운 박과의 시간 차이로 판정합니다.</summary>
        public RhythmJudgement Tap()
        {
            if (!IsActive) return RhythmJudgement.Miss;
            float beat = BeatSeconds;
            int nearest = Mathf.RoundToInt(_time / beat);
            float diff = Mathf.Abs(_time - nearest * beat);
            var judgement = nearest == _judgedBeat || nearest <= 0 ? RhythmJudgement.Miss
                : diff <= _tuning.PerfectWindowSeconds ? RhythmJudgement.Perfect
                : diff <= _tuning.GoodWindowSeconds ? RhythmJudgement.Good
                : RhythmJudgement.Miss;

            LastJudgement = judgement;
            if (judgement == RhythmJudgement.Miss)
            {
                Combo = 0;
                Judged?.Invoke(judgement, 0);
                return judgement;
            }

            _judgedBeat = nearest;
            Combo++;
            BestCombo = Mathf.Max(BestCombo, Combo);
            AddStack();
            int hits = judgement == RhythmJudgement.Perfect ? _tuning.PerfectExtraHits : _tuning.GoodExtraHits;
            int landed = ExtraHits(hits);
            Judged?.Invoke(judgement, landed);
            return judgement;
        }

        int ExtraHits(int count)
        {
            var target = _targetProvider?.Invoke();
            if (target == null || !target.IsAlive || _damage == null) return 0;
            int landed = 0;
            float baseAmount = _owner.Stats.Get(StatType.WeaponDamage) * _tuning.ExtraHitDamagePct / 100f;
            for (int i = 0; i < count && target.IsAlive; i++)
            {
                var result = _damage.Deal(new DamageRequest
                {
                    Attacker = _owner,
                    Target = target,
                    Tags = SkillTag.Attack | SkillTag.Melee,
                    Type = DamageType.Physical,
                    BaseAmount = baseAmount,
                    CanCrit = true
                });
                if (result.Landed) landed++;
            }
            return landed;
        }

        void AddStack()
        {
            Stacks = Mathf.Min(_tuning.MaxStacks, Stacks + 1);
            _stackTimer = _tuning.StackDurationSeconds;
            ApplyBuff();
        }

        void ClearStacks()
        {
            Stacks = 0;
            ApplyBuff();
        }

        void ApplyBuff()
        {
            _owner.Stats.RemoveSource(_buffSource);
            if (Stacks > 0) _owner.Stats.AddModifier(new StatModifier(StatType.AttackSpeedPct, Stacks * _tuning.AttackSpeedPctPerStack, _buffSource));
        }
    }
}

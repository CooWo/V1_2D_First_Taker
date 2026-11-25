using System.Collections.Generic;
using System.Linq;
using ShadowRPG.Core.AutoBattle;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Skills.Behaviors;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Status;

namespace ShadowRPG.Core.Skills
{
    /// <summary>스킬 정의 조회 창구. 데이터 출처(코드/JSON/서버)를 바꿔도 소비자는 그대로입니다 (DIP).</summary>
    public interface ISkillCatalog
    {
        IReadOnlyList<SkillDefinition> All { get; }
        SkillDefinition Get(string code);
        SkillDefinition BasicMelee { get; }
        SkillDefinition BasicRanged { get; }
    }

    /// <summary>GDD 5장 마스터 스킬 리스트 (힘 8 / 민첩 8 / 지능 8) + 기본 공격 2종.</summary>
    public sealed class SkillCatalog : ISkillCatalog
    {
        readonly Dictionary<string, SkillDefinition> _byCode;

        public SkillCatalog(IEnumerable<SkillDefinition> definitions, SkillDefinition basicMelee, SkillDefinition basicRanged)
        {
            All = definitions.ToList();
            _byCode = All.ToDictionary(d => d.Code);
            BasicMelee = basicMelee;
            BasicRanged = basicRanged;
            _byCode[basicMelee.Code] = basicMelee;
            _byCode[basicRanged.Code] = basicRanged;
        }

        public IReadOnlyList<SkillDefinition> All { get; }
        public SkillDefinition BasicMelee { get; }
        public SkillDefinition BasicRanged { get; }

        public SkillDefinition Get(string code) => _byCode.TryGetValue(code, out var def) ? def : null;

        public static SkillCatalog CreateDefault()
        {
            const SkillTag S = SkillTag.Strength, D = SkillTag.Dexterity, I = SkillTag.Intelligence;
            const SkillTag Atk = SkillTag.Attack, Spl = SkillTag.Spell, Mel = SkillTag.Melee, Rng = SkillTag.Ranged;
            const SkillTag Prj = SkillTag.Projectile, Aoe = SkillTag.Area, Phy = SkillTag.Physical;

            var list = new List<SkillDefinition>
            {
                // ---------------- 힘 (STR) ----------------
                new SkillDefinition
                {
                    Code = "STR-01", Name = "대지 분쇄", EnglishName = "Earthshatter", Attribute = CoreStat.Strength,
                    Tags = S | Atk | Mel | SkillTag.Slam | Aoe | Phy, Category = AutoCastCategory.Primary, DamageType = DamageType.Physical,
                    Coefficient = 1.1f, ResourceCost = 6f, Range = 2.2f, CastTime = 0.75f, Behavior = new EarthshatterBehavior(),
                    Summary = "전방 부채꼴 충격파로 적을 둔화", RareGimmick = "범위 +20%", LegendaryAwakening = "그림자 바위 기둥 3개 연쇄 폭발"
                },
                new SkillDefinition
                {
                    Code = "STR-02", Name = "소용돌이 참격", EnglishName = "Cyclone Cleave", Attribute = CoreStat.Strength,
                    Tags = S | Atk | Mel | SkillTag.Channelling | Aoe | SkillTag.Movement | Phy, Category = AutoCastCategory.Primary,
                    Coefficient = 0.45f, ResourceCost = 4f, Range = 2f, CastTime = 0.3f, Behavior = new CycloneCleaveBehavior(),
                    Condition = When.ResourceAtLeast(0.2f),
                    Summary = "회전하며 적 무리를 통과해 연속 베기", RareGimmick = "범위 +20%", LegendaryAwakening = "반경 +35%, 중력 소용돌이"
                },
                new SkillDefinition
                {
                    Code = "STR-03", Name = "용암 파쇄격", EnglishName = "Molten Strike", Attribute = CoreStat.Strength,
                    Tags = S | Atk | Mel | Prj | Aoe | SkillTag.Fire, Category = AutoCastCategory.Primary, DamageType = DamageType.Fire,
                    Coefficient = 0.9f, ResourceCost = 5f, Range = 1.7f, CastTime = 0.65f, Behavior = new MoltenStrikeBehavior(),
                    Summary = "타격 지점에서 화염 구체 3개 포물선 낙하", RareGimmick = "폭발 범위 +20%", LegendaryAwakening = "구체 +3, 용암 지대"
                },
                new SkillDefinition
                {
                    Code = "STR-04", Name = "선조의 그림자 망치", EnglishName = "Ancestral Call", Attribute = CoreStat.Strength,
                    Tags = S | Atk | Mel | SkillTag.Slam | SkillTag.Clone | Phy, Category = AutoCastCategory.Primary,
                    Coefficient = 1.3f, ResourceCost = 6f, Range = 1.8f, CastTime = 0.8f, Behavior = new AncestralCallBehavior(),
                    Summary = "그림자가 뻗어 주변 적 2체 추가 강타", RareGimmick = "주 대상 둔화", LegendaryAwakening = "1:1 시 분신 60% 합동 타격"
                },
                new SkillDefinition
                {
                    Code = "STR-05", Name = "지각 변동파", EnglishName = "Tectonic Wave", Attribute = CoreStat.Strength,
                    Tags = S | Atk | Rng | Aoe | Phy | SkillTag.Fire | Prj, Category = AutoCastCategory.Primary, DamageType = DamageType.Fire,
                    Coefficient = 0.9f, ResourceCost = 7f, Range = 8f, CastTime = 0.7f, Behavior = new TectonicWaveBehavior(),
                    Summary = "지면을 가르는 장거리 화염 균열 파동", RareGimmick = "파동 폭 +20%", LegendaryAwakening = "관통 시 좌우 갈래 균열"
                },
                new SkillDefinition
                {
                    Code = "STR-06", Name = "투척 분쇄창", EnglishName = "Shattering Javelin", Attribute = CoreStat.Strength,
                    Tags = S | Atk | Rng | Prj | Phy, Category = AutoCastCategory.Primary,
                    Coefficient = 1.4f, ResourceCost = 7f, Range = 9f, CastTime = 0.7f, Behavior = new ShatteringJavelinBehavior(),
                    Summary = "무거운 투창 환영 투척 + 밀쳐내기", RareGimmick = "관통 +1", LegendaryAwakening = "8방향 관통 파편 비산"
                },
                new SkillDefinition
                {
                    Code = "STR-07", Name = "불멸의 외침", EnglishName = "Immortal Warcry", Attribute = CoreStat.Strength,
                    Tags = S | SkillTag.Warcry | Aoe | SkillTag.Survival | SkillTag.Buff, Category = AutoCastCategory.Tactical,
                    Coefficient = 0f, Cooldown = 8f, ResourceCost = 8f, Range = 4f, CastTime = 0.4f, Behavior = new ImmortalWarcryBehavior(),
                    Condition = When.Any(When.LifeBelow(0.65f), When.EnemiesWithin(4f, 3)),
                    Summary = "적 수 비례 보호막 + 주변 적 약화", RareGimmick = "-", LegendaryAwakening = "보호막 동안 [강타] 1.3배"
                },
                new SkillDefinition
                {
                    Code = "STR-08", Name = "복수의 가시 갑주", EnglishName = "Vengeance", Attribute = CoreStat.Strength,
                    Tags = S | Atk | Mel | Aoe | SkillTag.Trigger | Phy, Category = AutoCastCategory.Reactive,
                    Coefficient = 0.8f, Range = 2f, CastTime = 0f, Behavior = new VengeanceBehavior(),
                    Summary = "피격/막기 시 35% 확률 360° 반격 (내부 쿨 0.8초)", RareGimmick = "범위 +20%", LegendaryAwakening = "막기 시 100%, 방어도 합산"
                },

                // ---------------- 민첩 (DEX) ----------------
                new SkillDefinition
                {
                    Code = "DEX-01", Name = "점멸 타격", EnglishName = "Flicker Strike", Attribute = CoreStat.Dexterity,
                    Tags = D | Atk | Mel | SkillTag.Movement | SkillTag.MultiHit | Phy, Category = AutoCastCategory.Primary,
                    Coefficient = 1.1f, Cooldown = 1f, ResourceCost = 6f, Range = 9f, CastTime = 0.45f, Behavior = new FlickerStrikeBehavior(),
                    Summary = "적 그림자 뒤로 순간이동하며 베기 (무적)", RareGimmick = "범위 +20%", LegendaryAwakening = "처치 시 쿨타임 초기화"
                },
                new SkillDefinition
                {
                    Code = "DEX-02", Name = "쌍독아 난무", EnglishName = "Viper Flurry", Attribute = CoreStat.Dexterity,
                    Tags = D | Atk | Mel | SkillTag.MultiHit | SkillTag.Duration | SkillTag.Poison, Category = AutoCastCategory.Primary,
                    Coefficient = 1.2f, ResourceCost = 4f, Range = 1.5f, CastTime = 0.45f, Behavior = new ViperFlurryBehavior(),
                    Summary = "초고속 3연타 + 맹독 중첩", RareGimmick = "출혈 부여", LegendaryAwakening = "5중첩 시 맹독 절개"
                },
                new SkillDefinition
                {
                    Code = "DEX-03", Name = "서리 칼날", EnglishName = "Frost Blades", Attribute = CoreStat.Dexterity,
                    Tags = D | Atk | Mel | Prj | SkillTag.Chain | SkillTag.Cold, Category = AutoCastCategory.Primary, DamageType = DamageType.Cold,
                    Coefficient = 1f, ResourceCost = 5f, Range = 3.2f, CastTime = 0.6f, Behavior = new FrostBladesBehavior(),
                    Summary = "긴 냉기 일격 + 후방 얼음 칼날 4개", RareGimmick = "둔화", LegendaryAwakening = "칼날 +2 연쇄, 빙결 처치 폭발"
                },
                new SkillDefinition
                {
                    Code = "DEX-04", Name = "회오리 사격", EnglishName = "Tornado Shot", Attribute = CoreStat.Dexterity,
                    Tags = D | Atk | Rng | Prj | Aoe | Phy, Category = AutoCastCategory.Primary,
                    Coefficient = 0.8f, ResourceCost = 7f, Range = 10f, CastTime = 0.7f, Behavior = new TornadoShotBehavior(),
                    Summary = "목표 지점 폭발 후 2차 화살 5발 난사", RareGimmick = "2차 화살 관통 +1", LegendaryAwakening = "잔류 폭풍 + 유도 화살"
                },
                new SkillDefinition
                {
                    Code = "DEX-05", Name = "번개 화살비", EnglishName = "Lightning Rain", Attribute = CoreStat.Dexterity,
                    Tags = D | Atk | Rng | Prj | Aoe | SkillTag.Lightning, Category = AutoCastCategory.Burst, DamageType = DamageType.Lightning,
                    Coefficient = 1f, Cooldown = 3f, ResourceCost = 10f, Range = 10f, CastTime = 0.5f, Behavior = new LightningRainBehavior(),
                    Summary = "목표 지점 번개 폭우 + 감전", RareGimmick = "범위 +20%", LegendaryAwakening = "감전 적에게 추가 낙뢰 3회"
                },
                new SkillDefinition
                {
                    Code = "DEX-06", Name = "환영 무기 투척", EnglishName = "Spectral Throw", Attribute = CoreStat.Dexterity,
                    Tags = D | Atk | Rng | Prj | SkillTag.Pierce | Phy, Category = AutoCastCategory.Primary,
                    Coefficient = 0.9f, ResourceCost = 6f, Range = 7f, CastTime = 0.6f, Behavior = new SpectralThrowBehavior(),
                    Summary = "무기 그림자 복제본 왕복 관통", RareGimmick = "판정 +20%", LegendaryAwakening = "복귀 후 3초 공전"
                },
                new SkillDefinition
                {
                    Code = "DEX-07", Name = "그림자 연막 질주", EnglishName = "Smoke Dash", Attribute = CoreStat.Dexterity,
                    Tags = D | SkillTag.Movement | SkillTag.Evasion | SkillTag.Buff, Category = AutoCastCategory.Mobility, DamageType = DamageType.Shadow,
                    Coefficient = 0.6f, Cooldown = 4f, ResourceCost = 5f, Range = 6f, CastTime = 0.1f, Behavior = new SmokeDashBehavior(),
                    Summary = "연막 질주 + 3초 회피 +25%", RareGimmick = "-", LegendaryAwakening = "출발/도착 분신 미끼 폭발"
                },
                new SkillDefinition
                {
                    Code = "DEX-08", Name = "암살자의 표식", EnglishName = "Assassin's Mark", Attribute = CoreStat.Dexterity,
                    Tags = D | SkillTag.Mark | SkillTag.Trigger | SkillTag.Critical, Category = AutoCastCategory.Tactical,
                    Coefficient = 0f, Cooldown = 6f, ResourceCost = 6f, Range = 10f, CastTime = 0.2f, Behavior = new AssassinsMarkBehavior(),
                    Condition = new NoActiveMarkCondition(),
                    Summary = "대상이 받는 치명타 +8% / 치명타 피해 +30%", RareGimmick = "표식 효과 +25%", LegendaryAwakening = "처치 시 회복 + 강화 표식 전이"
                },

                // ---------------- 지능 (INT) ----------------
                new SkillDefinition
                {
                    Code = "INT-01", Name = "연쇄 번개", EnglishName = "Arc Lightning", Attribute = CoreStat.Intelligence,
                    Tags = I | Spl | Rng | SkillTag.Chain | SkillTag.Lightning, Category = AutoCastCategory.Primary, DamageType = DamageType.Lightning,
                    Coefficient = 0.75f, ResourceCost = 6f, Range = 8f, CastTime = 0.55f, Behavior = new ArcLightningBehavior(),
                    Summary = "적 사이를 최대 5회 연쇄", RareGimmick = "감전 부여", LegendaryAwakening = "남은 연쇄 1회당 +18% 곱연산"
                },
                new SkillDefinition
                {
                    Code = "INT-02", Name = "공허의 소용돌이", EnglishName = "Void Vortex", Attribute = CoreStat.Intelligence,
                    Tags = I | Spl | Rng | Aoe | SkillTag.Duration | SkillTag.Shadow, Category = AutoCastCategory.Burst, DamageType = DamageType.Shadow,
                    Coefficient = 1f, Cooldown = 6f, ResourceCost = 14f, Range = 9f, CastTime = 0.6f, Behavior = new VoidVortexBehavior(),
                    Condition = When.EnemiesWithin(9f, 3),
                    Summary = "4초 블랙홀이 적을 끌어당김", RareGimmick = "범위 +20%", LegendaryAwakening = "종료 시 공허 붕괴 폭발"
                },
                new SkillDefinition
                {
                    Code = "INT-03", Name = "운석 낙하", EnglishName = "Meteor Shower", Attribute = CoreStat.Intelligence,
                    Tags = I | Spl | Rng | Aoe | SkillTag.Fire, Category = AutoCastCategory.Burst, DamageType = DamageType.Fire,
                    Coefficient = 2.4f, Cooldown = 5f, ResourceCost = 18f, Range = 10f, CastTime = 0.8f, Behavior = new MeteorShowerBehavior(),
                    Condition = When.ResourceAtLeast(0.4f),
                    Summary = "거대 운석 + 점화", RareGimmick = "범위 +20%", LegendaryAwakening = "유성우 5발, 점화 2배 속도"
                },
                new SkillDefinition
                {
                    Code = "INT-04", Name = "빙결 영혼 구체", EnglishName = "Freezing Orb", Attribute = CoreStat.Intelligence,
                    Tags = I | Spl | Rng | Prj | SkillTag.Cold, Category = AutoCastCategory.Primary, DamageType = DamageType.Cold,
                    Coefficient = 1f, ResourceCost = 9f, Range = 8f, CastTime = 0.65f, Behavior = new FreezingOrbBehavior(),
                    Summary = "유영하며 얼음 바늘 360° 난사", RareGimmick = "바늘 둔화", LegendaryAwakening = "성장 + 빙창 12개"
                },
                new SkillDefinition
                {
                    Code = "INT-05", Name = "칼날 소용돌이", EnglishName = "Blade Vortex", Attribute = CoreStat.Intelligence,
                    Tags = I | Spl | Mel | Aoe | SkillTag.Duration | Phy, Category = AutoCastCategory.Burst,
                    Coefficient = 1f, Cooldown = 2.5f, ResourceCost = 6f, Range = 5f, CastTime = 0.35f, Behavior = new BladeVortexBehavior(),
                    Condition = When.EnemiesWithin(6f, 1),
                    Summary = "주변 공전 칼날 최대 5중첩", RareGimmick = "범위 +20%", LegendaryAwakening = "5중첩 1.5배 + 보호막 흡수"
                },
                new SkillDefinition
                {
                    Code = "INT-06", Name = "비전 마검격", EnglishName = "Arcane Spellblade", Attribute = CoreStat.Intelligence,
                    Tags = I | Atk | Spl | Mel | Aoe | SkillTag.Arcane, Category = AutoCastCategory.Primary, DamageType = DamageType.Arcane,
                    Coefficient = 1f, ResourceCost = 5f, Range = 2.1f, CastTime = 0.6f, Behavior = new ArcaneSpellbladeBehavior(),
                    Summary = "주문 피해 증가가 100% 적용되는 근접 베기", RareGimmick = "범위 +20%", LegendaryAwakening = "3타마다 다른 주문 무료 발사"
                },
                new SkillDefinition
                {
                    Code = "INT-07", Name = "심연의 방벽", EnglishName = "Abyssal Aegis", Attribute = CoreStat.Intelligence,
                    Tags = I | Spl | SkillTag.Defense | Aoe | SkillTag.Trigger | SkillTag.Cold, Category = AutoCastCategory.Survival, DamageType = DamageType.Cold,
                    Coefficient = 0.5f, Cooldown = 10f, Range = 5f, CastTime = 0.2f, Behavior = new AbyssalAegisBehavior(),
                    Summary = "최대 자원 비례 피해 흡수 방벽", RareGimmick = "방벽량 +20%", LegendaryAwakening = "파괴/만료 시 5m 빙결"
                },
                new SkillDefinition
                {
                    Code = "INT-08", Name = "그림자 전령", EnglishName = "Herald of Shadow", Attribute = CoreStat.Intelligence,
                    Tags = I | SkillTag.Aura | Aoe | SkillTag.Trigger | SkillTag.Shadow, Category = AutoCastCategory.Aura, DamageType = DamageType.Shadow,
                    Coefficient = 0.8f, Reservation = 0.15f, Range = 0f, CastTime = 0.3f, Behavior = new HeraldOfShadowBehavior(),
                    Summary = "피해 +12%, 상태이상 적 처치 시 시체 폭발", RareGimmick = "폭발 범위 +20%", LegendaryAwakening = "폭발 사슬 반응"
                }
            };

            var basicMelee = new SkillDefinition
            {
                Code = "BASIC-M", Name = "그림자 일격", EnglishName = "Shadow Strike", Attribute = CoreStat.Strength,
                Tags = Atk | Mel | Phy, Category = AutoCastCategory.Primary, Coefficient = 0.8f, Range = 1.5f, CastTime = 0.6f,
                Behavior = new ShadowStrikeBehavior(), Summary = "기본 근접 공격 (자원 생성)"
            };
            var basicRanged = new SkillDefinition
            {
                Code = "BASIC-R", Name = "그림자 화살", EnglishName = "Shadow Bolt", Attribute = CoreStat.Dexterity,
                Tags = Atk | Rng | Prj | Phy, Category = AutoCastCategory.Primary, Coefficient = 0.6f, Range = 8f, CastTime = 0.6f,
                Behavior = new ShadowBoltBehavior(), Summary = "기본 원거리 공격 (자원 생성)"
            };

            return new SkillCatalog(list, basicMelee, basicRanged);
        }
    }

    /// <summary>표식이 걸린 적이 없을 때만 새로 표식을 새깁니다.</summary>
    public sealed class NoActiveMarkCondition : IAutoCastCondition
    {
        public bool IsMet(AutoBattleContext context, SkillInstance skill)
        {
            var list = ListPool<Actors.Combatant>.Get();
            context.World.QueryEnemies(context.Self.Team, context.Self.Position, skill.Definition.Range, list);
            bool anyEnemy = list.Count > 0;
            bool anyMarked = false;
            foreach (var enemy in list)
                if (enemy.Statuses.Has(StatusIds.AssassinMark)) { anyMarked = true; break; }
            ListPool<Actors.Combatant>.Release(list);
            return anyEnemy && !anyMarked;
        }
    }
}

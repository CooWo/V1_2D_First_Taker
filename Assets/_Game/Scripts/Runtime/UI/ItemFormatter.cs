using System.Text;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Stats;
using ShadowRPG.Runtime.Presentation;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>장비·능력치를 화면용 문자열로 바꿉니다 (표시 규칙 한 곳 모음).</summary>
    public static class ItemFormatter
    {
        public static string ColoredName(Item item)
            => item == null ? "<color=#5A6070>(비어 있음)</color>" : $"<color=#{Palette.Hex(Palette.For(item.Rarity))}>{item.DisplayName}</color>";

        public static string ShortGrant(Item item, GameContent content)
        {
            if (item == null) return string.Empty;
            var sb = new StringBuilder();
            if (item.GrantedSkill.IsValid)
            {
                var skill = content.Skills.Get(item.GrantedSkill.SkillCode);
                var color = Palette.Hex(Palette.For(item.GrantedSkill.Rarity));
                sb.Append($"<color=#{color}>◆ {skill?.Name ?? item.GrantedSkill.SkillCode}</color>");
            }
            if (item.SupportCode != null)
            {
                var support = content.Supports.Get(item.SupportCode);
                sb.Append($"<color=#9FD3FF>◇ {support?.Name ?? item.SupportCode}</color>");
            }
            if (item.ReducesSetRequirement) sb.Append(" <color=#3FE06A>[세트 -1]</color>");
            return sb.ToString();
        }

        /// <summary>전투력 계산기 (HUD가 연결). 모든 아이템 표기에 전투력.</summary>
        public static System.Func<Item, int> Power;

        public static string Describe(Item item, GameContent content)
        {
            if (item == null) return string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine($"{ColoredName(item)}  <size=18>Lv.{item.ItemLevel} {item.Rarity.DisplayName()} · {SlotRules.DisplayName(item.Slot)}</size>");
            if (Power != null) sb.AppendLine($"<size=20><color=#FFD84A>전투력 {Power(item):N0}</color></size>" + (item.IsRhythm ? $"  <size=18><color=#C8A0FF>리듬 무기 · 박자 {item.BeatSeconds:0.00}초</color></size>" : string.Empty));
            sb.AppendLine($"<size=18>요구: {item.Requirements} · 직업 제한 없음</size>");
            foreach (var m in item.AllModifiers) sb.AppendLine($"<size=18>  {StatName(m.Stat)} {FormatValue(m.Stat, m.Value)}</size>");
            string grant = ShortGrant(item, content);
            if (grant.Length > 0) sb.AppendLine($"<size=18>{grant}</size>");
            if (item.PowerDescription != null) sb.AppendLine($"<size=18><color=#FF8A2A>전설 권능: {item.PowerDescription}</color></size>");
            if (item.SetId != null) sb.AppendLine($"<size=18><color=#3FE06A>{content.Sets.Get(item.SetId)?.Name}</color></size>");
            return sb.ToString();
        }

        public static string FormatValue(StatType stat, float value)
        {
            string sign = value >= 0 ? "+" : "";
            return stat.ToString().EndsWith("Pct") ? $"{sign}{value:0.#}%" : $"{sign}{value:0.##}";
        }

        public static string StatName(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength: return "힘";
                case StatType.Dexterity: return "민첩";
                case StatType.Intelligence: return "지능";
                case StatType.MaxLife: return "최대 생명력";
                case StatType.LifePct: return "생명력";
                case StatType.LifeRegen: return "생명력 재생";
                case StatType.Armor: return "방어도";
                case StatType.ArmorEffectPct: return "방어도 효율";
                case StatType.EnergyShield: return "에너지 보호막";
                case StatType.EnergyShieldPct: return "보호막";
                case StatType.EvasionPct: return "회피";
                case StatType.BlockChancePct: return "막기 확률";
                case StatType.AllResistPct: return "모든 저항";
                case StatType.EliteDamageReductionPct: return "정예 피해 감소";
                case StatType.MaxResource: return "최대 자원";
                case StatType.ResourceRegen: return "자원 재생";
                case StatType.ResourceCostReductionPct: return "자원 소모 감소";
                case StatType.WeaponDamage: return "무기 공격력";
                case StatType.AttacksPerSecond: return "초당 공격";
                case StatType.FlatDamage: return "추가 피해";
                case StatType.AttackSpeedPct: return "공격 속도";
                case StatType.CastSpeedPct: return "시전 속도";
                case StatType.CritChancePct: return "치명타 확률";
                case StatType.CritDamagePct: return "치명타 피해";
                case StatType.LifeOnHit: return "적중 시 회복";
                case StatType.AllDamagePct: return "피해";
                case StatType.MeleeDamagePct: return "근접 피해";
                case StatType.ProjectileDamagePct: return "투사체 피해";
                case StatType.SpellDamagePct: return "주문 피해";
                case StatType.ElementalDamagePct: return "원소 피해";
                case StatType.PhysicalDamagePct: return "물리 피해";
                case StatType.BossDamagePct: return "보스 피해";
                case StatType.AreaPct: return "범위";
                case StatType.CooldownReductionPct: return "쿨타임 감소";
                case StatType.ExtraProjectiles: return "투사체";
                case StatType.ExtraChains: return "연쇄";
                case StatType.ExtraPierce: return "관통";
                case StatType.MoveSpeedPct: return "이동 속도";
                case StatType.FireDamagePct: return "불 피해";
                case StatType.ColdDamagePct: return "얼음 피해";
                case StatType.LightningDamagePct: return "번개 피해";
                case StatType.EarthDamagePct: return "대지 피해";
                case StatType.LightDamagePct: return "빛 피해";
                case StatType.DarkDamagePct: return "어둠 피해";
                case StatType.FireResistPct: return "불 저항";
                case StatType.ColdResistPct: return "얼음 저항";
                case StatType.LightningResistPct: return "번개 저항";
                case StatType.EarthResistPct: return "대지 저항";
                case StatType.LightResistPct: return "빛 저항";
                case StatType.DarkResistPct: return "어둠 저항";
                case StatType.GoldFindPct: return "골드 획득";
                case StatType.ExperiencePct: return "경험치";
                case StatType.PetPowerPct: return "펫 능력";
                case StatType.PetCooldownPct: return "펫 쿨타임 감소";
                default: return stat.ToString();
            }
        }
    }
}

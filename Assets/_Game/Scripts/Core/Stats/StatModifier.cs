namespace ShadowRPG.Core.Stats
{
    /// <summary>능력치 한 줄. Source는 장비·세트·직업 등 출처이며, 출처 단위로 일괄 제거합니다.</summary>
    public readonly struct StatModifier
    {
        public readonly StatType Stat;
        public readonly float Value;
        public readonly object Source;

        public StatModifier(StatType stat, float value, object source = null)
        {
            Stat = stat;
            Value = value;
            Source = source;
        }

        public StatModifier WithSource(object source) => new StatModifier(Stat, Value, source);

        public override string ToString() => $"{Stat} {Value:+0.##;-0.##}";
    }
}

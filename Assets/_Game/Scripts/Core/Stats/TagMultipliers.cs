using System.Collections.Generic;

namespace ShadowRPG.Core.Stats
{
    /// <summary>
    /// 특정 태그 조합을 모두 가진 스킬에 곱해지는 "최종 곱연산" 배율 (예: 4세트 효과, 전설 권능).
    /// </summary>
    public readonly struct TagMultiplier
    {
        public readonly SkillTag RequiredTags;
        public readonly float Multiplier;
        public readonly object Source;

        public TagMultiplier(SkillTag requiredTags, float multiplier, object source = null)
        {
            RequiredTags = requiredTags;
            Multiplier = multiplier;
            Source = source;
        }

        public TagMultiplier WithSource(object source) => new TagMultiplier(RequiredTags, Multiplier, source);
    }

    public sealed class TagMultiplierSet
    {
        readonly List<TagMultiplier> _items = new List<TagMultiplier>();

        public IReadOnlyList<TagMultiplier> Items => _items;

        public void Add(TagMultiplier multiplier) => _items.Add(multiplier);

        public void RemoveSource(object source) => _items.RemoveAll(m => Equals(m.Source, source));

        public float For(SkillTag tags)
        {
            float result = 1f;
            for (int i = 0; i < _items.Count; i++)
                if (tags.HasAll(_items[i].RequiredTags)) result *= _items[i].Multiplier;
            return result;
        }
    }
}

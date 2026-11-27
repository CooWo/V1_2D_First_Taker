using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowRPG.Runtime.Art
{
    /// <summary>
    /// 외형 키 → 스프라이트 묶음. 게임 규칙은 키(문자열)만 알고, 그림은 여기서 찾습니다.
    /// 키 규칙: "클래스/무기외형" (예: knight/2h_sword) 또는 몬스터 VisualKey (예: skeleton_minion).
    /// 정확한 키가 없으면 '/' 앞부분(클래스 기본 외형)으로, 그래도 없으면 기본값으로 떨어집니다.
    /// 아트 교체 = 이 카탈로그 항목의 묶음만 바꾸면 됩니다.
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowRPG/Art/Appearance Catalog", fileName = "AppearanceCatalog")]
    public sealed class AppearanceCatalog : ScriptableObject
    {
        public const string ResourcePath = "Art/AppearanceCatalog";

        [Serializable]
        public sealed class Entry
        {
            public string key;
            public DirectionalSpriteSet set;
            [Tooltip("게임 안 표시 배율")]
            public float scale = 1f;
            [Tooltip("스프라이트 색 곱하기")]
            public Color tint = Color.white;
        }

        public string fallbackKey = "knight";
        public List<Entry> entries = new List<Entry>();

        public bool TryGet(string key, out Entry entry)
        {
            entry = null;
            if (!string.IsNullOrEmpty(key))
            {
                entry = entries.Find(e => e.key == key && e.set != null);
                if (entry == null)
                {
                    int slash = key.IndexOf('/');
                    if (slash > 0)
                    {
                        string prefix = key.Substring(0, slash);
                        entry = entries.Find(e => e.key == prefix && e.set != null)
                                ?? entries.Find(e => e.key.StartsWith(prefix + "/", StringComparison.Ordinal) && e.set != null);
                    }
                }
            }
            return entry != null;
        }

        public Entry GetOrFallback(string key)
        {
            if (TryGet(key, out var entry)) return entry;
            TryGet(fallbackKey, out entry);
            return entry;
        }

        public void Set(string key, DirectionalSpriteSet set, float scale, Color tint)
        {
            var entry = entries.Find(e => e.key == key);
            if (entry == null) entries.Add(entry = new Entry { key = key });
            entry.set = set;
            entry.scale = scale;
            entry.tint = tint;
        }

        static AppearanceCatalog _loaded;
        public static AppearanceCatalog Load() => _loaded != null ? _loaded : _loaded = Resources.Load<AppearanceCatalog>(ResourcePath);
    }
}

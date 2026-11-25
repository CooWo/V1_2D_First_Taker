using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>
    /// 키 → 애니메이션 프레임 묶음. 아트 교체는 이 에셋만 바꾸면 됩니다 (코드는 키로만 찾습니다).
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowRPG/Sprite Library", fileName = "SpriteLibrary")]
    public sealed class SpriteLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string key;
            public Sprite[] frames = Array.Empty<Sprite>();
            public float framesPerSecond = 8f;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();
        [SerializeField] Sprite[] floorTiles = Array.Empty<Sprite>();
        [SerializeField] Sprite wallTile;

        Dictionary<string, Entry> _lookup;

        public IReadOnlyList<Sprite> FloorTiles => floorTiles;
        public Sprite WallTile => wallTile;

        public bool TryGet(string key, out Entry entry)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, Entry>();
                foreach (var e in entries)
                    if (e != null && !string.IsNullOrEmpty(e.key) && e.frames != null && e.frames.Length > 0)
                        _lookup[e.key] = e;
            }
            return _lookup.TryGetValue(key ?? string.Empty, out entry);
        }

#if UNITY_EDITOR
        public void EditorSet(List<Entry> newEntries, Sprite[] floors, Sprite wall)
        {
            entries = newEntries;
            floorTiles = floors;
            wallTile = wall;
            _lookup = null;
        }
#endif
    }
}

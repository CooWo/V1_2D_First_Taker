using UnityEngine;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>부드러운(안티앨리어싱) 원·고리 UI 그림 (조이스틱·리듬 원).</summary>
    public static class UiShapes
    {
        static Sprite _disc, _ring, _thinRing;

        public static Sprite Disc => _disc != null ? _disc : _disc = Make(256, r => Mathf.Clamp01((1f - r) * 128f));
        public static Sprite Ring => _ring != null ? _ring : _ring = Make(256, r => Mathf.Clamp01((0.06f - Mathf.Abs(r - 0.92f)) * 128f));
        public static Sprite ThinRing => _thinRing != null ? _thinRing : _thinRing = Make(256, r => Mathf.Clamp01((0.03f - Mathf.Abs(r - 0.95f)) * 128f));

        static Sprite Make(int size, System.Func<float, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha(Mathf.Sqrt(dx * dx + dy * dy)) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}

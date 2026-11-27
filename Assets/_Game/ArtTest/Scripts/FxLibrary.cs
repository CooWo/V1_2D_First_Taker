using UnityEngine;

namespace ShadowRPG.ArtTest
{
    /// <summary>
    /// 시험용 이펙트·바닥 텍스처를 코드로 생성합니다 (정식 이펙트는 손그림/AI 2D 프레임으로 교체 예정).
    /// 모두 흰색 기반이라 SpriteRenderer.color로 속성 색을 입힙니다.
    /// </summary>
    public static class FxLibrary
    {
        static Sprite[] _slash;
        static Sprite _spark, _glow, _ring, _puff, _shadow, _floor;

        static bool Alive(Object o) => o != null;

        /// <summary>초승달 베기 6프레임 (휘두르며 펼쳐지고 꼬리가 사라짐).</summary>
        public static Sprite[] Slash
        {
            get
            {
                if (_slash != null && Alive(_slash[0])) return _slash;
                const int size = 256, frames = 6;
                var tex = NewTexture(size * frames, size);
                var pixels = new Color32[tex.width * tex.height];
                for (int f = 0; f < frames; f++)
                {
                    float progress = (f + 1f) / frames;          // 휘두른 정도
                    float fade = f >= 3 ? 1f - (f - 2f) / 4f : 1f; // 끝무렵 사라짐
                    for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; // -180..180, 0 = 오른쪽
                        float start = -75f, end = Mathf.Lerp(-75f, 75f, progress);
                        if (angle < start || angle > end) continue;
                        float along = Mathf.InverseLerp(start, end, angle);        // 0 꼬리 → 1 칼끝
                        float thickness = Mathf.Lerp(0.04f, 0.2f, along);
                        float band = 1f - Mathf.Abs(r - 0.72f) / thickness;
                        if (band <= 0f) continue;
                        float core = Mathf.Pow(band, 0.6f) * Mathf.Pow(along, 0.7f) * fade;
                        float hot = Mathf.Pow(band, 4f);
                        byte a = (byte)(Mathf.Clamp01(core) * 255f);
                        byte w = (byte)(Mathf.Lerp(150f, 255f, hot));
                        pixels[y * tex.width + f * size + x] = new Color32(w, w, 255, a);
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                _slash = new Sprite[frames];
                for (int f = 0; f < frames; f++)
                    _slash[f] = Sprite.Create(tex, new Rect(f * size, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _slash;
            }
        }

        public static Sprite Spark => Alive(_spark) ? _spark : _spark = Radial(128, (dx, dy, r) =>
        {
            float rays = Mathf.Max(Mathf.Exp(-Mathf.Abs(dx) * 40f), Mathf.Exp(-Mathf.Abs(dy) * 40f)) * Mathf.Clamp01(1f - r);
            float diag = Mathf.Max(Mathf.Exp(-Mathf.Abs(dx - dy) * 30f), Mathf.Exp(-Mathf.Abs(dx + dy) * 30f)) * Mathf.Clamp01(0.6f - r) * 0.8f;
            return Mathf.Clamp01(rays + diag + Mathf.Exp(-r * 9f));
        });

        public static Sprite Glow => Alive(_glow) ? _glow : _glow = Radial(128, (dx, dy, r) => Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f));

        public static Sprite Ring => Alive(_ring) ? _ring : _ring = Radial(256, (dx, dy, r) => Mathf.Clamp01(1f - Mathf.Abs(r - 0.85f) * 9f) * Mathf.Clamp01(r * 1.5f));

        public static Sprite Puff => Alive(_puff) ? _puff : _puff = Radial(128, (dx, dy, r) =>
            Mathf.Clamp01((1f - r) * 1.6f) * (0.55f + 0.45f * Mathf.PerlinNoise(dx * 4f + 10f, dy * 4f + 3f)));

        public static Sprite GroundShadow => Alive(_shadow) ? _shadow : _shadow = Radial(64, (dx, dy, r) => Mathf.Clamp01((1f - r) * 1.8f) * 0.7f);

        /// <summary>어두운 돌바닥 타일 (불규칙 판석 + 틈 + 얼룩).</summary>
        public static Sprite Floor
        {
            get
            {
                if (Alive(_floor)) return _floor;
                const int size = 256;
                var tex = NewTexture(size, size);
                tex.wrapMode = TextureWrapMode.Repeat;
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // 판석: 행마다 어긋난 벽돌식 배치
                    int row = y / 64;
                    int offset = row % 2 == 0 ? 0 : 48;
                    int bx = (x + offset) % 96, by = y % 64;
                    float edge = Mathf.Min(Mathf.Min(bx, 96 - bx), Mathf.Min(by, 64 - by));
                    float groove = Mathf.Clamp01(edge / 3f);
                    float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.6f + Mathf.PerlinNoise(x * 0.2f + 7f, y * 0.2f + 3f) * 0.4f;
                    float stoneTone = 0.75f + 0.25f * Mathf.PerlinNoise((x + offset) / 96 * 3.1f + row, row * 1.7f);
                    float v = (0.2f + 0.12f * n) * stoneTone * Mathf.Lerp(0.35f, 1f, groove);
                    pixels[y * size + x] = new Color(v * 0.9f, v * 0.92f, v * 1.12f, 1f);
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                _floor = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect);
                return _floor;
            }
        }

        delegate float Shape(float dx, float dy, float r);

        static Sprite Radial(int size, Shape shape)
        {
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(shape(dx, dy, r)) * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 2f);
        }

        static Texture2D NewTexture(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
    }
}

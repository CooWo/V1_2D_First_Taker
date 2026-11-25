using System.Collections.Generic;
using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>
    /// 코드로 그리는 도트 스프라이트 (그림자 주인공 + 이펙트 기본 도형).
    /// 정식 아트가 준비되면 SpriteLibrary의 "shadow_hero" 키로 교체됩니다.
    /// </summary>
    public static class PixelArtFactory
    {
        const int PixelsPerUnit = 16;

        static readonly Dictionary<char, Color32> HeroPalette = new Dictionary<char, Color32>
        {
            { 'K', new Color32(9, 11, 18, 255) },
            { 'S', new Color32(22, 27, 40, 255) },
            { 'W', new Color32(30, 38, 60, 230) },
            { 'T', new Color32(40, 47, 64, 255) },
            { 't', new Color32(58, 66, 86, 255) },
            { 'B', new Color32(24, 28, 40, 255) },
            { 'b', new Color32(110, 116, 132, 255) },
            { 'A', new Color32(86, 92, 108, 255) },
            { 'P', new Color32(26, 31, 46, 255) },
            { 'O', new Color32(62, 48, 44, 255) },
            { 'E', new Color32(63, 169, 255, 255) },
            { 'e', new Color32(190, 232, 255, 255) }
        };

        // 16 x 24, 위에서 아래로. 검은 아지랑이 머리 + 푸른 안광 + 무채색 초보자 의상.
        static readonly string[] HeroBody =
        {
            "......K..K......",
            "...K.KK.KK.K....",
            "..KKKSKKKSKK.K..",
            ".KKSKKKKKKKSKK..",
            "..KKKKKKKKKKKK..",
            ".KKKKKKKKKKKKKK.",
            "..KKeEKKKKEeKK..",
            "..KKKEKKKKEKKK..",
            "...KKKKKKKKKK...",
            "...SKKKKKKKKS...",
            "..AtTTTTTTTTtA..",
            ".AATTTTTTTTTTAA.",
            ".AATtTTTTTTtTAA.",
            ".AA.TTTTTTTT.AA.",
            ".KA.BBBbbBBB.AK.",
            "....TTTTTTTT....",
        };

        static readonly string[] LegsIdle =
        {
            "....TTT..TTT....",
            "....PPP..PPP....",
            "....PPP..PPP....",
            "....PPP..PPP....",
            "...OOOP..POOO...",
            "...OOOO..OOOO...",
            "...OOOO..OOOO...",
            "................",
        };

        static readonly string[] LegsRunA =
        {
            "....TTT..TTT....",
            "...PPP....PPP...",
            "..PPP......PPP..",
            "..PPP......PPP..",
            ".OOOP.......OOO.",
            ".OOOO.......OOO.",
            "................",
            "................",
        };

        static readonly string[] LegsRunB =
        {
            "....TTT.TTT.....",
            ".....PPPPP......",
            ".....PPPPP......",
            "......PPP.......",
            ".....OOOOO......",
            ".....OOOOO......",
            "................",
            "................",
        };

        static readonly string[] WispAlt =
        {
            ".....K...K..K...",
            "..K.KK.KKK.K....",
        };

        static Sprite[] _heroIdle;
        static Sprite[] _heroRun;
        static readonly Dictionary<string, Sprite> Shapes = new Dictionary<string, Sprite>();

        // 도메인 리로드가 꺼진 에디터에서는 이전 Play 세션의 스프라이트가 파괴된 채 남으므로 매번 유효성을 확인합니다.
        public static Sprite[] HeroIdle => IsAlive(_heroIdle) ? _heroIdle : (_heroIdle = new[] { Hero(LegsIdle, false), Hero(LegsIdle, true) });
        public static Sprite[] HeroRun => IsAlive(_heroRun) ? _heroRun : (_heroRun = new[] { Hero(LegsRunA, false), Hero(LegsRunB, true), Hero(LegsRunA, true), Hero(LegsRunB, false) });

        static bool IsAlive(Sprite[] sprites) => sprites != null && sprites.Length > 0 && sprites[0] != null;

        static Sprite Hero(string[] legs, bool altWisp)
        {
            var rows = new List<string>(HeroBody);
            if (altWisp)
            {
                rows[0] = WispAlt[0];
                rows[1] = WispAlt[1];
            }
            rows.AddRange(legs);
            return FromRows(rows, HeroPalette, new Vector2(0.5f, 0f));
        }

        static Sprite FromRows(IReadOnlyList<string> rows, Dictionary<char, Color32> palette, Vector2 pivot)
        {
            const int width = 16;
            int height = rows.Count;
            var texture = NewTexture(width, height);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                string row = rows[height - 1 - y];
                for (int x = 0; x < width; x++)
                {
                    char c = x < row.Length ? row[x] : '.';
                    pixels[y * width + x] = palette.TryGetValue(c, out var color) ? color : new Color32(0, 0, 0, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerUnit);
        }

        // ---------- 이펙트 도형 ----------

        /// <summary>지름 1유닛 원 (부드러운 가장자리).</summary>
        public static Sprite Circle => Shape("circle", 32, (dx, dy, r) => Mathf.Clamp01((1f - r) * 6f));

        /// <summary>지름 1유닛 고리.</summary>
        public static Sprite Ring => Shape("ring", 64, (dx, dy, r) => Mathf.Clamp01(1f - Mathf.Abs(r - 0.88f) * 14f));

        /// <summary>1유닛 정사각형.</summary>
        public static Sprite Square => Shape("square", 4, (dx, dy, r) => 1f);

        /// <summary>마름모 (투사체·파편).</summary>
        public static Sprite Diamond => Shape("diamond", 16, (dx, dy, r) => Mathf.Abs(dx) + Mathf.Abs(dy) <= 1f ? 1f : 0f);

        /// <summary>초승달 베기 궤적.</summary>
        public static Sprite Arc => Shape("arc", 48, (dx, dy, r) =>
        {
            float outer = Mathf.Clamp01((1f - r) * 8f);
            float inner = Mathf.Clamp01((Vector2.Distance(new Vector2(dx, dy), new Vector2(-0.35f, 0f)) - 0.75f) * 8f);
            return dx > -0.2f ? outer * inner : 0f;
        });

        /// <summary>바닥 그림자 타원.</summary>
        public static Sprite Shadow => Shape("shadow", 32, (dx, dy, r) => Mathf.Clamp01((1f - r) * 2f) * 0.6f);

        delegate float AlphaFunction(float dx, float dy, float radius);

        static Sprite Shape(string key, int size, AlphaFunction alpha)
        {
            if (Shapes.TryGetValue(key, out var cached) && cached != null) return cached;

            var texture = NewTexture(size, size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(Mathf.Clamp01(alpha(dx, dy, r)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            Shapes[key] = sprite;
            return sprite;
        }

        static Texture2D NewTexture(int width, int height) => new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
    }
}

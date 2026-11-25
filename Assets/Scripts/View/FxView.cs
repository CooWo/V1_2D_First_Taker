using System.Collections.Generic;
using UnityEngine;

namespace LootHunters
{
    /// <summary>
    /// 타격감 담당. DungeonRun.OnFx 신호를 받아
    /// 데미지 숫자, 처치 파편, 스킬 고리, 화면 흔들림, 효과음을 만듭니다.
    /// 효과음은 코드로 합성하므로 오디오 파일이 없어도 동작합니다.
    /// </summary>
    [RequireComponent(typeof(DungeonRun))]
    public class FxView : MonoBehaviour
    {
        [SerializeField] bool playSound = true;
        [Range(0f, 1f)] [SerializeField] float volume = 0.35f;
        [SerializeField] bool screenShake = true;

        static readonly Color White  = new Color(1f, 1f, 1f);
        static readonly Color Yellow = new Color(1f, 0.85f, 0.2f);
        static readonly Color Red    = new Color(1f, 0.35f, 0.35f);
        static readonly Color Green  = new Color(0.45f, 1f, 0.5f);
        static readonly Color Cyan   = new Color(0.45f, 0.85f, 1f);
        static readonly Color Gold   = new Color(1f, 0.78f, 0.25f);
        static readonly Color Orange = new Color(1f, 0.55f, 0.2f);

        class Floater
        {
            public Transform t;
            public TextMesh text, shadow;
            public Vector3 vel;
            public float life, maxLife, size;
            public Color color;
        }

        class Particle
        {
            public Transform t;
            public SpriteRenderer sr;
            public Vector3 vel;
            public float life, maxLife, startScale, endScale;
            public Color color;
        }

        DungeonRun run;
        DungeonView view;
        Font font;
        AudioSource audioSrc;
        Transform root;
        Transform guardRing;
        int builtVersion = -1;
        float lastHitSound;
        readonly List<Floater> floaters = new List<Floater>();
        readonly List<Particle> particles = new List<Particle>();
        Dictionary<FxKind, AudioClip> clips;

        static Sprite dotSprite, ringSprite;

        void Awake()
        {
            run = GetComponent<DungeonRun>();
            view = GetComponent<DungeonView>();
            run.OnFx += OnFx;

            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "AppleGothic", "Noto Sans CJK KR", "Arial" }, 32);

            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0f;

            MakeSprites();
            clips = Sfx.Build();
        }

        void OnDestroy()
        {
            if (run != null) run.OnFx -= OnFx;
        }

        // ───────── 신호 처리 ─────────

        void OnFx(FxEvent e)
        {
            PlaySound(e.kind);
            if (run.State == RunState.Town || run.Map == null) return;
            EnsureRoot();

            var up = Vector2.up;
            switch (e.kind)
            {
                case FxKind.MonsterHit:
                    Float(e.pos, e.value.ToString(), White, 1f, 0.6f);
                    Burst(e.pos, White, 3, 2f, 0.06f);
                    break;

                case FxKind.Crit:
                    Float(e.pos, $"{e.value}!", Yellow, 1.5f, 0.8f);
                    Burst(e.pos, Yellow, 7, 3f, 0.08f);
                    Shake(0.12f);
                    break;

                case FxKind.HeroHit:
                    Float(e.pos, $"-{e.value}", Red, 1.1f, 0.7f);
                    Burst(e.pos, Red, 4, 2f, 0.06f);
                    Shake(0.16f);
                    break;

                case FxKind.Shielded:
                    Float(e.pos, "막음", Cyan, 0.9f, 0.6f);
                    break;

                case FxKind.Heal:
                    Float(e.pos + up * 0.3f, $"+{e.value}", Green, 1.2f, 0.9f);
                    Ring(e.pos, Green, 1.2f, 0.4f);
                    break;

                case FxKind.Kill:
                    Burst(e.pos, Gold, 12, 4f, 0.1f);
                    Ring(e.pos, Gold, 0.9f, 0.25f);
                    if (e.value > 0) Float(e.pos + up * 0.4f, $"+{e.value}G", Gold, 0.95f, 0.9f);
                    break;

                case FxKind.LevelUp:
                    Float(e.pos + up * 0.9f, e.text, Gold, 1.6f, 1.8f);
                    Ring(e.pos, Gold, 3f, 0.6f);
                    Burst(e.pos, Gold, 20, 5f, 0.12f);
                    Shake(0.15f);
                    break;

                case FxKind.Skill:
                    Float(e.pos + up * 0.9f, e.text, Orange, 1.2f, 0.9f);
                    switch ((SkillId)e.value)
                    {
                        case SkillId.Whirlwind: Ring(e.pos, Orange, 2f, 0.3f); Shake(0.1f); break;
                        case SkillId.Dash:      Burst(e.pos, Cyan, 10, 4f, 0.1f); Shake(0.12f); break;
                        case SkillId.Guard:     Ring(e.pos, Cyan, 1.4f, 0.4f); break;
                        case SkillId.Frenzy:    Ring(e.pos, Red, 1.6f, 0.4f); Burst(e.pos, Red, 10, 3f, 0.08f); break;
                    }
                    break;

                case FxKind.FloorClear:
                    // 마지막 몹 처치: 큰 글씨 + 고리 + 파편 + 강한 흔들림 + 진동 (슬로우 모션은 DungeonRun이 처리)
                    Float(e.pos + up * 1.2f, e.text, Gold, 2.2f, 0.7f);
                    Ring(e.pos, Gold, 4f, 0.5f);
                    Burst(e.pos, Gold, 30, 6f, 0.14f);
                    Shake(0.35f);
                    Vibrate();
                    break;

                case FxKind.Alert:
                    Float(e.pos + up * 0.55f, "!", Yellow, 1.3f, 0.6f);
                    break;
            }
        }

        // ───────── 매 프레임 ─────────

        void LateUpdate()
        {
            if (run.MapVersion != builtVersion) ClearAll();

            float dt = Time.deltaTime;

            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                f.life += dt;
                float k = f.life / f.maxLife;
                if (k >= 1f) { Destroy(f.t.gameObject); floaters.RemoveAt(i); continue; }

                f.t.position += f.vel * dt;
                f.vel *= Mathf.Exp(-4f * dt);
                float pop = f.life < 0.12f ? 1f + 0.6f * (1f - f.life / 0.12f) : 1f;
                f.t.localScale = Vector3.one * pop;
                float alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                var c = f.color; c.a = alpha;
                f.text.color = c;
                f.shadow.color = new Color(0f, 0f, 0f, alpha * 0.7f);
            }

            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.life += dt;
                float k = p.life / p.maxLife;
                if (k >= 1f) { Destroy(p.t.gameObject); particles.RemoveAt(i); continue; }

                p.t.position += p.vel * dt;
                p.vel *= Mathf.Exp(-5f * dt);
                p.t.localScale = Vector3.one * Mathf.Lerp(p.startScale, p.endScale, k);
                var c = p.color; c.a = 1f - k;
                p.sr.color = c;
            }

            UpdateGuardRing();
        }

        /// <summary>방어막이 켜져 있는 동안 영웅 주위에 파란 고리.</summary>
        void UpdateGuardRing()
        {
            bool show = run.State != RunState.Town && run.Map != null && run.GuardActive;
            if (!show)
            {
                if (guardRing != null) guardRing.gameObject.SetActive(false);
                return;
            }
            EnsureRoot();
            if (guardRing == null)
            {
                var sr = NewSprite("GuardRing", ringSprite, 555);
                guardRing = sr.transform;
            }
            guardRing.gameObject.SetActive(true);
            guardRing.position = new Vector3(run.HeroPos.x, run.HeroPos.y, 0f);
            float pulse = 1.1f + 0.08f * Mathf.Sin(Time.time * 8f);
            guardRing.localScale = Vector3.one * pulse;
            guardRing.GetComponent<SpriteRenderer>().color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f);
        }

        // ───────── 만들기 도우미 ─────────

        void Float(Vector2 pos, string s, Color color, float size, float life)
        {
            var go = new GameObject("FxText");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(pos.x + Random.Range(-0.15f, 0.15f), pos.y + 0.35f, 0f);

            var shadow = MakeText(go.transform, s, size, 559);
            shadow.transform.localPosition = new Vector3(0.03f, -0.03f, 0f);
            var text = MakeText(go.transform, s, size, 560);

            floaters.Add(new Floater
            {
                t = go.transform, text = text, shadow = shadow,
                vel = new Vector3(0f, 2.2f, 0f), maxLife = life, size = size, color = color,
            });
        }

        TextMesh MakeText(Transform parent, string s, float size, int order)
        {
            var go = new GameObject("T");
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = s;
            tm.fontSize = 48;
            tm.characterSize = 0.09f * size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = order;
            return tm;
        }

        void Burst(Vector2 pos, Color color, int count, float speed, float size)
        {
            for (int i = 0; i < count; i++)
            {
                var sr = NewSprite("Spark", dotSprite, 550);
                sr.transform.position = new Vector3(pos.x, pos.y, 0f);
                var dir = Random.insideUnitCircle.normalized * Random.Range(0.4f, 1f) * speed;
                particles.Add(new Particle
                {
                    t = sr.transform, sr = sr, vel = dir, color = color,
                    maxLife = Random.Range(0.25f, 0.45f), startScale = size, endScale = 0f,
                });
            }
        }

        void Ring(Vector2 pos, Color color, float radius, float life)
        {
            var sr = NewSprite("Ring", ringSprite, 549);
            sr.transform.position = new Vector3(pos.x, pos.y, 0f);
            particles.Add(new Particle
            {
                t = sr.transform, sr = sr, vel = Vector3.zero, color = color,
                maxLife = life, startScale = 0.2f, endScale = radius * 2f,
            });
        }

        SpriteRenderer NewSprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        void Shake(float s)
        {
            if (view == null) view = GetComponent<DungeonView>();
            if (screenShake && view != null) view.Shake(s);
        }

        [SerializeField] bool vibration = true;

        /// <summary>휴대폰 진동 (안드로이드 / iOS에서만, 에디터와 PC에서는 아무 일도 없음).</summary>
        void Vibrate()
        {
            if (!vibration) return;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        void PlaySound(FxKind kind)
        {
            if (!playSound || clips == null || !clips.TryGetValue(kind, out var clip)) return;
            // 같은 프레임에 타격음이 몰리면 귀가 아프니 간격을 둠
            if (kind == FxKind.MonsterHit || kind == FxKind.Alert)
            {
                if (Time.unscaledTime - lastHitSound < 0.05f) return;
                lastHitSound = Time.unscaledTime;
            }
            audioSrc.pitch = Random.Range(0.92f, 1.08f);
            audioSrc.PlayOneShot(clip, volume);
        }

        void EnsureRoot()
        {
            if (root == null) root = new GameObject("FX").transform;
        }

        void ClearAll()
        {
            builtVersion = run.MapVersion;
            if (root != null) Destroy(root.gameObject);
            root = null;
            guardRing = null;
            floaters.Clear();
            particles.Clear();
        }

        static void MakeSprites()
        {
            if (dotSprite != null) return;

            var dot = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    dot.SetPixel(x, y, Color.white);
            dot.Apply();
            dotSprite = Sprite.Create(dot, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);   // 1유닛

            const int n = 64;
            var ring = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    float dx = x - (n - 1) / 2f, dy = y - (n - 1) / 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / (n / 2f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.1f);
                    ring.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            ring.Apply();
            ringSprite = Sprite.Create(ring, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);   // 지름 1유닛
        }
    }

    /// <summary>코드로 만드는 간단한 8비트풍 효과음.</summary>
    static class Sfx
    {
        const int Rate = 44100;

        public static Dictionary<FxKind, AudioClip> Build()
        {
            var coin = Tone("coin", 0.16f, 900f, 1500f, 0f, 0.6f, 14f);
            return new Dictionary<FxKind, AudioClip>
            {
                { FxKind.MonsterHit, Tone("hit",    0.07f, 260f, 110f, 0.55f, 0f,   45f) },
                { FxKind.Crit,       Tone("crit",   0.14f, 520f, 160f, 0.45f, 0.5f, 22f) },
                { FxKind.HeroHit,    Tone("hurt",   0.12f, 170f,  70f, 0.6f,  0.3f, 25f) },
                { FxKind.Shielded,   Tone("block",  0.10f, 950f, 700f, 0f,    0f,   30f) },
                { FxKind.Heal,       Tone("heal",   0.30f, 500f, 950f, 0f,    0f,    8f) },
                { FxKind.Kill,       coin },
                { FxKind.Gold,       coin },
                { FxKind.LevelUp,    Arpeggio("levelup", new[] { 523f, 659f, 784f, 1047f }, 0.09f) },
                { FxKind.Skill,      Tone("skill",  0.25f, 180f, 640f, 0.35f, 0.2f, 10f) },
                { FxKind.Alert,      Tone("alert",  0.06f, 1100f, 1100f, 0f,  1f,   40f) },
                { FxKind.Stairs,     Tone("stairs", 0.35f, 320f, 140f, 0.1f,  0.4f,  6f) },
                { FxKind.FloorClear, Arpeggio("clear", new[] { 392f, 523f, 659f, 784f, 1047f }, 0.12f) },
            };
        }

        /// <summary>주파수가 f0→f1로 미끄러지는 음. noise=잡음 비율, square=사각파 비율, decay=감쇠 속도.</summary>
        static AudioClip Tone(string name, float dur, float f0, float f1, float noise, float square, float decay)
        {
            int len = Mathf.CeilToInt(dur * Rate);
            var data = new float[len];
            var rnd = new System.Random(name.GetHashCode());
            double phase = 0;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float f = Mathf.Lerp(f0, f1, t / dur);
                phase += 2.0 * System.Math.PI * f / Rate;
                float sine = (float)System.Math.Sin(phase);
                float sq = sine >= 0f ? 1f : -1f;
                float tone = Mathf.Lerp(sine, sq, square);
                float nz = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float env = Mathf.Exp(-t * decay) * Mathf.Min(1f, t * 400f);
                data[i] = Mathf.Lerp(tone, nz, noise) * env * 0.6f;
            }
            var clip = AudioClip.Create(name, len, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Arpeggio(string name, float[] notes, float noteDur)
        {
            int per = Mathf.CeilToInt(noteDur * Rate);
            var data = new float[per * notes.Length];
            for (int n = 0; n < notes.Length; n++)
            {
                double phase = 0;
                for (int i = 0; i < per; i++)
                {
                    float t = (float)i / Rate;
                    phase += 2.0 * System.Math.PI * notes[n] / Rate;
                    float sq = System.Math.Sin(phase) >= 0 ? 1f : -1f;
                    float env = Mathf.Exp(-t * 10f) * Mathf.Min(1f, t * 400f);
                    data[n * per + i] = sq * env * 0.35f;
                }
            }
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

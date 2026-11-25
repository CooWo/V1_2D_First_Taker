using System.Collections.Generic;
using UnityEngine;

namespace LootHunters
{
    /// <summary>
    /// 2D 탑뷰 화면. DungeonRun의 맵/영웅/몬스터를 스프라이트로 그리고 카메라가 영웅을 따라갑니다.
    /// DungeonSprites를 넣으면 픽셀아트로, 비워 두면 색 도형으로 표시합니다.
    /// </summary>
    [RequireComponent(typeof(DungeonRun))]
    public class DungeonView : MonoBehaviour
    {
        [SerializeField] DungeonSprites sprites;
        [Tooltip("화면 가로에 보이는 칸 수 (작을수록 확대). 세로 모바일 화면 기준.")]
        [SerializeField] float tilesAcross = 10f;
        [Tooltip("가로 화면(에디터 등)에서 너무 확대되지 않게 하는 최소 세로 절반 칸 수")]
        [SerializeField] float minHalfHeight = 5f;
        [Tooltip("왼쪽 UI 패널이 차지하는 화면 비율. 맵은 그 오른쪽에 그립니다.")]
        [SerializeField] float cameraFollowSpeed = 10f;

        static readonly Color FloorColor  = new Color(0.32f, 0.29f, 0.26f);
        static readonly Color WallColor   = new Color(0.16f, 0.15f, 0.20f);
        static readonly Color StairsColor = new Color(0.95f, 0.80f, 0.30f);
        static readonly Color HeroColor   = new Color(0.35f, 0.75f, 1.00f);
        const float Dim = 0.35f;   // 가 봤지만 지금 안 보이는 곳 밝기

        class TileView { public SpriteRenderer sr; public Vector2Int pos; public Color baseColor; }
        class ActorView
        {
            public Transform root;
            public SpriteRenderer body;
            public SpriteRenderer shadow;
            public SpriteRenderer barBg, barSr;
            public DungeonSprites.Anim anim;   // null이면 한 장짜리 그림
            public float phase;                // 몬스터마다 애니메이션이 어긋나게
            public float moveTimer;            // 최근에 움직였는지
            public float barY = 0.55f;
            public Transform barFill;
            public Color baseColor;
            public int lastHp;
            public float flash;
            public Vector2 lastPos;
        }

        DungeonRun run;
        Camera cam;
        Transform mapRoot;
        readonly List<TileView> tiles = new List<TileView>();
        ActorView hero;
        readonly Dictionary<MapMonster, ActorView> monsters = new Dictionary<MapMonster, ActorView>();
        readonly List<MapMonster> staleViews = new List<MapMonster>();
        int builtVersion = -1;
        Vector3 camBase = new Vector3(0f, 0f, -10f);
        float shake;
        Vector2Int lastLightTile = new Vector2Int(-999, -999);

        static Sprite squareSprite, circleSprite, pixelSprite;

        void Awake()
        {
            run = GetComponent<DungeonRun>();
            MakeFallbackSprites();
            SetupCamera();
            hero = CreateActor("Hero", sprites != null ? sprites.hero : null, HeroColor, 10, sprites != null ? sprites.heroAnim : null);
            hero.root.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (run.MapVersion != builtVersion) Rebuild();

            bool inDungeon = run.State != RunState.Town && run.Map != null;
            hero.root.gameObject.SetActive(inDungeon);
            // 세로 모바일: 카메라는 화면 전체, UI는 그 위에 겹쳐 그림
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            if (!inDungeon) return;

            // 배율: 화면 가로에 tilesAcross칸이 보이게 (세로 화면 기준). 가로 화면에서는 너무 확대되지 않게 최소값.
            float size = Mathf.Max(minHalfHeight, tilesAcross / (2f * Mathf.Max(0.1f, cam.aspect)));
            cam.orthographicSize = size;

            // 위아래 UI에 가려지지 않는 가운데 영역의 중앙에 영웅이 오도록 카메라를 내림
            float visibleCenter = (MobileLayout.BottomFraction + (1f - MobileLayout.TopFraction)) * 0.5f;
            float yOffset = (visibleCenter - 0.5f) * 2f * size;

            // 카메라 따라가기 + 흔들림
            var target = new Vector3(run.HeroPos.x, run.HeroPos.y - yOffset, -10f);
            camBase = Vector3.Lerp(camBase, target, 1f - Mathf.Exp(-cameraFollowSpeed * Time.deltaTime));
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.5f);
            var offset = shake > 0f ? (Vector3)(Random.insideUnitCircle * shake) : Vector3.zero;
            cam.transform.position = camBase + offset;

            UpdateLighting();
            UpdateActor(hero, run.HeroPos, run.Hero.Combatant, true);

            // 귀환 시전 중이면 파랗게 깜빡임
            if (run.State == RunState.Returning)
                hero.body.color = Color.Lerp(hero.body.color, new Color(0.5f, 0.8f, 1f), 0.5f + 0.5f * Mathf.Sin(Time.time * 12f));

            // 몬스터
            foreach (var m in run.Monsters)
            {
                if (m.IsDead)
                {
                    if (monsters.TryGetValue(m, out var dead)) { Destroy(dead.root.gameObject); monsters.Remove(m); }
                    continue;
                }
                if (!monsters.TryGetValue(m, out var v))
                {
                    var sprite = sprites != null ? sprites.FindMonster(m.Data) : null;
                    v = CreateActor(m.Data.displayName, sprite, m.Data.tint, 9, sprites != null ? sprites.FindMonsterAnim(m.Data) : null);
                    v.root.SetParent(mapRoot, false);
                    v.root.position = m.Pos;
                    v.lastPos = m.Pos;
                    monsters[m] = v;
                }
                float camDist = Vector2.Distance(m.Pos, run.HeroPos);
                bool visible = camDist <= run.SightRadius && (camDist < 2.5f || run.Map.LineClear(m.Pos, run.HeroPos));
                v.root.gameObject.SetActive(visible);
                if (visible) UpdateActor(v, m.Pos, m.Combatant, false);
            }

            // 새 무리가 나올 때 목록에서 빠진 죽은 몬스터의 그림 정리
            if (monsters.Count > run.Monsters.Count)
            {
                staleViews.Clear();
                foreach (var kv in monsters)
                    if (!run.Monsters.Contains(kv.Key)) staleViews.Add(kv.Key);
                foreach (var key in staleViews)
                {
                    Destroy(monsters[key].root.gameObject);
                    monsters.Remove(key);
                }
            }
        }

        // ───────── 맵 다시 그리기 ─────────

        void Rebuild()
        {
            builtVersion = run.MapVersion;
            if (mapRoot != null) Destroy(mapRoot.gameObject);
            tiles.Clear();
            monsters.Clear();
            lastLightTile = new Vector2Int(-999, -999);

            var map = run.Map;
            if (map == null) return;

            mapRoot = new GameObject("DungeonMap").transform;
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    var t = map.Get(x, y);
                    if (t == Tile.Wall)
                    {
                        if (!TouchesFloor(map, x, y)) continue;   // 바닥과 닿은 벽만 그림
                        AddTile(x, y, WallSprite(map, x, y), WallColor, 0);
                        continue;
                    }
                    AddTile(x, y, FloorSprite(x, y), FloorColor, 0);
                    if (t == Tile.Stairs)
                        AddTile(x, y, sprites != null ? sprites.stairs : null, StairsColor, 1);
                }
            }

            hero.root.position = run.HeroPos;
            hero.lastPos = run.HeroPos;
            camBase = new Vector3(run.HeroPos.x, run.HeroPos.y, -10f);
            cam.transform.position = camBase;
        }

        /// <summary>화면 흔들림 (월드 단위 세기). 여러 번 불리면 가장 센 값이 남습니다.</summary>
        public void Shake(float strength) => shake = Mathf.Max(shake, strength);

        /// <summary>바닥: 대부분 기본 바닥, 가끔 금 간 바닥.</summary>
        Sprite FloorSprite(int x, int y)
        {
            if (sprites == null) return null;
            int h = DungeonSprites.Hash(x, y);
            if (sprites.floorRare != null && sprites.floorRare.Length > 0 && h % 100 < 6)
                return sprites.floorRare[(h / 100) % sprites.floorRare.Length];
            return DungeonSprites.Pick(sprites.floor, x, y);
        }

        /// <summary>벽: 아래가 바닥이면 벽돌 앞면(가끔 장식), 아니면 윗면.</summary>
        Sprite WallSprite(DungeonMap map, int x, int y)
        {
            if (sprites == null) return null;
            bool front = map.IsWalkable(x, y - 1);
            if (front && sprites.wallFront != null)
            {
                int h = DungeonSprites.Hash(x, y);
                var v = sprites.wallFrontVariants;
                if (v != null && v.Length > 0 && h % 100 < 12) return v[(h / 100) % v.Length];
                return sprites.wallFront;
            }
            if (!front && sprites.wallTop != null) return sprites.wallTop;
            return DungeonSprites.Pick(sprites.wall, x, y);
        }

        static bool TouchesFloor(DungeonMap map, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (map.IsWalkable(x + dx, y + dy)) return true;
            return false;
        }

        void AddTile(int x, int y, Sprite sprite, Color fallbackColor, int order)
        {
            var go = new GameObject($"T{x}_{y}");
            go.transform.SetParent(mapRoot, false);
            go.transform.position = new Vector3(x, y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            Color baseColor;
            if (sprite != null) { sr.sprite = sprite; baseColor = Color.white; }
            else { sr.sprite = order == 1 ? circleSprite : squareSprite; baseColor = fallbackColor; }
            FitToTile(go.transform, sr.sprite, order == 1 && sprite == null ? 0.6f : 1f);
            sr.color = Color.black;
            tiles.Add(new TileView { sr = sr, pos = new Vector2Int(x, y), baseColor = baseColor });
        }

        /// <summary>영웅이 다른 칸으로 옮겼을 때만 밝기 갱신: 보이는 곳 / 가 본 곳(어둡게) / 안 가 본 곳(검정).</summary>
        void UpdateLighting()
        {
            var ht = DungeonMap.TileOf(run.HeroPos);
            if (ht == lastLightTile) return;
            lastLightTile = ht;

            var map = run.Map;
            float r = run.SightRadius;
            foreach (var t in tiles)
            {
                bool explored = ExploredNear(map, t.pos);
                if (!explored) { t.sr.enabled = false; continue; }
                t.sr.enabled = true;
                bool visible = ((Vector2)t.pos - run.HeroPos).sqrMagnitude <= r * r;
                t.sr.color = visible ? t.baseColor : t.baseColor * new Color(Dim, Dim, Dim, 1f);
            }
        }

        /// <summary>벽은 자기 칸이 아니라 옆 바닥이 밝혀졌으면 보이게.</summary>
        static bool ExploredNear(DungeonMap map, Vector2Int p)
        {
            if (map.Explored[p.x, p.y]) return true;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int x = p.x + dx, y = p.y + dy;
                    if (map.InBounds(x, y) && map.Explored[x, y] && map.IsWalkable(x, y)) return true;
                }
            return false;
        }

        // ───────── 영웅 / 몬스터 ─────────

        // 정렬 순서: 바닥/벽 0, 그림자 5, 캐릭터 100~300 (아래쪽일수록 앞), 체력바 400, 연출(FxView) 500 이상
        const float FeetOffset = -0.38f;   // 몸 중심에서 발까지

        ActorView CreateActor(string name, Sprite sprite, Color fallbackColor, int order, DungeonSprites.Anim anim = null)
        {
            var root = new GameObject(name).transform;

            // 발밑 그림자
            var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(root, false);
            shadow.sprite = circleSprite;
            shadow.color = new Color(0f, 0f, 0f, 0.38f);
            shadow.sortingOrder = 5;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(root, false);
            var body = bodyGo.AddComponent<SpriteRenderer>();
            body.sortingOrder = order;
            Color baseColor = Color.white;
            float barY = 0.55f;
            float shadowW = 0.7f;
            if (anim != null && anim.IsValid)
            {
                // 픽셀아트 원래 크기 (16픽셀 = 1칸), 발이 몸 아래쪽에 오게
                body.sprite = anim.idle[0];
                bodyGo.transform.localPosition = new Vector3(0f, FeetOffset, 0f);
                bodyGo.transform.localScale = Vector3.one * anim.scale;
                float h = anim.idle[0].bounds.size.y * anim.scale;
                shadowW = Mathf.Clamp(anim.idle[0].bounds.size.x * anim.scale * 0.75f, 0.5f, 1.6f);
                barY = FeetOffset + h + 0.08f;
            }
            else if (sprite != null) { body.sprite = sprite; FitToTile(bodyGo.transform, sprite, 0.9f); }
            else { body.sprite = circleSprite; baseColor = fallbackColor; FitToTile(bodyGo.transform, circleSprite, 0.7f); }
            body.color = baseColor;
            shadow.transform.localPosition = new Vector3(0f, FeetOffset + 0.04f, 0f);
            FitToTile(shadow.transform, circleSprite, 1f);
            shadow.transform.localScale = new Vector3(shadow.transform.localScale.x * shadowW, shadow.transform.localScale.y * 0.3f, 1f);

            // 머리 위 체력바
            var barBg = new GameObject("BarBg").AddComponent<SpriteRenderer>();
            barBg.transform.SetParent(root, false);
            barBg.sprite = pixelSprite;
            barBg.color = new Color(0f, 0f, 0f, 0.7f);
            barBg.sortingOrder = 400;
            barBg.transform.localPosition = new Vector3(0f, barY, 0f);
            barBg.transform.localScale = new Vector3(0.8f, 0.1f, 1f);

            var fill = new GameObject("BarFill").AddComponent<SpriteRenderer>();
            fill.transform.SetParent(root, false);
            fill.sprite = pixelSprite;
            fill.color = order == 10 ? new Color(0.3f, 0.85f, 0.4f) : new Color(0.9f, 0.25f, 0.25f);
            fill.sortingOrder = 401;
            fill.transform.localPosition = new Vector3(0f, barY, 0f);
            fill.transform.localScale = new Vector3(0.76f, 0.07f, 1f);

            return new ActorView
            {
                root = root, body = body, shadow = shadow, barBg = barBg, barSr = fill,
                barFill = fill.transform, baseColor = baseColor, lastHp = -1,
                anim = anim != null && anim.IsValid ? anim : null,
                phase = Random.value * 4f, barY = barY,
            };
        }

        void UpdateActor(ActorView v, Vector2 pos, Combatant c, bool isHero)
        {
            v.root.position = new Vector3(pos.x, pos.y, 0f);

            // 왼쪽으로 가면 좌우 반전
            float dx = pos.x - v.lastPos.x;
            if (Mathf.Abs(dx) > 0.001f) v.body.flipX = dx < 0f;
            float moved = (pos - v.lastPos).sqrMagnitude;
            v.lastPos = pos;
            v.moveTimer = moved > 0.000004f ? 0.12f : Mathf.Max(0f, v.moveTimer - Time.deltaTime);

            // 아래쪽에 있는 캐릭터가 앞에 그려지게
            v.body.sortingOrder = 100 + Mathf.Clamp(Mathf.RoundToInt((80f - pos.y) * 2f), 0, 199) + (isHero ? 1 : 0);

            // 걷기 / 서 있기 애니메이션 (초당 8프레임)
            if (v.anim != null)
            {
                var frames = v.moveTimer > 0f && v.anim.run != null && v.anim.run.Length > 0 ? v.anim.run : v.anim.idle;
                int f = Mathf.FloorToInt(Time.time * 8f + v.phase) % frames.Length;
                if (frames[f] != null) v.body.sprite = frames[f];
            }

            // 맞으면 빨갛게 번쩍
            if (v.lastHp >= 0 && c.CurrentHp < v.lastHp) v.flash = 1f;
            v.lastHp = c.CurrentHp;
            v.flash = Mathf.MoveTowards(v.flash, 0f, Time.deltaTime * 6f);
            v.body.color = Color.Lerp(v.baseColor, new Color(1f, 0.2f, 0.2f), v.flash);

            // 체력바 (왼쪽 기준으로 줄어듦)
            float ratio = c.Stats.maxHp > 0 ? Mathf.Clamp01((float)c.CurrentHp / c.Stats.maxHp) : 0f;
            v.barFill.localScale = new Vector3(0.76f * ratio, 0.07f, 1f);
            v.barFill.localPosition = new Vector3(-0.38f * (1f - ratio), v.barY, 0f);
            bool showBar = isHero || ratio < 1f;
            if (v.barBg.enabled != showBar) { v.barBg.enabled = showBar; v.barSr.enabled = showBar; }
        }

        // ───────── 카메라 / 기본 도형 ─────────

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = minHalfHeight;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        /// <summary>스프라이트 크기와 상관없이 1칸(×scale)에 맞춤. PPU 설정이 달라도 동작합니다.</summary>
        static void FitToTile(Transform t, Sprite s, float scale)
        {
            if (s == null) return;
            var size = s.bounds.size;
            if (size.x <= 0f || size.y <= 0f) return;
            t.localScale = new Vector3(scale / size.x, scale / size.y, 1f);
        }

        static void MakeFallbackSprites()
        {
            if (squareSprite != null) return;
            const int n = 16;

            var sq = NewTexture(n);
            var ci = NewTexture(n);
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    bool edge = x == 0 || y == 0 || x == n - 1 || y == n - 1;
                    float shade = edge ? 0.8f : 1f - ((x * 7 + y * 13) % 5) * 0.03f;
                    sq.SetPixel(x, y, new Color(shade, shade, shade, 1f));

                    float dx = x - (n - 1) / 2f, dy = y - (n - 1) / 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    ci.SetPixel(x, y, d <= n / 2f - 0.5f ? (d > n / 2f - 2f ? new Color(0.75f, 0.75f, 0.75f, 1f) : Color.white) : Color.clear);
                }
            }
            sq.Apply();
            ci.Apply();

            var px = NewTexture(1);
            px.SetPixel(0, 0, Color.white);
            px.Apply();

            squareSprite = Sprite.Create(sq, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            circleSprite = Sprite.Create(ci, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            pixelSprite = Sprite.Create(px, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }

        static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShadowRPG.ArtTest
{
    /// <summary>
    /// 아트 시험 장면: "어두운 Hades식 사전 렌더 캐릭터 + 테일즈위버식 타격 임팩트"를 한 화면에서 확인합니다.
    /// 주인공이 자동으로 적을 베고, 6초마다 3연격 콤보(마지막 일격에 충격파·화면 번쩍임)를 씁니다.
    /// </summary>
    public sealed class ArtTestDemo : MonoBehaviour
    {
        [SerializeField] DirectionalSpriteSet hero;
        [SerializeField] DirectionalSpriteSet brute;
        [SerializeField] Material additiveMaterial;
        [SerializeField] Material flashMaterial;
        [SerializeField] int enemyCount = 6;
        [SerializeField] float comboInterval = 6f;

        static readonly Color SlashColor = new Color(0.55f, 0.78f, 1f);
        static readonly Color ComboColor = new Color(0.72f, 0.45f, 1f);

        sealed class Unit
        {
            public GameObject Root;
            public DirectionalSpriteAnimator Anim;
            public Vector2 Position;
            public Vector2 Knock;
            public float Life;
            public float MaxLife;
            public bool Dead;
            public float AttackCooldown;
            public float RespawnAt;
            public float FadeTimer;
        }

        readonly List<Unit> _enemies = new List<Unit>();
        Unit _hero;
        Camera _camera;
        HitFeel _feel;
        FxPlayer _fx;
        DamageNumbers _numbers;
        Light2D _heroLight;
        Light2D _impactLight;
        readonly List<(Light2D light, float baseIntensity, float seed)> _torches = new List<(Light2D, float, float)>();
        Material _bodyMaterial;
        Material _alphaMaterial;
        float _comboTimer;
        int _comboStep;
        int _hitStack;
        float _hitStackTimer;

#if UNITY_EDITOR
        public void EditorAssign(DirectionalSpriteSet heroSet, DirectionalSpriteSet bruteSet, Material additive, Material flash)
        {
            hero = heroSet;
            brute = bruteSet;
            additiveMaterial = additive;
            flashMaterial = flash;
        }
#endif

        void Start()
        {
            Application.targetFrameRate = 60;
            _camera = Camera.main;
            _camera.orthographic = true;
            _camera.orthographicSize = 4.3f;
            _camera.backgroundColor = new Color(0.01f, 0.01f, 0.02f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.transform.position = new Vector3(0f, 0.6f, -10f);

            var probe = new GameObject("MaterialProbe").AddComponent<SpriteRenderer>();
            _bodyMaterial = probe.sharedMaterial;
            Destroy(probe.gameObject);
            _alphaMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));

            BuildPostProcessing();
            BuildLights();
            BuildFloor();
            BuildUi();

            _fx = new GameObject("Fx").AddComponent<FxPlayer>();
            _fx.Initialize(additiveMaterial, _alphaMaterial);

            _hero = CreateUnit(hero, "Hero", Vector2.zero, 999f);
            for (int i = 0; i < enemyCount; i++) _enemies.Add(CreateUnit(brute, "Brute", SpawnPoint(i), 100f));
            _comboTimer = 2.5f;
        }

        // ---------------- 장면 구성 ----------------

        void BuildPostProcessing()
        {
            var data = _camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;

            var volume = new GameObject("PostProcess").AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.5f);
            bloom.scatter.Override(0.7f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.45f);
            vignette.smoothness.Override(0.55f);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(-0.1f);
            color.contrast.Override(18f);
            color.saturation.Override(-15f);
            color.colorFilter.Override(new Color(0.9f, 0.93f, 1f));
            volume.profile = profile;
        }

        void BuildLights()
        {
            var global = new GameObject("GlobalLight").AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 0.6f;
            global.color = new Color(0.55f, 0.6f, 0.85f);

            _heroLight = Point("HeroLight", new Color(0.35f, 0.6f, 1f), 3.6f, 0.9f);
            _impactLight = Point("ImpactLight", new Color(0.7f, 0.8f, 1f), 3f, 0f);

            var torchPositions = new[] { new Vector2(-6f, 3.5f), new Vector2(6f, 3.5f), new Vector2(-6f, -3f), new Vector2(6f, -3f) };
            for (int i = 0; i < torchPositions.Length; i++)
            {
                var torch = Point("Torch" + i, new Color(1f, 0.55f, 0.22f), 5.5f, 1.1f);
                torch.transform.position = torchPositions[i];
                _torches.Add((torch, 1.1f, i * 13.7f));
            }
        }

        static Light2D Point(string name, Color color, float radius, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = radius * 0.1f;
            light.intensity = intensity;
            light.falloffIntensity = 0.65f;
            return light;
        }

        void BuildFloor()
        {
            var floor = new GameObject("Floor").AddComponent<SpriteRenderer>();
            floor.sprite = FxLibrary.Floor;
            floor.drawMode = SpriteDrawMode.Tiled;
            floor.size = new Vector2(34f, 22f);
            floor.sortingOrder = -30000;
        }

        void BuildUi()
        {
            var canvasObject = new GameObject("Overlay", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasObject.transform;

            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Arial" }, 32);

            var numbersRoot = Stretch(root, "Numbers");
            _numbers = numbersRoot.gameObject.AddComponent<DamageNumbers>();
            _numbers.Initialize(numbersRoot, _camera, font);

            var flash = Stretch(root, "Flash").gameObject.AddComponent<UnityEngine.UI.Image>();
            flash.color = Color.clear;
            flash.raycastTarget = false;

            var caption = new GameObject("Caption", typeof(RectTransform));
            caption.transform.SetParent(root, false);
            var rt = (RectTransform)caption.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -20f);
            rt.sizeDelta = new Vector2(1200f, 120f);
            var text = caption.AddComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.fontSize = 26;
            text.color = new Color(0.75f, 0.8f, 0.95f);
            text.text = "아트 시험 장면 — 3D→8방향 2D 사전 렌더 + 어두운 톤 + 타격 연출\n<size=20>임시 모델: Quaternius RPG Characters (CC0). 6초마다 3연격 콤보</size>";

            _feel = gameObject.AddComponent<HitFeel>();
            _feel.Initialize(_camera, flash);
        }

        static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        Unit CreateUnit(DirectionalSpriteSet set, string name, Vector2 position, float life)
        {
            var root = new GameObject(name);
            var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(root.transform, false);
            shadow.sprite = FxLibrary.GroundShadow;
            shadow.sharedMaterial = _alphaMaterial;
            shadow.color = new Color(0f, 0f, 0f, 0.75f);
            shadow.transform.localScale = new Vector3(1.2f, 0.45f, 1f);
            shadow.sortingOrder = -20000;

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<SpriteRenderer>();
            var anim = body.AddComponent<DirectionalSpriteAnimator>();
            anim.Initialize(set, _bodyMaterial, flashMaterial);
            anim.Face(Vector2.down);
            anim.Play("idle");

            var unit = new Unit { Root = root, Anim = anim, Position = position, Life = life, MaxLife = life };
            Sync(unit);
            return unit;
        }

        Vector2 SpawnPoint(int index)
        {
            float angle = index * (360f / Mathf.Max(1, enemyCount)) + Random.Range(-15f, 15f);
            return new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad) * 6.5f, Mathf.Sin(angle * Mathf.Deg2Rad) * 4f);
        }

        // ---------------- 진행 ----------------

        void Update()
        {
            float dt = Time.deltaTime;
            _hitStackTimer -= Time.unscaledDeltaTime;
            if (_hitStackTimer <= 0f) _hitStack = 0;

            UpdateHero(dt);
            foreach (var enemy in _enemies) UpdateEnemy(enemy, dt);

            _heroLight.transform.position = _hero.Position + Vector2.up * 0.9f;
            _impactLight.intensity = Mathf.MoveTowards(_impactLight.intensity, 0f, Time.unscaledDeltaTime * 9f);
            foreach (var (light, baseIntensity, seed) in _torches)
                light.intensity = baseIntensity * (0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 3f, seed));
        }

        void UpdateHero(float dt)
        {
            Sync(_hero);
            _comboTimer -= dt;
            bool busy = _hero.Anim.IsPlaying && (_hero.Anim.CurrentClip == "attack" || _hero.Anim.CurrentClip == "attack2");
            if (busy) return;

            var target = Nearest(_hero.Position);
            if (target == null)
            {
                _hero.Anim.Play("idle");
                return;
            }

            var toTarget = target.Position - _hero.Position;
            _hero.Anim.Face(toTarget);
            if (toTarget.magnitude > 1.45f)
            {
                _hero.Position += toTarget.normalized * 3.6f * dt;
                _hero.Anim.Speed = 1f;
                _hero.Anim.Play("run");
                return;
            }

            if (_comboStep > 0 || _comboTimer <= 0f) ComboAttack(toTarget);
            else NormalAttack(toTarget);
        }

        void NormalAttack(Vector2 direction)
        {
            _hero.Anim.Speed = 1.15f;
            string clip = Random.value < 0.5f ? "attack" : "attack2";
            _hero.Anim.Play(clip, () => Strike(direction, 1f, SlashColor, 1.65f, 130f, false), null, true);
        }

        /// <summary>3연격: 빠른 두 번 + 마지막 강타 (충격파·화면 번쩍임·긴 히트스톱).</summary>
        void ComboAttack(Vector2 direction)
        {
            _comboStep++;
            _hero.Anim.Speed = 1.7f;
            bool finisher = _comboStep >= 3;
            _hero.Anim.Play(finisher ? "attack2" : "attack", () =>
            {
                Strike(direction, finisher ? 2.2f : 1.3f, ComboColor, finisher ? 2.6f : 1.9f, finisher ? 360f : 150f, finisher);
            }, null, true);
            if (!finisher) return;
            _comboStep = 0;
            _comboTimer = comboInterval;
        }

        void Strike(Vector2 direction, float power, Color color, float radius, float arc, bool finisher)
        {
            var origin = _hero.Position;
            _fx.Slash(origin, direction, color, finisher ? 1.6f : power > 1f ? 1.25f : 1f, Random.value < 0.5f);
            if (finisher)
            {
                _fx.Shockwave(origin, color, radius);
                _feel.Flash(new Color(0.75f, 0.6f, 1f), 0.35f);
            }

            bool hitAny = false;
            foreach (var enemy in _enemies)
            {
                if (enemy.Dead) continue;
                var to = enemy.Position - origin;
                if (to.magnitude > radius || Vector2.Angle(direction, to) > arc * 0.5f) continue;
                hitAny = true;
                bool crit = finisher || Random.value < 0.2f;
                int damage = Mathf.RoundToInt(Random.Range(14f, 22f) * power * (crit ? 1.8f : 1f));
                Damage(enemy, damage, crit, to.normalized * (finisher ? 9f : 4.5f * power), color, power);
            }

            if (!hitAny) return;
            _feel.HitStop(finisher ? 0.13f : 0.045f * power);
            _feel.Shake(finisher ? 0.55f : 0.16f * power);
            _impactLight.transform.position = origin + direction.normalized + Vector2.up * 0.6f;
            _impactLight.color = color;
            _impactLight.intensity = finisher ? 3.2f : 1.6f;
        }

        void Damage(Unit enemy, int amount, bool crit, Vector2 knock, Color color, float power)
        {
            enemy.Life -= amount;
            enemy.Knock = knock;
            enemy.Anim.Flash(Color.white, 1f);
            _fx.Impact(enemy.Position, color, crit ? power * 1.4f : power);
            _hitStackTimer = 0.5f;
            _numbers.Show((Vector3)enemy.Position + Vector3.up * 1.6f, amount, crit, _hitStack++ % 4);

            if (enemy.Life > 0f)
            {
                enemy.Anim.Play("hit", null, () => enemy.Anim.Play("idle"), true);
                return;
            }

            enemy.Dead = true;
            enemy.RespawnAt = Time.time + 2.2f;
            enemy.FadeTimer = 0f;
            enemy.Anim.Play("death", null, null, true);
            _fx.Death(enemy.Position);
        }

        void UpdateEnemy(Unit enemy, float dt)
        {
            enemy.Position += enemy.Knock * dt;
            enemy.Knock = Vector2.MoveTowards(enemy.Knock, Vector2.zero, dt * 28f);

            if (enemy.Dead)
            {
                enemy.FadeTimer += dt;
                float alpha = Mathf.Clamp01(1.6f - enemy.FadeTimer);
                enemy.Anim.Body.color = new Color(1f, 1f, 1f, alpha);
                if (Time.time >= enemy.RespawnAt) Respawn(enemy);
                Sync(enemy);
                return;
            }

            Sync(enemy);
            if (enemy.Anim.IsPlaying && (enemy.Anim.CurrentClip == "hit" || enemy.Anim.CurrentClip == "attack")) return;

            var toHero = _hero.Position - enemy.Position;
            enemy.Anim.Face(toHero);
            enemy.AttackCooldown -= dt;
            if (toHero.magnitude > 1.25f)
            {
                enemy.Position += (toHero.normalized + Separation(enemy)) * 1.7f * dt;
                enemy.Anim.Play("run");
                return;
            }

            if (enemy.AttackCooldown > 0f)
            {
                enemy.Anim.Play("idle");
                return;
            }
            enemy.AttackCooldown = Random.Range(2f, 3f);
            enemy.Anim.Play("attack", () =>
            {
                _hero.Anim.Flash(new Color(1f, 0.25f, 0.2f), 0.8f);
                _feel.Shake(0.08f);
                _fx.Impact(_hero.Position, new Color(1f, 0.35f, 0.25f), 0.7f);
            }, () => enemy.Anim.Play("idle"), true);
        }

        Vector2 Separation(Unit self)
        {
            var push = Vector2.zero;
            foreach (var other in _enemies)
            {
                if (other == self || other.Dead) continue;
                var away = self.Position - other.Position;
                float d = away.magnitude;
                if (d < 0.9f && d > 0.001f) push += away / d * (0.9f - d) * 2f;
            }
            return push;
        }

        void Respawn(Unit enemy)
        {
            enemy.Dead = false;
            enemy.Life = enemy.MaxLife;
            enemy.Position = SpawnPoint(Random.Range(0, enemyCount));
            enemy.Knock = Vector2.zero;
            enemy.Anim.Body.color = Color.white;
            enemy.Anim.Speed = 1f;
            enemy.Anim.Play("idle", null, null, true);
        }

        Unit Nearest(Vector2 from)
        {
            Unit best = null;
            float bestDistance = float.MaxValue;
            foreach (var enemy in _enemies)
            {
                if (enemy.Dead) continue;
                float d = (enemy.Position - from).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = enemy;
            }
            return best;
        }

        static void Sync(Unit unit)
        {
            unit.Root.transform.position = unit.Position;
            unit.Anim.Body.sortingOrder = 10000 - Mathf.RoundToInt(unit.Position.y * 100f);
        }
    }
}

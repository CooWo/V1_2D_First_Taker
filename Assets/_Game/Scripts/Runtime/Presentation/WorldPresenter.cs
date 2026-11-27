using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>
    /// CombatWorld 이벤트를 구독해 전투원·투사체·장판·연출 뷰를 만들고 동기화합니다.
    /// 시뮬레이션 규칙은 전혀 알지 못합니다 (표현 전용).
    /// </summary>
    public sealed class WorldPresenter : MonoBehaviour
    {
        readonly Dictionary<Combatant, CombatantView> _combatants = new Dictionary<Combatant, CombatantView>();
        readonly List<CombatantView> _dying = new List<CombatantView>();
        readonly Dictionary<Projectile, ProjectileView> _projectiles = new Dictionary<Projectile, ProjectileView>();
        readonly Stack<ProjectileView> _projectilePool = new Stack<ProjectileView>();
        readonly Dictionary<Zone, ZoneView> _zones = new Dictionary<Zone, ZoneView>();
        readonly Dictionary<Hazard, HazardView> _hazards = new Dictionary<Hazard, HazardView>();

        CombatWorld _world;
        SpriteLibrary _library;
        VfxPlayer _vfx;
        IFloatingText _text;
        Combatant _player;

        public Transform PlayerTransform { get; private set; }

        /// <summary>전투원 → 외형 키 (장비에 따라 바뀜). SessionView가 정해 줍니다.</summary>
        public System.Func<Combatant, string> AppearanceKey { get; set; }

        FeelDirector _feel;

        public CombatantView ViewOf(Combatant c) => c != null && _combatants.TryGetValue(c, out var v) ? v : null;

        public void Initialize(CombatWorld world, Combatant player, SpriteLibrary library, IFloatingText text, System.Func<Combatant, string> appearanceKey, FeelDirector feel)
        {
            AppearanceKey = appearanceKey;
            _feel = feel;
            _world = world;
            _player = player;
            _library = library;
            _text = text;

            _vfx = new GameObject("Vfx").AddComponent<VfxPlayer>();
            _vfx.transform.SetParent(transform, false);
            _vfx.Initialize(text);

            world.CombatantAdded += OnCombatantAdded;
            world.CombatantRemoved += OnCombatantRemoved;
            world.ProjectileSpawned += OnProjectileSpawned;
            world.ProjectileRemoved += OnProjectileRemoved;
            world.ZoneSpawned += OnZoneSpawned;
            world.ZoneRemoved += OnZoneRemoved;
            world.VfxEmitted += _vfx.Play;
            world.Damage.Resolved += OnDamageResolved;
            world.HazardSpawned += OnHazardSpawned;
            world.HazardRemoved += OnHazardRemoved;

            foreach (var c in world.Combatants) OnCombatantAdded(c);
        }

        /// <summary>펫·포탈 같은 비전투 표식을 붙입니다.</summary>
        public void AddFollower(string name, string visualKey, Sprite[] fallback, System.Func<Vector2> position, System.Func<bool> visible, Color tint, float scale, float bob)
        {
            var frames = fallback;
            float fps = 6f;
            if (_library != null && _library.TryGet(visualKey, out var entry))
            {
                frames = entry.frames;
                fps = entry.framesPerSecond;
            }
            var view = new GameObject(name).AddComponent<FollowerView>();
            view.transform.SetParent(transform, false);
            view.Bind(frames, fps, position, visible, tint, scale, bob);
        }

        void OnHazardSpawned(Hazard hazard)
        {
            var view = new GameObject("Hazard_" + hazard.Label).AddComponent<HazardView>();
            view.transform.SetParent(transform, false);
            view.Bind(hazard);
            _hazards[hazard] = view;
        }

        void OnHazardRemoved(Hazard hazard)
        {
            if (!_hazards.TryGetValue(hazard, out var view)) return;
            _hazards.Remove(hazard);
            Destroy(view.gameObject);
            if (hazard.IsResolved) _vfx.Play(VfxEvent.At(VfxKind.Explosion, hazard.Center, hazard.Shape == HazardShape.Line ? 1.2f : hazard.Radius * 0.6f, hazard.Element, 0.3f));
        }

        void OnDestroy()
        {
            if (_world == null) return;
            _world.CombatantAdded -= OnCombatantAdded;
            _world.CombatantRemoved -= OnCombatantRemoved;
            _world.ProjectileSpawned -= OnProjectileSpawned;
            _world.ProjectileRemoved -= OnProjectileRemoved;
            _world.ZoneSpawned -= OnZoneSpawned;
            _world.ZoneRemoved -= OnZoneRemoved;
            _world.VfxEmitted -= _vfx.Play;
            _world.Damage.Resolved -= OnDamageResolved;
            _world.HazardSpawned -= OnHazardSpawned;
            _world.HazardRemoved -= OnHazardRemoved;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            foreach (var view in _combatants.Values) view.Sync(dt);

            for (int i = _dying.Count - 1; i >= 0; i--)
            {
                _dying[i].Sync(dt);
                if (!_dying[i].IsFinished) continue;
                Destroy(_dying[i].gameObject);
                _dying.RemoveAt(i);
            }

            foreach (var view in _projectiles.Values) view.Sync();
            foreach (var view in _zones.Values) view.Sync(dt);
            foreach (var view in _hazards.Values) view.Sync();
        }

        void OnCombatantAdded(Combatant combatant)
        {
            if (_combatants.ContainsKey(combatant)) return;

            var view = new GameObject(combatant.Name).AddComponent<CombatantView>();
            view.transform.SetParent(transform, false);

            bool isPlayer = combatant == _player;
            view.Bind(combatant, AppearanceKey, !isPlayer, isPlayer);
            _combatants[combatant] = view;
            if (isPlayer) PlayerTransform = view.transform;
        }

        void OnCombatantRemoved(Combatant combatant)
        {
            if (!_combatants.TryGetValue(combatant, out var view)) return;
            _combatants.Remove(combatant);
            _dying.Add(view);
        }

        void OnProjectileSpawned(Projectile projectile)
        {
            var view = _projectilePool.Count > 0 ? _projectilePool.Pop() : CreateProjectileView();
            view.gameObject.SetActive(true);
            view.Bind(projectile);
            _projectiles[projectile] = view;
        }

        ProjectileView CreateProjectileView()
        {
            var go = new GameObject("Projectile");
            go.transform.SetParent(transform, false);
            return go.AddComponent<ProjectileView>();
        }

        void OnProjectileRemoved(Projectile projectile)
        {
            if (!_projectiles.TryGetValue(projectile, out var view)) return;
            _projectiles.Remove(projectile);
            view.gameObject.SetActive(false);
            _projectilePool.Push(view);
        }

        void OnZoneSpawned(Zone zone)
        {
            var view = new GameObject("Zone_" + zone.VisualKey).AddComponent<ZoneView>();
            view.transform.SetParent(transform, false);
            view.Bind(zone);
            _zones[zone] = view;
        }

        void OnZoneRemoved(Zone zone)
        {
            if (!_zones.TryGetValue(zone, out var view)) return;
            _zones.Remove(zone);
            Destroy(view.gameObject);
        }

        void OnDamageResolved(DamageResult result) => _feel?.OnDamage(result);
    }
}

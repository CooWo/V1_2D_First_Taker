using ShadowRPG.Core.Game;
using ShadowRPG.Core.Raid;
using ShadowRPG.Runtime.Presentation;
using ShadowRPG.Runtime.UI;
using ShadowRPG.Runtime.UI.Menus;
using ShadowRPG.Runtime.UI.Modes;
using UnityEngine;

namespace ShadowRPG.Runtime
{
    /// <summary>
    /// 전투 세션 하나의 화면 묶음 (바닥, 월드 표시, HUD, 펫·포탈 표식). 세션이 바뀌면 통째로 버리고 새로 만듭니다.
    /// </summary>
    public sealed class SessionView : MonoBehaviour
    {
        ArenaView _arena;
        SpriteLibrary _library;
        int _seed;
        int _arenaAct = -1;

        public void Build(ICombatSession session, GameMeta meta, SpriteLibrary library, Camera camera, IModeSwitcher modes, int seed)
        {
            _library = library;
            _seed = seed;

            var dungeon = session as DungeonSession;
            BuildArena(session, dungeon);

            var ctx = new MenuContext { Meta = meta, Session = session, Modes = modes, Log = _ => { } };
            var hud = new GameObject("Hud").AddComponent<HudView>();
            hud.transform.SetParent(transform, false);
            hud.Initialize(session, ctx, camera, (root, c) => CreateModule(root, session, c), dungeon != null);

            var presenter = new GameObject("World").AddComponent<WorldPresenter>();
            presenter.transform.SetParent(transform, false);
            presenter.Initialize(session.World, session.Player.Combatant, library, hud.FloatingText);

            var pet = session.Pet;
            if (pet != null)
                presenter.AddFollower("Pet", pet.Definition.VisualKey, new[] { PixelArtFactory.Circle }, () => pet.Position, null, Color.white, 0.85f, 0.08f);

            if (dungeon != null)
            {
                presenter.AddFollower("Portal", "portal", new[] { PixelArtFactory.Ring }, () => dungeon.PortalPosition,
                    () => dungeon.State == FloorState.Portal, new Color(0.7f, 0.85f, 1f), 1.8f, 0f);
                dungeon.FloorStarted += _ => BuildArena(session, dungeon);
            }

            session.Player.MoveInput = new CompositeMoveInput(hud.Joystick, new KeyboardMoveInput());

            if (!camera.TryGetComponent<CameraFollow>(out var follow)) follow = camera.gameObject.AddComponent<CameraFollow>();
            follow.SetTarget(presenter.PlayerTransform);
        }

        static IHudModule CreateModule(Transform root, ICombatSession session, MenuContext ctx)
        {
            if (session is DungeonSession dungeon) return new DungeonHud(root, dungeon, ctx);
            if (session is RaidSession raid) return new RaidHud(root, raid, ctx);
            return null;
        }

        void BuildArena(ICombatSession session, DungeonSession dungeon)
        {
            int act = dungeon != null ? dungeon.Plan.ActIndex : -2;
            if (_arena != null && act == _arenaAct) return;
            if (_arena != null) Destroy(_arena.gameObject);

            _arenaAct = act;
            var tint = dungeon != null ? dungeon.Act.FloorTint : new Color(0.75f, 0.45f, 0.45f);
            _arena = new GameObject("Arena").AddComponent<ArenaView>();
            _arena.transform.SetParent(transform, false);
            _arena.Build(session.World.Bounds, _library, _seed + act, tint);
        }
    }
}

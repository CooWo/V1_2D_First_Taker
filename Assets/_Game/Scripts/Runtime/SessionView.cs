using System.Collections.Generic;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Raid;
using ShadowRPG.Core.Town;
using ShadowRPG.Runtime.Presentation;
using ShadowRPG.Runtime.UI;
using ShadowRPG.Runtime.UI.Menus;
using ShadowRPG.Runtime.UI.Modes;
using UnityEngine;

namespace ShadowRPG.Runtime
{
    /// <summary>
    /// 세션 하나의 화면 묶음 (지형, 월드 표시, HUD, 펫). 세션이 바뀌면 통째로 버리고 새로 만듭니다.
    /// 어떤 모드에 어떤 메뉴·HUD를 붙일지도 여기서 정합니다.
    /// </summary>
    public sealed class SessionView : MonoBehaviour
    {
        ArenaView _arena;
        SpriteLibrary _library;
        int _seed;

        public void Build(ICombatSession session, GameMeta meta, SpriteLibrary library, Camera camera, IModeSwitcher modes, int seed)
        {
            _library = library;
            _seed = seed;

            var ctx = new MenuContext { Meta = meta, Session = session, Modes = modes, Log = _ => { } };
            var hud = new GameObject("Hud").AddComponent<HudView>();
            hud.transform.SetParent(transform, false);
            hud.Initialize(session, ctx, camera, (root, c) => CreateModule(root, session, c), (root, c) => CreateMenus(root, session, c));

            var presenter = new GameObject("World").AddComponent<WorldPresenter>();
            presenter.transform.SetParent(transform, false);
            presenter.Initialize(session.World, session.Player.Combatant, library, hud.FloatingText);

            var pet = session.Pet;
            if (pet != null)
                presenter.AddFollower("Pet", pet.Definition.VisualKey, new[] { PixelArtFactory.Circle }, () => pet.Position, null, Color.white, 0.85f, 0.08f);

            BuildTerrain(session);
            if (session is DungeonSession dungeon) dungeon.FloorStarted += _ => BuildTerrain(session);

            session.Player.MoveInput = new CompositeMoveInput(hud.Joystick, new KeyboardMoveInput());

            if (!camera.TryGetComponent<CameraFollow>(out var follow)) follow = camera.gameObject.AddComponent<CameraFollow>();
            follow.SetTarget(presenter.PlayerTransform);
        }

        static IHudModule CreateModule(RectTransform root, ICombatSession session, MenuContext ctx)
        {
            switch (session)
            {
                case DungeonSession dungeon: return new DungeonHud(root, dungeon, ctx);
                case RaidSession raid: return new RaidHud(root, raid, ctx);
                case TownSession town: return new TownHud(root, town, ctx);
                default: return null;
            }
        }

        /// <summary>모드별 메뉴: 마을은 성장·보관 전부, 던전·레이드는 장비·가방만.</summary>
        static IEnumerable<IMenu> CreateMenus(RectTransform root, ICombatSession session, MenuContext ctx)
        {
            yield return new EquipmentMenu(root, ctx);
            yield return new BagMenu(root, ctx);
            if (!(session is TownSession)) yield break;
            yield return new StashMenu(root, ctx);
            yield return new TreeMenu(root, ctx);
            yield return new PetMenu(root, ctx);
            yield return new RaidLobbyMenu(root, ctx);
            yield return new DungeonSelectMenu(root, ctx);
        }

        void BuildTerrain(ICombatSession session)
        {
            if (_arena != null) Destroy(_arena.gameObject);
            _arena = new GameObject("Arena").AddComponent<ArenaView>();
            _arena.transform.SetParent(transform, false);

            switch (session)
            {
                case DungeonSession dungeon:
                    _arena.Build(dungeon.Layout.Map, _library, _seed + dungeon.Plan.Floor, dungeon.Act.FloorTint);
                    break;
                case TownSession town:
                    _arena.Build(town.Layout.Map, _library, _seed, new Color(0.7f, 0.72f, 0.8f), solidWalls: true);
                    break;
                default:
                    _arena.Build(session.World.Bounds, _library, _seed, new Color(0.75f, 0.45f, 0.45f));
                    break;
            }
        }
    }
}

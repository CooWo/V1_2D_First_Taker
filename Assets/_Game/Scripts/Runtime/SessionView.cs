using System.Collections.Generic;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Raid;
using ShadowRPG.Core.Town;
using ShadowRPG.Runtime.Presentation;
using ShadowRPG.Runtime.UI;
using ShadowRPG.Runtime.UI.Menus;
using ShadowRPG.Runtime.UI.Modes;
using ShadowRPG.Runtime.Art;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
        Camera _camera;
        DungeonMapView _map;

        public void Build(ICombatSession session, GameMeta meta, SpriteLibrary library, Camera camera, IModeSwitcher modes, int seed)
        {
            _library = library;
            _seed = seed;
            _camera = camera;
            var tuning = meta.Tuning.Presentation;

            var ctx = new MenuContext { Meta = meta, Session = session, Modes = modes, Log = _ => { } };
            var hud = new GameObject("Hud").AddComponent<HudView>();
            hud.transform.SetParent(transform, false);
            hud.Initialize(session, ctx, camera, (root, c) => CreateModule(root, session, c), (root, c) => CreateMenus(root, session, c));

            var feel = new GameObject("Feel").AddComponent<FeelDirector>();
            feel.transform.SetParent(transform, false);
            feel.ShakeEnabled = meta.Settings.ScreenShake;
            feel.NumbersEnabled = meta.Settings.DamageNumbers && tuning.ShowDamageNumbers;

            var presenter = new GameObject("World").AddComponent<WorldPresenter>();
            presenter.transform.SetParent(transform, false);
            var player = session.Player;
            presenter.Initialize(session.World, player.Combatant, library, hud.FloatingText,
                c => c == player.Combatant ? AppearanceResolver.ForPlayer(player) : c.VisualKey, feel);
            feel.Initialize(tuning, player.Combatant, hud.FloatingText, presenter.ViewOf, CreateFlashOverlay());

            var pet = session.Pet;
            if (pet != null)
                WispView.Create(presenter.transform, () => pet.Position, new Color(0.45f, 0.8f, 1f));

            BuildTerrain(session);
            if (session is DungeonSession dungeon) dungeon.FloorStarted += _ => BuildTerrain(session);

            session.Player.MoveInput = new ViewSpaceMoveInput(new CompositeMoveInput(hud.Joystick, new KeyboardMoveInput()));

            var rig = CameraRig.Ensure(camera, tuning);
            rig.Follow(presenter.PlayerTransform);
        }

        UnityEngine.UI.Image CreateFlashOverlay()
        {
            var canvas = UiFactory.CreateCanvas("ScreenFlash", 5);
            canvas.transform.SetParent(transform, false);
            var image = UiFactory.Panel(UiFactory.Stretch(canvas.transform, "Flash"), new Color(1f, 1f, 1f, 0f));
            image.enabled = false;
            return image;
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

        /// <summary>모드별 메뉴. 마을은 전부, 던전·레이드는 보기·정리 위주 (장비 교체·성장은 마을에서).</summary>
        static IEnumerable<IMenu> CreateMenus(RectTransform root, ICombatSession session, MenuContext ctx)
        {
            yield return new EquipmentMenu(root, ctx);
            yield return new BagMenu(root, ctx);
            yield return new StatsMenu(root, ctx);
            yield return new QuestMenu(root, ctx);
            yield return new SettingsMenu(root, ctx);
            if (!(session is TownSession)) yield break;
            yield return new StashMenu(root, ctx);
            yield return new PresetMenu(root, ctx);
            yield return new TrainingMenu(root, ctx);
            yield return new TreeMenu(root, ctx);
            yield return new PetMenu(root, ctx);
            yield return new RaidLobbyMenu(root, ctx);
            yield return new DungeonSelectMenu(root, ctx);
            yield return new AttendanceMenu(root, ctx);
            yield return new ShopMenu(root, ctx);
            foreach (var (label, text) in ComingSoon) yield return new ComingSoonMenu(root, label, text);
        }

        static readonly (string label, string text)[] ComingSoon =
        {
            ("PvP", "비동기 PvP: 다른 유저의 자동 PvP 세팅과 대전합니다.\n· 세팅 슬롯 하나를 PvP 세팅으로 지정 (세팅 슬롯 메뉴)\n· 매칭: 등급 + 점수 · 보상: 템포·꾸미기 · 시즌제\n<color=#8090A8>서버 연동 후 열립니다.</color>"),
            ("길드", "일반적인 모바일 길드 콘텐츠.\n<color=#8090A8>오프라인으로 먼저 구성한 뒤 범위를 정합니다 (열린 쟁점 N).</color>"),
            ("도감", "아이템(전설·세트·고유 권능) · 스킬 · 몬스터 · 보스 — 미획득도 미리보기.\n<color=#8090A8>수집 보상과 함께 준비 중.</color>"),
            ("업적", "누적 기록: 처치 수, ACT 개방, 전설 획득, 리듬 연속 성공, 도감 완성도.\n보상은 칭호·꾸미기 위주."),
            ("시즌 패스", "무료/유료 2단 보상 트랙 (시즌 미션 진행). 보상: 골드·템포·꾸미기."),
            ("꾸미기", "외형 · 스킬 이펙트 · 칭호. 장비 외형과 별도로 덧입힙니다."),
            ("친구", "친구 목록 · 레이드 초대.\n<color=#8090A8>서버 연동 후.</color>"),
            ("랭킹", "PvP · 도감 랭킹.\n<color=#8090A8>서버 연동 후.</color>"),
            ("우편함", "보상 · 공지 우편.\n<color=#8090A8>서버 연동 후.</color>"),
            ("공지", "공지 사항."),
            ("고객지원", "문의 · 약관.")
        };

        void BuildTerrain(ICombatSession session)
        {
            if (_arena != null) Destroy(_arena.gameObject);
            if (_map != null) Destroy(_map.gameObject);
            var catalog = EnvironmentCatalog.Load();

            EnvironmentTheme theme = null;
            Color tint = Color.white;
            Core.World.GridMap map = null;
            Vector2 start = session.Player.Combatant.Position;
            switch (session)
            {
                case DungeonSession dungeon:
                    theme = catalog != null ? catalog.ForAct(dungeon.Plan.ActIndex) : null;
                    tint = catalog != null ? catalog.TintForAct(dungeon.Plan.ActIndex) : Color.white;
                    map = dungeon.Layout.Map;
                    start = dungeon.Layout.StartPosition;
                    break;
                case TownSession town:
                    theme = catalog != null ? catalog.town : null;
                    map = town.Layout.Map;
                    break;
            }

            if (theme == null || map == null)
            {
                // 환경 묶음이 없거나 (굽기 전) 레이드처럼 벽 없는 경기장: 예전 타일맵
                _arena = new GameObject("Arena").AddComponent<ArenaView>();
                _arena.transform.SetParent(transform, false);
                if (map != null) _arena.Build(map, _library, _seed, new Color(0.55f, 0.58f, 0.7f));
                else _arena.Build(session.World.Bounds, _library, _seed, new Color(0.75f, 0.45f, 0.45f));
                return;
            }

            _map = new GameObject("Map").AddComponent<DungeonMapView>();
            _map.transform.SetParent(transform, false);
            _map.Build(map, theme, tint, _seed + session.World.GetHashCode(), start, session is TownSession);
            ApplyMood(theme, tint);
        }

        void ApplyMood(EnvironmentTheme theme, Color tint)
        {
            if (_camera != null) _camera.backgroundColor = theme.voidColor;
            foreach (var light in FindObjectsByType<Light2D>())
            {
                if (light.lightType != Light2D.LightType.Global) continue;
                light.color = theme.ambientColor * Color.Lerp(Color.white, tint, 0.5f);
                light.intensity = theme.ambientIntensity;
            }
        }
    }
}

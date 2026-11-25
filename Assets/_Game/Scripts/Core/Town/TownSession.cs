using System;
using System.Collections.Generic;
using ShadowRPG.Core.Actors;
using ShadowRPG.Core.Character;
using ShadowRPG.Core.Combat;
using ShadowRPG.Core.Dungeon;
using ShadowRPG.Core.Game;
using ShadowRPG.Core.Pets;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Town
{
    /// <summary>마을에 보이는 다른 캐릭터 한 명.</summary>
    public sealed class TownResident
    {
        public Combatant Combatant;
        public string DisplayName;
        public string Title;
        public Vector2 Target;
        public float IdleTimer;
    }

    /// <summary>
    /// 마을의 다른 캐릭터 공급원. 지금은 오프라인 시뮬레이션이고,
    /// 온라인이 붙으면 실제 접속자 위치를 받는 구현으로 교체합니다 (DIP).
    /// </summary>
    public interface ITownPresenceSource
    {
        IReadOnlyList<TownResident> CreateResidents(GameMeta meta, FloorLayout town, IRandom random);
    }

    public sealed class BotTownPresence : ITownPresenceSource
    {
        static readonly string[] Names = { "새벽그림자", "푸른안광", "잿빛기사", "밤의궁수", "심연술사", "폐지왕", "검은아지랑이", "별빛수도사", "고요한칼", "망각의등불" };
        static readonly (string visual, string title)[] Looks =
        {
            ("knight_m", "기사"), ("wizzard_f", "마법사"), ("elf_m", "궁수"), ("lizard_m", "수도사"),
            ("knight_f", "기사"), ("wizzard_m", "마법사"), ("elf_f", "궁수"), ("dwarf_m", "기사")
        };

        public IReadOnlyList<TownResident> CreateResidents(GameMeta meta, FloorLayout town, IRandom random)
        {
            var residents = new List<TownResident>();
            int count = 7;
            for (int i = 0; i < count; i++)
            {
                var look = Looks[i % Looks.Length];
                var combatant = new Combatant(Names[i % Names.Length], Team.Player)
                {
                    VisualKey = look.visual,
                    BaseMoveSpeed = 1.6f + random.Value() * 0.8f,
                    Position = TownLayout.RandomWalkable(town, random)
                };
                combatant.Stats.SetBase(StatType.MaxLife, 100f);
                combatant.Health.ResetToFull();
                int level = 1 + random.Range(0, 40);
                residents.Add(new TownResident
                {
                    Combatant = combatant,
                    DisplayName = combatant.Name,
                    Title = $"Lv.{level} {look.title}",
                    Target = combatant.Position,
                    IdleTimer = random.Range(0f, 3f)
                });
            }
            return residents;
        }
    }

    /// <summary>마을 지도: 가운데 광장(분수), 사방 길, 건물 블록.</summary>
    public static class TownLayout
    {
        public static FloorLayout Build()
        {
            const int width = 44, height = 36;
            var map = new GridMap(width, height, new Vector2(-width / 2f, -height / 2f));
            map.Fill(new RectInt(2, 2, width - 4, height - 4), true);

            // 건물 블록 (벽)
            foreach (var block in new[]
                     {
                         new RectInt(4, 4, 9, 7), new RectInt(width - 13, 4, 9, 7),
                         new RectInt(4, height - 11, 9, 7), new RectInt(width - 13, height - 11, 9, 7),
                         new RectInt(17, height - 8, 10, 4)
                     })
                map.Fill(block, false);

            // 광장 분수
            map.Fill(new RectInt(width / 2 - 2, height / 2 - 2, 4, 4), false);

            var plaza = new RectInt(width / 2 - 8, height / 2 - 6, 16, 12);
            return new FloorLayout
            {
                Name = "그림자 마을",
                Map = map,
                Rooms = { plaza },
                Start = new Vector2Int(width / 2, height / 2 - 5)
            };
        }

        public static Vector2 RandomWalkable(FloorLayout layout, IRandom random)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                var cell = new Vector2Int(random.Range(1, layout.Map.Width - 1), random.Range(1, layout.Map.Height - 1));
                if (layout.Map.IsWalkable(cell.x, cell.y)) return layout.Map.CenterOf(cell);
            }
            return layout.StartPosition;
        }
    }

    /// <summary>
    /// 마을: 전투 없는 허브. 다른 캐릭터들이 돌아다니고, 창고·트리·펫·강화·던전/레이드 입장을 여기서 합니다.
    /// </summary>
    public sealed class TownSession : ICombatSession
    {
        readonly IRandom _random;
        readonly List<TownResident> _residents = new List<TownResident>();

        public TownSession(GameMeta meta, BalanceSettings balance, IRandom random, ITownPresenceSource presence)
        {
            Meta = meta;
            _random = random;
            Layout = TownLayout.Build();

            World = CombatSetup.CreateWorld(balance, random, Layout.Map.Bounds);
            World.SetNavigation(new GridNavigator(Layout.Map, () => World.Time));

            Player = meta.CreatePlayer(World);
            Player.Combatant.Position = Layout.StartPosition;
            World.Add(Player.Combatant);
            Pet = meta.CreatePet(Player, World);
            Pet?.SnapTo(Layout.StartPosition);

            _residents.AddRange(presence.CreateResidents(meta, Layout, random));
            foreach (var r in _residents) World.Add(r.Combatant);
        }

        public GameMeta Meta { get; }
        public FloorLayout Layout { get; }
        public CombatWorld World { get; }
        public PlayerCharacter Player { get; }
        public PetCompanion Pet { get; }
        public IReadOnlyList<TownResident> Residents => _residents;
        public bool IsPlayerDead => false;
        public float RespawnRemaining => 0f;
        public float TimeScale => 1f;

        public event Action<string> Log;

        public void Say(string message) => Log?.Invoke(message);

        public void Tick(float dt)
        {
            dt = Mathf.Min(dt, 0.05f);
            Player.Tick(dt);
            Pet?.Tick(dt);
            foreach (var resident in _residents) Wander(resident, dt);
            World.Tick(dt);
        }

        void Wander(TownResident resident, float dt)
        {
            var c = resident.Combatant;
            if (resident.IdleTimer > 0f)
            {
                resident.IdleTimer -= dt;
                return;
            }
            if (Vector2.Distance(c.Position, resident.Target) < 0.5f)
            {
                resident.Target = TownLayout.RandomWalkable(Layout, _random);
                resident.IdleTimer = _random.Range(1.5f, 5f);
                return;
            }
            c.Move(World.SteerTowards(c.Position, resident.Target), dt);
        }

        public void Dispose() => Meta.Capture(Player);
    }
}

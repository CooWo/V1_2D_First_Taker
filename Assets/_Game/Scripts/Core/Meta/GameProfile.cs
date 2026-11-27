using System;
using System.Collections.Generic;
using ShadowRPG.Core.Dungeon;
using ShadowRPG.Core.Economy;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Pets;

namespace ShadowRPG.Core.Meta
{
    /// <summary>현재 시각 (일일 제한 계산용). 테스트에서 시간을 바꿔 끼울 수 있게 분리합니다.</summary>
    public interface IClock
    {
        DateTime Now { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
    }

    /// <summary>레이드 진행 상태: 일일 입장 횟수 + 보스 남은 체력 (공격할 때마다 누적되어 깎임).</summary>
    [Serializable]
    public sealed class RaidState
    {
        public string DayKey = string.Empty;
        public int EntriesUsed;
        public int BossLevel = 1;
        public float BossLife = -1f;
        public long LifetimeDamage;
        public int BossKills;
    }

    /// <summary>
    /// 세션을 넘어 유지되는 플레이어 데이터. 전투 세션(던전/레이드)은 이것을 읽어 캐릭터를 만들고,
    /// 끝나면 다시 기록합니다.
    /// </summary>
    public sealed class GameProfile
    {
        public GameProfile(long gold = 0, int stashExtraRows = 0)
        {
            var bag = Tuning.GameTuning.Current.Bag;
            Wallet = new Wallet(gold);
            Bag = new Inventory(bag.BagWidth, bag.BagHeight);
            Stash = new Inventory(bag.StashWidth, Math.Min(bag.StashMaxHeight, bag.StashHeight + stashExtraRows));
            State.StashExtraRows = stashExtraRows;
        }

        /// <summary>방향성 v1 성장 상태 (스탯·단련·템포·세팅 슬롯·퀘스트·출석·옵션).</summary>
        public ProgressionState State = new ProgressionState();

        public Wallet Wallet { get; }
        /// <summary>던전에 들고 들어가는 가방 (죽으면 절반 손실).</summary>
        public Inventory Bag { get; }
        /// <summary>마을 창고 (안전).</summary>
        public Inventory Stash { get; }
        /// <summary>입장 가능한 던전 수 (ACT 구분 없이 앞에서부터, 보스 층을 깨면 +1).</summary>
        public int UnlockedDungeons = 1;
        public string ClassId = "knight";
        public int Level = 1;
        public float Experience;
        public DungeonProgress Progress = new DungeonProgress();
        public readonly Dictionary<EquipSlot, Item> Equipped = new Dictionary<EquipSlot, Item>();
        public readonly HashSet<int> TreeOwned = new HashSet<int>();
        public int TreeSeed = 20261126;
        public int TreeSize = 3000;
        public readonly List<PetState> Pets = new List<PetState>();
        public string ActivePetId;
        public RaidState Raid = new RaidState();

        public bool HasStarted => Equipped.Count > 0;

        public PetState ActivePet => Pets.Find(p => p.PetId == ActivePetId);

        public PetState GetOrAddPet(string petId)
        {
            var state = Pets.Find(p => p.PetId == petId);
            if (state != null) return state;
            state = new PetState { PetId = petId };
            Pets.Add(state);
            return state;
        }
    }

    /// <summary>하루 입장 횟수 제한 (자정 기준 초기화).</summary>
    public sealed class DailyEntryLimit
    {
        readonly IClock _clock;
        readonly RaidState _state;

        public DailyEntryLimit(IClock clock, RaidState state, int maxPerDay)
        {
            _clock = clock;
            _state = state;
            MaxPerDay = maxPerDay;
        }

        public int MaxPerDay { get; }

        string Today => _clock.Now.ToString("yyyyMMdd");

        public int Remaining
        {
            get
            {
                Roll();
                return Math.Max(0, MaxPerDay - _state.EntriesUsed);
            }
        }

        public bool TryConsume()
        {
            Roll();
            if (_state.EntriesUsed >= MaxPerDay) return false;
            _state.EntriesUsed++;
            return true;
        }

        void Roll()
        {
            if (_state.DayKey == Today) return;
            _state.DayKey = Today;
            _state.EntriesUsed = 0;
        }
    }
}

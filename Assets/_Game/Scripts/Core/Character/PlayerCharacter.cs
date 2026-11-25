using ShadowRPG.Core.Actors;
using ShadowRPG.Core.AutoBattle;
using ShadowRPG.Core.Classes;
using ShadowRPG.Core.Items;
using ShadowRPG.Core.Skills;
using ShadowRPG.Core.Stats;
using ShadowRPG.Core.Supports;
using ShadowRPG.Core.World;
using UnityEngine;

namespace ShadowRPG.Core.Character
{
    /// <summary>
    /// 플레이어 집합체. 각 책임(장비·세트·스킬 묶음·직업·성장·자동 사냥)은 전용 객체가 맡고,
    /// 이 클래스는 그것들을 연결하고 매 프레임 순서대로 진행시킵니다.
    /// </summary>
    public sealed class PlayerCharacter
    {
        public PlayerCharacter(ICombatWorld world, ISkillCatalog skills, ISupportCatalog supports, ISetCatalog sets, Inventory bag = null, string name = "그림자")
        {
            Combatant = new Combatant(name, Team.Player, new IStatDerivationRule[] { new CoreStatDerivationRule() })
            {
                VisualKey = "shadow_hero",
                BaseMoveSpeed = 3.4f,
                Radius = 0.35f
            };
            ApplyBaseStats(Combatant.Stats);
            Combatant.Loadout = new SkillLoadout();

            Equipment = new Equipment(new IEquipValidator[] { new SlotCompatibilityValidator(), new StatRequirementValidator() });
            Inventory = bag ?? new Inventory(Meta.GameProfile.DefaultBagCapacity);
            StatApplier = new EquipmentStatApplier(Equipment, Combatant);
            Sets = new SetBonusTracker(Equipment, Combatant, sets);
            LoadoutBuilder = new LoadoutBuilder(Equipment, Combatant, world, skills, supports);
            Class = new ClassPassiveApplier(Combatant);
            Progress = new PlayerProgress(Combatant);

            Context = new AutoBattleContext(Combatant, world);
            Planner = new AutoCastPlanner(Context, AutoCastPlanner.DefaultRules());
            Movement = new AutoMovementPolicy();
            MoveInput = new NoMoveInput();

            LoadoutBuilder.Rebuild();
            Combatant.Health.ResetToFull();
            Combatant.Resource.Fill();
        }

        public Combatant Combatant { get; }
        public Equipment Equipment { get; }
        public Inventory Inventory { get; }
        public EquipmentStatApplier StatApplier { get; }
        public SetBonusTracker Sets { get; }
        public LoadoutBuilder LoadoutBuilder { get; }
        public ClassPassiveApplier Class { get; }
        public PlayerProgress Progress { get; }
        public AutoBattleContext Context { get; }
        public AutoCastPlanner Planner { get; }
        public IMovementPolicy Movement { get; set; }
        public IMoveInput MoveInput { get; set; }

        /// <summary>자동 이동 (끄면 조이스틱으로만 이동, 스킬은 계속 자동 시전).</summary>
        public bool AutoMove { get; set; } = true;

        public void ChangeClass(CharacterClass characterClass)
        {
            Class.Apply(characterClass);
            Progress.Affinity = characterClass.Affinity;
            Progress.ApplyLevelBonus();
        }

        public void Tick(float dt)
        {
            if (!Combatant.IsAlive) return;

            Combatant.Loadout.Tick(dt);
            Planner.Tick(dt);

            var manual = MoveInput.Direction;
            if (manual.sqrMagnitude > 0.01f)
            {
                Combatant.Move(manual, dt);
                return;
            }

            if (!AutoMove) return;
            Context.Refresh();
            var direction = Movement.Decide(Context, dt);
            if (direction.sqrMagnitude > 0.0001f) Combatant.Move(direction, dt);
        }

        static void ApplyBaseStats(StatSheet stats)
        {
            stats.SetBase(StatType.Strength, 20f);
            stats.SetBase(StatType.Dexterity, 20f);
            stats.SetBase(StatType.Intelligence, 20f);
            stats.SetBase(StatType.MaxLife, 120f);
            stats.SetBase(StatType.MaxResource, 60f);
            stats.SetBase(StatType.ResourceRegen, 2f);
            stats.SetBase(StatType.WeaponDamage, 5f);
            stats.SetBase(StatType.AttacksPerSecond, 1.2f);
            stats.SetBase(StatType.CritChancePct, 5f);
            stats.SetBase(StatType.LifeRegen, 1f);
            stats.SetBase(StatType.LifeOnHit, 1f);
        }

        public static Vector2 SpawnPoint(Rect bounds) => bounds.center;
    }
}

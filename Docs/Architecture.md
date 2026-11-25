# 아키텍처 (SOLID)

GDD(`Docs/GDD.md`)의 규칙을 **Unity에 의존하지 않는 Core**와 **표현 전용 Runtime**으로 나눠 구현했습니다.

```
Assets/_Game/
├─ Scripts/Core      (ShadowRPG.Core.asmdef)     게임 규칙 전체. MonoBehaviour 없음 → EditMode 테스트 가능
├─ Scripts/Runtime   (ShadowRPG.Runtime.asmdef)  화면·입력·UI. Core 이벤트를 구독해 그리기만 함
├─ Scripts/Editor    (ShadowRPG.Editor.asmdef)   메뉴 ShadowRPG/Build Demo Scene
├─ Tests/EditMode    GDD 규칙 검증 테스트
├─ Resources/SpriteLibrary.asset   키 → 스프라이트 (0x72 CC0 타일셋)
└─ Scenes/ShadowBattle.unity       데모 씬 (빌드 첫 씬)
```

## GDD → 코드 대응

| GDD | 코드 |
| --- | --- |
| 1.2 3대 코어 스탯 환산 | `Stats/CoreStatDerivationRule` (IStatDerivationRule) |
| 1.1 직업 = 전직 패시브, 스킬 제한 없음 | `Classes/CharacterClass`, `ClassPassiveApplier`, `Items/StatRequirementValidator` |
| 2.1 9슬롯 / 무기·방어구=스킬, 장신구=보조 | `Items/SlotRules`, `Character/LoadoutBuilder` |
| 2.2 2/3/4 세트, 성물 세트 -1 | `Items/SetCatalog`, `SetBonusTracker` |
| 3 스킬 등급 1.0/1.15/1.35/1.65 | `Skills/SkillRarityProfile` |
| 4.1-① 그림자 갑주 | `Combat/ShadowFortifyRule`, `Status/ShadowFortifyStatus` |
| 4.1-② 그림자 추적 대시 | `AutoBattle/AutoMovementPolicy` |
| 4.1-③ 근접 1.4배 / 회복 100%:45% | `Combat/RangeAsymmetryRule`, `LifeOnHitRule` |
| 4.1-④ 투사체 첫 적중 소멸 | `World/CombatWorld.ResolveAfterHit` |
| 4.2 자동 시전 우선순위 1→6 (0.1초) | `AutoBattle/AutoCastPlanner` + `AuraRule`…`PrimaryRule` |
| 5.1~5.3 스킬 24종 + 전설 각성 | `Skills/Behaviors/*Skills.cs`, `SkillCatalog` |
| 5.4 보조 권능 6종 | `Supports/Supports.cs` |
| 6.1 장비 JSON 스키마 | `Items/Item` (+ `LootGenerator`) |

## SOLID 적용

- **S (단일 책임)**: `Equipment`는 슬롯 보관만, 능력치 반영은 `EquipmentStatApplier`, 세트는 `SetBonusTracker`, 스킬 묶음은 `LoadoutBuilder`. `SkillExecutor`는 시전 순서만, 무엇을 하는지는 `ISkillBehavior`.
- **O (개방-폐쇄)**: 새 스킬 = `SkillDefinition` + `ISkillBehavior` 클래스 추가. 새 피해 규칙 = `IOutgoingDamageRule`/`IIncomingDamageRule` 추가 후 `GameSession`에 등록. 자동 사냥 단계 = `IAutoCastRule` 추가. 장착 제약 = `IEquipValidator` 추가.
- **L (리스코프 치환)**: 모든 스킬 동작·규칙·보조 권능은 같은 계약을 지키므로 서로 바꿔 끼워도 호출 측이 깨지지 않습니다 (`ModifierSupport`, `TimedStatus` 파생).
- **I (인터페이스 분리)**: `ICombatWorld` = `ITargetQuery` + `IEffectSpawner`. 상태이상은 `IIncomingDamageModifier`, `IMoveSpeedModifier`, `IActionDisabler` 등 필요한 역할만 구현.
- **D (의존성 역전)**: Core는 `IRandom`, `IMoveInput`, `ISkillCatalog`, `ILootGenerator`, `IEnemyFactory` 같은 추상화에만 의존. 실제 조립은 `GameSession`(Core)과 `GameBootstrap`(Unity) 두 곳에서만 합니다. 표현 계층은 `VfxEvent`·이벤트만 받습니다.

## 실행

1. Unity에서 `Assets/_Game/Scenes/ShadowBattle.unity`를 열고 Play.
2. 씬이 없으면 메뉴 **ShadowRPG → Build Demo Scene**.
3. 에디터 테스트: WASD/방향키 이동, 손을 떼면 자동 사냥. 모바일은 좌하단 가상 조이스틱.
4. 밸런스 수치는 씬의 `GameBootstrap > Balance` (GDD 4장 값) 에서 조정.

## 아트

- 주인공: `PixelArtFactory`가 코드로 그린 16×24 도트 (검은 아지랑이 + 푸른 안광 + 초보자 의상). 정식 스프라이트가 생기면 `SpriteLibrary`에 `shadow_hero` / `shadow_hero_run` 키로 등록하면 자동 교체됩니다.
- 몬스터·바닥: 0x72 Dungeon Tileset II (CC0).

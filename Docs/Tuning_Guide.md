# 수치 조정 가이드 (Tuning Guide)

> 이 문서만 보고 게임 수치를 바꿀 수 있도록 정리했습니다.
> 수치는 **탭(파일) 18개**로 나뉘어 있고, 탭 하나가 JSON 파일 하나입니다.
> 기준 문서: `Direction_v1.md`, `Balance_Draft_v1.md`, `Menu_Structure_Draft_v1.md`

## 사용법

1. 파일 위치: `Assets/_Game/Resources/Tuning/{탭 이름}.json`
   - 예: 던전 탭 → `Assets/_Game/Resources/Tuning/Dungeon.json`
2. 메모장·VS Code 등 아무 텍스트 편집기로 열어 **숫자만** 바꾸고 저장합니다.
3. Unity에서 Play를 다시 누르면 적용됩니다 (게임 시작 시 한 번 읽음).

### 지킬 것

- 키 이름(따옴표 안 영어)은 바꾸지 마세요. 값만 바꿉니다.
- 소수는 점으로 씁니다: `1.5`. 쉼표·따옴표·괄호 짝을 지워 깨지면 그 탭은 **기본값**으로 돌아가고 Console에 `[Tuning] … 읽기 실패`가 뜹니다.
- 항목을 지워도 됩니다. 지운 항목은 코드의 기본값을 씁니다.
- 처음 상태로 되돌리기: Unity 메뉴 **ShadowRPG → Tuning → Reset All JSON To Defaults**.
- 지운 파일만 다시 만들기: **ShadowRPG → Tuning → Export Missing JSON**.
- 기본값 원본: `Assets/_Game/Scripts/Core/Tuning/GameTuning.cs` (이 문서의 "기본값" 열과 같음).

### 표 읽는 법

| 열 | 뜻 |
| --- | --- |
| 키 | JSON 안의 이름 |
| 기본값 | 지금 들어 있는 값 |
| 뜻 | 무엇을 바꾸는지 |
| 조정 팁 | 올리면/내리면 어떻게 되는지, 권장 범위 |

`%` 표시 항목은 퍼센트 숫자(4 = 4%)이고, `배율` 항목은 곱하기(1.15 = 15% 증가)입니다.

## 탭 목록

| # | 탭 (파일) | 다루는 것 |
| --- | --- | --- |
| 1 | [던전 (Dungeon)](#탭-1-던전--dungeonjson) | ACT·던전 이름, 층 수, 몹 무리 수, 지도 크기, 결과창 |
| 2 | [몬스터 (Monster)](#탭-2-몬스터--monsterjson) | 난이도 곡선, 정예·보스 배율, 경험치 |
| 3 | [드랍 (Drop)](#탭-3-드랍--dropjson) | 드랍 확률, 층별 희귀도, ACT 상한, 아이템 레벨, 옵션 줄 수·종류 |
| 4 | [아이템 (Item)](#탭-4-아이템--itemjson) | 판매가, 전투력 계산 |
| 5 | [성장 (Growth)](#탭-5-성장--growthjson) | 경험치 곡선, 스탯 포인트, 재분배 비용, 시작 골드 |
| 6 | [단련 (Training)](#탭-6-단련--trainingjson) | 골드 단련 비용·효과·ACT 한도 |
| 7 | [강화 (Enhance)](#탭-7-강화--enhancejson) | 장비 강화 비용·효과 |
| 8 | [트리 (Tree)](#탭-8-트리--treejson) | 성장 트리 크기·비용 |
| 9 | [펫 (Pet)](#탭-9-펫--petjson) | 펫 레벨·등급 비용 |
| 10 | [전투 (Combat)](#탭-10-전투--combatjson) | 근·원거리 균형, 자동 전투 판단, 상한 |
| 11 | [리듬 (Rhythm)](#탭-11-리듬--rhythmjson) | 리듬 무기 판정·보상·원 위치 |
| 12 | [가방·사망 (Bag)](#탭-12-가방사망--bagjson) | 가방·창고 크기, 사망 손실, 광고 회수 |
| 13 | [편의 (Convenience)](#탭-13-편의--conveniencejson) | 배속, 세팅 슬롯 |
| 14 | [퀘스트 (Quest)](#탭-14-퀘스트--questjson) | 일일 퀘스트 풀·보상 |
| 15 | [출석 (Attendance)](#탭-15-출석--attendancejson) | 28일 출석판, 신규 7일 |
| 16 | [상점 (Shop)](#탭-16-상점--shopjson) | 템포 상품, 패키지, 편의 상품, 골드 상점 |
| 17 | [레이드 (Raid)](#탭-17-레이드--raidjson) | 일일 입장, 전투력 기준 |
| 18 | [연출 (Presentation)](#탭-18-연출--presentationjson) | 카메라 거리, 히트스톱, 흔들림, 피해 숫자, 조명 |
| 부록 | [아트·UI 설정](#부록-아트ui-설정-json이-아닌-에셋) | 굽기 각도·밀도, 환경 묶음, UI 겉모양 |

---

## 탭 1. 던전 — `Dungeon.json`

### 구조

| 키 | 기본값 | 뜻 | 조정 팁 |
| --- | --- | --- | --- |
| FloorsPerDungeon | 10 | 던전 하나의 층 수. 마지막 층은 보스 | 방향성 확정값 10. 줄이면 한 판이 짧아짐 |
| Acts | 4개 | ACT 목록 (아래 표) | 항목을 추가하면 ACT가 늘어남 |

`Acts` 안의 항목 하나:

| 키 | 기본값 예 (ACT 1) | 뜻 |
| --- | --- | --- |
| Name | "버려진 지하묘지" | ACT 이름 |
| Dungeons | ["무너진 입구", "납골 회랑", "속삭이는 성소", "검은 제단"] | 던전 이름 목록. **개수 = 그 ACT의 던전 수** (방향성: 4~6개) |
| EnemyIds | ["skeleton_minion", "skeleton_rogue"] | 등장 몬스터 Id (아래 목록) |

기본 ACT 구성: ACT 1 던전 4개 · ACT 2 5개 · ACT 3 5개 · ACT 4 6개 (총 20개).

사용할 수 있는 몬스터 Id: `skeleton_minion`(해골 졸개, 근접) · `skeleton_rogue`(해골 석궁병, 원거리) · `skeleton_mage`(해골 주술사, 원거리 어둠) · `skeleton_warrior`(해골 전사, 근접 탱커).

### 몹 무리 (층당 몬스터 수)

층당 일반 몬스터 = **무리 수 × 무리당 마리 수**.
무리 수 = `StartPacks` + (층−1) ÷ `FloorsPerExtraPack` + 던전 순번 × `ExtraPacksPerDungeon`, 최대 `MaxPacks`.

| 키 | 기본값 | 뜻 | 조정 팁 |
| --- | --- | --- | --- |
| StartPacks | 3 | 1층 무리 수 | 방향성: 시작 3팀 |
| MobsPerPack | 8 | 무리 하나의 마리 수 | 방향성: 한 화면 약 8마리 |
| FloorsPerExtraPack | 3 | 몇 층마다 무리 +1 | 낮추면 빨리 늘어남 |
| ExtraPacksPerDungeon | 1 | ACT 안 던전 순번마다 무리 +1 | 0이면 던전마다 같음 |
| MaxPacks | 7 | 층당 최대 무리 수 | 층당 약 1분 유지가 목표 |
| PackSpread | 2.4 | 무리가 퍼지는 반경 (유닛) | 크면 듬성듬성 |
| EliteFromFloor | 3 | 정예가 나오기 시작하는 층 | |
| ElitesPerFloor | 1 | 층당 정예 수 | |
| ExtraElitesPerTwoActs | 1 | ACT 2개마다 정예 +1 | |
| LevelPerFloor | 0.6 | 전체 층 하나당 몬스터 레벨 증가 (경험치·드랍 기준) | 힘은 몬스터 탭의 배율이 정함 |

### 결과창·진행

| 키 | 기본값 | 뜻 | 조정 팁 |
| --- | --- | --- | --- |
| AutoContinueSeconds | 5 | 결과창 자동 진행 대기 (초). 설정 "층 클리어 5초 뒤 자동 진행"이 켜졌을 때, 보스 층은 자동 귀환 | |
| ClearSlowMoSeconds | 0.6 | 마지막 몹 처치 후 슬로우 모션 길이 (게임 시간) | |
| SlowMoScale | 0.25 | 슬로우 모션 속도 (1 = 정상) | 낮을수록 더 느림 |
| FloorClearGoldPerLevel | 12 | 층 클리어 골드 = 이 값 × 몬스터 레벨 | 골드 경제의 기본 수입 |
| BossClearGoldMultiplier | 3 | 보스 층 클리어 골드 배율 | |

### 지도 (방 + 좁은 통로)

| 키 | 기본값 | 뜻 | 조정 팁 |
| --- | --- | --- | --- |
| RoomCountMin | 5 | 층당 방 개수 최소 | 방향성: 너무 크지 않게 |
| RoomCountMax | 8 | 층당 방 개수 최대 | |
| RoomSizeMin | 7 | 방 한 변 최소 (칸) | |
| RoomSizeMax | 11 | 방 한 변 최대 (칸). 보스 방은 이보다 큼 | |
| CorridorWidth | 2 | 통로 폭 (칸) | 1~2 = 1:1 구도. 3 이상이면 광장처럼 됨 |
| CorridorLengthMin | 3 | 방 사이 최소 간격 (칸) | |
| SafeRadiusFromStart | 7 | 시작 지점에서 이 거리 안에는 몹을 두지 않음 | |

---

## 탭 2. 몬스터 — `Monster.json`

몬스터 힘 = 원형 × `GlobalLife/GlobalDamage` × **(1 + 층 × FloorStatPct%) × DungeonMultiplier^던전순번 × ActMultiplier^ACT** × 등급 배율.
→ 층 안에서는 조금씩, ACT 경계에서 크게 뜀 (ACT = 벽).

| 키 | 기본값 | 뜻 | 조정 팁 |
| --- | --- | --- | --- |
| GlobalLife | 1.6 | 모든 몬스터 생명력 배율 | **초반 난이도 손잡이.** 쉬우면 올리기 |
| GlobalDamage | 1.7 | 모든 몬스터 공격력 배율 | 자주 죽으면 내리기 |
| FloorStatPct | 4 | 층마다 공격·생명 +% | 방향성: 소폭 |
| DungeonMultiplier | 1.15 | 다음 던전마다 배율 | |
| ActMultiplier | 1.8 | 다음 ACT마다 배율 | 벽의 높이 |
| EliteLife | 4 | 정예 생명력 배율 | |
| EliteDamage | 1.5 | 정예 공격력 배율 | |
| EliteSize | 1.3 | 정예 크기 배율 | |
| BossLife | 18 | 보스 생명력 배율 | |
| BossDamage | 2.2 | 보스 공격력 배율 | |
| BossSize | 1.8 | 보스 충돌 크기 배율 | 그림 크기는 아트 카탈로그 |
| BossSpeed | 0.85 | 보스 이동 속도 배율 | |
| ArmorPerLevel | 0.2 | 레벨당 방어력 증가 계수 | |
| ResistPerLevel | 1.5 | 레벨당 모든 저항 % | |
| ResistCap | 40 | 몬스터 저항 상한 % | |
| XpPerLevel | 4 | 처치 경험치 = 이 값 × 몬스터 레벨 | |
| EliteXp | 6 | 정예 경험치 배율 | |
| BossXp | 25 | 보스 경험치 배율 | |
| AggroRange | 8 | 몬스터가 플레이어를 알아채는 거리 (유닛) | |
| AlertRadius | 5 | 깨어난 몬스터가 주변 동료를 깨우는 거리 | |

---

## 탭 3. 드랍 — `Drop.json`

| 키 | 기본값 | 뜻 | 조정 팁 |
| --- | --- | --- | --- |
| NormalDropChancePct | 12 | 일반 몹 드랍 확률 % | 높이면 결과창이 붐빔 (가방 선택 압박↑) |
| EliteDrops | 1 | 정예 드랍 개수 | |
| BossDrops | 3 | 보스 드랍 개수 | |
| BossGuaranteesRare | true | 보스는 희귀 이상 확정 | |
| BossRarityBonus | 2 | 보스 드랍의 희귀·전설·세트 가중치 배율 | |
| Act1BossLegendaryChancePct | 3 | ACT 1 보스의 전설 소확률 % (상한 예외) | |

### 층 깊이 → 희귀도 (`Bands`)

`FromFloor` 층부터 적용되는 가중치입니다 (합이 100일 필요는 없음, 비율로 계산).

| FromFloor | Normal | Magic | Rare | Legendary | Set |
| --- | --- | --- | --- | --- | --- |
| 1 | 70 | 30 | 0 | 0 | 0 |
| 4 | 50 | 35 | 15 | 0 | 0 |
| 7 | 35 | 37 | 24 | 3 | 1 |
| 10 | 0 | 30 | 55 | 10 | 5 |

### ACT별 등급 상한 (`ActRarityCap`)

`["Rare", "Legendary", "Set", "Set"]` — ACT 1은 희귀까지, ACT 2는 전설까지, ACT 3부터 세트. 값: `Normal`, `Magic`, `Rare`, `Legendary`, `Set`.

### 아이템 레벨

아이템 레벨 = (ACT−1) × `ItemLevelPerAct` + 던전 순번(1부터) × `ItemLevelPerDungeon` + 층 ÷ `FloorsPerItemLevel`

| 키 | 기본값 |
| --- | --- |
| ItemLevelPerAct | 25 |
| ItemLevelPerDungeon | 4 |
| FloorsPerItemLevel | 2 |

### 옵션 줄 수 (`AffixLines`) — ACT가 높을수록 많음

| Act | Normal | Magic | Rare | Legendary(세트 포함) |
| --- | --- | --- | --- | --- |
| 1 | 1 | 2 | 3 | 3 |
| 2 | 1 | 2 | 4 | 4 |
| 3 | 1 | 3 | 5 | 5 |
| 4 | 2 | 3 | 6 | 6 |

### 옵션 종류 (`AffixPools`) — ACT마다 해금

| Act | 열리는 옵션 (`Stats`) |
| --- | --- |
| 1 | 힘·민첩·지능·생명·무기 피해·모든 피해%·방어·공격 속도·치명타·적중 시 생명·이동 속도·골드 |
| 2 | 속성 피해% 6종 (불·얼음·번개·대지·빛·어둠) · 속성 저항 6종 · 모든 저항 |
| 3 | 쿨감 · 범위 · 투사체 +1 · 연쇄 +1 · 치명타 피해 · 회피 · 막기 |
| 4 | 보스 피해 · 정예 피해 감소 · 생명 재생 · 자원 재생 · 자원 비용 감소 · 경험치 · 펫 위력 |

옵션 이름은 영어 키(`FireDamagePct` 등)로 적습니다. 풀이 넓을수록 원하는 조합이 나올 확률이 낮아집니다 (파밍 동기).

---

## 탭 4. 아이템 — `Item.json`

### 판매가 (자동 판매·결과창 판매)

판매가 = 등급 기본가 × (1 + 아이템 레벨 × `SellPerItemLevel`) × (1 + 강화 × `SellPerEnhance`) × (1 + 골드 획득%)

| 키 | 기본값 | 뜻 |
| --- | --- | --- |
| SellNormal | 5 | 일반 기본가 |
| SellMagic | 15 | 마법 |
| SellRare | 45 | 희귀 |
| SellLegendary | 150 | 전설 |
| SellSet | 120 | 세트 |
| SellPerItemLevel | 0.15 | 아이템 레벨당 가격 증가 |
| SellPerEnhance | 0.25 | 강화 단계당 가격 증가 |

### 전투력 (모든 아이템에 표기 — 비교용 지표)

| 키 | 기본값 | 뜻 |
| --- | --- | --- |
| PowerPerStatPoint | 1 | 힘·민첩·지능 1당 |
| PowerPerLife | 0.2 | 생명력 1당 |
| PowerPerDamage | 3 | 무기·고정 피해 1당 |
| PowerPerPct | 2 | % 옵션 1당 |
| PowerPerArmor | 0.25 | 방어력 1당 |
| PowerPerSkillRarity | 40 | 부여 스킬 등급당 |
| AutoEquipOnReturnDefault | true | (참고) 자동 장착 옵션 기본값 |

---

## 탭 5. 성장 — `Growth.json`

| 키 | 기본값 | 뜻 | 조정 팁 |
| --- | --- | --- | --- |
| ExpBase | 25 | 다음 레벨 경험치 = ExpBase × 레벨^ExpExponent | |
| ExpExponent | 1.35 | 경험치 곡선 기울기 | 높이면 고레벨이 느려짐 |
| StatPointsPerLevel | 1 | 레벨업당 스탯 포인트 (힘/민첩/지능 직접 분배) | 방향성 확정값 1 |
| LifePerLevel | 12 | 레벨당 생명력 | |
| RespecGoldPerLevel | 10 | 스탯 재분배 비용 = 레벨 × 이 값 | 방향성: 부담 없게 |
| StartingGold | 200 | 처음 시작 골드 | |
| StartingStatPoints | 0 | 처음 주는 스탯 포인트 | |

---

## 탭 6. 단련 — `Training.json`

골드로 기본 체급을 올림. 비용(n) = `CostBase` × `CostGrowth`^n, 한도 = 열린 ACT 수 × `LevelsPerAct`.

| 키 | 기본값 | 뜻 | 조정 팁 |
| --- | --- | --- | --- |
| CostBase | 50 | 첫 단계 비용 | |
| CostGrowth | 1.12 | 단계마다 비용 배율 | 높이면 골드 효율이 빨리 떨어짐 |
| LevelsPerAct | 20 | ACT 하나당 단련 한도 | 벽 앞에서 골드로 넘을 수 있는 양 |
| AttackPctPerLevel | 1 | 공격력 단련 1단계 = 모든 피해 +% | |
| LifePctPerLevel | 1 | 생명력 +% | |
| ArmorPctPerLevel | 1 | 방어 효과 +% | |
| ResistPctPerLevel | 0.5 | 모든 저항 +% | |

---

## 탭 7. 강화 — `Enhance.json`

비용 = `CostBase` × `CostGrowth`^단계 × 등급 배율 × (1 + 아이템 레벨 × `CostPerItemLevel`)

| 키 | 기본값 | 뜻 |
| --- | --- | --- |
| MaxLevel | 15 | 최대 강화 단계 |
| BonusPerLevel | 0.08 | 단계당 장비 수치 +8% |
| CostBase | 30 | 기본 비용 |
| CostGrowth | 1.32 | 단계마다 비용 배율 |
| CostPerItemLevel | 0.1 | 아이템 레벨 가중 |
| RarityNormal / Magic / Rare / Legendary / Set | 1 / 1.5 / 2.5 / 4 / 3.5 | 등급별 비용 배율 |

---

## 탭 8. 트리 — `Tree.json`

| 키 | 기본값 | 뜻 | 조정 팁 |
| --- | --- | --- | --- |
| NodeCount | 3000 | 트리 노드 수 (1,000~10,000) | 바꾸면 지도가 새로 생성됨 — **이미 산 노드 번호가 달라짐** |
| Seed | 20261126 | 트리 모양 시드 | 바꾸면 모양이 달라짐 |
| CostBase | 25 | 노드 비용 = CostBase × (보유 노드 수+1)^CostExponent × 등급 배율 | |
| CostExponent | 1.35 | 살수록 비싸지는 정도 | |
| ResetGoldPerNode | 5 | (예비 — 초기화 기능 연결 전) 트리 초기화 비용 (노드당) | |

---

## 탭 9. 펫 — `Pet.json`

| 키 | 기본값 | 뜻 |
| --- | --- | --- |
| LevelCostBase | 60 | 레벨업 비용 = 60 × 1.2^(레벨−1) × 등급 |
| LevelCostGrowth | 1.2 | 레벨업 비용 배율 |
| GradeCostBase | 800 | 등급업 비용 = 800 × 5^(등급−1) |
| GradeCostGrowth | 5 | 등급업 비용 배율 |
| MaxGrade | 5 | 최대 등급 |
| MaxGradeByAct | [2, 3, 4, 5] | (검토 중) ACT별 최대 등급 |

---

## 탭 10. 전투 — `Combat.json`

| 키 | 기본값 | 뜻 |
| --- | --- | --- |
| MeleeDamageMultiplier | 1.4 | 근접 스킬 기본 피해 배율 |
| RangedDamageMultiplier | 1.0 | 원거리 스킬 기본 피해 배율 |
| MeleeLifeOnHitEfficiency | 1.0 | 근접 적중 시 생명 회복 효율 |
| RangedLifeOnHitEfficiency | 0.45 | 원거리 적중 시 생명 회복 효율 |
| FortifyDuration | 4 | 그림자 갑주 지속 (초) |
| FortifyDamageReduction | 0.3 | 그림자 갑주 피해 감소 (0.3 = 30%) |
| ShadowStepRadius | 3.5 | 자동 접근 이동 거리 |
| ShadowStepDuration | 0.15 | 접근 이동 시간 (초) |
| ShadowStepCooldown | 0.5 | 접근 이동 재사용 (초) |
| AutoCastInterval | 0.1 | 자동 시전 판단 주기 (초) |
| RangedThreatRadius | 1.5 | 원거리 캐릭터가 위협으로 느끼는 거리 |
| RangedThreatCount | 2 | 이만큼 붙으면 거리 벌리기 |
| MeleeEngageDistance | 4 | 근접 캐릭터 돌진 거리 |
| SurvivalLifeThreshold | 0.5 | 생명이 이 비율 아래면 생존 스킬 우선 |
| EnemySearchRadius | 14 | 자동 전투 적 탐색 거리 |
| BaseCritMultiplier | 1.5 | 기본 치명타 배율 |
| MaxResistPct | 75 | 저항 상한 % |
| MaxBlockPct | 75 | 막기 상한 % |
| MaxEvasionPct | 60 | 회피 상한 % |
| MaxCritChancePct | 95 | 치명타 확률 상한 % |

---

## 탭 11. 리듬 — `Rhythm.json`

첫 무기(리듬 무기): 바깥 원이 줄어들어 판정 원에 맞을 때 터치. 성공 → 추가 타격 + 공격 속도 중첩. 실패해도 불이익 없음.

| 키 | 기본값 | 뜻 | 조정 팁 |
| --- | --- | --- | --- |
| DefaultBeatSeconds | 0.9 | 기본 박자 간격 (초). 직업별 첫 무기: 기사 ×1.0 · 마법사 ×1.15 · 궁수 ×0.9 · 수도사 ×0.75 | 짧을수록 손이 바쁨 |
| RingStartScale | 2.6 | 바깥 원 시작 크기 (판정 원 = 1) | |
| PerfectWindowSeconds | 0.08 | 완벽 판정 허용 오차 (초, ±) | 넓히면 쉬움 |
| GoodWindowSeconds | 0.17 | 좋음 판정 허용 오차 (초, ±) | |
| PerfectExtraHits | 2 | 완벽 시 추가 타격 수 | 리듬 빌드 > 자동 빌드의 핵심 |
| GoodExtraHits | 1 | 좋음 시 추가 타격 수 | |
| ExtraHitDamagePct | 60 | 추가 타격 피해 = 무기 피해의 % | |
| AttackSpeedPctPerStack | 6 | 성공마다 공격 속도 +% (중첩) | |
| MaxStacks | 8 | 최대 중첩 | |
| StackDurationSeconds | 3 | 중첩 유지 시간 (성공하면 갱신) | |
| RingScreenX | 0.8 | 원 기본 위치 가로 (0 왼쪽 ~ 1 오른쪽) | 설정에서 좌/우 변경 가능 |
| RingScreenY | 0.3 | 원 기본 위치 세로 (0 아래 ~ 1 위) | |
| RingSizeDp | 190 | 원 크기 (UI 단위, 화면 가로 1080 기준) | |
| VibrateOnPerfect | true | 완벽 판정 시 진동 (설정에서 끌 수 있음) | |

---

## 탭 12. 가방·사망 — `Bag.json`

| 키 | 기본값 | 뜻 | 조정 팁 |
| --- | --- | --- | --- |
| BagWidth | 8 | 가방 가로 칸 | 가방이 작을수록 "무엇을 들고 갈까" 압박↑ |
| BagHeight | 5 | 가방 세로 칸 | |
| StashWidth | 10 | 창고 가로 칸 | |
| StashHeight | 8 | 창고 세로 칸 (기본) | |
| StashExpandRows | 2 | 창고 확장 1회당 줄 수 (구매 후 다음 접속부터 적용) | |
| StashMaxHeight | 16 | 창고 최대 세로 칸 | |
| MaxPicksPerFloor | 0 | 층당 가져갈 수 있는 개수 제한 (0 = 가방 칸으로만 제한) | |
| DeathLossFraction | 0.5 | 사망 시 잃는 가방 아이템 비율 | 방향성 확정값 0.5 |
| AdRecoveryUnlimited | true | 광고 회수 무제한 | 방향성 확정 |
| AdRecoveryPerDay | 0 | (예비) 하루 광고 회수 제한 | |

아이템 칸 크기: 반지·목걸이 1×1 · 성물 1×2 · 투구·신발 2×2 · 갑옷 2×3 · 지팡이 1×4 · 대검 2×4 · 활 2×3 등 (코드: `ItemShapes`).

---

## 탭 13. 편의 — `Convenience.json`

| 키 | 기본값 | 뜻 |
| --- | --- | --- |
| Speeds | [1, 2, 3] | 던전 배속 단계 (배속 버튼이 순서대로 바꿈) |
| FreeMaxSpeed | 2 | 무료로 쓸 수 있는 최대 배속. 이보다 높으면 이용권·월정액 필요 |
| PresetSlotsBase | 3 | 기본 세팅 슬롯 수 |
| PresetSlotsMax | 6 | 최대 세팅 슬롯 수 |
| PresetSlotTempoPrice | 300 | 세팅 슬롯 +1 가격 (템포) |

---

## 탭 14. 퀘스트 — `Quest.json`

| 키 | 기본값 | 뜻 |
| --- | --- | --- |
| DailyCount | 5 | 하루 퀘스트 수 (풀에서 날짜 기준 무작위) |
| AllClearGold | 2000 | 전부 완료 보너스 골드 |
| AllClearTempo | 30 | 전부 완료 보너스 템포 |
| Pool | 17개 | 퀘스트 풀 (아래) |

`Pool` 항목: `Id`(바꾸지 말 것) · `Category` · `Text`(`{0}` 자리에 목표 수) · `Target`(목표) · `Param`(조건) · `RewardGold` · `RewardTempo`

| Id | 분류 | 내용 | 목표 | 보상 (골드 · 템포) |
| --- | --- | --- | --- | --- |
| turnin_rare | 반납 | 희귀 등급 장비 반납 (Param = 최소 등급) | 1 | 800 · 5 |
| turnin_big | 반납 | 6칸 이상 장비 반납 (Param = 최소 칸 수) | 2 | 600 · 5 |
| turnin_fire | 반납 | 불 속성 옵션 장비 반납 (Param = 옵션 키) | 1 | 700 · 5 |
| kill | 처치 | 몬스터 처치 | 300 | 500 · 3 |
| kill_elite | 처치 | 정예 처치 | 10 | 600 · 3 |
| kill_boss | 처치 | 던전 보스 처치 | 1 | 800 · 5 |
| depth | 깊이 | 아무 던전 N층 이상 도달 | 7 | 700 · 5 |
| streak | 깊이 | 귀환 없이 N층 연속 클리어 | 5 | 900 · 5 |
| deathless | 생존 | 사망 없이 10층 클리어 | 1 | 1200 · 8 |
| rhythm | 리듬 | 리듬 판정 성공 | 200 | 500 · 3 |
| rhythm_combo | 리듬 | 리듬 연속 성공 | 30 | 700 · 5 |
| train | 성장 | 단련 | 3 | 400 · 2 |
| tree | 성장 | 트리 노드 구매 | 2 | 400 · 2 |
| enhance | 성장 | 장비 강화 | 1 | 400 · 2 |
| sell_gold | 경제 | 판매로 골드 획득 | 3000 | 500 · 3 |
| pvp | 콘텐츠 | PvP (서버 연동 후) | 3 | 500 · 5 |
| raid | 콘텐츠 | 레이드 | 1 | 800 · 5 |

---

## 탭 15. 출석 — `Attendance.json`

| 키 | 기본값 | 뜻 |
| --- | --- | --- |
| CycleDays | 28 | 출석판 주기 (일) |
| DailyGold | 1000 | 매일 골드 |
| DailyGoldPerAct | 500 | 열린 ACT 하나당 매일 골드 추가 |
| Special | 4개 | 특별한 날: 7일 템포 50 + 장비 상자 · 14일 템포 80 + 꾸미기 상자 · 21일 템포 100 + 장비 상자 · 28일 템포 200 + 꾸미기 상자 |
| Newbie | 7개 | 신규 7일: 1일 골드 3000 · 2일 템포 50 · 3일 골드 5000 + 세팅 슬롯 체험 · 4일 템포 50 · 5일 골드 8000 + 펫 재료 · 6일 템포 80 · 7일 템포 150 + 희귀 장비 상자 |

`Special`/`Newbie` 항목: `Day` · `Gold` · `Tempo` · `Item`(이름, 지금은 표시만).

---

## 탭 16. 상점 — `Shop.json`

원칙: 전투력을 직접 파는 상품은 두지 않음. 현금 결제는 스토어 연동 전까지 "준비 중"으로 표시됩니다.

상품 항목: `Id`(바꾸지 말 것) · `Name` · `Currency`(`KRW` 또는 `Tempo`) · `Price` · `Tempo`(지급 템포) · `Gold`(지급 골드) · `Note`

| 묶음 | 상품 (기본) |
| --- | --- |
| TempoPacks | 템포 60 ₩1,200 · 템포 300 ₩5,900 · 템포 980 ₩19,000 · 템포 3280 ₩59,000 (첫 구매 2배) |
| Packages | 스타터 ₩4,900 (템포 300 + 골드 30,000, 1회) · 월정액 ₩5,900 (매일 템포 30 + 배속 ×3) · 시즌 패스 ₩12,000 |
| Convenience | 세팅 슬롯 +1 템포 300 · 창고 확장(2줄) 템포 200 · 배속 ×3 (1일) 템포 50 |

| 키 | 기본값 | 뜻 |
| --- | --- | --- |
| GoldShopItems | 6 | 골드 상점 장비 수 (매일 바뀜, 현재 ACT 기준) |
| GoldShopPriceMultiplier | 8 | 골드 상점 가격 = 판매가 × 이 값 |
| GoldShopRefreshHours | 24 | (예비) 새로 고침 주기 |
| GoldShopRerollGold | 500 | 골드로 즉시 새로 고침 비용 |

---

## 탭 17. 레이드 — `Raid.json`

| 키 | 기본값 | 뜻 |
| --- | --- | --- |
| EntriesPerDay | 3 | 하루 입장 횟수 |
| PowerRequirementByTier | [500, 1500, 4000, 10000] | (예비 — 열린 쟁점 S) 레이드 단계별 참가 전투력 기준. 아직 입장 검사에 쓰이지 않음 |
| PartySize | 4 | 파티 인원 |
| MatchmakingSeconds | 5 | (예비) 무작위 매칭 대기 |

---

## 탭 18. 연출 — `Presentation.json`

### 카메라

| 키 | 기본값 | 뜻 | 조정 팁 |
| --- | --- | --- | --- |
| CameraViewWidth | 10 | 세로 화면에서 보이는 가로 폭 (유닛 = 바닥 1칸) | **키우면 더 멀리서 봄** (캐릭터 작아짐) |
| CameraViewWidthLandscape | 20 | (예비 — 스팀판) 가로 화면일 때 가로 폭. 모바일은 세로 고정 | |
| CameraFollowDamping | 0.25 | 카메라가 따라오는 부드러움 (초) | 0이면 딱 붙음 |
| CameraLookAhead | 0.8 | 이동 방향으로 앞을 보는 정도 | |
| CameraVerticalOffset | 0.8 | 캐릭터보다 위를 보는 높이 | |

### 타격감 (테일즈위버식 임팩트)

| 키 | 기본값 | 뜻 |
| --- | --- | --- |
| HitStopNormal | 0.035 | 일반 타격 정지 (초) |
| HitStopCrit | 0.07 | 치명타 정지 |
| HitStopFinisher | 0.12 | 강타 마무리 정지 |
| HitStopBoss | 0.16 | 보스 처치 정지 |
| HitStopCooldown | 0.12 | 정지 사이 최소 간격 (연타에 끈적임 방지) |
| ShakeNormal | 0.06 | 일반 타격 흔들림 |
| ShakeCrit | 0.16 | 치명타 흔들림 |
| ShakePlayerHurt | 0.22 | 내가 맞을 때 흔들림 |
| ShakeBoss | 0.45 | 보스 처치 흔들림 |
| ScreenFlashFinisher | 0.22 | 큰 일격 화면 번쩍임 세기 |
| HurtVignette | 0.35 | 맞을 때 붉은 화면 세기 |
| KnockbackNormal | 0.18 | 맞은 적 밀림 (연출) |
| KnockbackCrit | 0.35 | 치명타 밀림 |
| ShowDamageNumbers | true | 피해 숫자 표시 (설정에서도 끌 수 있음) |
| DamageNumberSize | 26 | 피해 숫자 크기 |
| CritNumberScale | 1.45 | 치명타 숫자 크기 배율 |
| DamageNumberLifetime | 0.75 | 숫자가 떠 있는 시간 (초) |

### 조명·화면

| 키 | 기본값 | 뜻 |
| --- | --- | --- |
| AmbientIntensity | 0.85 | (참고) 전체 조명 기준값. 실제 밝기는 환경 묶음의 `ambientIntensity` |
| HeroLightIntensity | 0.7 | 캐릭터 주변 푸른 빛 세기 |
| BloomIntensity | 0.9 | 빛 번짐 세기 |
| VignetteIntensity | 0.38 | 화면 가장자리 어둡게 |

---

## 부록. 아트·UI 설정 (JSON이 아닌 에셋)

Unity Inspector에서 고칩니다 (에셋을 클릭 → 오른쪽 Inspector).

### 굽기 설정 — `Assets/_Game/Art/Bake/BakeSettings.asset`

고친 뒤 메뉴 **ShadowRPG → Art → 2. Bake All**로 다시 굽습니다.

| 항목 | 기본값 | 뜻 |
| --- | --- | --- |
| cameraPitch | 30 | 내려다보는 각도 (도). **`Resources/Art/Projection.asset`에서 자동으로 맞춰짐** (게임 화면과 공통) |
| unitsPerMeter | 0.5 | 3D 1미터 = 게임 0.5칸 |
| pixelsPerUnit | 128 | 게임 1칸 = 128픽셀 (모든 그림 공통 밀도) |
| characterCell | 224 | 캐릭터 한 칸 픽셀 |
| sampleFps / maxFrames | 12 / 12 | 동작 프레임 수 |
| mirrorDirections | 켬 | 5방향만 굽고 3방향은 반전 (용량 절약) |
| supersample | 3 | 확대 렌더 배수 (가장자리 품질) |
| outlineWidth / outlineColor | 2 / 거의 검정 | 바깥 외곽선 |
| innerLineStrength | 0.8 | 안쪽 선 진하기 |
| shadowColor · midColor · skyAmbient · groundAmbient | — | 명암 색 |
| topHighlight · saturation · exposure | 0.18 · 0.9 · 1.0 | 손그림 채색 느낌 (어두운 톤) |
| wallHeightScale | 0.35 | 벽 높이 배율 (낮은 석조 벽 = 약 1.4m) |
| environmentYaw | 45 | 벽·소품 카메라 방향 (아이소메트릭 = 45, 자동) |

### 캐릭터 외형 — `Assets/_Game/Art/Bake/Specs/Char_*.asset`

외형 하나 = 설명서 하나 (모델 · 보일 부품 · 붙일 무기 · 동작 이름 · 타격 시점). AI로 만든 모델로 바꿀 때 `modelPath`와 동작 이름만 고칩니다.
게임 안 크기·색은 `Assets/_Game/Resources/Art/AppearanceCatalog.asset`의 `scale`·`tint`.

### 환경 묶음 — `Assets/_Game/Art/Baked/Environment/{Crypt|Town}/*_Theme.asset`

| 항목 | 뜻 |
| --- | --- |
| floorTint · wallTint | 바닥·벽 색 (어두운 분위기는 여기서) |
| voidColor | 벽 너머 공허 색 |
| wallFaceHeight | 벽 정면 높이 |
| edgePropChance · wallPropChance · scatterPropChance | 소품 밀도 |
| props[] | 소품별 빛 색·반경·세기·깜빡임 |
| ambientColor · ambientIntensity | 전체 조명 |

ACT별 색감: `Assets/_Game/Resources/Art/EnvironmentCatalog.asset`의 `actTints` (ACT 1 회청 · 2 잿빛 · 3 얼음 · 4 붉음).

### UI 겉모양 — `Assets/_Game/Resources/UI/UiSkin.asset`

글꼴 · 패널 · 버튼 그림과 색 (`panelTint`, `buttonTint`, `accent`…). 정식 UI 아트로 바꿀 때 그림만 교체합니다.

### 이펙트 — `Assets/_Game/Resources/Art/FxCatalog.asset`

베기·불꽃·연기·충격파 그림 목록 (지금은 Kenney CC0). 정식 이펙트로 바꿀 때 목록만 교체합니다.

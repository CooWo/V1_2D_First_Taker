using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LootHunters
{
    /// <summary>
    /// 정식 화면 (uGUI, 3단계 기능 포함). 캔버스, 버튼, 스크롤 목록을 전부 코드로 만들기 때문에
    /// 씬에서 따로 배치할 것이 없습니다. DungeonRun과 같은 오브젝트에 붙이면 됩니다.
    /// 화면 구성: 상단 상태바 / 왼쪽 던전 패널 / 오른쪽 창고·대장간 탭
    /// </summary>
    [RequireComponent(typeof(DungeonRun))]
    public class GameUI : MonoBehaviour
    {
        [SerializeField] int maxLogLines = 14;

        static readonly Color BgColor     = new Color(0.07f, 0.08f, 0.11f);
        static readonly Color PanelColor  = new Color(0.12f, 0.14f, 0.19f);
        static readonly Color RowColor    = new Color(0.17f, 0.20f, 0.27f);
        static readonly Color ButtonColor = new Color(0.24f, 0.28f, 0.37f);
        static readonly Color TabOn       = new Color(0.78f, 0.58f, 0.22f);
        static readonly Color HeroHp      = new Color(0.30f, 0.75f, 0.40f);
        static readonly Color MonsterHp   = new Color(0.85f, 0.30f, 0.30f);
        const string GoldHex = "#E8B84A";

        DungeonRun run;
        Font font;
        readonly Queue<string> logs = new Queue<string>();
        bool dirty = true;
        bool showStash = true;

        // 화면 요소 참조
        Text statusText, dungeonTitle, dungeonInfo, heroLabel, monsterLabel, logText, stashHeader;
        RectTransform heroFill, monsterFill, stashList, smithList;
        Button prevBtn, nextBtn, enterBtn, returnBtn, equipBestBtn, sellJunkBtn, tabStash, tabSmith;
        GameObject stashPage, smithPage;
        Button potionBtn, scrollBtn, buyPotionBtn, buyScrollBtn, resetBtn, modeBtn;
        Text potionText, scrollText, modeText, returnText;
        GameObject background, townPanel, mapHud;
        static readonly Color ExpColor = new Color(0.55f, 0.45f, 0.95f);
        Text levelLabel, pointsText, speedText, buffText;
        RectTransform expFill;
        readonly Button[] statBtns = new Button[3];
        readonly Text[] statTexts = new Text[3];
        Button speedBtn;
        GameObject titleScreen;

        // 세로 모바일 레이아웃 요소
        RectTransform canvasRoot;
        GameObject townTitleRow, townActionRow, skillRow, itemRow, joystickArea, monsterBox, statPopup;
        Button statOpenBtn;
        Text statOpenText, statInfoText;
        RectTransform bottomPanel;
        GameObject logBoxGo, resultPanel;
        LayoutElement dungeonInfoLe;
        Text resultTitle, resultBody, resultNextText;
        DungeonRun.FloorResultInfo shownResult;

        class SkillSlot { public Button btn; public RectTransform cdFill; public Text label; public Text cdText; }
        readonly SkillSlot[] skillSlots = new SkillSlot[SkillDef.All.Length];
        GameObject popup;
        Text popupText;
        Button popupCancel;
        Action popupConfirm;

        // ───────── 생명주기 ─────────

        void Awake()
        {
            run = GetComponent<DungeonRun>();
            // 4단계 컴포넌트가 없으면 자동으로 붙임 (스프라이트를 연결하려면 직접 붙여 두는 걸 권장)
            if (GetComponent<PlayerController>() == null) gameObject.AddComponent<PlayerController>();
            if (GetComponent<DungeonView>() == null) gameObject.AddComponent<DungeonView>();
            if (GetComponent<FxView>() == null) gameObject.AddComponent<FxView>();
            run.OnLog += AddLog;
            run.OnChanged += MarkDirty;

            // OS 폰트를 써서 한글을 확실히 표시 (Windows: 맑은 고딕, Mac: Apple SD 고딕)
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "AppleGothic", "Noto Sans CJK KR", "Arial" }, 16);

            EnsureEventSystem();
            BuildUI();
        }

        void OnDestroy()
        {
            if (run == null) return;
            run.OnLog -= AddLog;
            run.OnChanged -= MarkDirty;
        }

        void Update()
        {
            if (run.Hero == null) return;
            UpdateLive();
            if (dirty)
            {
                dirty = false;
                RebuildStash();
                RebuildSmith();
            }
        }

        void MarkDirty() => dirty = true;

        void AddLog(string msg)
        {
            logs.Enqueue(msg);
            while (logs.Count > maxLogLines) logs.Dequeue();
            if (logText != null) logText.text = string.Join("\n", logs);
        }

        // ───────── 매 프레임 갱신 (HP 바, 버튼 상태) ─────────

        void UpdateLive()
        {
            var hero = run.Hero;
            var hc = hero.Combatant;
            bool inTown = run.State == RunState.Town;

            // 던전에서는 배경과 마을 패널을 숨겨 오른쪽에 맵이 보이게
            background.SetActive(inTown);
            townPanel.SetActive(inTown);

            mapHud.SetActive(!inTown);
            joystickArea.SetActive(!inTown);
            townTitleRow.SetActive(inTown);
            townActionRow.SetActive(inTown);
            skillRow.SetActive(!inTown);
            itemRow.SetActive(!inTown);

            // 카메라가 위아래 UI를 피해 영웅을 가운데 두도록 실제 비율을 알려 줌
            // 던전에서는 하단 패널을 낮춰 게임 화면을 넓게, 마을에서는 높게
            float bottomH = inTown ? BottomTownH : BottomDungeonH;
            if (!Mathf.Approximately(bottomPanel.offsetMax.y, bottomH))
                bottomPanel.offsetMax = new Vector2(0, bottomH);
            logBoxGo.SetActive(inTown);
            dungeonInfoLe.preferredHeight = inTown ? 40 : 34;

            float canvasH = canvasRoot.rect.height;
            if (canvasH > 1f)
            {
                MobileLayout.TopFraction = TopH / canvasH;
                MobileLayout.BottomFraction = bottomH / canvasH;
            }

            UpdateResultPanel();

            speedText.text = $"속도 x{run.GameSpeed}";
            ((Image)speedBtn.targetGraphic).color = run.GameSpeed > 1 ? new Color(0.30f, 0.55f, 0.85f) : ButtonColor;

            // 레벨 / 스탯
            var p = hero.progress;
            levelLabel.text = $"<b>Lv {p.level}</b>   EXP {p.exp} / {p.ExpToNext}";
            SetBar(expFill, p.ExpToNext > 0 ? (float)p.exp / p.ExpToNext : 0f);
            statOpenText.text = p.points > 0 ? $"스탯 <color=#7CFC8A><b>+{p.points}</b></color>" : "스탯";
            ((Image)statOpenBtn.targetGraphic).color = p.points > 0 ? new Color(0.25f, 0.45f, 0.30f) : ButtonColor;
            if (statPopup.activeSelf)
            {
                pointsText.text = p.points > 0 ? $"<color=#7CFC8A><b>남은 포인트 {p.points}</b></color>" : "<color=#888888>남은 포인트 0</color>";
                statInfoText.text =
                    $"Lv {p.level}   전투력 <b>{hero.Power}</b>\n" +
                    $"HP {hc.Stats.maxHp}   ATK {hc.Stats.attack}   DEF {hc.Stats.defense}\n" +
                    $"치명타 {p.CritChance * 100f:0}%   공격 속도 x{p.AttackSpeedMult:0.00}\n" +
                    $"무기 {ItemName(hero.weapon)}\n방어구 {ItemName(hero.armor)}";
                int[] statValues = { p.str, p.vit, p.agi };
                for (int i = 0; i < 3; i++)
                {
                    statTexts[i].text = $"{statValues[i]}  +";
                    statBtns[i].interactable = p.points > 0;
                }
            }

            // 스킬 칸
            for (int i = 0; i < skillSlots.Length; i++)
            {
                var s = skillSlots[i];
                var def = SkillDef.All[i];
                bool learned = run.SkillUnlocked(i);
                float ratio = learned ? run.SkillCooldownRatio(i) : 1f;
                s.cdFill.anchorMax = new Vector2(1f, ratio);
                s.label.text = learned
                    ? $"<b>{def.name}</b>"
                    : $"{def.name}\n<size=12><color=#FF9090>Lv {def.unlockLevel}</color></size>";
                s.cdText.text = learned && run.SkillCooldown(i) > 0f ? $"{run.SkillCooldown(i):0.0}" : "";
                ((Image)s.btn.targetGraphic).color = run.CanUseSkill(i) ? new Color(0.35f, 0.42f, 0.58f) : ButtonColor;
            }

            // 버프
            var buffs = new List<string>();
            if (!inTown) buffs.Add($"<color=#FFFFFF>무리 {run.Wave}/{run.WavesPerFloor}\n남은 {run.AliveMonsters} · 처치 {run.FloorKills}</color>");
            if (run.GuardActive) buffs.Add($"방어막 {hc.Shield}");
            if (run.FrenzyActive) buffs.Add("<color=#FF9A5A>광폭화</color>");
            buffText.text = string.Join("\n", buffs);
            modeText.text = run.AutoMode ? "▶ 자동 ON" : "■ 수동\n<size=12>눌러서 자동</size>";
            ((Image)modeBtn.targetGraphic).color = run.AutoMode ? TabOn : ButtonColor;
            returnText.text = run.State == RunState.Returning ? $"귀환 중 {run.ReturnProgress * 100f:0}%" : "귀환";

            statusText.text = $"<color={GoldHex}><b>{hero.gold}G</b></color>    전투력 <b>{hero.Power}</b>";

            int idx = run.SelectedIndex;
            var d = run.Dungeon;
            bool unlocked = run.IsUnlocked(idx);
            dungeonTitle.text = unlocked ? $"<b>{d.dungeonName}</b>" : $"<b>{d.dungeonName}</b>  <color=#FF7070>(잠김)</color>";

            if (inTown)
            {
                dungeonInfo.text = $"{d.description}\n필요 전투력 {d.requiredPower}   최고 기록 {run.BestFloor(idx)}층   " +
                                   $"({idx + 1}/{run.Dungeons.Count})";
            }
            else
            {
                var floor = d.GetFloor(run.Depth);
                string ret = run.State == RunState.Returning ? "   <color=#8FD3FF>귀환 중...</color>" : "";
                string stairs = run.ClearingFloor || run.FloorResultPending ? "   <color=#F2CC4D>클리어!</color>" : "";
                dungeonInfo.text = $"<b>{d.dungeonName}</b> {run.Depth + 1}층 · {floor.floorName}{stairs}{ret}\n" +
                                   $"소지 {hero.carriedLoot.Count}개 / <color={GoldHex}>{hero.carriedGold}G</color>  (죽으면 절반 손실)";
            }

            prevBtn.interactable = nextBtn.interactable = inTown && run.Dungeons.Count > 1;
            enterBtn.interactable = inTown && unlocked;
            returnBtn.interactable = run.State == RunState.InDungeon;
            equipBestBtn.interactable = sellJunkBtn.interactable = inTown;

            potionText.text = $"포션 {hero.potions}";
            scrollText.text = $"두루마리 {hero.scrolls}";
            potionBtn.interactable = run.CanUsePotion;
            scrollBtn.interactable = run.CanUseScroll;
            buyPotionBtn.interactable = inTown && hero.gold >= run.Consumables.potionPrice;
            buyScrollBtn.interactable = inTown && hero.gold >= run.Consumables.scrollPrice;
            resetBtn.interactable = inTown;

            var report = run.ConsumeOfflineReport();
            if (report != null) ShowPopup(report, null);

            heroLabel.text = $"HP  {hc.CurrentHp} / {hc.Stats.maxHp}" +
                             $"   <size=12><color=#B0B0B0>ATK {hc.Stats.attack}  DEF {hc.Stats.defense}</color></size>";
            SetBar(heroFill, hc.Stats.maxHp > 0 ? (float)hc.CurrentHp / hc.Stats.maxHp : 0f);

            var m = run.CurrentMonster;
            monsterBox.SetActive(m != null);
            if (m != null)
            {
                monsterLabel.text = $"{m.Name}  {m.CurrentHp} / {m.Stats.maxHp}";
                SetBar(monsterFill, m.Stats.maxHp > 0 ? (float)m.CurrentHp / m.Stats.maxHp : 0f);
            }
            else
            {
                monsterLabel.text = inTown ? "마을에서 휴식 중" : "다음 상대를 찾는 중...";
                SetBar(monsterFill, 0f);
            }
        }

        // ───────── 목록 다시 만들기 (변화가 있을 때만) ─────────

        void RebuildStash()
        {
            Clear(stashList);
            var hero = run.Hero;
            bool inTown = run.State == RunState.Town;
            stashHeader.text = inTown
                ? $"창고 {hero.stash.Count}개"
                : $"창고 {hero.stash.Count}개   <color=#AAAAAA>(던전 중에는 장착/판매 불가)</color>";

            // 정렬: 무기 → 방어구 → 재료/보물, 같은 종류는 전투력·가격 높은 순
            var items = new List<ItemInstance>(hero.stash);
            items.Sort((a, b) =>
            {
                int c = a.Slot.CompareTo(b.Slot);
                if (c != 0) return c;
                c = b.Power.CompareTo(a.Power);
                return c != 0 ? c : b.SellPrice.CompareTo(a.SellPrice);
            });

            if (items.Count == 0)
            {
                AddText(stashList, "비어 있음. 던전에서 전리품을 가져오세요.", 15, TextAnchor.MiddleCenter, Color.gray)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            }

            foreach (var item in items)
            {
                var row = AddRow(stashList, 50);
                string compare = "";
                if (item.IsEquipment)
                {
                    var eq = hero.GetEquipped(item.Slot);
                    int diff = item.Power - (eq != null ? eq.Power : 0);
                    compare = diff > 0 ? $"  <color=#7CFC8A>▲{diff}</color>" : diff < 0 ? $"  <color=#FF8080>▼{-diff}</color>" : "  =";
                }
                var label = AddText(row, $"{Colored(item)}{compare}\n<size=12><color=#B0B0B0>{SlotName(item.Slot)}  {StatLine(item.Bonus)}  판매 {item.SellPrice}G</color></size>", 15);
                label.Flex();

                var captured = item;
                if (item.IsEquipment)
                    AddButton(row, "장착", () => run.Equip(captured), 64).interactable = inTown;
                AddButton(row, "판매", () => run.Sell(captured), 64).interactable = inTown;
            }
        }

        void RebuildSmith()
        {
            Clear(smithList);
            var hero = run.Hero;
            bool inTown = run.State == RunState.Town;

            AddText(smithList, $"장착 중인 장비를 골드로 강화합니다. 단계마다 기본 수치의 25%씩 오르고, 최대 +{ItemInstance.MaxLevel}입니다.",
                    14, TextAnchor.MiddleLeft, new Color(0.75f, 0.75f, 0.75f))
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 44;

            foreach (var slot in new[] { ItemSlot.Weapon, ItemSlot.Armor })
            {
                var item = hero.GetEquipped(slot);
                var row = AddRow(smithList, 84);

                if (item == null)
                {
                    AddText(row, $"{SlotName(slot)}: 장착한 장비 없음\n<size=12><color=#B0B0B0>창고에서 장비를 먼저 장착하세요.</color></size>", 15)
                        .Flex();
                    continue;
                }

                string body;
                if (item.CanUpgrade)
                {
                    bool afford = hero.gold >= item.UpgradeCost;
                    string costColor = afford ? GoldHex : "#FF7070";
                    body = $"{SlotName(slot)}: {Colored(item)}\n" +
                           $"<size=13>{StatLine(item.Bonus)}  →  <color=#7CFC8A>{StatLine(item.NextBonus)}</color></size>\n" +
                           $"<size=13>비용 <color={costColor}>{item.UpgradeCost}G</color></size>";
                }
                else
                {
                    body = $"{SlotName(slot)}: {Colored(item)}\n<size=13>{StatLine(item.Bonus)}</size>\n<size=13>최대 강화 완료</size>";
                }

                AddText(row, body, 15).Flex();
                var s = slot;
                AddButton(row, "강화", () => run.Upgrade(s), 80).interactable =
                    inTown && item.CanUpgrade && hero.gold >= item.UpgradeCost;
            }

            // ── 합성 ──
            AddText(smithList, "<b>합성</b>  <size=13><color=#B0B0B0>창고의 같은 장비를 모아 상위 장비로 만듭니다. 가장 높은 강화 단계를 이어받습니다.</color></size>",
                    15, TextAnchor.MiddleLeft)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 48;

            var groups = Crafting.MergeGroups(hero);
            if (groups.Count == 0)
            {
                AddText(smithList, "합성할 수 있는 장비가 창고에 없습니다.", 14, TextAnchor.MiddleCenter, Color.gray)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            }
            foreach (var (data, count) in groups)
            {
                var row = AddRow(smithList, 56);
                bool ready = count >= data.mergeCount;
                string countColor = ready ? "#7CFC8A" : "#FF8080";
                AddText(row,
                    $"<color={RarityHex(data.rarity)}>{data.displayName}</color> <color={countColor}>{count}/{data.mergeCount}</color>" +
                    $"  →  <color={RarityHex(data.mergeResult.rarity)}>{data.mergeResult.displayName}</color>\n" +
                    $"<size=12><color=#B0B0B0>{StatLine(data.mergeResult.bonus)} (강화 전 기준)</color></size>", 15).Flex();
                var captured = data;
                AddButton(row, "합성", () => run.Merge(captured), 80).interactable = inTown && ready;
            }
        }

        // ───────── 화면 구성 (한 번만 실행) ─────────

        // 세로 모바일 레이아웃 (캔버스 기준 540 x 960, 가로폭 기준으로 크기 맞춤)
        const float TopH = 112f;            // 상단 바 높이
        const float BottomTownH = 330f;     // 마을: 하단 패널 높이 (던전 선택/입장/로그)
        const float BottomDungeonH = 186f;  // 던전: 하단 패널 높이 (게임 화면을 넓게)

        void BuildUI()
        {
            var canvasGo = new GameObject("GameUI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;   // 가로폭 기준: 기기마다 세로 길이만 달라짐
            var root = (RectTransform)canvasGo.transform;
            canvasRoot = root;

            // 마을 배경 (던전에서는 숨겨서 맵이 보이게)
            var bg = NewRect("Background", root);
            Stretch(bg, Vector2.zero, Vector2.one);
            AddImage(bg, BgColor);
            background = bg.gameObject;

            BuildJoystick(root);
            BuildMapHud(root);
            BuildTopBar(root);
            BuildBottomPanel(root);
            BuildTownPanel(root);
            BuildStatPopup(root);
            BuildResultPanel(root);
            BuildPopup(root);
            BuildTitle(root);
            ShowTab(true);
            AddLog("던전 입장을 눌러 시작하세요. 레벨업하면 스탯 포인트를 나눠 주세요.");
        }

        /// <summary>상단 바 (불투명): 골드, 전투력, 영웅 HP, 레벨/경험치.</summary>
        void BuildTopBar(RectTransform root)
        {
            var top = NewRect("TopBar", root);
            Stretch(top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -TopH), Vector2.zero);
            AddImage(top, PanelColor);
            AddVertical(top, 4, 8);

            var row = AddHorizontal(top, 30);
            statusText = AddText(row, "", 17);
            statusText.Flex();
            statOpenBtn = AddButton(row, "스탯", () => ShowStatPopup(true), 110);
            statOpenText = statOpenBtn.GetComponentInChildren<Text>();

            heroLabel = AddText(top, "", 15);
            heroLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
            heroFill = AddBar(top, HeroHp);
            ((RectTransform)heroFill.parent).GetComponent<LayoutElement>().preferredHeight = 12;

            levelLabel = AddText(top, "", 13);
            levelLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;
            expFill = AddBar(top, ExpColor);
            ((RectTransform)expFill.parent).GetComponent<LayoutElement>().preferredHeight = 6;
        }

        /// <summary>하단 패널 (불투명): 던전 정보, 스킬, 아이템, 로그. 마을에서는 던전 선택/입장.</summary>
        void BuildBottomPanel(RectTransform root)
        {
            var bottom = NewRect("BottomPanel", root);
            Stretch(bottom, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, BottomTownH));
            AddImage(bottom, PanelColor);
            AddVertical(bottom, 6, 8);
            bottomPanel = bottom;

            // 마을: 던전 고르기
            var titleRowRt = AddHorizontal(bottom, 44);
            prevBtn = AddButton(titleRowRt, "◀", () => run.SelectDungeon(-1), 56);
            dungeonTitle = AddText(titleRowRt, "", 20, TextAnchor.MiddleCenter);
            dungeonTitle.Flex();
            nextBtn = AddButton(titleRowRt, "▶", () => run.SelectDungeon(1), 56);
            townTitleRow = titleRowRt.gameObject;

            dungeonInfo = AddText(bottom, "", 14, TextAnchor.UpperLeft, new Color(0.82f, 0.82f, 0.82f));
            dungeonInfoLe = dungeonInfo.gameObject.AddComponent<LayoutElement>();
            dungeonInfoLe.preferredHeight = 40;

            // 던전: 스킬 4칸
            var skillRowRt = AddHorizontal(bottom, 76);
            BuildSkillSlots(skillRowRt);
            skillRow = skillRowRt.gameObject;

            // 던전: 포션 / 두루마리 / 귀환
            var itemRowRt = AddHorizontal(bottom, 44);
            potionBtn = AddButton(itemRowRt, "포션", run.UsePotion);
            potionText = potionBtn.GetComponentInChildren<Text>();
            scrollBtn = AddButton(itemRowRt, "두루마리", run.UseScroll);
            scrollText = scrollBtn.GetComponentInChildren<Text>();
            returnBtn = AddButton(itemRowRt, "귀환", run.RequestReturn);
            returnText = returnBtn.GetComponentInChildren<Text>();
            itemRow = itemRowRt.gameObject;

            // 마을: 입장 / 초기화
            var townRowRt = AddHorizontal(bottom, 64);
            enterBtn = AddButton(townRowRt, "던전 입장", run.EnterDungeon);
            ((Image)enterBtn.targetGraphic).color = TabOn;
            var enterText = enterBtn.GetComponentInChildren<Text>();
            enterText.fontSize = 22;
            enterText.fontStyle = FontStyle.Bold;
            resetBtn = AddButton(townRowRt, "초기화", () =>
                ShowPopup("저장된 진행 상황을 모두 지우고 처음부터 시작할까요?", run.ResetProgress), 100);
            townActionRow = townRowRt.gameObject;

            // 로그 (남는 높이를 차지)
            var logBox = NewRect("LogBox", bottom);
            AddImage(logBox, new Color(0, 0, 0, 0.3f));
            var logLe = logBox.gameObject.AddComponent<LayoutElement>();
            logLe.flexibleHeight = 1;
            logLe.minHeight = 40;
            logBoxGo = logBox.gameObject;
            logText = AddText(logBox, "", 13, TextAnchor.LowerLeft, new Color(0.85f, 0.85f, 0.85f));
            Stretch(logText.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -4));
        }

        /// <summary>마을 가운데 영역: 창고·상점 / 대장간 탭 (불투명).</summary>
        void BuildTownPanel(RectTransform root)
        {
            var mid = NewRect("TownPanel", root);
            Stretch(mid, Vector2.zero, Vector2.one, new Vector2(0, BottomTownH + 6), new Vector2(0, -TopH - 6));
            AddImage(mid, PanelColor);
            townPanel = mid.gameObject;
            AddVertical(mid, 7, 10);

            var tabRow = AddHorizontal(mid, 46);
            tabStash = AddButton(tabRow, "창고 / 상점", () => ShowTab(true));
            tabSmith = AddButton(tabRow, "대장간", () => ShowTab(false));

            // 창고 페이지
            stashPage = NewRect("StashPage", mid).gameObject;
            stashPage.AddComponent<LayoutElement>().flexibleHeight = 1;
            AddVertical((RectTransform)stashPage.transform, 6, 0);
            stashHeader = AddText(stashPage.transform, "", 15);
            stashHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            var shopRow = AddHorizontal(stashPage.transform, 46);
            buyPotionBtn = AddButton(shopRow, $"포션 구매 {run.Consumables.potionPrice}G", run.BuyPotion);
            buyScrollBtn = AddButton(shopRow, $"두루마리 구매 {run.Consumables.scrollPrice}G", run.BuyScroll);
            var stashBtns = AddHorizontal(stashPage.transform, 46);
            equipBestBtn = AddButton(stashBtns, "최고 장비 장착", run.EquipBest);
            sellJunkBtn = AddButton(stashBtns, "잡동사니 일괄 판매", run.SellJunk);
            stashList = AddScrollList(stashPage.transform);

            // 대장간 페이지
            smithPage = NewRect("SmithPage", mid).gameObject;
            smithPage.AddComponent<LayoutElement>().flexibleHeight = 1;
            AddVertical((RectTransform)smithPage.transform, 6, 0);
            smithList = AddScrollList(smithPage.transform);
        }

        /// <summary>맵 영역 전체가 조이스틱 터치 영역 (투명). 위아래 UI보다 아래에 깔립니다.</summary>
        void BuildJoystick(RectTransform root)
        {
            var areaRt = NewRect("JoystickArea", root);
            Stretch(areaRt, Vector2.zero, Vector2.one, new Vector2(0, BottomDungeonH), new Vector2(0, -TopH));
            AddImage(areaRt, new Color(0, 0, 0, 0f));   // 터치만 받는 투명 판
            joystickArea = areaRt.gameObject;

            var baseRt = NewRect("Base", areaRt);
            baseRt.anchorMin = baseRt.anchorMax = new Vector2(0.5f, 0.5f);
            baseRt.sizeDelta = new Vector2(104, 104);   // 작게: 게임 화면을 덜 가림
            var baseImg = AddImage(baseRt, new Color(1f, 1f, 1f, 0.55f));
            baseImg.sprite = VirtualJoystick.CircleSprite(false);
            baseImg.raycastTarget = false;

            var knob = NewRect("Knob", baseRt);
            knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(46, 46);
            var knobImg = AddImage(knob, new Color(1f, 1f, 1f, 0.8f));
            knobImg.sprite = VirtualJoystick.CircleSprite(true);
            knobImg.raycastTarget = false;

            areaRt.gameObject.AddComponent<VirtualJoystick>().Init(baseRt, knob, 44f);
        }

        /// <summary>맵 위에 떠 있는 정보: 왼쪽 위 현재 상대, 오른쪽 위 자동/배속 버튼과 무리 정보.</summary>
        void BuildMapHud(RectTransform root)
        {
            var hud = NewRect("MapHud", root);
            Stretch(hud, Vector2.zero, Vector2.one, new Vector2(10, BottomDungeonH + 10), new Vector2(-10, -TopH - 10));
            mapHud = hud.gameObject;

            // 오른쪽 위: 자동 전투 / 배속
            var right = NewRect("Right", hud);
            right.anchorMin = right.anchorMax = right.pivot = new Vector2(1, 1);
            right.sizeDelta = new Vector2(150, 56);
            modeBtn = AddButton(right, "자동", run.ToggleAuto);
            Stretch((RectTransform)modeBtn.transform, Vector2.zero, Vector2.one);
            modeText = modeBtn.GetComponentInChildren<Text>();
            modeText.fontSize = 18;
            modeText.fontStyle = FontStyle.Bold;
            modeBtn.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);

            var sp = NewRect("SpeedButton", right);
            sp.anchorMin = new Vector2(0, 0);
            sp.anchorMax = new Vector2(1, 0);
            sp.pivot = new Vector2(0.5f, 1f);
            sp.anchoredPosition = new Vector2(0, -6);
            sp.sizeDelta = new Vector2(0, 40);
            speedBtn = AddButton(sp, "x1", run.CycleSpeed);
            Stretch((RectTransform)speedBtn.transform, Vector2.zero, Vector2.one);
            speedText = speedBtn.GetComponentInChildren<Text>();
            speedText.fontSize = 16;

            buffText = AddText(right, "", 14, TextAnchor.UpperRight, new Color(0.75f, 0.9f, 1f));
            var brt = buffText.rectTransform;
            brt.anchorMin = new Vector2(-0.6f, 0);
            brt.anchorMax = new Vector2(1, 0);
            brt.pivot = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(0, -52);
            brt.sizeDelta = new Vector2(0, 80);
            buffText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);

            // 왼쪽 위: 지금 싸우는 상대
            var left = NewRect("Target", hud);
            left.anchorMin = left.anchorMax = left.pivot = new Vector2(0, 1);
            left.sizeDelta = new Vector2(250, 44);
            monsterLabel = AddText(left, "", 14);
            Stretch(monsterLabel.rectTransform, new Vector2(0, 0.45f), Vector2.one);
            monsterLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
            var barRt = NewRect("Bar", left);
            Stretch(barRt, Vector2.zero, new Vector2(1, 0.4f));
            AddImage(barRt, new Color(0, 0, 0, 0.55f));
            monsterFill = NewRect("Fill", barRt);
            Stretch(monsterFill, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
            AddImage(monsterFill, MonsterHp);
            monsterBox = left.gameObject;
        }

        /// <summary>스킬 4칸. 누르거나 1~4 키로 사용. 쿨타임은 어둡게 덮였다가 위에서부터 걷힙니다.</summary>
        void BuildSkillSlots(RectTransform row)
        {
            for (int i = 0; i < SkillDef.All.Length; i++)
            {
                int index = i;
                var btn = AddButton(row, "", () => run.UseSkill(index));
                var label = btn.GetComponentInChildren<Text>();
                label.fontSize = 15;
                Stretch(label.rectTransform, new Vector2(0, 0.45f), Vector2.one);   // 이름은 위쪽

                var cd = NewRect("Cooldown", btn.transform);
                Stretch(cd, Vector2.zero, new Vector2(1, 0));
                AddImage(cd, new Color(0, 0, 0, 0.65f)).raycastTarget = false;
                var cdText = AddText(btn.transform, "", 22, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f));
                Stretch(cdText.rectTransform, Vector2.zero, new Vector2(1, 0.5f));   // 남은 시간은 아래쪽
                cdText.fontStyle = FontStyle.Bold;
                cdText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
                label.transform.SetAsLastSibling();
                cdText.transform.SetAsLastSibling();

                skillSlots[i] = new SkillSlot { btn = btn, cdFill = cd, label = label, cdText = cdText };
            }
        }

        /// <summary>스탯 분배 창. 상단 바의 스탯 버튼으로 열고, 던전 안에서도 쓸 수 있습니다.</summary>
        void BuildStatPopup(RectTransform root)
        {
            var dim = NewRect("StatPopup", root);
            Stretch(dim, Vector2.zero, Vector2.one);
            AddImage(dim, new Color(0, 0, 0, 0.6f));
            statPopup = dim.gameObject;

            var box = NewRect("Box", dim);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(480, 470);
            AddImage(box, PanelColor);
            AddVertical(box, 10, 18);

            var title = AddText(box, "<b>스탯</b>", 22, TextAnchor.MiddleCenter);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;

            statInfoText = AddText(box, "", 15, TextAnchor.UpperLeft, new Color(0.85f, 0.85f, 0.85f));
            statInfoText.gameObject.AddComponent<LayoutElement>().preferredHeight = 120;

            pointsText = AddText(box, "", 18, TextAnchor.MiddleCenter);
            pointsText.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

            for (int i = 0; i < 3; i++)
            {
                var kind = (StatKind)i;
                var r = AddHorizontal(box, 54);
                var desc = AddText(r, $"<b>{HeroProgress.Name(kind)}</b>\n<size=13><color=#B0B0B0>{HeroProgress.Effect(kind)}</color></size>", 16);
                desc.Flex();
                statBtns[i] = AddButton(r, "+", () => run.AddStat(kind), 130);
                statTexts[i] = statBtns[i].GetComponentInChildren<Text>();
                statTexts[i].fontSize = 18;
            }

            AddButton(box, "닫기", () => ShowStatPopup(false)).GetComponent<LayoutElement>().preferredHeight = 48;
            statPopup.SetActive(false);
        }

        void ShowStatPopup(bool show) => statPopup.SetActive(show);

        /// <summary>층 결과창. 몹을 다 잡으면 뜨고, 다음 층 / 마을 복귀를 고릅니다.</summary>
        void BuildResultPanel(RectTransform root)
        {
            var dim = NewRect("FloorResult", root);
            Stretch(dim, Vector2.zero, Vector2.one);
            AddImage(dim, new Color(0, 0, 0, 0.7f));
            resultPanel = dim.gameObject;

            var box = NewRect("Box", dim);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(470, 520);
            AddImage(box, PanelColor);
            AddVertical(box, 12, 22);

            resultTitle = AddText(box, "", 30, TextAnchor.MiddleCenter, new Color(0.95f, 0.78f, 0.3f));
            resultTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;
            resultTitle.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);

            resultBody = AddText(box, "", 18, TextAnchor.UpperLeft);
            resultBody.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;

            var warn = AddText(box, "<size=13><color=#B0B0B0>다음 층은 더 강합니다. 죽으면 들고 있는 전리품의 절반을 잃어요.</color></size>",
                               13, TextAnchor.MiddleCenter);
            warn.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;

            var next = AddButton(box, "다음 층으로 ▶", () => run.ContinueNextFloor());
            next.GetComponent<LayoutElement>().preferredHeight = 64;
            ((Image)next.targetGraphic).color = TabOn;
            var nt = next.GetComponentInChildren<Text>();
            resultNextText = nt;
            nt.fontSize = 22;
            nt.fontStyle = FontStyle.Bold;

            var home = AddButton(box, "마을로 복귀 (전리품 보관)", () => run.ReturnFromResult());
            home.GetComponent<LayoutElement>().preferredHeight = 52;
            home.GetComponentInChildren<Text>().fontSize = 18;

            resultPanel.SetActive(false);
        }

        void UpdateResultPanel()
        {
            bool show = run.FloorResultPending && run.FloorResult != null;
            if (resultPanel.activeSelf != show) resultPanel.SetActive(show);
            if (show)
                resultNextText.text = run.AutoContinueEnabled
                    ? $"다음 층으로 ▶  <size=16>({Mathf.CeilToInt(Mathf.Max(0f, run.ResultCountdown))}초 뒤 자동)</size>"
                    : "다음 층으로 ▶";
            if (!show || shownResult == run.FloorResult) return;

            var r = run.FloorResult;
            shownResult = r;
            int mins = Mathf.FloorToInt(r.seconds / 60f), secs = Mathf.FloorToInt(r.seconds % 60f);
            resultTitle.text = $"<b>{r.floor}층 클리어!</b>";
            resultBody.text =
                $"처치            <b>{r.kills}마리</b>\n" +
                $"걸린 시간     <b>{mins}분 {secs:00}초</b>\n" +
                $"경험치         <b>+{r.exp}</b>\n" +
                $"골드            <color={GoldHex}><b>+{r.gold}G</b></color>\n" +
                $"아이템         <b>+{r.items}개</b>\n\n" +
                $"<size=15><color=#B0B0B0>지금 들고 있는 전리품: 아이템 {r.totalItems}개 · {r.totalGold}G</color></size>";
        }

        /// <summary>처음 켰을 때 보이는 타이틀 화면.</summary>
        void BuildTitle(RectTransform root)
        {
            var t = NewRect("Title", root);
            Stretch(t, Vector2.zero, Vector2.one);
            AddImage(t, new Color(0.03f, 0.03f, 0.05f, 1f));
            titleScreen = t.gameObject;

            var title = AddText(t, "<b>LOOT\nHUNTERS</b>", 64, TextAnchor.MiddleCenter, new Color(0.95f, 0.75f, 0.3f));
            Stretch(title.rectTransform, new Vector2(0, 0.58f), new Vector2(1, 0.85f));
            title.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);

            var sub = AddText(t, "던전에서 몬스터 무리를 사냥하고\n전리품을 들고 살아서 돌아오세요.\n죽으면 들고 있던 전리품의 절반을 잃습니다.",
                              17, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f));
            Stretch(sub.rectTransform, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.56f));

            var btnRt = NewRect("Start", t);
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0.3f);
            btnRt.sizeDelta = new Vector2(300, 76);
            var start = AddButton(btnRt, "게임 시작", () => titleScreen.SetActive(false));
            Stretch((RectTransform)start.transform, Vector2.zero, Vector2.one);
            ((Image)start.targetGraphic).color = TabOn;
            var st = start.GetComponentInChildren<Text>();
            st.fontSize = 26;
            st.fontStyle = FontStyle.Bold;

            var keys = AddText(t, "자동 전투가 기본입니다.\n화면을 끌면 직접 이동, 자동 버튼으로 다시 자동 전투.",
                               14, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.6f));
            Stretch(keys.rectTransform, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.2f));
        }

        /// <summary>화면 전체를 덮는 안내창. 맨 마지막에 만들어서 항상 위에 보입니다.</summary>
        void BuildPopup(RectTransform root)
        {
            var dim = NewRect("Popup", root);
            Stretch(dim, Vector2.zero, Vector2.one);
            AddImage(dim, new Color(0, 0, 0, 0.6f));   // 뒤쪽 버튼 클릭 차단
            popup = dim.gameObject;

            var box = NewRect("Box", dim);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(480, 300);
            AddImage(box, PanelColor);
            AddVertical(box, 12, 20);

            popupText = AddText(box, "", 17, TextAnchor.MiddleCenter);
            popupText.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;

            var row = AddHorizontal(box, 44);
            AddButton(row, "확인", () =>
            {
                popup.SetActive(false);
                var action = popupConfirm;
                popupConfirm = null;
                action?.Invoke();
            });
            popupCancel = AddButton(row, "취소", () => { popup.SetActive(false); popupConfirm = null; });

            popup.SetActive(false);
        }

        /// <summary>onConfirm이 null이면 확인 버튼만, 있으면 확인/취소를 보여 줍니다.</summary>
        void ShowPopup(string message, Action onConfirm)
        {
            popupText.text = message;
            popupConfirm = onConfirm;
            popupCancel.gameObject.SetActive(onConfirm != null);
            popup.SetActive(true);
        }

        void ShowTab(bool stash)
        {
            showStash = stash;
            stashPage.SetActive(showStash);
            smithPage.SetActive(!showStash);
            ((Image)tabStash.targetGraphic).color = showStash ? TabOn : ButtonColor;
            ((Image)tabSmith.targetGraphic).color = showStash ? ButtonColor : TabOn;
            dirty = true;
        }

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            var es = new GameObject("EventSystem", typeof(EventSystem));
            // 새 Input System 패키지가 있으면 그 모듈을, 없으면 기존 입력 모듈을 씁니다.
            var newInput = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (newInput != null) es.AddComponent(newInput);
            else es.AddComponent<StandaloneInputModule>();
        }

        // ───────── UI 생성 도우미 ─────────

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin = default, Vector2 offMax = default)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        static Image AddImage(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        static void AddVertical(RectTransform rt, float spacing, int padding)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
        }

        static RectTransform AddHorizontal(Transform parent, float height)
        {
            var rt = NewRect("Row", parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleHeight = 0;   // 남는 세로 공간을 가져가지 않게 (목록/로그가 가져감)
            return rt;
        }

        static RectTransform AddRow(RectTransform list, float height)
        {
            var row = AddHorizontal(list, height);
            AddImage(row, RowColor);
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(10, 8, 4, 4);
            return row;
        }

        Text AddText(Transform parent, string text, int size, TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null)
        {
            var rt = NewRect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color ?? Color.white;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        Button AddButton(Transform parent, string label, Action onClick, float width = -1)
        {
            var rt = NewRect("Button", parent);
            var img = AddImage(rt, ButtonColor);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());

            var t = AddText(rt, label, 15, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one);

            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 44;   // 손가락으로 누르기 좋은 높이
            if (width > 0) le.preferredWidth = le.minWidth = width;
            else le.flexibleWidth = 1;
            return b;
        }

        /// <summary>HP 바. 채움 비율은 anchorMax.x로 조절합니다 (스프라이트 없이 동작).</summary>
        static RectTransform AddBar(RectTransform parent, Color fillColor)
        {
            var bar = NewRect("Bar", parent);
            AddImage(bar, new Color(0, 0, 0, 0.45f));
            bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            var fill = NewRect("Fill", bar);
            Stretch(fill, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
            AddImage(fill, fillColor);
            return fill;
        }

        static void SetBar(RectTransform fill, float ratio)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }

        static RectTransform AddScrollList(Transform parent)
        {
            var sv = NewRect("ScrollView", parent);
            AddImage(sv, new Color(0, 0, 0, 0.25f));
            var le = sv.gameObject.AddComponent<LayoutElement>();
            le.flexibleHeight = 1;
            le.minHeight = 100;

            var scroll = sv.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var viewport = NewRect("Viewport", sv);
            Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        static void Clear(RectTransform list)
        {
            for (int i = list.childCount - 1; i >= 0; i--)
            {
                var child = list.GetChild(i).gameObject;
                child.SetActive(false);   // 이번 프레임 레이아웃에서 바로 빠지도록
                Destroy(child);
            }
        }

        // ───────── 표시용 문자열 ─────────

        static string RarityHex(Rarity r) => r switch
        {
            Rarity.Rare => "#5AA9FF",
            Rarity.Epic => "#C77DFF",
            _ => "#D0D0D0",
        };

        static string Colored(ItemInstance item) => $"<color={RarityHex(item.Rarity)}>{item.DisplayName}</color>";

        static string ItemName(ItemInstance item) => item != null ? Colored(item) : "<color=#888888>없음</color>";

        static string SlotName(ItemSlot s) => s switch
        {
            ItemSlot.Weapon => "무기",
            ItemSlot.Armor => "방어구",
            ItemSlot.Material => "재료",
            _ => "보물",
        };

        static string StatLine(Stats s)
        {
            var parts = new List<string>();
            if (s.attack != 0) parts.Add($"ATK +{s.attack}");
            if (s.defense != 0) parts.Add($"DEF +{s.defense}");
            if (s.maxHp != 0) parts.Add($"HP +{s.maxHp}");
            return parts.Count > 0 ? string.Join("  ", parts) : "-";
        }
    }

    static class LayoutExtensions
    {
        /// <summary>가로 줄 안에서 남는 폭을 차지하고, 글자가 길면 줄바꿈되게 합니다.</summary>
        public static void Flex(this Text t)
        {
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.preferredWidth = 0;
            le.minWidth = 0;
        }
    }
}

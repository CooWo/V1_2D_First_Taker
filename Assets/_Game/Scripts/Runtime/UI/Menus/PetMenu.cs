using System.Text;
using ShadowRPG.Core.Pets;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>펫: 장착 / 레벨업 / 등급업 (등급마다 스킬 추가 개방). 비용은 골드.</summary>
    public sealed class PetMenu : IMenu
    {
        readonly MenuContext _ctx;
        int _selected;

        public PetMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "PetMenu");
            Panel.RowClicked += i => { _selected = i; Refresh(); };
        }

        public string ButtonLabel => "펫";
        public ListPanel Panel { get; }

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var meta = _ctx.Meta;
            var pets = meta.Content.Pets.All;
            _selected = Mathf.Clamp(_selected, 0, pets.Count - 1);

            Panel.SetTitle($"<b>펫</b>  <size=22><color=#FFD84A>{meta.Wallet.Gold:N0} G</color></size>");
            Panel.SetItems(pets.Count, i =>
            {
                var state = meta.Profile.GetOrAddPet(pets[i].Id);
                string stars = new string('★', state.Grade) + new string('☆', PetService.MaxGrade - state.Grade);
                string active = meta.Profile.ActivePetId == pets[i].Id ? " <color=#7FD8FF>[동행 중]</color>" : string.Empty;
                string marker = i == _selected ? "▶ " : string.Empty;
                return $"{marker}<color=#FFD84A>{stars}</color> {pets[i].Name}  Lv.{state.Level}/{PetService.MaxLevel(state)}{active}";
            });

            var definition = pets[_selected];
            var selected = meta.Profile.GetOrAddPet(definition.Id);
            Panel.SetDetail(Describe(definition, selected));

            bool maxLevel = selected.Level >= PetService.MaxLevel(selected);
            Panel.SetFooter(0, "동행", () => SetActive(definition.Id));
            Panel.SetFooter(1, maxLevel ? "최대 레벨" : $"레벨업 {meta.Pets.LevelUpCost(selected):N0}G", () => LevelUp(selected));
            Panel.SetFooter(2, selected.Grade >= PetService.MaxGrade ? "최대 등급" : $"등급업 {meta.Pets.GradeUpCost(selected):N0}G", () => GradeUp(selected));
        }

        string Describe(PetDefinition definition, PetState state)
        {
            float power = PetService.Power(state, _ctx.Session.Player.Combatant.Stats);
            var sb = new StringBuilder();
            sb.AppendLine($"<b>{definition.Name}</b>  효과 배율 x{power:0.00}  <size=18>(등급업은 최대 레벨에서 가능)</size>");
            foreach (var skill in definition.Skills)
            {
                bool open = skill.UnlockGrade <= state.Grade;
                string color = open ? "C8FFC8" : "5A6070";
                sb.AppendLine($"<size=19><color=#{color}>{(open ? "●" : "○")} {skill.Name} (★{skill.UnlockGrade}) — {skill.Effect.Describe(power)} · 쿨 {skill.Cooldown:0}초</color></size>");
            }
            return sb.ToString();
        }

        void SetActive(string petId)
        {
            _ctx.Meta.Profile.ActivePetId = petId;
            _ctx.Meta.RequestSave();
            _ctx.Log("동행 펫 변경 — 다음 층(또는 레이드)부터 적용");
            Refresh();
        }

        void LevelUp(PetState state)
        {
            _ctx.Log(_ctx.Meta.LevelUpPet(state) ? $"펫 레벨업 → Lv.{state.Level}" : "레벨업 불가 (골드 부족 또는 최대 레벨)");
            Refresh();
        }

        void GradeUp(PetState state)
        {
            _ctx.Log(_ctx.Meta.GradeUpPet(state) ? $"펫 등급업 → ★{state.Grade}" : "등급업 불가 (최대 레벨 필요 또는 골드 부족)");
            Refresh();
        }
    }
}

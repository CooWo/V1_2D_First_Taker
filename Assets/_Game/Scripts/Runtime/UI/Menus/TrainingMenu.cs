using ShadowRPG.Core.Growth;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>단련: 골드로 기본 체급. 단계마다 비용 증가, 한도 = 열린 ACT × 단계.</summary>
    public sealed class TrainingMenu : IMenu
    {
        static readonly TrainingTrack[] Tracks = { TrainingTrack.Attack, TrainingTrack.Life, TrainingTrack.Armor, TrainingTrack.Resist };
        readonly MenuContext _ctx;
        int _selected;

        public TrainingMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "TrainingMenu", 4);
            Panel.RowClicked += i => { _selected = Mathf.Clamp(i, 0, Tracks.Length - 1); Train(1); };
        }

        public string ButtonLabel => "단련";
        public ListPanel Panel { get; }

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var meta = _ctx.Meta;
            var t = meta.Training;
            int cap = t.Cap(meta.ActsOpened);
            Panel.SetTitle($"<b>단련</b>  <size=20>한도 {cap}단계 (ACT {meta.ActsOpened})  <color=#FFD84A>{meta.Wallet.Gold:N0} G</color></size>");
            Panel.SetItems(Tracks.Length, i =>
            {
                var track = Tracks[i];
                int level = t.LevelOf(track);
                string unit = track == TrainingTrack.Resist ? "%" : "%";
                string cost = level >= cap ? "<color=#FF7A6A>한도</color>" : $"<color=#FFD84A>{t.Cost(track):N0}G</color>";
                return $"{(i == _selected ? "▶ " : string.Empty)}<b>{t.Name(track)}</b> {level}/{cap}단계  +{level * t.EffectPerLevel(track):0.#}{unit}  <size=18>다음 {cost}</size>";
            });
            Panel.SetDetail("<size=19>골드는 기본 체급을 올려 주지만 ACT마다 한도가 있습니다.\n벽은 빌드(아이템·트리)로 넘고, 다음 ACT를 열면 한도가 늘어납니다.</size>");
            Panel.SetFooter(0, "단련 ×1", () => Train(1));
            Panel.SetFooter(1, "단련 ×10", () => Train(10));
            Panel.SetFooter(2, "닫기", () => Panel.SetVisible(false));
        }

        void Train(int times)
        {
            string error = null;
            int done = 0;
            for (int i = 0; i < times; i++)
            {
                error = _ctx.Meta.Train(_ctx.Session.Player, Tracks[_selected]);
                if (error != null) break;
                done++;
            }
            if (done > 0) _ctx.Log($"{_ctx.Meta.Training.Name(Tracks[_selected])} 단련 +{done}");
            if (error != null) _ctx.Log(error);
            Refresh();
        }
    }
}

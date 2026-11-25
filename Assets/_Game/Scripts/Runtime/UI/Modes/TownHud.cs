using ShadowRPG.Core.Dungeon;
using ShadowRPG.Core.Town;
using ShadowRPG.Runtime.UI.Menus;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Modes
{
    /// <summary>마을 HUD: 마을 정보 + 다른 캐릭터 이름표.</summary>
    public sealed class TownHud : IHudModule
    {
        readonly TownSession _session;
        readonly MenuContext _ctx;
        readonly UnityEngine.UI.Text _info;

        public TownHud(RectTransform root, TownSession session, MenuContext ctx)
        {
            _session = session;
            _ctx = ctx;
            _info = UiFactory.Label(UiFactory.Anchored(root, "TownInfo", new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(470f, 200f)),
                string.Empty, 22, TextAnchor.UpperRight, Color.white);

            var tags = NameTagLayer.Create(root, Camera.main);
            foreach (var resident in session.Residents)
            {
                var r = resident;
                tags.Add($"{r.DisplayName}\n<size=14><color=#8FA8D0>{r.Title}</color></size>", () => r.Combatant.Position);
            }
        }

        public void Refresh(float dt)
        {
            var meta = _ctx.Meta;
            var bag = meta.Profile.Bag;
            _info.text =
                $"<b>{_session.Layout.Name}</b>\n" +
                $"<size=19>개방 던전 {meta.Profile.UnlockedDungeons}/{meta.Content.Acts.TotalDungeons()} · 레이드 {meta.RaidEntries.Remaining}/{meta.RaidEntries.MaxPerDay}</size>\n" +
                $"<size=19>가방 {bag.UsedCells}/{bag.Capacity}칸 · 창고 {meta.Profile.Stash.UsedCells}/{meta.Profile.Stash.Capacity}칸</size>\n" +
                $"<size=19>마을 사람 {_session.Residents.Count}명</size>\n" +
                $"<color=#FFD84A>{meta.Wallet.Gold:N0} G</color>";
        }
    }
}

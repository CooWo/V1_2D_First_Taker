using ShadowRPG.Core.Character;
using ShadowRPG.Core.Items;

namespace ShadowRPG.Runtime.Art
{
    /// <summary>
    /// 플레이어 외형 키 = "직업/무기 외형". 장비를 바꾸면 키가 바뀌고, 카탈로그에서 다른 그림을 찾습니다.
    /// 새 외형을 추가할 때: 굽기 설명서(키) 추가 → 여기서 무기 종류와 키를 연결.
    /// </summary>
    public static class AppearanceResolver
    {
        public static string ForPlayer(PlayerCharacter player)
        {
            string cls = player.Class.Current?.Id ?? "knight";
            var weapon = player.Equipment.Get(EquipSlot.RightHand);
            var sub = weapon != null ? weapon.Subtype : WeaponSubtype.None;
            switch (cls)
            {
                case "mage": return sub == WeaponSubtype.Wand ? "mage/wand_book" : "mage/staff";
                case "archer": return sub == WeaponSubtype.Bow ? "archer/crossbow" : "archer/daggers";
                case "monk": return sub == WeaponSubtype.CombatGlove || sub == WeaponSubtype.None ? "monk/fists" : "monk/axes";
                default: return sub == WeaponSubtype.Greatsword ? "knight/2h_sword" : "knight/sword_shield";
            }
        }
    }
}

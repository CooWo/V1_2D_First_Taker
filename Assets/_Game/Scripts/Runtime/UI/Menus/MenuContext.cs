using System;
using ShadowRPG.Core.Game;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>모드 전환 창구 (마을 / 던전 / 레이드). UI는 전환 방법을 모릅니다.</summary>
    public interface IModeSwitcher
    {
        void EnterTown();
        void EnterDungeon(int globalDungeon);
        bool TryEnterRaid(out string reason);
    }

    /// <summary>메뉴들이 공유하는 의존성 묶음.</summary>
    public sealed class MenuContext
    {
        public GameMeta Meta;
        public ICombatSession Session;
        public IModeSwitcher Modes;
        public Action<string> Log;
    }

    /// <summary>HUD 메뉴 하나 (장비·가방·창고·트리·펫·던전·레이드).</summary>
    public interface IMenu
    {
        string ButtonLabel { get; }
        ListPanel Panel { get; }
        void Refresh();
    }
}

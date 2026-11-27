using System;
using ShadowRPG.Core.Meta;
using UnityEngine;

namespace ShadowRPG.Runtime.UI.Menus
{
    /// <summary>설정: 전투(자동 진행·자동 장착·배속·자동 이동) · 리듬(원 위치) · 연출 · 그래픽.</summary>
    public sealed class SettingsMenu : IMenu
    {
        readonly MenuContext _ctx;
        readonly (string label, Func<PlayerSettings, string> value, Action<PlayerSettings> toggle)[] _rows;

        public SettingsMenu(Transform parent, MenuContext ctx)
        {
            _ctx = ctx;
            Panel = new ListPanel(parent, "SettingsMenu", 10);
            Panel.RowClicked += OnRow;
            _rows = new (string, Func<PlayerSettings, string>, Action<PlayerSettings>)[]
            {
                ("층 클리어 5초 뒤 자동 진행", s => OnOff(s.AutoContinue), s => s.AutoContinue = !s.AutoContinue),
                ("귀환 시 더 좋은 장비 자동 장착", s => OnOff(s.AutoEquipOnReturn), s => s.AutoEquipOnReturn = !s.AutoEquipOnReturn),
                ("기본 배속", s => $"×{s.Speed:0}", s => s.Speed = NextSpeed(s.Speed)),
                ("자동 이동", s => OnOff(s.AutoMove), s => { s.AutoMove = !s.AutoMove; if (_ctx.Session.Player != null) _ctx.Session.Player.AutoMove = s.AutoMove; }),
                ("리듬 원 위치", s => s.RhythmRingX >= 0f && s.RhythmRingX < 0.5f ? "왼쪽" : "오른쪽", s => s.RhythmRingX = s.RhythmRingX >= 0f && s.RhythmRingX < 0.5f ? 0.8f : 0.2f),
                ("화면 흔들림", s => OnOff(s.ScreenShake), s => s.ScreenShake = !s.ScreenShake),
                ("피해 숫자", s => OnOff(s.DamageNumbers), s => s.DamageNumbers = !s.DamageNumbers),
                ("진동", s => OnOff(s.Vibration), s => s.Vibration = !s.Vibration),
                ("프레임", s => $"{s.FrameRate}", s => { s.FrameRate = s.FrameRate >= 60 ? 30 : 60; Application.targetFrameRate = s.FrameRate; }),
                ("가로 화면", s => OnOff(s.Landscape), s => { s.Landscape = !s.Landscape; ScreenMode.Apply(s.Landscape); })
            };
        }

        public string ButtonLabel => "설정";
        public ListPanel Panel { get; }

        static string OnOff(bool on) => on ? "<color=#3FE06A>켬</color>" : "<color=#8090A8>끔</color>";

        float NextSpeed(float current)
        {
            var speeds = _ctx.Meta.Tuning.Convenience.Speeds;
            int index = Array.IndexOf(speeds, current);
            return speeds[(index + 1) % speeds.Length];
        }

        public void Refresh()
        {
            if (!Panel.Visible) return;
            var s = _ctx.Meta.Settings;
            Panel.SetTitle("<b>설정</b>");
            Panel.SetItems(_rows.Length, i => $"{_rows[i].label}  <b>{_rows[i].value(s)}</b>");
            Panel.SetDetail("<size=18><color=#8090A8>배속 ×3은 이용권(상점) 또는 월정액이 있을 때 적용됩니다. 화면 흔들림·피해 숫자는 다음 화면부터 반영.</color></size>");
            Panel.SetFooter(0, string.Empty, null, false);
            Panel.SetFooter(1, string.Empty, null, false);
            Panel.SetFooter(2, "닫기", () => Panel.SetVisible(false));
        }

        void OnRow(int index)
        {
            if (index < 0 || index >= _rows.Length) return;
            _rows[index].toggle(_ctx.Meta.Settings);
            _ctx.Meta.RequestSave();
            Refresh();
        }
    }

    /// <summary>세로(기본) ↔ 가로 화면 전환.</summary>
    public static class ScreenMode
    {
        public static void Apply(bool landscape)
        {
            Screen.autorotateToPortrait = !landscape;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = landscape;
            Screen.autorotateToLandscapeRight = landscape;
            Screen.orientation = landscape ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
        }
    }
}

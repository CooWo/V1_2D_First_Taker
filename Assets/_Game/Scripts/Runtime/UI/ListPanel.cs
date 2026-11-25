using System;
using ShadowRPG.Runtime.Presentation;
using UnityEngine;

namespace ShadowRPG.Runtime.UI
{
    /// <summary>
    /// 재사용 목록 창: 제목 + 페이지 단위 행 버튼 + 상세 글 + 하단 버튼 3개.
    /// 가방·장비·트리·펫·전리품 선택 창이 모두 이것을 씁니다 (데이터는 각 프레젠터가 공급).
    /// </summary>
    public sealed class ListPanel
    {
        public const float Width = 900f;
        const int FooterCount = 3;

        /// <summary>가로 화면(기준 높이 1080)에서도 잘리지 않는 높이.</summary>
        public static float Height => UiFactory.IsPortrait ? 1000f : 940f;

        readonly RectTransform _root;
        readonly UnityEngine.UI.Text _title;
        readonly UnityEngine.UI.Text _detail;
        readonly UnityEngine.UI.Button[] _rows;
        readonly UnityEngine.UI.Text[] _rowLabels;
        readonly UnityEngine.UI.Button[] _footer = new UnityEngine.UI.Button[FooterCount];
        readonly UnityEngine.UI.Text[] _footerLabels = new UnityEngine.UI.Text[FooterCount];
        readonly Action[] _footerActions = new Action[FooterCount];
        readonly UnityEngine.UI.Text _pageLabel;
        Func<int, string> _label;
        int _count;

        public ListPanel(Transform parent, string name, int rowsPerPage = 9)
        {
            _root = UiFactory.Anchored(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0f, UiFactory.IsPortrait ? 40f : 0f), new Vector2(Width, Height));
            UiFactory.Panel(_root, new Color(0.04f, 0.05f, 0.08f, 0.96f), true);
            UiFactory.Border(_root, Palette.PanelBorder);

            _title = UiFactory.Label(UiFactory.Anchored(_root, "Title", new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(Width - 300f, 56f)),
                string.Empty, 28, TextAnchor.MiddleLeft, Color.white);

            UiFactory.Button(_root, "Close", new Vector2(1f, 1f), new Vector2(-12f, -10f), new Vector2(80f, 56f), "X", () => SetVisible(false));
            UiFactory.Button(_root, "Next", new Vector2(1f, 1f), new Vector2(-100f, -10f), new Vector2(64f, 56f), "▶", () => Page(1));
            _pageLabel = UiFactory.Label(UiFactory.Anchored(_root, "Page", new Vector2(1f, 1f), new Vector2(-170f, -10f), new Vector2(70f, 56f)),
                string.Empty, 20, TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.85f));
            UiFactory.Button(_root, "Prev", new Vector2(1f, 1f), new Vector2(-244f, -10f), new Vector2(64f, 56f), "◀", () => Page(-1));

            RowsPerPage = rowsPerPage;
            _rows = new UnityEngine.UI.Button[rowsPerPage];
            _rowLabels = new UnityEngine.UI.Text[rowsPerPage];
            for (int i = 0; i < rowsPerPage; i++)
            {
                int index = i;
                _rows[i] = UiFactory.Button(_root, "Row" + i, new Vector2(0.5f, 1f), new Vector2(0f, -78f - i * 60f), new Vector2(Width - 40f, 54f), string.Empty,
                    () => RowClicked?.Invoke(PageIndex * RowsPerPage + index));
                _rowLabels[i] = _rows[i].GetComponentInChildren<UnityEngine.UI.Text>();
                _rowLabels[i].alignment = TextAnchor.MiddleLeft;
                _rowLabels[i].fontSize = 21;
            }

            float detailTop = -84f - rowsPerPage * 60f;
            DetailArea = UiFactory.Anchored(_root, "Detail", new Vector2(0f, 1f), new Vector2(20f, detailTop), new Vector2(Width - 40f, Height + detailTop - 100f));
            _detail = UiFactory.Label(DetailArea, string.Empty, 20, TextAnchor.UpperLeft, Color.white);

            float buttonWidth = (Width - 40f - 20f * (FooterCount - 1)) / FooterCount;
            for (int i = 0; i < FooterCount; i++)
            {
                int index = i;
                _footer[i] = UiFactory.Button(_root, "Footer" + i, new Vector2(0f, 0f), new Vector2(20f + i * (buttonWidth + 20f), 18f), new Vector2(buttonWidth, 66f),
                    string.Empty, () => _footerActions[index]?.Invoke());
                _footerLabels[i] = _footer[i].GetComponentInChildren<UnityEngine.UI.Text>();
                _footerLabels[i].fontSize = 22;
            }

            _root.gameObject.SetActive(false);
        }

        public int RowsPerPage { get; }
        public int PageIndex { get; private set; }
        public RectTransform Root => _root;
        public RectTransform DetailArea { get; }
        public bool Visible => _root.gameObject.activeSelf;

        public event Action<int> RowClicked;
        public event Action<bool> VisibilityChanged;

        public void SetVisible(bool visible)
        {
            if (Visible == visible) return;
            _root.gameObject.SetActive(visible);
            VisibilityChanged?.Invoke(visible);
        }

        public void SetTitle(string title) => _title.text = title;
        public void SetDetail(string detail) => _detail.text = detail;

        /// <summary>항목 수와 행 글자 공급 함수. 현재 페이지만 다시 그립니다.</summary>
        public void SetItems(int count, Func<int, string> label)
        {
            _count = count;
            _label = label;
            int pages = Mathf.Max(1, Mathf.CeilToInt(count / (float)RowsPerPage));
            PageIndex = Mathf.Clamp(PageIndex, 0, pages - 1);
            _pageLabel.text = $"{PageIndex + 1}/{pages}";
            for (int r = 0; r < RowsPerPage; r++)
            {
                int i = PageIndex * RowsPerPage + r;
                bool has = i < count;
                _rows[r].gameObject.SetActive(has);
                if (has) _rowLabels[r].text = "  " + label(i);
            }
        }

        public void SetFooter(int index, string label, Action onClick, bool visible = true)
        {
            _footer[index].gameObject.SetActive(visible);
            _footerLabels[index].text = label;
            _footerActions[index] = onClick;
        }

        void Page(int delta)
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(_count / (float)RowsPerPage));
            PageIndex = (PageIndex + delta + pages) % pages;
            if (_label != null) SetItems(_count, _label);
        }
    }
}

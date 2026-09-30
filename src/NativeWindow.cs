using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NativeUILib
{
    /// <summary>
    /// 原生风格的窗口：九宫格面板 + 可拖动标题栏 + 自动高度的垂直流式内容区 + 超出自动滚动。
    ///
    /// 用法：
    /// <code>
    /// var w = NativeUI.Window("我的面板");
    /// w.AddLabel("你好");
    /// w.AddToggle("开启功能", true, v => { ... });
    /// w.AddSlider("强度", 0f, 1f, 0.5f, v => { ... }, "0.00");
    /// w.AddButton("关闭", () => w.Close());
    /// </code>
    /// </summary>
    public sealed class NativeWindow : MonoBehaviour
    {
        // ── 注册表 ──────────────────────────────────────────────
        internal static readonly List<NativeWindow> All = new List<NativeWindow>();

        public static int OpenCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < All.Count; i++) if (All[i] != null) n++;
                return n;
            }
        }

        public static void CloseAll()
        {
            var snapshot = All.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
                if (snapshot[i] != null) snapshot[i].Close();
        }

        // ── 公开状态 ────────────────────────────────────────────

        public event Action Closed;
        public event Action Shown;

        /// <summary>可选标识：用于位置记忆 / 查找。</summary>
        public string Id;

        /// <summary>是否根据内容自动调整高度（默认 true）。</summary>
        public bool AutoSize = true;

        /// <summary>按 Esc 关闭（默认 false，避免和游戏暂停键冲突）。</summary>
        public bool CloseOnEscape;

        public RectTransform Rect => _rt;
        public RectTransform Content => _content;
        public GameObject GameObject => gameObject;

        public bool Visible
        {
            get { return gameObject.activeSelf; }
            set
            {
                gameObject.SetActive(value);
                if (value && Shown != null) Shown();
            }
        }

        public string Title
        {
            get { return _title != null ? _title.text : string.Empty; }
            set { if (_title != null) _title.text = value ?? string.Empty; }
        }

        // ── 内部字段 ────────────────────────────────────────────

        private RectTransform _rt;
        private RectTransform _body;
        private RectTransform _viewport;
        private RectTransform _content;
        private RectTransform _titleBar;
        private TextMeshProUGUI _title;
        private VerticalLayoutGroup _vlg;
        private ContentSizeFitter _csf;
        private bool _dirty;

        // ─────────────────────────────────────────────────────────
        // 创建
        // ─────────────────────────────────────────────────────────

        internal static NativeWindow Create(string title, Vector2 size, Transform parent, bool draggable, bool closable)
        {
            NativeUI.NotifyInteractiveUI();
            float s = NativeTheme.Scale;
            if (parent == null) parent = NativeUI.Parent;

            var root = NativeBuild.Rect("NativeWindow", parent);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;

            var bg = root.AddComponent<Image>();
            bg.sprite = NativeAssets.PanelSmall;
            bg.type = Image.Type.Sliced;
            bg.color = NativeTheme.Panel;
            bg.raycastTarget = true;

            var win = root.AddComponent<NativeWindow>();
            win._rt = rt;

            // ── 标题栏 ──
            var titleBar = NativeBuild.RectT("TitleBar", rt);
            titleBar.anchorMin = new Vector2(0f, 1f);
            titleBar.anchorMax = new Vector2(1f, 1f);
            titleBar.pivot = new Vector2(0.5f, 1f);
            titleBar.anchoredPosition = Vector2.zero;
            titleBar.sizeDelta = new Vector2(0f, NativeTheme.TitleHeight * s);
            var titleImg = titleBar.gameObject.AddComponent<Image>();
            titleImg.sprite = NativeAssets.PanelNano;
            titleImg.type = Image.Type.Sliced;
            titleImg.color = NativeTheme.TitleBar;
            win._titleBar = titleBar;

            win._title = NativeBuild.Text("Title", titleBar, title ?? string.Empty, NativeTheme.FontSize(1.05f));
            NativeBuild.Stretch(win._title.rectTransform, 10f * s, 0f, (closable ? 38f : 10f) * s, 0f);

            if (closable)
            {
                var close = NativeBuild.NativeButton("Close", titleBar, "X", () => win.Close(), -1f);
                var crt = close.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(1f, 0.5f);
                crt.anchorMax = new Vector2(1f, 0.5f);
                crt.pivot = new Vector2(1f, 0.5f);
                crt.anchoredPosition = new Vector2(-3f * s, 0f);
                // 像素字体的字形接近 1:1 宽高比，按钮太窄时 'X' 放不下会被裁掉；
                // 给足宽度 + 缩小字号 + 关闭溢出裁剪，保证一定显示出来。
                crt.sizeDelta = new Vector2(30f * s, 24f * s);

                var closeLabel = close.GetComponentInChildren<TextMeshProUGUI>(true);
                if (closeLabel != null)
                {
                    closeLabel.fontSize = NativeTheme.FontSize(0.85f);
                    closeLabel.overflowMode = TextOverflowModes.Overflow;
                    NativeBuild.Stretch(closeLabel.rectTransform, 2f * s, 0f, 2f * s, 0f);
                }
                NativeBuild.Tooltip(close.gameObject, "关闭", null);
            }

            if (draggable)
            {
                var dragger = titleBar.gameObject.AddComponent<NativeDragger>();
                dragger.Target = rt;
            }

            // ── 内容区 ──
            var body = NativeBuild.RectT("Body", rt);
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(NativeTheme.Padding * s, NativeTheme.Padding * s);
            body.offsetMax = new Vector2(-NativeTheme.Padding * s, -(NativeTheme.TitleHeight + NativeTheme.Padding) * s);
            // 透明但可命中的 Image：让滚轮事件能落在 Body 上（ScrollRect 需要射线目标）
            var bodyHit = body.gameObject.AddComponent<Image>();
            bodyHit.color = new Color(0f, 0f, 0f, 0f);
            bodyHit.raycastTarget = true;
            win._body = body;

            var viewport = NativeBuild.RectT("Viewport", body);
            NativeBuild.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = NativeBuild.RectT("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = NativeTheme.Spacing * s;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            win._vlg = vlg;

            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            win._csf = csf;

            var scroll = body.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f * s;
            scroll.inertia = false;

            win._content = content;
            win._rt = rt;

            NativeBuild.ToUILayer(root);
            All.Add(win);
            win.RefreshLayout();
            return win;
        }

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDestroy()
        {
            SavePosition();
            All.Remove(this);
            if (Closed != null)
            {
                try { Closed(); }
                catch (Exception e) { NativeLog.Error(e); }
            }
        }

        private void Update()
        {
            if (CloseOnEscape && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void LateUpdate()
        {
            if (_dirty) RefreshLayout();
        }

        public void Close()
        {
            // Closed 事件统一在 OnDestroy 里触发，避免重复
            UnityEngine.Object.Destroy(gameObject);
        }

        // ─────────────────────────────────────────────────────────
        // 布局
        // ─────────────────────────────────────────────────────────

        /// <summary>立即重算内容高度并按需调整窗口高度（Add* 会自动调用）。</summary>
        public void RefreshLayout()
        {
            _dirty = false;
            if (!AutoSize || _content == null || _rt == null) return;

            float s = NativeTheme.Scale;
            float chrome = (NativeTheme.TitleHeight + NativeTheme.Padding * 2f) * s;
            try
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
                float contentH = _content.rect.height;
                float want = chrome + contentH;
                float maxH = Screen.height * 0.75f;
                if (want > maxH) want = maxH;
                if (want < chrome + 10f) want = chrome + 10f;

                var size = _rt.sizeDelta;
                if (Mathf.Abs(size.y - want) > 0.5f)
                {
                    _rt.sizeDelta = new Vector2(size.x, want);
                    // 宽度没变，但高度变化会影响视口裁剪，再刷一次让滚动条区域收敛
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
                }
            }
            catch (Exception e)
            {
                NativeLog.Warn("窗口布局刷新失败：" + e.Message);
            }
        }

        private void MarkDirty()
        {
            _dirty = true;
            RefreshLayout();
        }

        /// <summary>把窗口限制在父容器可视范围内。</summary>
        public void ClampToScreen()
        {
            if (_rt == null) return;
            var parent = _rt.parent as RectTransform;
            if (parent == null) return;
            float pw = parent.rect.width;
            float ph = parent.rect.height;
            if (pw <= 1f || ph <= 1f) return;

            var size = _rt.sizeDelta;
            var pos = _rt.anchoredPosition;
            float halfX = Mathf.Max(0f, (pw - size.x) * 0.5f);
            float halfY = Mathf.Max(0f, (ph - size.y) * 0.5f);
            pos.x = Mathf.Clamp(pos.x, -halfX, halfX);
            pos.y = Mathf.Clamp(pos.y, -halfY, halfY);
            _rt.anchoredPosition = pos;
        }

        /// <summary>重新指定窗口尺寸（AutoSize 会忽略高度）。</summary>
        public NativeWindow SetSize(float width, float height)
        {
            if (_rt == null) return this;
            _rt.sizeDelta = new Vector2(width * NativeTheme.Scale, height * NativeTheme.Scale);
            MarkDirty();
            return this;
        }

        public NativeWindow SetPosition(float x, float y)
        {
            if (_rt != null) _rt.anchoredPosition = new Vector2(x, y);
            return this;
        }

        public NativeWindow Center()
        {
            if (_rt != null) _rt.anchoredPosition = Vector2.zero;
            return this;
        }

        /// <summary>把窗口位置记忆到 PlayerPrefs（key 会自动加前缀）。</summary>
        public NativeWindow RememberPosition(string key)
        {
            Id = key;
            if (_rt == null || string.IsNullOrEmpty(key)) return this;
            float x = PlayerPrefs.GetFloat(PrefKey("x"), float.NaN);
            float y = PlayerPrefs.GetFloat(PrefKey("y"), float.NaN);
            if (!float.IsNaN(x) && !float.IsNaN(y))
            {
                _rt.anchoredPosition = new Vector2(x, y);
                ClampToScreen();
            }
            return this;
        }

        public void SavePosition()
        {
            if (_rt == null || string.IsNullOrEmpty(Id)) return;
            try
            {
                PlayerPrefs.SetFloat(PrefKey("x"), _rt.anchoredPosition.x);
                PlayerPrefs.SetFloat(PrefKey("y"), _rt.anchoredPosition.y);
            }
            catch { }
        }

        private string PrefKey(string axis) => "nativeuilib.window." + Id + "." + axis;

        // ─────────────────────────────────────────────────────────
        // 内容：文本
        // ─────────────────────────────────────────────────────────

        public TextMeshProUGUI AddLabel(string text, float sizeMult = 1f,
                                        TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var t = NativeBuild.Text("Label", _content, text, NativeTheme.FontSize(sizeMult), align, true);
            float m = 2f * NativeTheme.Scale;
            t.margin = new Vector4(m, 0f, m, 0f);
            MarkDirty();
            return t;
        }

        /// <summary>居中放大的标题文本（金色）。</summary>
        public TextMeshProUGUI AddHeader(string text, float sizeMult = 1.3f)
        {
            var t = AddLabel(text, sizeMult, TextAlignmentOptions.Center);
            t.color = NativeTheme.Accent;
            return t;
        }

        /// <summary>一个撑满宽度的原生按钮。</summary>
        public Button AddButton(string text, Action onClick, float width = 0f, string tooltip = null)
        {
            float s = NativeTheme.Scale;
            var btn = NativeBuild.NativeButton("Button", _content, text, onClick, NativeTheme.ButtonHeight * s);
            if (width > 0f)
            {
                var le = btn.GetComponent<LayoutElement>();
                le.preferredWidth = width * s;
                le.flexibleWidth = 0f;
            }
            NativeBuild.Tooltip(btn.gameObject, tooltip, null);
            MarkDirty();
            return btn;
        }

        /// <summary>灰黑色分隔线。</summary>
        public GameObject AddSeparator()
        {
            var img = NativeBuild.Panel("Separator", _content, NativeAssets.White, NativeTheme.Separator, false);
            NativeBuild.Layout(img.gameObject, preferredHeight: 2f * NativeTheme.Scale);
            MarkDirty();
            return img.gameObject;
        }

        public GameObject AddSpace(float height = 8f)
        {
            var go = NativeBuild.Rect("Space", _content);
            NativeBuild.Layout(go, preferredHeight: height * NativeTheme.Scale);
            MarkDirty();
            return go;
        }

        /// <summary>把任意自定义控件塞进内容区（会参与垂直流式布局）。</summary>
        public T Add<T>(T component) where T : Component
        {
            if (component != null)
            {
                component.transform.SetParent(_content, false);
                MarkDirty();
            }
            return component;
        }

        public GameObject Add(GameObject child)
        {
            if (child != null)
            {
                child.transform.SetParent(_content, false);
                MarkDirty();
            }
            return child;
        }

        // ─────────────────────────────────────────────────────────
        // 内容：原生设置行
        // ─────────────────────────────────────────────────────────

        /// <summary>底层：直接插一行原生控件。</summary>
        public NativeRow AddRow(NativeRowKind kind, string label, string tooltip = null)
        {
            var row = NativeRow.Create(kind, _content);
            row.SetLabel(label);
            if (!string.IsNullOrEmpty(tooltip)) row.SetTooltip(label, tooltip);
            MarkDirty();
            return row;
        }

        public NativeRow AddToggle(string label, bool value, Action<bool> onChange = null, string tooltip = null)
        {
            var row = AddRow(NativeRowKind.Bool, label, tooltip);
            if (row.Toggle != null)
            {
                row.SetBool(value, false);
                if (onChange != null) row.OnBool(onChange);
            }
            return row;
        }

        public NativeRow AddSlider(string label, float min, float max, float value,
                                   Action<float> onChange = null, string format = null, string tooltip = null)
        {
            var row = AddRow(NativeRowKind.Float, label, tooltip);
            if (row.Slider != null)
            {
                var slider = row.Slider;
                slider.minValue = min;
                slider.maxValue = max;
                if (!string.IsNullOrEmpty(format)) row.SetValueFormatter(v => v.ToString(format));
                row.SetFloat(value, false);
                if (onChange != null) row.OnFloat(onChange);
            }
            return row;
        }

        /// <summary>文本输入行。默认行类型是 Int 预制体（其 Child1 就是 TMP_InputField）。</summary>
        public NativeRow AddInput(string label, string value, Action<string> onChange = null,
                                  bool freeText = true, string tooltip = null)
        {
            var row = AddRow(NativeRowKind.Int, label, tooltip);
            if (row.Input != null)
            {
                if (freeText) row.Input.contentType = TMP_InputField.ContentType.Standard;
                row.SetText(value, false);
                if (onChange != null) row.OnText(onChange);
            }
            return row;
        }

        public NativeRow AddDropdown(string label, string[] options, int index,
                                     Action<int> onChange = null, string tooltip = null)
        {
            var row = AddRow(NativeRowKind.Dropdown, label, tooltip);
            if (row.Dropdown != null)
            {
                row.SetOptions(options);
                row.SetDropdownIndex(index, false);
                if (onChange != null) row.OnDropdown(onChange);
            }
            else if (row.Cycler != null)
            {
                row.SetOptions(options);
                row.SetInt(index, false);
                if (onChange != null) row.OnDropdown(onChange);
            }
            return row;
        }

        /// <summary>左侧说明 + 右侧一个按钮的原生行（常用于"重置/应用"这类操作）。</summary>
        public NativeRow AddButtonRow(string label, string buttonText, Action onClick, string tooltip = null)
        {
            var row = AddRow(NativeRowKind.Button, label, tooltip);
            if (row.Button != null)
            {
                var tmp = row.Button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null) tmp.text = buttonText ?? string.Empty;
                row.OnButton(onClick);
            }
            return row;
        }

        // ─────────────────────────────────────────────────────────
        // 便捷组合
        // ─────────────────────────────────────────────────────────

        /// <summary>一行：说明文字 + 右侧按钮（水平布局，非原生预制体）。</summary>
        public Button AddInlineButton(string label, string buttonText, Action onClick)
        {
            float s = NativeTheme.Scale;
            var row = NativeBuild.Rect("InlineRow", _content);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 8f * s;
            NativeBuild.Layout(row, preferredHeight: NativeTheme.ButtonHeight * s);

            var text = NativeBuild.Text("Label", row.transform, label, NativeTheme.FontSize(0.95f));
            NativeBuild.Layout(text.gameObject, flexibleWidth: 1f);

            var btn = NativeBuild.NativeButton("Button", row.transform, buttonText, onClick, -1f);
            NativeBuild.Layout(btn.gameObject, preferredWidth: NativeTheme.ControlWidth * s, flexibleWidth: 1f);

            MarkDirty();
            return btn;
        }

        /// <summary>把标题栏的拖动也开放给额外区域（例如自绘的表头）。</summary>
        public void MakeDraggable(GameObject target)
        {
            if (target == null) return;
            var d = target.GetComponent<NativeDragger>();
            if (d == null) d = target.AddComponent<NativeDragger>();
            d.Target = _rt;
        }
    }

    /// <summary>标题栏拖动。</summary>
    internal sealed class NativeDragger : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform Target;

        private RectTransform _parent;
        private Vector2 _grabOffset;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Target == null) return;
            _parent = Target.parent as RectTransform;
            Vector2 local;
            if (_parent != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, eventData.position, eventData.pressEventCamera, out local))
            {
                _grabOffset = local - Target.anchoredPosition;
            }
            else
            {
                _grabOffset = Vector2.zero;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Target == null) return;
            if (_parent == null) _parent = Target.parent as RectTransform;
            if (_parent == null) return;

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, eventData.position, eventData.pressEventCamera, out local))
                return;

            Target.anchoredPosition = local - _grabOffset;

            var win = Target.GetComponent<NativeWindow>();
            if (win != null) win.ClampToScreen();
        }
    }
}

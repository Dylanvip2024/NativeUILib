using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NativeUILib
{
    /// <summary>原生设置行的类型。对应游戏 Resources 里的 Special/GameSetting* 预制体。</summary>
    public enum NativeRowKind
    {
        /// <summary>开关 → Toggle（Special/GameSettingBool）</summary>
        Bool = 0,
        /// <summary>滑条 → Slider（Special/GameSettingFloat）</summary>
        Float = 1,
        /// <summary>文本输入 → TMP_InputField（Special/GameSettingInt）</summary>
        Int = 2,
        /// <summary>下拉框 → TMP_Dropdown（Special/GameSettingDropdown）</summary>
        Dropdown = 3,
        /// <summary>按钮行（Special/GameSettingInput，原生是键位绑定按钮）</summary>
        Button = 4,
    }

    /// <summary>
    /// 一行"游戏原生设置项"。
    ///
    /// 优先直接实例化游戏自己的 <c>Special/GameSetting*</c> 预制体（外观 100% 原生，
    /// 子节点约定：Child0=名称文本、Child1=控件、Child2=数值文本）。
    /// 预制体不可用时自动降级为用原生贴图自绘的控件（<see cref="Native"/> 为 false）。
    /// </summary>
    public sealed class NativeRow : MonoBehaviour
    {
        public NativeRowKind Kind { get; private set; }
        /// <summary>是否来自游戏原生预制体。</summary>
        public bool Native { get; private set; }

        public TextMeshProUGUI Label { get; private set; }
        public RectTransform Control { get; private set; }
        /// <summary>滑条右侧的数值文本（原生 float 行才有）。</summary>
        public TextMeshProUGUI ValueText { get; private set; }

        public Toggle Toggle { get; private set; }
        public Slider Slider { get; private set; }
        public TMP_Dropdown Dropdown { get; private set; }
        public TMP_InputField Input { get; private set; }
        public Button Button { get; private set; }

        internal NativeCycler Cycler;
        internal Func<float, string> ValueFormatter;

        // ─────────────────────────────────────────────────────────
        // 创建
        // ─────────────────────────────────────────────────────────

        internal static string ResourcePath(NativeRowKind kind)
        {
            switch (kind)
            {
                case NativeRowKind.Bool: return "Special/GameSettingBool";
                case NativeRowKind.Float: return "Special/GameSettingFloat";
                case NativeRowKind.Int: return "Special/GameSettingInt";
                case NativeRowKind.Dropdown: return "Special/GameSettingDropdown";
                case NativeRowKind.Button: return "Special/GameSettingInput";
                default: return null;
            }
        }

        internal static NativeRow Create(NativeRowKind kind, Transform parent)
        {
            GameObject go = null;
            bool native = false;
            string path = ResourcePath(kind);

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var prefab = Resources.Load<GameObject>(path);
                    if (prefab != null)
                    {
                        go = UnityEngine.Object.Instantiate(prefab, parent, false);
                        native = true;
                    }
                    else
                    {
                        NativeLog.Warn("原生控件预制体不存在：" + path + "，改用自绘控件");
                    }
                }
                catch (Exception e)
                {
                    NativeLog.Warn("实例化 " + path + " 失败：" + e.Message + "，改用自绘控件");
                    go = null;
                }
            }

            if (go == null)
            {
                try { go = BuildFallback(kind, parent); }
                catch (Exception e)
                {
                    NativeLog.Error("构建自绘控件失败：" + e);
                    go = NativeBuild.Rect("NativeRow_Error", parent);
                    NativeBuild.Layout(go, preferredHeight: NativeTheme.RowHeight * NativeTheme.Scale);
                    var t = NativeBuild.Text("Label", go.transform, "控件创建失败：" + kind, NativeTheme.FontSize(0.9f));
                    NativeBuild.Stretch(t.rectTransform, 4f, 0f, 4f, 0f);
                    t.color = NativeTheme.Danger;
                }
                native = false;
            }

            go.name = "NativeRow_" + kind;
            var row = go.GetComponent<NativeRow>();
            if (row == null) row = go.AddComponent<NativeRow>();
            row.Kind = kind;
            row.Native = native;
            row.Bind();
            return row;
        }

        private void Bind()
        {
            var t = transform;
            Label = t.childCount > 0 ? t.GetChild(0).GetComponent<TextMeshProUGUI>() : null;
            Control = t.childCount > 1 ? t.GetChild(1) as RectTransform : null;
            ValueText = t.childCount > 2 ? t.GetChild(2).GetComponent<TextMeshProUGUI>() : null;

            if (Control == null) return;
            Toggle = Control.GetComponent<Toggle>();
            Slider = Control.GetComponent<Slider>();
            Dropdown = Control.GetComponent<TMP_Dropdown>();
            Input = Control.GetComponent<TMP_InputField>();
            Button = Control.GetComponent<Button>();
            Cycler = Control.GetComponentInChildren<NativeCycler>(true);
            if (Button == null && Cycler != null) Button = Cycler.Button;
            if (Button == null && Input == null && Dropdown == null) Button = Control.GetComponentInChildren<Button>(true);
        }

        // ─────────────────────────────────────────────────────────
        // 自绘降级实现
        // ─────────────────────────────────────────────────────────

        private static GameObject BuildFallback(NativeRowKind kind, Transform parent)
        {
            float s = NativeTheme.Scale;
            var row = NativeBuild.Rect("NativeRow", parent);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 8f * s;
            NativeBuild.Layout(row, preferredHeight: NativeTheme.RowHeight * s);

            var label = NativeBuild.Text("Label", row.transform, string.Empty, NativeTheme.FontSize(0.95f));
            NativeBuild.Layout(label.gameObject, flexibleWidth: 1f);

            var control = NativeBuild.RectT("Control", row.transform);
            NativeBuild.Layout(control.gameObject, preferredWidth: NativeTheme.ControlWidth * s, flexibleWidth: 1f);

            switch (kind)
            {
                case NativeRowKind.Bool: BuildToggleInto(control); break;
                case NativeRowKind.Float: BuildSliderInto(control); break;
                case NativeRowKind.Int: BuildInputInto(control); break;
                case NativeRowKind.Dropdown: BuildCyclerInto(control); break;
                default: BuildButtonInto(control); break;
            }
            return row;
        }

        private static void BuildToggleInto(RectTransform parent)
        {
            float s = NativeTheme.Scale;
            var bg = NativeBuild.Panel("Toggle", parent, NativeAssets.PanelNano, NativeTheme.NormalState);
            NativeBuild.Stretch(bg.rectTransform);
            var toggle = bg.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bg;

            var mark = NativeBuild.Panel("Checkmark", bg.transform, NativeAssets.Check, NativeTheme.Accent, false);
            NativeBuild.Stretch(mark.rectTransform, 18f * s, 3f * s, 3f * s, 3f * s);

            toggle.graphic = mark;
            toggle.isOn = false;
        }

        private static void BuildSliderInto(RectTransform parent)
        {
            float s = NativeTheme.Scale;
            var root = NativeBuild.RectT("Slider", parent);
            NativeBuild.Stretch(root);
            var slider = root.gameObject.AddComponent<Slider>();

            var bg = NativeBuild.Panel("Background", root, NativeAssets.PanelNano, new Color(1f, 1f, 1f, 0.5f));
            var bgrt = bg.rectTransform;
            bgrt.anchorMin = new Vector2(0f, 0.3f);
            bgrt.anchorMax = new Vector2(1f, 0.7f);
            bgrt.offsetMin = Vector2.zero;
            bgrt.offsetMax = Vector2.zero;

            var fillArea = NativeBuild.RectT("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.3f);
            fillArea.anchorMax = new Vector2(1f, 0.7f);
            fillArea.offsetMin = new Vector2(4f * s, 0f);
            fillArea.offsetMax = new Vector2(-4f * s, 0f);

            var fill = NativeBuild.Panel("Fill", fillArea, NativeAssets.White, new Color(1f, 1f, 1f, 0.85f), false);
            NativeBuild.Stretch(fill.rectTransform);

            var handleArea = NativeBuild.RectT("Handle Slide Area", root);
            NativeBuild.Stretch(handleArea, 6f * s, 0f, 6f * s, 0f);

            var handle = NativeBuild.Panel("Handle", handleArea, NativeAssets.PanelNano, Color.white);
            var hrt = handle.rectTransform;
            hrt.anchorMin = Vector2.zero;
            hrt.anchorMax = Vector2.one;
            hrt.sizeDelta = new Vector2(10f * s, 0f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = hrt;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
        }

        private static void BuildInputInto(RectTransform parent)
        {
            float s = NativeTheme.Scale;
            var bg = NativeBuild.Panel("Input", parent, NativeAssets.PanelNano, new Color(1f, 1f, 1f, 0.8f));
            NativeBuild.Stretch(bg.rectTransform);
            var input = bg.gameObject.AddComponent<TMP_InputField>();

            var area = NativeBuild.RectT("Text Area", bg.transform);
            NativeBuild.Stretch(area, 5f * s, 2f * s, 5f * s, 2f * s);
            area.gameObject.AddComponent<RectMask2D>();

            var placeholder = NativeBuild.Text("Placeholder", area, "…", NativeTheme.FontSize(0.9f));
            NativeBuild.Stretch(placeholder.rectTransform);
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);

            var text = NativeBuild.Text("Text", area, string.Empty, NativeTheme.FontSize(0.9f));
            NativeBuild.Stretch(text.rectTransform);

            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = bg;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
        }

        private static void BuildCyclerInto(RectTransform parent)
        {
            var btn = NativeBuild.NativeButton("Cycler", parent, "-", null, -1f);
            NativeBuild.Stretch(btn.GetComponent<RectTransform>());
            var cycler = btn.gameObject.AddComponent<NativeCycler>();
            cycler.Button = btn;
            cycler.Label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private static void BuildButtonInto(RectTransform parent)
        {
            var btn = NativeBuild.NativeButton("Button", parent, string.Empty, null, -1f);
            NativeBuild.Stretch(btn.GetComponent<RectTransform>());
        }

        // ─────────────────────────────────────────────────────────
        // 通用操作
        // ─────────────────────────────────────────────────────────

        public NativeRow SetLabel(string text)
        {
            if (Label != null) Label.text = text ?? string.Empty;
            if (Kind == NativeRowKind.Button && Button != null && Label == null)
            {
                var t = Button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (t != null) t.text = text ?? string.Empty;
            }
            return this;
        }

        /// <summary>挂原生 tooltip（鼠标悬停时由 GlobalDark 统一绘制）。</summary>
        public NativeRow SetTooltip(string name, string desc = null)
        {
            var target = Label != null ? Label.gameObject : gameObject;
            NativeBuild.Tooltip(target, name, desc);
            return this;
        }

        public NativeRow SetInteractable(bool on)
        {
            if (Toggle != null) Toggle.interactable = on;
            if (Slider != null) Slider.interactable = on;
            if (Dropdown != null) Dropdown.interactable = on;
            if (Input != null) Input.interactable = on;
            if (Button != null) Button.interactable = on;
            return this;
        }

        public NativeRow SetValueFormatter(Func<float, string> formatter)
        {
            ValueFormatter = formatter;
            if (Slider != null) RefreshValueText(Slider.value);
            return this;
        }

        internal void RefreshValueText(float v)
        {
            if (ValueText == null) return;
            ValueText.text = ValueFormatter != null ? ValueFormatter(v) : v.ToString("0.##");
        }

        // ── 取值 / 赋值 ──

        public bool GetBool() => Toggle != null && Toggle.isOn;
        public float GetFloat() => Slider != null ? Slider.value : 0f;
        public int GetInt()
        {
            if (Dropdown != null) return Dropdown.value;
            if (Cycler != null) return Cycler.Index;
            if (Input != null)
            {
                int v;
                return int.TryParse(Input.text, out v) ? v : 0;
            }
            return 0;
        }
        public string GetText() => Input != null ? Input.text : string.Empty;

        public NativeRow SetBool(bool value, bool notify = false)
        {
            if (Toggle == null) return this;
            if (notify) Toggle.isOn = value;
            else Toggle.SetIsOnWithoutNotify(value);
            return this;
        }

        public NativeRow SetFloat(float value, bool notify = false)
        {
            if (Slider == null) return this;
            if (notify) Slider.value = value;
            else Slider.SetValueWithoutNotify(value);
            RefreshValueText(value);
            return this;
        }

        public NativeRow SetInt(int value, bool notify = false)
        {
            if (Dropdown != null)
            {
                if (notify) Dropdown.value = value;
                else Dropdown.SetValueWithoutNotify(value);
            }
            else if (Cycler != null)
            {
                Cycler.SetIndex(value, notify);
            }
            else if (Input != null)
            {
                SetText(value.ToString(), notify);
            }
            return this;
        }

        public NativeRow SetText(string value, bool notify = false)
        {
            if (Input == null) return this;
            value = value ?? string.Empty;
            if (notify) Input.text = value;
            else Input.SetTextWithoutNotify(value);
            return this;
        }

        public NativeRow SetOptions(string[] options)
        {
            if (options == null) options = new string[0];
            if (Dropdown != null)
            {
                Dropdown.ClearOptions();
                var list = new System.Collections.Generic.List<TMP_Dropdown.OptionData>(options.Length);
                for (int i = 0; i < options.Length; i++)
                    list.Add(new TMP_Dropdown.OptionData(options[i]));
                Dropdown.AddOptions(list);
            }
            else if (Cycler != null)
            {
                Cycler.SetOptions(options);
            }
            return this;
        }

        public NativeRow SetDropdownIndex(int index, bool notify = false)
        {
            if (Dropdown != null)
            {
                int max = Dropdown.options != null ? Dropdown.options.Count - 1 : 0;
                if (max < 0) max = 0;
                int v = Mathf.Clamp(index, 0, max);
                if (notify) Dropdown.value = v;
                else Dropdown.SetValueWithoutNotify(v);
            }
            else if (Cycler != null)
            {
                Cycler.SetIndex(index, notify);
            }
            return this;
        }

        // ── 事件 ──

        public NativeRow OnBool(Action<bool> callback)
        {
            if (Toggle != null && callback != null) Toggle.onValueChanged.AddListener(v => callback(v));
            return this;
        }

        public NativeRow OnFloat(Action<float> callback)
        {
            if (Slider != null && callback != null)
            {
                Slider.onValueChanged.AddListener(delegate (float v)
                {
                    RefreshValueText(v);
                    callback(v);
                });
            }
            return this;
        }

        public NativeRow OnInt(Action<int> callback)
        {
            if (Dropdown != null && callback != null) Dropdown.onValueChanged.AddListener(v => callback(v));
            else if (Cycler != null) Cycler.Changed += callback;
            else if (Input != null && callback != null)
                Input.onEndEdit.AddListener(delegate (string s)
                {
                    int v;
                    if (int.TryParse(s, out v)) callback(v);
                });
            return this;
        }

        public NativeRow OnText(Action<string> callback)
        {
            if (Input != null && callback != null) Input.onEndEdit.AddListener(s => callback(s));
            return this;
        }

        public NativeRow OnDropdown(Action<int> callback)
        {
            if (Dropdown != null && callback != null) Dropdown.onValueChanged.AddListener(v => callback(v));
            else if (Cycler != null) Cycler.Changed += callback;
            return this;
        }

        public NativeRow OnButton(Action callback)
        {
            if (Button != null && callback != null)
                Button.onClick.AddListener(delegate
                {
                    NativeUI.PlayClick();
                    callback();
                });
            return this;
        }
    }

    /// <summary>
    /// 下拉框的降级实现：点一下切到下一个选项（当游戏没有 TMP_Dropdown 预制体时使用）。
    /// </summary>
    public sealed class NativeCycler : MonoBehaviour
    {
        public string[] Options = new string[0];
        public int Index;
        public Button Button;
        public TextMeshProUGUI Label;
        public event Action<int> Changed;

        private void Awake()
        {
            if (Button == null) Button = GetComponent<Button>();
            if (Button != null) Button.onClick.AddListener(Next);
            if (Label == null) Label = GetComponentInChildren<TextMeshProUGUI>(true);
            Refresh();
        }

        public void SetOptions(string[] options)
        {
            Options = options ?? new string[0];
            if (Index >= Options.Length) Index = Mathf.Max(0, Options.Length - 1);
            Refresh();
        }

        public void SetIndex(int index, bool notify = false)
        {
            int max = Options.Length - 1;
            if (max < 0) max = 0;
            Index = Mathf.Clamp(index, 0, max);
            Refresh();
            if (notify && Changed != null) Changed(Index);
        }

        public void Next()
        {
            if (Options.Length == 0) return;
            Index++;
            if (Index >= Options.Length) Index = 0;
            Refresh();
            if (Changed != null) Changed(Index);
        }

        private void Refresh()
        {
            if (Label != null)
                Label.text = (Options.Length > 0 && Index >= 0 && Index < Options.Length) ? Options[Index] : "-";
        }
    }
}

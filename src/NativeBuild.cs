using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NativeUILib
{
    /// <summary>内部构建原语。所有 UI 元素都在这里产出，方便统一改风格。</summary>
    internal static class NativeBuild
    {
        internal static GameObject Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        internal static RectTransform RectT(string name, Transform parent)
        {
            return Rect(name, parent).GetComponent<RectTransform>();
        }

        internal static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        internal static Image Panel(string name, Transform parent, Sprite sprite, Color color, bool sliced = true)
        {
            var go = Rect(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : NativeAssets.White;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = true;
            return img;
        }

        internal static TextMeshProUGUI Text(string name, Transform parent, string text, int fontSize,
                                             TextAlignmentOptions align = TextAlignmentOptions.Left, bool wrap = false,
                                             TextOverflowModes overflow = TextOverflowModes.Truncate)
        {
            // 原生像素字体没有中文字形：发现缺字时自动挂一次系统字体 fallback
            NativeUI.EnsureGlyphCoverage(text);

            var go = Rect(name, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            var font = NativeAssets.Font;
            if (font != null) t.font = font;
            t.fontSize = fontSize;
            t.text = text ?? string.Empty;
            t.color = NativeTheme.Text;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableWordWrapping = wrap;
            // 注意：默认用 Truncate 而不是 Ellipsis —— 像素字体很可能没有 U+2026('…')，
            // 一旦 Ellipsis 生效又找不到省略号字形，TMP 会什么都不画（表现为"按钮里没有字"）。
            t.overflowMode = overflow;
            return t;
        }

        internal static LayoutElement Layout(GameObject go, float preferredHeight = -1f, float preferredWidth = -1f, float flexibleWidth = -1f)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (preferredHeight >= 0f) le.preferredHeight = preferredHeight;
            if (preferredWidth >= 0f) le.preferredWidth = preferredWidth;
            if (flexibleWidth >= 0f) le.flexibleWidth = flexibleWidth;
            return le;
        }

        /// <summary>原生风格按钮（九宫格贴图 + 游戏手感的状态色 + 原生点击音）。</summary>
        internal static Button NativeButton(string name, Transform parent, string text, Action onClick,
                                            float height = -1f, Sprite sprite = null)
        {
            NativeUI.NotifyInteractiveUI();
            float s = NativeTheme.Scale;
            var img = Panel(name, parent, sprite != null ? sprite : NativeAssets.PanelNano, Color.white, true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var cb = btn.colors;
            cb.normalColor = NativeTheme.NormalState;
            cb.highlightedColor = NativeTheme.HoverState;
            cb.pressedColor = NativeTheme.PressedState;
            cb.selectedColor = NativeTheme.NormalState;
            cb.disabledColor = NativeTheme.DisabledState;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            if (height > 0f) Layout(img.gameObject, preferredHeight: height);

            var label = Text("Text", img.transform, text, NativeTheme.FontSize(0.95f), TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 6f * s, 0f, 6f * s, 0f);

            if (onClick != null)
            {
                btn.onClick.AddListener(delegate
                {
                    NativeUI.PlayClick();
                    try { onClick(); }
                    catch (Exception e) { NativeLog.Error(e); }
                });
            }
            return btn;
        }

        internal static void Tooltip(GameObject go, string name, string desc)
        {
            if (go == null) return;
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(desc)) return;
            var tip = go.GetComponent<UITooltip>();
            if (tip == null) tip = go.AddComponent<UITooltip>();
            tip.skipLocale = true;
            tip.tipName = name ?? string.Empty;
            tip.tipDesc = desc ?? string.Empty;
        }

        /// <summary>把整棵子树放到 "UI" 层，这样游戏的 UIUtil.IsPointerOverUIElement() 才能识别并屏蔽世界点击。</summary>
        internal static void ToUILayer(GameObject go)
        {
            if (go == null) return;
            int ui = LayerMask.NameToLayer("UI");
            if (ui < 0) return;
            SetLayerRecursive(go, ui);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }
    }
}

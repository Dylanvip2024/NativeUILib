using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NativeUILib
{
    /// <summary>
    /// 给 <c>OnGUI</c>（IMGUI）模组用的"原生皮肤"。
    ///
    /// 背景：游戏本体**完全没有 IMGUI**，它自己的 UI 是 UGUI + TextMeshPro。
    /// 所以 IMGUI 只能"仿原生"——做法是把游戏的原生九宫格贴图贴到 <see cref="GUISkin"/> 上，
    /// 并用从 TMP 字体反查出来的旧版 Font 做字形（这就是 KrokoshaCasualtiesMP 的 UIBullshit 做的事）。
    ///
    /// 用法：
    /// <code>
    /// private void OnGUI()
    /// {
    ///     using (NativeGUISkin.Scope())      // 或 Begin() / try-finally End()
    ///     {
    ///         GUILayout.Window(0, new Rect(10, 10, 300, 200), DrawWindow, "标题");
    ///     }
    /// }
    /// </code>
    /// </summary>
    public static class NativeGUISkin
    {
        /// <summary>第一次 Begin() 时自动 Install。</summary>
        public static bool AutoInstall = true;

        /// <summary>贴图放大倍数（相对 1080p 基准）。3 表示 4K 下放 6 倍，保持像素锐利。</summary>
        public static float TextureScaleMultiplier = 3f;

        /// <summary>uiBlockSmall 原始九宫格边距（像素）。</summary>
        public static int SmallSourceBorder = 4;

        /// <summary>uiBlockNano 原始九宫格边距（像素）。</summary>
        public static int NanoSourceBorder = 2;

        private static GUISkin _original;
        private static GUISkin _skin;
        private static bool _dirty = true;
        private static int _depth;
        private static int _pendingFocused;

        /// <summary>是否已经构建好皮肤。</summary>
        public static bool Installed => _skin != null;

        /// <summary>
        /// 上一帧鼠标停留在 IMGUI 元素上的数量。
        /// 配合 <see cref="InstallInputBlockPatch"/> 可以让游戏不要响应"穿透"过来的世界点击。
        /// </summary>
        public static int FocusedRects { get; private set; }

        /// <summary>当前是否处于 <see cref="Begin"/> / <see cref="End"/> 之间。</summary>
        public static bool IsActive => _depth > 0;

        internal static void EndFrame()
        {
            FocusedRects = _pendingFocused;
            _pendingFocused = 0;
        }

        /// <summary>分辨率变化 / 场景切换后调用，下次 Begin() 会重建贴图。</summary>
        public static void MarkDirty()
        {
            _dirty = true;
        }

        // ─────────────────────────────────────────────────────────
        // 安装 / 作用域
        // ─────────────────────────────────────────────────────────

        /// <summary>构建（或重建）原生皮肤。幂等。</summary>
        public static void Install()
        {
            try
            {
                NativeBootstrap.Ensure();
                if (_original == null) _original = GUI.skin;
                if (_original == null) return;
                if (_skin == null)
                {
                    _skin = UnityEngine.Object.Instantiate(_original);
                    _skin.hideFlags = HideFlags.HideAndDontSave;
                }
                ApplySkin(_skin);
                _dirty = false;
            }
            catch (Exception e)
            {
                NativeLog.Error("构建原生 IMGUI 皮肤失败：" + e);
                _skin = null;
            }
        }

        /// <summary>在 OnGUI 开头调用：把 GUI.skin 切到原生皮肤。</summary>
        public static void Begin()
        {
            _depth++;
            if (_depth > 1) return;
            if (AutoInstall && (_dirty || _skin == null)) Install();
            if (_skin != null) GUI.skin = _skin;
        }

        /// <summary>在 OnGUI 结尾调用：还原 GUI.skin（不要影响别的模组）。</summary>
        public static void End()
        {
            if (_depth <= 0) return;
            _depth--;
            if (_depth == 0 && _original != null) GUI.skin = _original;
        }

        /// <summary>用 <c>using</c> 包住你的 OnGUI 绘制代码。</summary>
        public static IDisposable Scope()
        {
            Begin();
            return new ScopeHandle();
        }

        private sealed class ScopeHandle : IDisposable
        {
            public void Dispose() => End();
        }

        // ─────────────────────────────────────────────────────────
        // 皮肤构建
        // ─────────────────────────────────────────────────────────

        private static void ApplySkin(GUISkin skin)
        {
            if (skin == null) return;

            float texScale = Mathf.Max(1f, TextureScaleMultiplier * NativeTheme.Scale);
            int smallBorder = Mathf.CeilToInt(SmallSourceBorder * texScale);
            int nanoBorder = Mathf.CeilToInt(NanoSourceBorder * texScale);

            var smallSpr = NativeAssets.PanelSmall;
            var nanoSpr = NativeAssets.PanelNano;
            var blockSpr = NativeAssets.Panel;
            var checkSpr = NativeAssets.Check;
            var whiteSpr = NativeAssets.White;

            Texture2D smallTex = smallSpr != null ? smallSpr.texture : null;
            Texture2D nanoTex = nanoSpr != null ? nanoSpr.texture : null;
            Texture2D blockTex = blockSpr != null ? blockSpr.texture : null;
            Texture2D checkTex = checkSpr != null ? checkSpr.texture : null;

            // ── 按钮（大按钮用 uiBlockSmall）──
            SetupButtonStyle(skin.button, smallTex ?? whiteSpr.texture, smallBorder, texScale);

            // ── 普通框 / 面板（uiBlockNano）──
            SetupBoxStyle(skin.box, nanoTex ?? whiteSpr.texture, nanoBorder, texScale);

            // ── 窗口 ──
            if (skin.window != null)
            {
                SetupBoxStyle(skin.window, smallTex ?? whiteSpr.texture, smallBorder, texScale);
                skin.window.padding = new RectOffset(smallBorder + 2, smallBorder + 2, smallBorder + 2, smallBorder + 2);
            }

            // ── 开关 ──
            if (skin.toggle != null)
            {
                if (nanoTex != null)
                {
                    skin.toggle.normal.background = NativeAssets.Process(nanoTex, texScale, 1.00f, 0.80f);
                    skin.toggle.hover.background = NativeAssets.Process(nanoTex, texScale, 0.90f, 0.85f);
                    skin.toggle.active.background = NativeAssets.Process(nanoTex, texScale, 0.80f, 0.85f);
                    skin.toggle.focused.background = skin.toggle.hover.background;
                    skin.toggle.border = new RectOffset(nanoBorder, nanoBorder, nanoBorder, nanoBorder);
                    skin.toggle.padding.left = nanoBorder;
                }
                if (checkTex != null)
                {
                    int cb = Mathf.CeilToInt(2f * texScale);
                    var on = NativeAssets.Process(checkTex, texScale, 1f, 1f);
                    skin.toggle.onNormal.background = on;
                    skin.toggle.onHover.background = on;
                    skin.toggle.onActive.background = on;
                    skin.toggle.onFocused.background = on;
                    skin.toggle.border = new RectOffset(cb, cb, cb, cb);
                    skin.toggle.padding.left = Mathf.CeilToInt(checkTex.width * texScale);
                }
                skin.toggle.fontSize = NativeTheme.FontSize(1f);
                skin.toggle.normal.textColor = NativeTheme.Text;
                skin.toggle.hover.textColor = NativeTheme.Text;
                skin.toggle.active.textColor = NativeTheme.Accent;
            }

            // ── 滑条 / 滚动条 ──
            SetupSlider(skin.horizontalSlider, skin.horizontalSliderThumb, nanoTex, nanoBorder, texScale, true);
            SetupSlider(skin.verticalSlider, skin.verticalSliderThumb, nanoTex, nanoBorder, texScale, false);
            SetupSlider(skin.horizontalScrollbar, skin.horizontalScrollbarThumb, nanoTex, nanoBorder, texScale, true);
            SetupSlider(skin.verticalScrollbar, skin.verticalScrollbarThumb, nanoTex, nanoBorder, texScale, false);

            // ── 输入框 ──
            SetupTextField(skin.textField, nanoTex, nanoBorder, texScale);
            SetupTextField(skin.textArea, nanoTex, nanoBorder, texScale);

            // ── 标签 / 文本颜色 ──
            if (skin.label != null)
            {
                skin.label.fontSize = NativeTheme.FontSize(1f);
                skin.label.normal.textColor = NativeTheme.Text;
                skin.label.richText = false;
            }
            if (skin.box != null) skin.box.fontSize = NativeTheme.FontSize(1f);

            // ── 字体（从 TMP 字体的 sourceFontFile 反查旧版 Font）──
            var font = NativeAssets.GUIFont;
            if (font != null)
            {
                skin.font = font;
                ClearStyleFonts(skin);
            }
            if (blockTex != null) skin.box.normal.background = NativeAssets.Process(blockTex, texScale, 1f, 0.9f);
        }

        private static void SetupButtonStyle(GUIStyle style, Texture2D tex, int border, float texScale)
        {
            if (style == null) return;
            if (tex != null)
            {
                style.normal.background = NativeAssets.Process(tex, texScale, 1.00f, 0.80f);
                style.hover.background = NativeAssets.Process(tex, texScale, 0.65f, 0.70f);
                style.active.background = NativeAssets.Process(tex, texScale, 0.40f, 0.70f);
                style.focused.background = NativeAssets.Process(tex, texScale, 0.40f, 0.70f);
                style.onNormal.background = NativeAssets.Process(tex, texScale, 1.05f, 1.00f);
                style.onHover.background = style.hover.background;
                style.onActive.background = style.active.background;
                style.onFocused.background = style.focused.background;
                style.border = new RectOffset(border, border, border, border);
                style.padding = new RectOffset(border, border, border, border);
                style.overflow = new RectOffset(0, 0, 0, 0);
            }
            style.normal.textColor = NativeTheme.Text;
            style.hover.textColor = NativeTheme.Text;
            style.active.textColor = NativeTheme.Accent;
            style.focused.textColor = NativeTheme.Text;
            style.fontSize = NativeTheme.FontSize(1f);
        }

        private static void SetupBoxStyle(GUIStyle style, Texture2D tex, int border, float texScale)
        {
            if (style == null) return;
            if (tex != null)
            {
                style.normal.background = NativeAssets.Process(tex, texScale, 1f, 0.90f);
                style.border = new RectOffset(border, border, border, border);
                style.padding = new RectOffset(border, border, border, border);
            }
            style.normal.textColor = NativeTheme.Text;
            style.fontSize = NativeTheme.FontSize(1f);
        }

        private static void SetupSlider(GUIStyle slider, GUIStyle thumb, Texture2D tex, int border, float texScale, bool horizontal)
        {
            if (slider == null) return;
            if (tex != null)
            {
                slider.normal.background = NativeAssets.Process(tex, texScale, 1f, 0.9f);
                slider.border = new RectOffset(border, border, border, border);
                slider.overflow = new RectOffset(0, 0, 0, 0);
                slider.padding = new RectOffset(0, 0, 0, 0);
            }
            if (thumb != null && tex != null)
            {
                int tb = Mathf.CeilToInt(NanoSourceBorder * texScale);
                thumb.normal.background = NativeAssets.Process(tex, texScale, 1f, 1f);
                thumb.hover.background = NativeAssets.Process(tex, texScale, 0.9f, 1f);
                thumb.active.background = NativeAssets.Process(tex, texScale, 0.8f, 1f);
                thumb.focused.background = thumb.hover.background;
                thumb.border = new RectOffset(tb, tb, tb, tb);
                thumb.overflow = new RectOffset(0, 0, 0, 0);
                float size = 20f * NativeTheme.Scale;
                if (horizontal) thumb.fixedWidth = size;
                else thumb.fixedHeight = size;
            }
            float thickness = 20f * NativeTheme.Scale;
            if (horizontal) slider.fixedHeight = thickness;
            else slider.fixedWidth = thickness;
        }

        private static void SetupTextField(GUIStyle style, Texture2D tex, int border, float texScale)
        {
            if (style == null) return;
            if (tex != null)
            {
                style.normal.background = NativeAssets.Process(tex, texScale, 0.80f, 0.80f);
                style.hover.background = NativeAssets.Process(tex, texScale, 0.90f, 0.80f);
                style.focused.background = NativeAssets.Process(tex, texScale, 1.00f, 0.80f);
                style.active.background = NativeAssets.Process(tex, texScale, 1.00f, 0.80f);
                style.border = new RectOffset(border, border, border, border);
            }
            style.normal.textColor = NativeTheme.Text;
            style.focused.textColor = NativeTheme.Text;
            style.active.textColor = NativeTheme.Text;
            style.hover.textColor = NativeTheme.Text;
            style.fontSize = NativeTheme.FontSize(1f);
        }

        private static void ClearStyleFonts(GUISkin skin)
        {
            // style.font = null 表示"继承 skin.font"
            var styles = new[]
            {
                skin.label, skin.box, skin.button, skin.toggle, skin.textField, skin.textArea,
                skin.window, skin.horizontalSlider, skin.horizontalSliderThumb,
                skin.verticalSlider, skin.verticalSliderThumb,
                skin.horizontalScrollbar, skin.horizontalScrollbarThumb,
                skin.verticalScrollbar, skin.verticalScrollbarThumb,
                skin.horizontalScrollbarLeftButton, skin.horizontalScrollbarRightButton,
                skin.verticalScrollbarUpButton, skin.verticalScrollbarDownButton,
            };
            for (int i = 0; i < styles.Length; i++)
            {
                if (styles[i] == null) continue;
                styles[i].font = null;
                styles[i].fontSize = NativeTheme.FontSize(1f);
            }
        }

        // ─────────────────────────────────────────────────────────
        // 绘制辅助
        // ─────────────────────────────────────────────────────────

        public static GUIStyle Style(string name)
        {
            var skin = GUI.skin;
            if (skin == null) return null;
            switch (name)
            {
                case "box": return skin.box;
                case "button": return skin.button;
                case "label": return skin.label;
                case "toggle": return skin.toggle;
                case "window": return skin.window;
                case "textField": return skin.textField;
                case "textArea": return skin.textArea;
                case "horizontalSlider": return skin.horizontalSlider;
                case "verticalSlider": return skin.verticalSlider;
                case "horizontalScrollbar": return skin.horizontalScrollbar;
                case "verticalScrollbar": return skin.verticalScrollbar;
                default: return skin.label;
            }
        }

        /// <summary>临时切换 GUI.skin（给 <c>GUILayout.Window</c> 的 GUI 回调用）。</summary>
        public static void WithSkin(Action action)
        {
            if (action == null) return;
            Begin();
            try { action(); }
            finally { End(); }
        }

        /// <summary>鼠标是否在给定 GUI 矩形内（IMGUI 的 y 轴向下，需要翻转）。</summary>
        public static bool IsCursorOver(in Rect rect)
        {
            var p = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return rect.Contains(p);
        }

        private static void RegisterHit(Rect rect)
        {
            if (IsCursorOver(rect)) _pendingFocused++;
        }

        /// <summary>画一个原生九宫格面板，并登记"鼠标悬停"（用于屏蔽世界输入）。</summary>
        public static void Panel(Rect rect, float brightness = 1f, float alpha = 0.9f)
        {
            var box = GUI.skin.box;
            var spr = NativeAssets.PanelNano;
            Texture2D oldBg = box.normal.background;
            RectOffset oldBorder = box.border;
            if (spr != null)
            {
                float texScale = Mathf.Max(1f, TextureScaleMultiplier * NativeTheme.Scale);
                int b = Mathf.CeilToInt(NanoSourceBorder * texScale);
                box.normal.background = NativeAssets.Process(spr.texture, texScale, brightness, alpha);
                box.border = new RectOffset(b, b, b, b);
            }
            GUI.Box(rect, GUIContent.none);
            box.normal.background = oldBg;
            box.border = oldBorder;
            RegisterHit(rect);
        }

        /// <summary>放大字号的标签（原生风格的"标题"）。</summary>
        public static void BigLabel(string text, float multiplier = 1.3f)
        {
            var style = GUI.skin.label;
            int old = style.fontSize;
            style.fontSize = Mathf.RoundToInt(old * multiplier);
            var content = new GUIContent(text ?? string.Empty);
            var size = style.CalcSize(content);
            GUILayout.Label(content, GUILayout.Height(size.y));
            style.fontSize = old;
        }

        /// <summary>按钮 + 命中登记。</summary>
        public static bool Button(Rect rect, string text)
        {
            bool clicked = GUI.Button(rect, text);
            RegisterHit(rect);
            return clicked;
        }

        /// <summary>按钮 + 是否聚焦（供自定义高亮用），并登记"鼠标在 IMGUI 上"。</summary>
        public static bool FocusableButton(Rect rect, string text, out bool focused, GUIStyle style = null)
        {
            bool clicked = style == null ? GUI.Button(rect, text) : GUI.Button(rect, text, style);
            focused = IsCursorOver(rect);
            if (focused) _pendingFocused++;
            return clicked;
        }

        // ─────────────────────────────────────────────────────────
        // 世界输入屏蔽（可选）
        // ─────────────────────────────────────────────────────────

        private static Harmony _harmony;

        /// <summary>
        /// 安装一个 Harmony 补丁：当鼠标停在 IMGUI 元素上时，让游戏的
        /// <c>UIUtil.IsPointerOverUIElement()</c> 返回 true，从而屏蔽世界点击（挖方块/开枪）。
        /// 只调用一次即可。
        /// </summary>
        public static void InstallInputBlockPatch()
        {
            if (_harmony != null) return;
            try
            {
                _harmony = new Harmony("com.mod.casualties.nativeuilib.imguiblock");
                _harmony.PatchAll(typeof(NativeIMGUIInputBlockPatch));
                NativeLog.Info("已安装 UIUtil.IsPointerOverUIElement 补丁（IMGUI 命中时屏蔽世界输入）");
            }
            catch (Exception e)
            {
                NativeLog.Error("安装输入屏蔽补丁失败：" + e);
                _harmony = null;
            }
        }
    }

    /// <summary>把 IMGUI 的命中计数并入游戏的"鼠标在 UI 上"判定。</summary>
    [HarmonyPatch(typeof(UIUtil), "IsPointerOverUIElement", new Type[] { typeof(List<RaycastResult>) })]
    internal static class NativeIMGUIInputBlockPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (!__result && NativeGUISkin.FocusedRects > 0) __result = true;
        }
    }
}

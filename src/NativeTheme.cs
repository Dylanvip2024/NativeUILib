using UnityEngine;

namespace NativeUILib
{
    /// <summary>
    /// 原生风格的尺寸 / 颜色 / 字号规范。
    ///
    /// 所有"数值"都以 1080p 为基准（和游戏 <c>GlobalDark.uiScale = Screen.height / 1080f</c> 一致），
    /// 使用时统一乘以 <see cref="Scale"/>。本库内部已经乘好了，只有你手写 RectTransform 时才需要。
    /// </summary>
    public static class NativeTheme
    {
        /// <summary>游戏原生 TMP 字体资产名（在 sharedassets0 里，不在 Resources）。</summary>
        public const string NativeFontName = "Retro GamingPix";

        // ── 缩放 ────────────────────────────────────────────────

        /// <summary>全局 UI 缩放（= 屏幕高 / 1080）。</summary>
        public static float Scale
        {
            get
            {
                float s = Screen.height / 1080f;
                return s < 0.5f ? 0.5f : s;
            }
        }

        public static Vector2 Scaled(float x, float y) => new Vector2(x, y) * Scale;

        public static float Scaled(float v) => v * Scale;

        /// <summary>原生风格字号：基准 20 * Scale * mult。</summary>
        public static int FontSize(float mult = 1f)
        {
            int v = Mathf.RoundToInt(20f * Scale * mult);
            return v < 6 ? 6 : v;
        }

        // ── 尺寸（未缩放，基准 1080p）────────────────────────────

        public const float TitleHeight = 30f;
        public const float RowHeight = 30f;
        public const float ButtonHeight = 34f;
        public const float ControlWidth = 150f;
        public const float Padding = 10f;
        public const float Spacing = 6f;

        // ── 颜色 ────────────────────────────────────────────────

        public static Color Text => Color.white;
        public static Color TextDim => new Color(0.9f, 0.9f, 0.9f, 1f);
        /// <summary>游戏预设选中用的金色（PreRunScript.presetSelectImage）。</summary>
        public static Color Accent => new Color(1f, 0.765f, 0f, 1f);
        public static Color Danger => new Color(1f, 0.35f, 0.35f, 1f);
        /// <summary>面板底色（白 + 高 alpha，配合九宫格贴图）。</summary>
        public static Color Panel => new Color(1f, 1f, 1f, 0.92f);
        public static Color TitleBar => new Color(1f, 1f, 1f, 0.85f);
        public static Color Separator => new Color(0f, 0f, 0f, 0.25f);
        public static Color Dim => new Color(0f, 0f, 0f, 0.55f);

        // ── 交互色（对齐 KrokMP 从游戏面板反推的 brightness/alpha）──

        /// <summary>把"亮度/透明度"换算成按钮各状态的颜色，和游戏原生按钮的观感一致。</summary>
        public static Color StateColor(float brightness, float alpha)
        {
            return new Color(brightness, brightness, brightness, alpha);
        }

        public static Color NormalState => StateColor(1f, 0.85f);
        public static Color HoverState => StateColor(0.65f, 0.70f);
        public static Color PressedState => StateColor(0.40f, 0.70f);
        public static Color DisabledState => StateColor(0.45f, 0.45f);
    }
}

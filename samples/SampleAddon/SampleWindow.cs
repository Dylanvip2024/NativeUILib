using System;
using NativeUILib;
using UnityEngine;
using UnityEngine.UI;

namespace SampleAddon
{
    /// <summary>
    /// 演示窗口：把 NativeUILib 的常用控件全用一遍。
    /// 直接照抄这个文件改就是你自己模组的设置面板。
    /// </summary>
    internal static class SampleWindow
    {
        internal static NativeWindow Build()
        {
            // 默认父节点 = 库自带的常驻 Overlay Canvas（跨场景存活，sortingOrder=100）
            var w = NativeUI.Window("NativeUILib 演示");
            w.RememberPosition("sample.demo");   // 记住用户拖动的位置

            w.AddHeader("原生风格控件演示");
            w.AddLabel("面板 / 按钮 / 开关 / 滑条 / 下拉框 / 输入框全部直接使用游戏自己的 UI 预制体" +
                       "（Special/GameSetting*），所以外观和游戏设置菜单一模一样。");

            w.AddSeparator();

            // 开关（原生 Toggle 行）
            w.AddToggle("启用功能", SampleAddonPlugin.DemoBool,
                v => { SampleAddonPlugin.DemoBool = v; },
                "打开或关闭某个功能");

            // 滑条（原生 Slider 行，第二行是数值文本）
            w.AddSlider("强度", 0f, 1f, SampleAddonPlugin.DemoFloat,
                v => { SampleAddonPlugin.DemoFloat = v; },
                "0.00", "数值越大效果越强");

            // 下拉框（原生 TMP_Dropdown 行）
            w.AddDropdown("模式", new[] { "普通", "困难", "自定义" }, SampleAddonPlugin.DemoMode,
                i => { SampleAddonPlugin.DemoMode = i; });

            // 输入框（复用 Int 行的 TMP_InputField）
            w.AddInput("名字", SampleAddonPlugin.DemoName,
                s => { SampleAddonPlugin.DemoName = s; NativeUI.Toast("名字 = " + s); },
                freeText: true);

            w.AddSeparator();

            // 说明文字 + 右侧按钮的原生行
            w.AddButtonRow("提示音", "试听", () => NativeUI.PlayMiniClick(), "播放游戏原生小按钮音效");

            // 自绘的行内按钮
            w.AddInlineButton("原生对话框", "打开", ShowDialog);
            w.AddInlineButton("游戏设置菜单", "打开", () => NativeUI.OpenNativeSettings(2)); // 2 = Game
            w.AddInlineButton("原生提示条", "显示", () => NativeUI.Toast("这是一条原生提示条"));

            w.AddSeparator();

            // 金色富文本（TMP 支持 <color=#>）
            w.AddLabel("<color=#FFC300>提示：</color>按 F7 或点下面的按钮关闭本窗口", 0.9f);

            w.AddButton("关闭", () => w.Close());
            w.AddLabel("素材状态：" + (NativeAssets.IsNativeFont ? "已拿到原生像素字体" : "字体回退中")
                       + " / 中文：" + (NativeUI.ChineseAvailable ? "可用" : "缺失"), 0.75f);

            return w;
        }

        private static void ShowDialog()
        {
            NativeUI.Confirm("示例确认框", "要执行一个假操作吗？",
                onYes: () => NativeUI.Toast("你点了 是"),
                onNo: () => NativeUI.Toast("你点了 否"));

            // 也可以做多选项：
            // NativeDialog.Choose("选择", "选一个吧",
            //     new NativeDialog.Choice("方案 A", () => { }),
            //     new NativeDialog.Choice("方案 B", () => { }),
            //     new NativeDialog.Choice("取消", null));
        }

        /// <summary>
        /// 演示"完全自己拼一个原生风格面板"（不依赖游戏预制体）。
        /// </summary>
        internal static void BuildManualPanel()
        {
            float s = NativeTheme.Scale;

            var panel = NativeUI.Panel(null, "uiBlockSmall", 360f, 160f);
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            // 标题
            var title = new GameObject("Title", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
            title.transform.SetParent(panel.transform, false);
            var font = NativeUI.Font;
            if (font != null) title.font = font;
            title.text = "手工面板";
            title.fontSize = NativeTheme.FontSize(1.2f);
            title.color = NativeTheme.Accent;
            title.alignment = TMPro.TextAlignmentOptions.Center;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -6f * s);
            trt.sizeDelta = new Vector2(-12f * s, 26f * s);

            // 原生按钮
            var btnGo = new GameObject("OK", typeof(RectTransform));
            btnGo.transform.SetParent(panel.transform, false);
            var img = btnGo.AddComponent<Image>();
            img.sprite = NativeAssets.PanelNano;      // 原生九宫格贴图
            img.type = Image.Type.Sliced;
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            var crt = btnGo.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 10f * s);
            crt.sizeDelta = new Vector2(120f * s, 30f * s);
            btn.onClick.AddListener(() =>
            {
                NativeUI.PlayClick();
                UnityEngine.Object.Destroy(panel.gameObject);
            });

            var label = new GameObject("Text", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
            label.transform.SetParent(btnGo.transform, false);
            if (font != null) label.font = font;
            label.text = "确定";
            label.fontSize = NativeTheme.FontSize(0.95f);
            label.alignment = TMPro.TextAlignmentOptions.Center;
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            // 原生 tooltip（由游戏的 GlobalDark 统一绘制）
            var tip = btnGo.AddComponent<UITooltip>();
            tip.skipLocale = true;
            tip.tipName = "确定";
            tip.tipDesc = "关掉这个手工面板";
        }
    }
}

using System;
using TMPro;
using NativeUILib;
using UnityEngine;
using UnityEngine.UI;

namespace SampleAddon
{
    /// <summary>
    /// 游戏内 HUD 示例：在游戏自己的 Canvas 上挂一个原生风格的小面板。
    ///
    /// 要点：
    /// <list type="bullet">
    /// <item>挂在 <c>PlayerCamera.main.mainCanvas</c> 上 = 跟游戏 HUD 同一个层级（会被游戏的 UI 缩放影响）；</item>
    /// <item>挂在 <c>NativeUI.Parent</c>（库自带 Overlay）= 不受游戏 UI 影响，跨场景常驻；</item>
    /// <item>无论哪种，整棵子树都会自动被放到 "UI" 层，所以鼠标悬停时游戏不会响应世界点击。</item>
    /// </list>
    /// </summary>
    internal sealed class SampleHud : MonoBehaviour
    {
        internal static bool Visible = true;

        private GameObject _root;
        private TextMeshProUGUI _text;
        private float _timer;

        private void Update()
        {
            if (!Visible || !NativeUI.InWorld || PlayerCamera.main == null)
            {
                if (_root != null) _root.SetActive(false);
                return;
            }

            if (_root == null) Build();
            if (_root == null) return;

            _root.SetActive(true);

            // 每 0.25 秒刷新一次文本即可（别每帧拼字符串）
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f)
            {
                _timer = 0.25f;
                var body = PlayerCamera.main.body;
                _text.text = string.Format("演示 HUD\n体力 {0:0}\n血量 {1:0}",
                    body != null ? body.stamina : 0f,
                    body != null ? body.brainHealth : 0f);
            }
        }

        private void Build()
        {
            try
            {
                float s = NativeTheme.Scale;

                // 面板：原生 uiBlockNano 九宫格
                var img = NativeUI.Panel(null, "uiBlockNano", 190f, 96f);
                _root = img.gameObject;
                _root.name = "[SampleAddon] HUD";
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);   // 右上角
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-16f * s, -16f * s);

                // 文本
                var go = new GameObject("Text", typeof(RectTransform));
                go.transform.SetParent(_root.transform, false);
                _text = go.AddComponent<TextMeshProUGUI>();
                var font = NativeUI.Font;
                if (font != null) _text.font = font;
                _text.fontSize = NativeTheme.FontSize(0.8f);
                _text.color = NativeTheme.Text;
                _text.alignment = TextAlignmentOptions.TopLeft;
                _text.text = "演示 HUD";
                var trt = _text.rectTransform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(8f * s, 6f * s);
                trt.offsetMax = new Vector2(-8f * s, -6f * s);

                // 原生 tooltip
                NativeBuild_AddTooltip(_root, "演示 HUD", "这是用 NativeUILib 画的原生风格 HUD");
            }
            catch (Exception e)
            {
                SampleAddonPlugin.Log.LogError("创建 HUD 失败：" + e);
                _root = null;
            }
        }

        private static void NativeBuild_AddTooltip(GameObject go, string name, string desc)
        {
            // NativeBuild 是库内部类型，外部只挂 UITooltip 组件即可
            var tip = go.GetComponent<UITooltip>();
            if (tip == null) tip = go.AddComponent<UITooltip>();
            tip.skipLocale = true;
            tip.tipName = name;
            tip.tipDesc = desc;
        }
    }
}

using System;
using NativeUILib;
using UnityEngine;

namespace SampleAddon
{
    /// <summary>
    /// IMGUI（OnGUI）版本的原生皮肤演示。
    ///
    /// 注意：**游戏本体不用 IMGUI**，所以这条路只能"仿原生"——把游戏的原生九宫格贴图
    /// 和从 TMP 反查出来的像素字体贴到 GUISkin 上（KrokoshaCasualtiesMP 的 UIBullshit 就是这么干的）。
    /// 新写的模组建议优先用 UGUI 版（NativeUI.Window）。
    ///
    /// 关键点：GameObject 上必须有原生 UI 的 IMGUI 文本，否则中文会是方块。
    /// </summary>
    internal sealed class SampleImGuiWindow : MonoBehaviour
    {
        internal static bool Visible;

        private Rect _rect = new Rect(40f, 120f, 380f, 260f);
        private const int WindowId = 0x4E55;  // 'NU'

        private void Awake()
        {
            // 可选：让"鼠标停在 IMGUI 上"时游戏不再响应世界点击（挖方块 / 开枪）
            NativeGUISkin.InstallInputBlockPatch();
        }

        private void OnGUI()
        {
            if (!Visible) return;

            // using 包住：进入时切到原生皮肤，退出时还原（不要影响其他模组的 OnGUI）
            using (NativeGUISkin.Scope())
            {
                float s = NativeTheme.Scale;
                _rect = GUILayout.Window(WindowId, _rect, DrawWindow, "IMGUI 原生皮肤演示",
                                         GUILayout.Width(380f * s), GUILayout.Height(260f * s));
            }
        }

        private void DrawWindow(int id)
        {
            float s = NativeTheme.Scale;

            NativeGUISkin.BigLabel("原生贴图 + 像素字体", 1.3f);
            GUILayout.Space(4f * s);
            GUILayout.Label("这个窗口的框、按钮、开关、滑条全部用游戏自带的九宫格贴图绘制。");
            GUILayout.Label("中文依赖系统字体 fallback（TMP 字体没有 CJK 字形）。");

            GUILayout.Space(6f * s);
            SampleAddonPlugin.DemoBool = GUILayout.Toggle(SampleAddonPlugin.DemoBool, " 启用功能");

            GUILayout.Space(4f * s);
            NativeGUISkin.BigLabel("强度：" + SampleAddonPlugin.DemoFloat.ToString("0.00"), 1f);
            SampleAddonPlugin.DemoFloat = GUILayout.HorizontalSlider(SampleAddonPlugin.DemoFloat, 0f, 1f);

            GUILayout.Space(6f * s);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("原生音效"))
            {
                NativeUI.PlayClick();
            }
            if (GUILayout.Button("关闭"))
            {
                Visible = false;
            }
            GUILayout.EndHorizontal();

            NativeGUISkin.Panel(new Rect(6f * s, _rect.height - 34f * s, _rect.width - 12f * s, 28f * s),
                                1f, 0.85f);
            GUI.Label(new Rect(12f * s, _rect.height - 32f * s, _rect.width - 24f * s, 24f * s),
                      "提示：这个底栏是用 NativeGUISkin.Panel 手绘的");

            GUI.DragWindow(new Rect(0f, 0f, _rect.width, 24f * s));
        }
    }
}

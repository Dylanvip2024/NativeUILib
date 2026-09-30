using System;
using BepInEx;
using HarmonyLib;
using NativeUILib;
using UnityEngine;

namespace SampleAddon
{
    /// <summary>
    /// 示例 Addon：演示 NativeUILib 的全部主要用法。
    ///
    /// 游戏内按 F7 打开演示窗口；主菜单 / 暂停菜单会多出一个 "演示面板" 按钮。
    /// </summary>
    [BepInPlugin(Guid, PluginName, PluginVersion)]
    // 声明对 NativeUILib 的依赖：确保库先初始化（软依赖即可，因为库是懒加载的）
    [BepInDependency(NativeUILibPlugin.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class SampleAddonPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.example.casualties.sampleaddon";
        public const string PluginName = "SampleAddon (NativeUILib 示例)";
        public const string PluginVersion = "1.0.0";

        internal static SampleAddonPlugin Instance { get; private set; }
        internal static BepInEx.Logging.ManualLogSource Log { get; private set; }

        /// <summary>演示用配置（真实项目请用 ConfigFile 或 PlayerPrefs 持久化）。</summary>
        internal static bool DemoBool = true;
        internal static float DemoFloat = 0.5f;
        internal static int DemoMode = 0;
        internal static string DemoName = "幸存者";

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // 1) 初始化库（其实不调也行，第一次用 API 时会自动初始化）
            NativeUI.Init();
            NativeUI.SceneChanged += OnSceneChanged;

            // 2) 打开原生设置菜单时用到的补丁（往游戏原生设置里加一项）
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(SampleSettingsModule));

            // 3) 挂两个演示组件：IMGUI 原生皮肤窗口 + 游戏内 HUD
            gameObject.AddComponent<SampleImGuiWindow>();
            gameObject.AddComponent<SampleHud>();

            Logger.LogInfo(PluginName + " 已加载 | F7=演示窗口 F8=提示条/确认框 F9=IMGUI窗口 F10=HUD");
        }

        private void OnDestroy()
        {
            NativeUI.SceneChanged -= OnSceneChanged;
            if (_harmony != null) _harmony.UnpatchSelf();
        }

        private void OnSceneChanged()
        {
            Logger.LogInfo("场景切换，重新注入菜单按钮");
            InjectMenuButtons();
        }

        private void Start()
        {
            // 延迟一帧：等场景里的 PreRunScript / PauseHandler 就绪
            NativeUI.RunNextFrame(InjectMenuButtons);
        }

        private void InjectMenuButtons()
        {
            // 主菜单按钮（克隆游戏原生按钮，外观完全一致）
            try
            {
                var go = NativeMenu.AddMainMenuButton("演示面板 (F7)", ToggleDemoWindow, rt =>
                {
                    // 自己微调位置：贴到底部中间
                    rt.anchorMin = new Vector2(0.35f, 0.02f);
                    rt.anchorMax = new Vector2(0.65f, 0.08f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.anchoredPosition = Vector2.zero;
                });
                if (go != null) Logger.LogInfo("已注入主菜单按钮");
            }
            catch (Exception e) { Logger.LogWarning("主菜单按钮注入失败：" + e.Message); }

            // 暂停菜单按钮
            try
            {
                var go = NativeMenu.AddPauseMenuButton("演示面板", ToggleDemoWindow);
                if (go != null) Logger.LogInfo("已注入暂停菜单按钮");
            }
            catch (Exception e) { Logger.LogWarning("暂停菜单按钮注入失败：" + e.Message); }
        }

        private void Update()
        {
            // F7：开关演示窗口
            if (Input.GetKeyDown(KeyCode.F7)) ToggleDemoWindow();

            // F8：原生提示条 + 确认框演示
            if (Input.GetKeyDown(KeyCode.F8))
            {
                NativeUI.Toast("这是一条原生提示条");
                NativeUI.Confirm("示例确认框", "要点一下确定吗？",
                    () => NativeUI.Toast("你点了确定"),
                    () => NativeUI.Toast("你点了取消"));
            }

            // F9：IMGUI 原生皮肤窗口
            if (Input.GetKeyDown(KeyCode.F9)) SampleImGuiWindow.Visible = !SampleImGuiWindow.Visible;

            // F10：原生风格 HUD
            if (Input.GetKeyDown(KeyCode.F10)) SampleHud.Visible = !SampleHud.Visible;

            // F11：打印素材诊断（排查"贴图/字体找不到"）
            if (Input.GetKeyDown(KeyCode.F11)) NativeUI.DumpAssets();
        }

        private static NativeWindow _demo;

        internal static void ToggleDemoWindow()
        {
            if (_demo != null)
            {
                _demo.Close();
                _demo = null;
                return;
            }
            _demo = SampleWindow.Build();
            _demo.Closed += () => _demo = null;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NativeUILib
{
    /// <summary>
    /// NativeUILib 的统一入口。所有"快捷画原生风格 UI"的调用都从这里开始。
    ///
    /// 典型用法：
    /// <code>
    /// var w = NativeUI.Window("我的模组设置");
    /// w.AddLabel("欢迎");
    /// w.AddToggle("启用", true, v => Enabled = v);
    /// w.AddButton("关闭", () => w.Close());
    ///
    /// NativeUI.Toast("已保存");
    /// NativeUI.Confirm("确认", "确定要重置吗？", () => Reset());
    /// </code>
    ///
    /// 所有 API 都是**懒初始化 + 异常安全**的：即使在没有 Canvas 的场景调用也不会抛异常，
    /// 只会记录一条警告。因此可以放心在 Awake / Update / OnGUI 任意时机调用。
    /// </summary>
    public static class NativeUI
    {
        public const string Version = "1.0.0";

        private static Canvas _overlay;
        private static Transform _customParent;
        private static int _sortingOrder = 100;
        private static bool _warnedEventSystem;
        private static bool _interactiveCreated;
        private static GameObject _lastToast;

        /// <summary>场景切换时触发（可用来重建挂在游戏 Canvas 上的 HUD）。</summary>
        public static event Action SceneChanged;

        // ─────────────────────────────────────────────────────────
        // 初始化 / 父节点
        // ─────────────────────────────────────────────────────────

        /// <summary>幂等初始化：创建常驻宿主与高层级 Overlay Canvas。</summary>
        public static void Init()
        {
            NativeBootstrap.Ensure();
            EnsureOverlay();
        }

        /// <summary>本库自己的常驻 Overlay Canvas（Screen Space Overlay，默认 sortingOrder=100）。</summary>
        public static Canvas Overlay
        {
            get
            {
                EnsureOverlay();
                return _overlay;
            }
        }

        /// <summary>默认父节点。用 <see cref="SetParent"/> 可以改成游戏自己的 Canvas。</summary>
        public static Transform Parent
        {
            get
            {
                if (_customParent != null) return _customParent;
                EnsureOverlay();
                return _overlay != null ? _overlay.transform : NativeBootstrap.HostObject.transform;
            }
        }

        /// <summary>
        /// 把默认父节点改成游戏自己的 Canvas，例如：
        /// <c>NativeUI.SetParent(PlayerCamera.main.mainCanvas.transform)</c>。
        /// 传 null 恢复成库自带的 Overlay。
        /// </summary>
        public static void SetParent(Transform parent)
        {
            _customParent = parent;
        }

        /// <summary>Overlay 的 Canvas.sortingOrder（越大越靠前）。</summary>
        public static int SortingOrder
        {
            get { return _sortingOrder; }
            set
            {
                _sortingOrder = value;
                if (_overlay != null) _overlay.sortingOrder = value;
            }
        }

        private static void EnsureOverlay()
        {
            if (_overlay != null) return;
            try
            {
                var bootstrap = NativeBootstrap.Ensure();
                var go = new GameObject("[NativeUILib] Overlay", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                go.transform.SetParent(bootstrap.transform, false);

                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = _sortingOrder;
                canvas.pixelPerfect = false;

                _overlay = canvas;
            }
            catch (Exception e)
            {
                NativeLog.Error("创建 Overlay Canvas 失败：" + e);
            }
        }

        // ─────────────────────────────────────────────────────────
        // EventSystem 体检
        // ─────────────────────────────────────────────────────────
        //
        // 注意：**不要**在创建 Canvas 的那一刻检查 EventSystem。
        // BepInEx 加载插件时游戏还停在引导场景（level0），EventSystem 还没出现，
        // 那时警告必然是误报——而且 UGUI 是"每次事件时查 EventSystem.current"，
        // 后面场景加载出 EventSystem 之后，先前创建的界面照样能点。
        //
        // 所以这里的策略是：
        //   1) 只在"确实创建了可交互 UI"之后才考虑检查；
        //   2) 只在游戏自己的 UI 基础设施（PreRunScript / PlayerCamera / GlobalDark）已经存在时才判定，
        //      否则说明还没进入正片，不做判断；
        //   3) 全程最多警告一次，且措辞只描述事实。

        /// <summary>当前是否存在 EventSystem（UGUI 按钮/滑条/拖动能响应鼠标的前提）。</summary>
        public static bool HasEventSystem
        {
            get
            {
                try { return UnityEngine.EventSystems.EventSystem.current != null; }
                catch { return false; }
            }
        }

        /// <summary>游戏自己的 UI 基础设施是否已经起来（用来区分"启动引导阶段"和"真的有问题"）。</summary>
        private static bool GameUIReady
        {
            get
            {
                try
                {
                    if (PreRunScript.instance != null) return true;
                    if (PlayerCamera.main != null) return true;
                    if (GlobalDark.main != null) return true;
                }
                catch { }
                return false;
            }
        }

        /// <summary>库内部用：有可交互 UI 被创建时调用，触发一次体检。</summary>
        internal static void NotifyInteractiveUI()
        {
            _interactiveCreated = true;
            TryCheckEventSystem();
        }

        private static void TryCheckEventSystem()
        {
            if (_warnedEventSystem || !_interactiveCreated) return;
            if (HasEventSystem) return;      // 有就没问题（大多数情况走到这里）
            if (!GameUIReady) return;        // 还在引导阶段，判断不了 —— 静默
            _warnedEventSystem = true;
            NativeLog.Warn("场景中没有 EventSystem，UGUI 交互（按钮/滑条/拖动）将不会响应鼠标。" +
                           "如果你的界面确实点不动，请检查是否有其它模组禁用了 EventSystem；" +
                           "否则可以忽略这条消息。可用 NativeUI.HasEventSystem 自查。");
        }

        internal static void NotifySceneChanged()
        {
            TryCheckEventSystem();
            if (SceneChanged != null)
            {
                try { SceneChanged(); }
                catch (Exception e) { NativeLog.Error(e); }
            }
        }

        /// <summary>下一帧执行（用常驻宿主的协程）。</summary>
        public static void RunNextFrame(Action action)
        {
            if (action == null) return;
            var host = NativeBootstrap.Ensure();
            host.StartCoroutine(RunNextFrameRoutine(action));
        }

        private static IEnumerator RunNextFrameRoutine(Action action)
        {
            yield return null;
            try { action(); }
            catch (Exception e) { NativeLog.Error(e); }
        }

        // ─────────────────────────────────────────────────────────
        // 窗口
        // ─────────────────────────────────────────────────────────

        /// <summary>创建一个原生风格窗口（默认 460x300，自动高度，可拖动，可关闭）。</summary>
        public static NativeWindow Window(string title, float width = 460f, float height = 300f, bool autoSize = true)
        {
            var w = NativeWindow.Create(title, NativeTheme.Scaled(width, height), Parent, true, true);
            w.AutoSize = autoSize;
            w.RefreshLayout();
            return w;
        }

        /// <summary>在指定父节点下创建窗口（例如挂到游戏自己的 Canvas 上）。</summary>
        public static NativeWindow Window(string title, Transform parent, float width = 460f, float height = 300f, bool autoSize = true)
        {
            var w = NativeWindow.Create(title, NativeTheme.Scaled(width, height), parent != null ? parent : Parent, true, true);
            w.AutoSize = autoSize;
            w.RefreshLayout();
            return w;
        }

        public static int OpenWindowCount => NativeWindow.OpenCount;

        public static void CloseAllWindows() => NativeWindow.CloseAll();

        // ─────────────────────────────────────────────────────────
        // 提示 / 对话框
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// 原生提示条。游戏内直接调用游戏自己的 <c>PlayerCamera.DoAlert</c>（100% 原生）；
        /// 主菜单没有 PlayerCamera 时用原生贴图自绘一个，几秒后自动消失。
        /// </summary>
        public static void Toast(string text, bool important = false, float seconds = 4f)
        {
            if (string.IsNullOrEmpty(text)) return;

            var cam = PlayerCamera.main;
            if (cam != null)
            {
                try
                {
                    cam.DoAlert(text, important);
                    return;
                }
                catch (Exception e) { NativeLog.Warn("DoAlert 失败，改用自绘提示条：" + e.Message); }
            }

            try
            {
                if (_lastToast != null) UnityEngine.Object.Destroy(_lastToast);
                float s = NativeTheme.Scale;
                var img = NativeBuild.Panel("NativeToast", Parent, NativeAssets.PanelSmall, NativeTheme.Panel);
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 120f * s);
                rt.sizeDelta = new Vector2(420f * s, 48f * s);

                var label = NativeBuild.Text("Text", img.transform, text, NativeTheme.FontSize(1f), TextAlignmentOptions.Center, true);
                NativeBuild.Stretch(label.rectTransform, 12f * s, 4f * s, 12f * s, 4f * s);

                NativeBuild.ToUILayer(img.gameObject);
                _lastToast = img.gameObject;
                UnityEngine.Object.Destroy(img.gameObject, Mathf.Max(0.5f, seconds));
            }
            catch (Exception e)
            {
                NativeLog.Error("自绘提示条失败：" + e);
            }
        }

        /// <summary>只有一个"确定"按钮的原生对话框。</summary>
        public static NativeWindow Message(string title, string message, string okText = "确定", Action onOk = null)
        {
            return NativeDialog.Message(title, message, okText, onOk);
        }

        /// <summary>原生"是 / 否"确认框。</summary>
        public static NativeWindow Confirm(string title, string message, Action onYes, Action onNo = null,
                                           string yesText = "是", string noText = "否")
        {
            return NativeDialog.Confirm(title, message, onYes, onNo, yesText, noText);
        }

        // ─────────────────────────────────────────────────────────
        // 游戏原生 UI 快捷入口
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// 打开游戏自己的设置菜单。
        /// <paramref name="category"/> 对应 <c>Setting.SettingCategory</c>：0=Video 1=Audio 2=Game 3=Input 4=Language。
        /// </summary>
        public static void OpenNativeSettings(int category = 2)
        {
            try
            {
                if (SettingsMenu.instance == null)
                {
                    Transform parent = null;
                    if (WorldGeneration.world == null && PreRunScript.instance != null)
                        parent = PreRunScript.instance.mainCanvas != null
                            ? PreRunScript.instance.mainCanvas.transform
                            : PreRunScript.instance.transform;
                    else if (PauseHandler.main != null)
                        parent = PauseHandler.main.transform;
                    else if (PlayerCamera.main != null && PlayerCamera.main.mainCanvas != null)
                        parent = PlayerCamera.main.mainCanvas.transform;

                    if (parent == null)
                    {
                        NativeLog.Warn("找不到可挂载原生设置菜单的 Canvas");
                        return;
                    }
                    SettingsMenu.OpenMenu(parent);
                    // Start() 还没跑，SelectTab 需要等一帧
                    RunNextFrame(() => SelectNativeSettingsTab(category));
                    return;
                }
                SelectNativeSettingsTab(category);
            }
            catch (Exception e)
            {
                NativeLog.Error("打开原生设置菜单失败：" + e);
            }
        }

        public static void SelectNativeSettingsTab(int category)
        {
            try
            {
                if (SettingsMenu.instance != null) SettingsMenu.instance.SelectTab(category);
            }
            catch (Exception e)
            {
                NativeLog.Warn("切换设置页签失败：" + e.Message);
            }
        }

        /// <summary>关闭游戏原生设置菜单（如果开着）。</summary>
        public static void CloseNativeSettings()
        {
            try
            {
                if (SettingsMenu.instance != null) SettingsMenu.instance.Close();
            }
            catch (Exception e) { NativeLog.Warn("关闭原生设置菜单失败：" + e.Message); }
        }

        /// <summary>用游戏自带的"打字机文本"界面显示一段剧情/说明文本。</summary>
        public static void ShowNativeText(string text, bool cover = false, List<Sprite> sprites = null, TMP_FontAsset font = null)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return;
                ScrollableText.CreateText(text, cover, sprites, font);
            }
            catch (Exception e) { NativeLog.Error("显示原生文本失败：" + e); }
        }

        // ─────────────────────────────────────────────────────────
        // 输入 / 状态
        // ─────────────────────────────────────────────────────────

        /// <summary>鼠标是否在 UI 上（游戏自己的判定，落在 UI 层的元素都会被算进去）。</summary>
        public static bool IsPointerOverUI()
        {
            try { return UIUtil.IsPointerOverUIElement(); }
            catch { return false; }
        }

        /// <summary>是否处于世界场景（而非主菜单）。</summary>
        public static bool InWorld
        {
            get { try { return WorldGeneration.world != null; } catch { return false; } }
        }

        /// <summary>是否处于暂停菜单中。</summary>
        public static bool IsPaused
        {
            get { try { return PauseHandler.main != null && PauseHandler.main.isPaused; } catch { return false; } }
        }

        /// <summary>游戏原生设置菜单是否开着。</summary>
        public static bool NativeSettingsOpen
        {
            get { try { return SettingsMenu.instance != null; } catch { return false; } }
        }

        /// <summary>控制台是否开着（游戏自带控制台）。</summary>
        public static bool ConsoleOpen
        {
            get { try { return ConsoleScript.instance != null && ConsoleScript.instance.active; } catch { return false; } }
        }

        // ─────────────────────────────────────────────────────────
        // 音效
        // ─────────────────────────────────────────────────────────

        /// <summary>播放游戏原生 UI 音效（静态版，主菜单也能用）。</summary>
        public static void PlayUISound(string clip, float pitch = 1f)
        {
            try { PlayerCamera.PlayUISound(clip, pitch); }
            catch (Exception e) { NativeLog.Warn("播放 UI 音效失败：" + e.Message); }
        }

        public static void PlayClick() => PlayUISound("click");
        public static void PlayMiniClick() => PlayUISound("miniClick");
        public static void PlayClose() => PlayUISound("close");
        public static void PlayOpen() => PlayUISound("menuOpen");
        public static void PlayDeny() => PlayUISound("warning");

        // ─────────────────────────────────────────────────────────
        // 素材
        // ─────────────────────────────────────────────────────────

        /// <summary>按名字取游戏原生 Sprite（找不到返回 null）。</summary>
        public static Sprite Sprite(string name) => NativeAssets.Find(name);

        /// <summary>游戏原生 TMP 字体（Retro GamingPix）。</summary>
        public static TMP_FontAsset Font => NativeAssets.Font;

        /// <summary>
        /// 是否在遇到字库缺字时自动注册系统中文字体作 fallback（默认 true）。
        /// 原生像素字体没有中文字形，中文模组基本都需要开启。
        /// </summary>
        public static bool AutoFontFallback = true;

        private static bool _fallbackTried;

        /// <summary>检查文本是否缺字；缺则注册一次系统字体 fallback。</summary>
        internal static void EnsureGlyphCoverage(string text)
        {
            if (!AutoFontFallback || _fallbackTried || string.IsNullOrEmpty(text)) return;
            var font = NativeAssets.Font;
            if (font == null) return;

            bool missing = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) continue;
                try
                {
                    if (!font.HasCharacter(c, true, false)) { missing = true; break; }
                }
                catch { }
            }
            if (!missing) return;

            _fallbackTried = true;
            NativeLog.Info("检测到原生字体缺字，尝试注册系统中文字体 fallback…");
            NativeAssets.RegisterSystemFontFallback();
        }

        /// <summary>手动注册一个 TMP 字体做 fallback（例如你自己打包的中文字体）。</summary>
        public static bool AddFontFallback(TMP_FontAsset fallback)
        {
            bool ok = NativeAssets.RegisterFallback(fallback);
            if (ok) _fallbackTried = true;
            return ok;
        }

        /// <summary>手动注册系统中文字体做 fallback（不给参数就用内置的 Windows 字体候选列表）。</summary>
        public static TMP_FontAsset AddSystemFontFallback(params string[] fontNames)
        {
            _fallbackTried = true;
            return NativeAssets.RegisterSystemFontFallback(fontNames);
        }

        /// <summary>当前原生字体（含 fallback）能否显示中文。</summary>
        public static bool ChineseAvailable => NativeAssets.SupportsChinese;

        /// <summary>用原生贴图创建一个九宫格面板，返回它的 Image。</summary>
        public static Image Panel(Transform parent, string spriteName = "uiBlockSmall",
                                  float width = 300f, float height = 120f, Color? color = null)
        {
            var img = NativeBuild.Panel("NativePanel", parent != null ? parent : Parent,
                                        NativeAssets.Find(spriteName) ?? NativeAssets.PanelSmall,
                                        color.HasValue ? color.Value : NativeTheme.Panel, true);
            img.rectTransform.sizeDelta = NativeTheme.Scaled(width, height);
            NativeBuild.ToUILayer(img.gameObject);
            return img;
        }

        // ─────────────────────────────────────────────────────────
        // 诊断
        // ─────────────────────────────────────────────────────────

        /// <summary>把素材状态 + 所有可用的原生 Sprite 名字写进日志（排查"为什么找不到贴图"用）。</summary>
        public static void DumpAssets(bool includeSpriteNames = true)
        {
            NativeLog.Info(NativeAssets.Describe());
            NativeLog.Info("运行环境：EventSystem=" + (HasEventSystem ? "存在" : "缺失")
                           + "，游戏UI=" + (GameUIReady ? "已就绪" : "未就绪")
                           + "，父节点=" + (Parent != null ? Parent.name : "<null>")
                           + "，已开窗口=" + OpenWindowCount);
            if (!includeSpriteNames) return;
            var names = NativeAssets.DumpSpriteNames();
            NativeLog.Info("可用 Sprite 共 " + names.Length + " 个：" + string.Join(", ", names));
        }
    }
}

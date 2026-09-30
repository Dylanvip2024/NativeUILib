using System;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NativeUILib
{
    /// <summary>
    /// NativeUILib 的 BepInEx 入口。
    ///
    /// 本程序集可以被其他模组直接引用（&lt;Private&gt;false&lt;/Private&gt;）并调用
    /// <see cref="NativeUI"/> 的静态 API。即使本插件没有被 BepInEx 当作插件加载，
    /// API 也会通过 <see cref="NativeBootstrap.Ensure"/> 自行初始化（懒加载）。
    /// </summary>
    [BepInPlugin(Guid, PluginName, PluginVersion)]
    public sealed class NativeUILibPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.mod.casualties.nativeuilib";
        public const string PluginName = "NativeUILib";
        public const string PluginVersion = "1.0.0";

        internal static NativeUILibPlugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            NativeLog.Attach(Logger);
            NativeBootstrap.Ensure();
            Logger.LogInfo(PluginName + " v" + PluginVersion + " 已加载；其他模组可直接调用 NativeUILib.NativeUI.*");
        }
    }

    /// <summary>日志：优先用 BepInEx 的 ManualLogSource，拿不到就退化到 UnityEngine.Debug。</summary>
    internal static class NativeLog
    {
        private const string Tag = "[NativeUILib] ";
        private static ManualLogSource _source;

        internal static void Attach(ManualLogSource source)
        {
            if (source != null) _source = source;
        }

        private static ManualLogSource Source
        {
            get
            {
                if (_source != null) return _source;
                try { _source = BepInEx.Logging.Logger.CreateLogSource("NativeUILib"); }
                catch { _source = null; }
                return _source;
            }
        }

        internal static void Info(string msg)
        {
            var s = Source;
            if (s != null) s.LogInfo(msg); else UnityEngine.Debug.Log(Tag + msg);
        }

        /// <summary>诊断性消息：进日志文件但不刷屏（默认不显示在控制台）。</summary>
        internal static void Debug(string msg)
        {
            var s = Source;
            if (s != null) s.LogDebug(msg); else UnityEngine.Debug.Log(Tag + msg);
        }

        internal static void Warn(string msg)
        {
            var s = Source;
            if (s != null) s.LogWarning(msg); else UnityEngine.Debug.LogWarning(Tag + msg);
        }

        internal static void Error(string msg)
        {
            var s = Source;
            if (s != null) s.LogError(msg); else UnityEngine.Debug.LogError(Tag + msg);
        }

        internal static void Error(Exception e)
        {
            Error(e == null ? "未知异常" : e.ToString());
        }
    }

    /// <summary>
    /// 常驻宿主：保证库在任何场景下都有一个不会被销毁的 MonoBehaviour 可以挂东西 / 跑协程 / 监听场景切换。
    /// 由 <see cref="NativeUILibPlugin"/> 或第一次 API 调用时创建。
    /// </summary>
    internal sealed class NativeBootstrap : MonoBehaviour
    {
        private static NativeBootstrap _instance;

        internal static NativeBootstrap Instance => _instance;
        internal static bool Exists => _instance != null;
        internal static GameObject HostObject => _instance != null ? _instance.gameObject : null;

        internal static NativeBootstrap Ensure()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("[NativeUILib]");
            go.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<NativeBootstrap>();
            return _instance;
        }

        private void Awake()
        {
            _instance = this;
            try { SceneManager.sceneLoaded += OnSceneLoaded; }
            catch (Exception e) { NativeLog.Warn("注册场景事件失败：" + e.Message); }
        }

        private void OnDestroy()
        {
            try { SceneManager.sceneLoaded -= OnSceneLoaded; } catch { }
            if (_instance == this) _instance = null;
        }

        private void LateUpdate()
        {
            // 每帧结束前结算 IMGUI 焦点计数（供 UIUtil 补丁在下一帧的 Update 里读取，与 KrokMP 的做法一致）
            NativeGUISkin.EndFrame();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                NativeAssets.Invalidate();
                NativeGUISkin.MarkDirty();
                NativeUI.NotifySceneChanged();
            }
            catch (Exception e) { NativeLog.Warn("场景切换处理失败：" + e.Message); }
        }
    }
}

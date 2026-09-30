using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NativeUILib
{
    /// <summary>
    /// 把入口按钮塞进游戏自己的菜单（克隆原生按钮，外观 100% 一致）。
    /// 主菜单 / 暂停菜单各一套。
    /// </summary>
    public static class NativeMenu
    {
        /// <summary>
        /// 在主菜单加一个原生按钮。
        /// 克隆模板取自主菜单的 <c>RunSettings/Presets/Cancel</c>。
        /// </summary>
        /// <param name="text">按钮文字。</param>
        /// <param name="onClick">点击回调（已自动加原生点击音）。</param>
        /// <param name="tweak">可选：拿到 RectTransform 后自己调锚点/位置。</param>
        public static GameObject AddMainMenuButton(string text, Action onClick, Action<RectTransform> tweak = null)
        {
            try
            {
                var prerun = PreRunScript.instance;
                if (prerun == null)
                {
                    NativeLog.Warn("AddMainMenuButton：PreRunScript.instance 为 null（不在主菜单？）");
                    return null;
                }

                var template = prerun.transform.Find("RunSettings/Presets/Cancel");
                if (template == null)
                {
                    NativeLog.Warn("AddMainMenuButton：找不到模板 RunSettings/Presets/Cancel，游戏版本可能已变更");
                    return null;
                }

                var go = UnityEngine.Object.Instantiate(template.gameObject, prerun.transform, false);
                go.name = "[NativeUILib] MainMenuButton";
                go.SetActive(true);
                CleanClone(go, text);

                var button = go.GetComponent<Button>();
                if (button != null && onClick != null)
                {
                    button.onClick.AddListener(delegate
                    {
                        NativeUI.PlayClick();
                        try { onClick(); }
                        catch (Exception e) { NativeLog.Error(e); }
                    });
                }

                var rt = go.GetComponent<RectTransform>();
                if (tweak != null && rt != null) tweak(rt);
                return go;
            }
            catch (Exception e)
            {
                NativeLog.Error("AddMainMenuButton 失败：" + e);
                return null;
            }
        }

        /// <summary>
        /// 在暂停菜单加一个原生按钮，自动排在最后一个按钮下方。
        /// 会尽量把自己注册进 <c>PauseHandler.buttons</c>（含私有的 buttonPos），
        /// 这样它能一起参与暂停菜单的入场动画；注册失败时按钮仍然可用，只是不做动画。
        /// </summary>
        public static GameObject AddPauseMenuButton(string text, Action onClick, Action<RectTransform> tweak = null)
        {
            try
            {
                var pause = PauseHandler.main;
                if (pause == null)
                {
                    NativeLog.Warn("AddPauseMenuButton：PauseHandler.main 为 null（不在游戏内？）");
                    return null;
                }

                RectTransform last = null;
                if (pause.buttons != null && pause.buttons.Count > 0)
                {
                    for (int i = pause.buttons.Count - 1; i >= 0; i--)
                    {
                        if (pause.buttons[i] != null) { last = pause.buttons[i]; break; }
                    }
                }
                if (last == null)
                {
                    NativeLog.Warn("AddPauseMenuButton：暂停菜单里没有可克隆的模板按钮");
                    return null;
                }

                var go = UnityEngine.Object.Instantiate(last.gameObject, last.parent, false);
                go.name = "[NativeUILib] PauseMenuButton";
                go.SetActive(true);
                CleanClone(go, text);

                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    float gap = (last.rect.height > 1f ? last.rect.height : 40f) + 4f;
                    var pos = last.localPosition;
                    rt.localPosition = new Vector3(pos.x, pos.y - gap, pos.z);
                    rt.localScale = Vector3.one;
                }

                var button = go.GetComponent<Button>();
                if (button != null && onClick != null)
                {
                    button.onClick.AddListener(delegate
                    {
                        NativeUI.PlayClick();
                        try { onClick(); }
                        catch (Exception e) { NativeLog.Error(e); }
                    });
                }

                if (tweak != null && rt != null) tweak(rt);
                RegisterPauseButton(pause, rt);
                return go;
            }
            catch (Exception e)
            {
                NativeLog.Error("AddPauseMenuButton 失败：" + e);
                return null;
            }
        }

        /// <summary>
        /// 打开游戏原生设置菜单（等价于 <see cref="NativeUI.OpenNativeSettings"/>）。
        /// </summary>
        public static void OpenSettings(int category = 2) => NativeUI.OpenNativeSettings(category);

        // ─────────────────────────────────────────────────────────

        private static void CleanClone(GameObject go, string text)
        {
            // 1) 清掉克隆来的点击逻辑（原生按钮用的是"持久监听器"，必须显式关掉）
            var button = go.GetComponent<Button>();
            if (button != null)
            {
                try { button.onClick.RemoveAllListeners(); } catch { }
                try { button.onClick.SetPersistentListenerState(0, UnityEventCallState.Off); } catch { }
            }

            // 2) 去掉本地化组件，避免它把我们的文字覆盖掉
            var localizers = go.GetComponentsInChildren<UILocalizer>(true);
            for (int i = 0; i < localizers.Length; i++)
                if (localizers[i] != null) UnityEngine.Object.Destroy(localizers[i]);

            // 3) 设置文字
            var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = text ?? string.Empty;
                label.color = Color.white;
            }
        }

        private static void RegisterPauseButton(PauseHandler pause, RectTransform rt)
        {
            if (pause == null || rt == null) return;
            try
            {
                // PauseHandler.Update() 会用 for(i<buttons.Count) 读私有的 buttonPos[i]，
                // 所以必须两个列表同时加，否则会 IndexOutOfRange。
                var field = typeof(PauseHandler).GetField("buttonPos", BindingFlags.NonPublic | BindingFlags.Instance);
                var positions = field != null ? field.GetValue(pause) as List<Vector2> : null;
                if (positions != null && pause.buttons != null)
                {
                    positions.Add(rt.localPosition);
                    pause.buttons.Add(rt);
                    return;
                }
                NativeLog.Warn("暂停菜单按钮未能注册进动画列表（buttonPos 反射失败）：按钮可用，但不会参与入场动画");
            }
            catch (Exception e)
            {
                NativeLog.Warn("暂停菜单按钮注册失败：" + e.Message);
            }
        }
    }
}

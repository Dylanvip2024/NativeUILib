using System;
using System.Collections.Generic;
using HarmonyLib;
using NativeUILib;
using UnityEngine;

namespace SampleAddon
{
    /// <summary>
    /// 把自定义项混进**游戏原生设置菜单**。
    ///
    /// 原理：游戏 SettingsMenu 是按 <c>Settings.DefaultSettings()</c> 返回的列表逐项生成原生行的，
    /// 所以我们只要往那个列表里加一个 <see cref="Setting"/>，设置菜单就会自动出现我们的一项
    /// （外观、tooltip、滑条/开关控件全自动）。
    ///
    /// 显示文字取 <c>Locale.GetOther("gameset" + name)</c>，描述取 <c>"gameset" + name + "dsc"</c>；
    /// 想让它们显示中文，就要在游戏的 Lang/*.json 里加对应的 key，或者自己往 Locale 表里塞。
    /// </summary>
    [HarmonyPatch(typeof(Settings), "DefaultSettings")]
    internal static class SampleSettingsModule
    {
        public const string BoolKey = "sampleaddon_enabled";
        public const string FloatKey = "sampleaddon_power";
        public const string KeybindKey = "sampleaddon_panel";

        private static void Postfix(ref List<Setting> __result)
        {
            try
            {
                if (__result == null) return;

                // ── 一个开关 ──
                if (Find(__result, BoolKey) == null)
                {
                    var s = new SettingBool();
                    s.name = BoolKey;
                    s.value = true;
                    s.category = Setting.SettingCategory.Game;
                    s.apply = delegate
                    {
                        // 注意：这里访问的是注册表里的同一个对象，值已经更新好了
                        var cur = Settings.Get<SettingBool>(BoolKey);
                        SampleAddonPlugin.DemoBool = cur != null && cur.value;
                    };
                    __result.Add(s);
                }

                // ── 一个滑条 ──
                if (Find(__result, FloatKey) == null)
                {
                    var s = new SettingFloat();
                    s.name = FloatKey;
                    s.value = 0.5f;
                    s.min = 0f;
                    s.max = 1f;
                    s.category = Setting.SettingCategory.Game;
                    s.formatValue = v => Mathf.RoundToInt(v * 100f) + "%";
                    s.apply = delegate
                    {
                        var cur = Settings.Get<SettingFloat>(FloatKey);
                        if (cur != null) SampleAddonPlugin.DemoFloat = cur.value;
                    };
                    __result.Add(s);
                }

                // ── 一个键位 ──
                if (Find(__result, KeybindKey) == null)
                {
                    var s = new SettingKeybind();
                    s.name = KeybindKey;
                    s.value = KeyCode.F7;
                    s.category = Setting.SettingCategory.Input;
                    s.apply = delegate { };
                    __result.Add(s);
                }
            }
            catch (Exception e)
            {
                SampleAddonPlugin.Log.LogError("注册原生设置项失败：" + e);
            }
        }

        private static Setting Find(List<Setting> list, string name)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].name == name) return list[i];
            return null;
        }
    }
}

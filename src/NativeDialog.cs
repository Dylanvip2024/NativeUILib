using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NativeUILib
{
    /// <summary>原生风格的模态对话框（全屏遮罩 + 居中窗口）。</summary>
    public static class NativeDialog
    {
        /// <summary>对话框上的一个按钮。</summary>
        public struct Choice
        {
            public string Text;
            public Action Action;

            public Choice(string text, Action action)
            {
                Text = text;
                Action = action;
            }
        }

        /// <summary>只有一个"确定"的提示框。</summary>
        public static NativeWindow Message(string title, string message, string okText = "确定", Action onOk = null)
        {
            return Build(title, message, new[] { new Choice(okText ?? "确定", onOk) });
        }

        /// <summary>"是 / 否"确认框。</summary>
        public static NativeWindow Confirm(string title, string message, Action onYes, Action onNo = null,
                                           string yesText = "是", string noText = "否")
        {
            return Build(title, message, new[]
            {
                new Choice(yesText ?? "是", onYes),
                new Choice(noText ?? "否", onNo),
            });
        }

        /// <summary>任意数量选项的对话框。</summary>
        public static NativeWindow Choose(string title, string message, params Choice[] choices)
        {
            if (choices == null || choices.Length == 0)
                choices = new[] { new Choice("确定", (Action)null) };
            return Build(title, message, choices);
        }

        private static NativeWindow Build(string title, string message, Choice[] choices)
        {
            float s = NativeTheme.Scale;
            try
            {
                var parent = NativeUI.Parent;

                // 遮罩：吃掉所有点击，营造模态
                var dim = NativeBuild.Panel("NativeDialogDim", parent, NativeAssets.White, NativeTheme.Dim, false);
                var dimRt = dim.rectTransform;
                dimRt.anchorMin = Vector2.zero;
                dimRt.anchorMax = Vector2.one;
                dimRt.offsetMin = Vector2.zero;
                dimRt.offsetMax = Vector2.zero;
                var dimButton = dim.gameObject.AddComponent<Button>();
                dimButton.transition = Selectable.Transition.None;
                dimButton.targetGraphic = dim;

                var win = NativeWindow.Create(title ?? string.Empty, NativeTheme.Scaled(430f, 200f), dim.transform, true, true);
                win.AutoSize = true;

                if (!string.IsNullOrEmpty(message))
                {
                    var label = win.AddLabel(message, 1f, TextAlignmentOptions.TopLeft);
                    label.color = NativeTheme.Text;
                }
                win.AddSpace(6f);

                // 按钮行
                var row = NativeBuild.Rect("Buttons", win.Content);
                var hlg = row.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = false;
                hlg.spacing = 8f * s;
                NativeBuild.Layout(row, preferredHeight: NativeTheme.ButtonHeight * s);

                for (int i = 0; i < choices.Length; i++)
                {
                    var choice = choices[i];
                    var btn = NativeBuild.NativeButton("Choice" + i, row.transform, choice.Text ?? "确定",
                        delegate
                        {
                            win.Close();
                            if (choice.Action != null) choice.Action();
                        }, NativeTheme.ButtonHeight * s);
                    NativeBuild.Layout(btn.gameObject, flexibleWidth: 1f);
                }

                var capturedDim = dim.gameObject;
                win.Closed += delegate
                {
                    if (capturedDim != null) UnityEngine.Object.Destroy(capturedDim);
                };

                NativeBuild.ToUILayer(dim.gameObject);
                win.RefreshLayout();
                win.ClampToScreen();
                return win;
            }
            catch (Exception e)
            {
                NativeLog.Error("创建对话框失败：" + e);
                return null;
            }
        }
    }
}

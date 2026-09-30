# NativeUILib —— AI 智能体使用说明

> **本文档的目标读者是 AI 编码助手。**
> 假设你已经知道"要给 Casualties Unknown 写一个 BepInEx 模组"，现在需要用 NativeUILib 画界面。
>
> 阅读顺序建议：**第 1 节（决策树）→ 第 2 节（硬约束）→ 第 3 节（模板）→ 需要时查第 4 节（API 索引）**。
> 第 8 节的**自检清单**在你交付代码前必须过一遍。
>
> 人类读者请看：[README.md](README.md)（English）｜ [README.zh-CN.md](README.zh-CN.md)（中文）

---

## 1. 决策树：你到底该调什么

```
需要给玩家看一个可以交互的面板？
├─ 是 → NativeUI.Window("标题")            ← 90% 的情况用这个
│        └─ 里面用 AddToggle / AddSlider / AddDropdown / AddInput / AddButton ...
│
├─ 只需要弹一句话 → NativeUI.Toast(text)
│
├─ 需要玩家确认 → NativeUI.Confirm(标题, 内容, onYes, onNo)
│                 NativeUI.Message(标题, 内容)          // 只有"确定"
│                 NativeDialog.Choose(标题, 内容, 选项...)  // 任意选项
│
├─ 想改游戏的设置界面 → 见 §6.3（Harmony 加一个 Setting 就自动出现在原生设置菜单里）
│
├─ 想在主菜单/暂停菜单加一个入口按钮 → NativeMenu.AddMainMenuButton / AddPauseMenuButton
│
├─ 想画游戏内 HUD → NativeUI.Panel(null, "uiBlockNano", w, h) + 自己加 TMP 文本
│                   参考 §5.9
│
└─ 你的模组本来就用 OnGUI(IMGUI) → using (NativeGUISkin.Scope()) { ... }   见 §5.11
```

**不要把新代码写成 IMGUI。** 游戏本体完全没有 IMGUI，UGUI 路线才是原生路线（详见 §2.1）。

---

## 2. 硬约束（违反会出 Bug，必须遵守）

### 2.1 游戏原生 UI = UGUI + TextMeshPro，没有 IMGUI

游戏自身从不调用 `OnGUI` / `GUI.skin`。所以：

- **新功能一律用 `NativeUI.Window`（UGUI）**；
- `NativeGUISkin` 只服务于"已经用 OnGUI 写好的老模组"，它属于**仿原生**，不是原生。

### 2.2 原生素材必须"场景加载后"才拿得到

`uiBlockSmall` / `uiBlockNano` / `Retro GamingPix` 这些**不在 `Resources` 目录**，它们是场景资产。
本库用 `Resources.FindObjectsOfTypeAll<Sprite>()` 和扫描 `TMP_Text.font` 来拿，因此：

- 在 `Awake()` 最早期调用可能拿到 `null` → 库会自动降级（面板变成纯白、字体用 TMP 默认）；
- **库会在每次 `SceneManager.sceneLoaded` 后自动清缓存重试**，你不需要管；
- 如果你在 `Awake` 就想开窗口，请用 `NativeUI.RunNextFrame(() => ...)` 或放到 `Start()`。

### 2.3 EventSystem 缺失时按钮不会响应

游戏运行时本来就有 EventSystem。库的检查策略是**刻意保守**的，不会在启动瞬间误报：

- 创建 Canvas 时**不检查**（BepInEx 加载插件时游戏还停在引导场景 `level0`，那时检查必然是误报）；
- 只有在你**真的创建了可交互 UI**（窗口 / 按钮 / 对话框）之后才体检；
- 而且只有**游戏自己的 UI 基础设施已经起来**（`PreRunScript.instance` / `PlayerCamera.main` /
  `GlobalDark.main` 任一存在）时才判定，否则静默跳过；
- 全程最多警告一次，且只描述事实。

所以日志里真的出现这条警告，说明**确实**是异常情况（比如别的模组禁用了 EventSystem）。想自查：

```csharp
bool ok = NativeUI.HasEventSystem;   // 随时可查
```

UGUI 是"每次事件时查 `EventSystem.current`"，因此**早期创建的界面在 EventSystem 出现后会自动恢复可用**，
不需要重建。

### 2.4 UI 必须在 "UI" 层

库会自动把创建的整个子树设到 `UI` 层（`LayerMask.NameToLayer("UI")`），这样游戏的
`UIUtil.IsPointerOverUIElement()` 才会认为"鼠标在 UI 上"，从而：

- 不让左键攻击/挖方块穿透到世界；
- 触发 `GlobalDark` 的原生 tooltip 流程。

**你自己 `new GameObject` 加的 UI 请不要再手动改层**；如果你自己拼了控件，记得 `NativeUI.Panel` 之类
的工厂方法会帮你设好，手搓的话要自己设。

### 2.5 时间：UI 动画一律用 `Time.unscaledDeltaTime`

暂停菜单里 `Time.timeScale == 0`，用 `Time.deltaTime` 的动画会卡死。库内部已遵守。

### 2.6 `PlayerCamera.main` 在主菜单是 `null`

用 `NativeUI.InWorld` 判断，而不是直接访问 `PlayerCamera.main`。播放音效请用
`NativeUI.PlayClick()`（静态版，主菜单也能用）。

### 2.7 本库的 Overlay Canvas 是 `DontDestroyOnLoad`

所以**窗口会跨场景存活**。如果你的窗口回调里引用了场景对象（`PlayerCamera`、`Body`…），
请在 `NativeUI.SceneChanged += () => { 关掉/重建窗口; }` 里处理，否则读到的会是已销毁对象。

---

## 3. 60 秒上手（把这个抄进你的模组）

```csharp
using System;
using BepInEx;
using NativeUILib;
using UnityEngine;

namespace MyMod
{
    [BepInPlugin("com.me.mymod", "MyMod", "1.0.0")]
    [BepInDependency(NativeUILibPlugin.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class MyPlugin : BaseUnityPlugin
    {
        private NativeWindow _window;

        private void Awake()
        {
            // 不要在这里建窗口（EventSystem / 素材可能还没就绪）
        }

        private void Start()
        {
            NativeUI.RunNextFrame(() =>
            {
                NativeMenu.AddPauseMenuButton("我的面板", Toggle);
                NativeMenu.AddMainMenuButton("我的面板", Toggle);
                NativeUI.Toast("MyMod 已加载");
            });
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6)) Toggle();
        }

        private void Toggle()
        {
            if (_window != null) { _window.Close(); _window = null; return; }

            var w = NativeUI.Window("我的面板");
            w.RememberPosition("mymod.main");        // 记住拖动位置

            w.AddHeader("MyMod");
            w.AddLabel("这是一个原生风格面板。");
            w.AddToggle("启用功能", MyState.Enabled, v => MyState.Enabled = v, "悬停提示");
            w.AddSlider("强度", 0f, 1f, MyState.Power, v => MyState.Power = v, "0.00");
            w.AddSeparator();
            w.AddButtonRow("重置", "点我", () => NativeUI.Toast("已重置"));
            w.AddButton("关闭", () => w.Close());

            w.Closed += () => _window = null;
            _window = w;
        }
    }

    internal static class MyState
    {
        public static bool Enabled = true;
        public static float Power = 0.5f;
    }
}
```

对应的 `.csproj` 片段：

```xml
<ItemGroup>
  <Reference Include="NativeUILib">
    <HintPath>..\NativeUILib\build\NativeUILib.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

---

## 4. API 索引（精确签名，可直接照抄）

> 命名空间：`using NativeUILib;`
> 所有"控件工厂"方法**永不抛异常**（内部 try/catch），失败时返回能用的降级对象。见 §7。

### 4.1 `NativeUI`（静态入口）

```csharp
// —— 初始化 / 父节点 ——
static void          Init();                                  // 幂等；一般不用手动调
static Canvas        Overlay { get; }                         // 库自带常驻 Overlay Canvas
static Transform     Parent  { get; }                         // 默认父节点（Overlay 或你 SetParent 的）
static void          SetParent(Transform parent);             // 改成游戏自己的 Canvas；传 null 还原
static int           SortingOrder { get; set; }               // Overlay 的 sortingOrder，默认 100
static void          RunNextFrame(Action action);             // 下一帧执行（安全创建 UI 用）
static event Action  SceneChanged;                            // 场景切换

// —— 窗口 ——
static NativeWindow  Window(string title, float width = 460f, float height = 300f, bool autoSize = true);
static NativeWindow  Window(string title, Transform parent, float width = 460f, float height = 300f, bool autoSize = true);
static int           OpenWindowCount { get; }
static void          CloseAllWindows();

// —— 提示 / 对话框 ——
static void          Toast(string text, bool important = false, float seconds = 4f);
static NativeWindow  Message(string title, string message, string okText = "确定", Action onOk = null);
static NativeWindow  Confirm(string title, string message, Action onYes, Action onNo = null,
                             string yesText = "是", string noText = "否");

// —— 游戏原生 UI 快捷入口 ——
static void          OpenNativeSettings(int category = 2);    // 0=Video 1=Audio 2=Game 3=Input 4=Language
static void          SelectNativeSettingsTab(int category);
static void          CloseNativeSettings();
static void          ShowNativeText(string text, bool cover = false, List<Sprite> sprites = null,
                                    TMP_FontAsset font = null);   // 游戏自带"打字机"文本界面

// —— 状态查询 ——
static bool          IsPointerOverUI();
static bool          HasEventSystem { get; }   // UGUI 交互前提；缺了按钮点不动
static bool          InWorld   { get; }     // WorldGeneration.world != null
static bool          IsPaused  { get; }
static bool          NativeSettingsOpen { get; }
static bool          ConsoleOpen { get; }

// —— 音效（静态版，主菜单可用）——
static void          PlayUISound(string clip, float pitch = 1f);   // click/miniClick/close/menuOpen/menuClose/warning/time
static void          PlayClick(); PlayMiniClick(); PlayClose(); PlayOpen(); PlayDeny();

// —— 素材 ——
static Sprite        Sprite(string name);           // 找不到返回 null
static TMP_FontAsset Font { get; }
static Image         Panel(Transform parent = null, string spriteName = "uiBlockSmall",
                           float width = 300f, float height = 120f, Color? color = null);

// —— 字体：中文支持 ——
static bool          AutoFontFallback;                       // 默认 true：缺字自动挂系统中文字体
static bool          AddFontFallback(TMP_FontAsset fallback);
static TMP_FontAsset AddSystemFontFallback(params string[] fontNames);  // 不传参=内置 Windows 候选
static bool          ChineseAvailable { get; }

// —— 诊断 ——
static void          DumpAssets(bool includeSpriteNames = true);  // 打印素材状态 + 所有 Sprite 名
```

### 4.2 `NativeWindow`

```csharp
// 属性
RectTransform Rect { get; }          // 窗口根
RectTransform Content { get; }       // 内容区（所有 Add* 都塞这里）
GameObject    GameObject { get; }
bool          Visible { get; set; }
string        Title { get; set; }
bool          AutoSize;              // 默认 true：按内容自动调整高度（上限屏幕 75%）
bool          CloseOnEscape;         // 默认 false
string        Id;
event Action  Closed;                // 在 OnDestroy 时触发（Close() 后当帧末）
event Action  Shown;

// 内容：文本
TextMeshProUGUI AddLabel(string text, float sizeMult = 1f, TextAlignmentOptions align = Left);
TextMeshProUGUI AddHeader(string text, float sizeMult = 1.3f);        // 居中 + 金色

// 内容：按钮
Button AddButton(string text, Action onClick, float width = 0f, string tooltip = null);
Button AddInlineButton(string label, string buttonText, Action onClick);   // 左说明 + 右按钮

// 内容：原生设置行（返回 NativeRow，见 §4.3）
NativeRow AddRow(NativeRowKind kind, string label, string tooltip = null);
NativeRow AddToggle  (string label, bool value, Action<bool> onChange = null, string tooltip = null);
NativeRow AddSlider  (string label, float min, float max, float value,
                      Action<float> onChange = null, string format = null, string tooltip = null);
NativeRow AddInput   (string label, string value, Action<string> onChange = null,
                      bool freeText = true, string tooltip = null);
NativeRow AddDropdown(string label, string[] options, int index,
                      Action<int> onChange = null, string tooltip = null);
NativeRow AddButtonRow(string label, string buttonText, Action onClick, string tooltip = null);

// 内容：其他
GameObject    AddSeparator();
GameObject    AddSpace(float height = 8f);
T             Add<T>(T component) where T : Component;   // 把自定义控件挂进流式布局
GameObject    Add(GameObject child);

// 布局 / 位置
void   RefreshLayout();                       // 强制重算（Add* 会自动调）
void   ClampToScreen();
NativeWindow SetSize(float width, float height);
NativeWindow SetPosition(float x, float y);
NativeWindow Center();
NativeWindow RememberPosition(string key);    // PlayerPrefs 记忆位置
void   SavePosition();
void   MakeDraggable(GameObject target);      // 让额外区域也能拖动窗口
void   Close();
```

### 4.3 `NativeRow`（一行原生控件）

```csharp
// 类型
enum NativeRowKind { Bool = 0, Float = 1, Int = 2, Dropdown = 3, Button = 4 }

// 状态
NativeRowKind Kind   { get; }
bool          Native { get; }        // true = 来自游戏预制体；false = 自绘降级
TextMeshProUGUI Label { get; }       // 行首名称文本
RectTransform   Control { get; }     // 控件本体（Child1）
TextMeshProUGUI ValueText { get; }   // 数值文本（Float 行才有）

Toggle         Toggle   { get; }
Slider         Slider   { get; }
TMP_Dropdown   Dropdown { get; }
TMP_InputField Input    { get; }
Button         Button   { get; }

// 链式配置
NativeRow SetLabel(string text);
NativeRow SetTooltip(string name, string desc = null);
NativeRow SetInteractable(bool on);
NativeRow SetValueFormatter(Func<float,string> formatter);   // Float 行的数值显示格式

// 取值
bool   GetBool();  float GetFloat();  int GetInt();  string GetText();

// 赋值（notify=false 时**不会**触发回调）
NativeRow SetBool(bool value, bool notify = false);
NativeRow SetFloat(float value, bool notify = false);
NativeRow SetInt(int value, bool notify = false);
NativeRow SetText(string value, bool notify = false);
NativeRow SetOptions(string[] options);
NativeRow SetDropdownIndex(int index, bool notify = false);

// 事件
NativeRow OnBool(Action<bool>);  OnFloat(Action<float>);  OnInt(Action<int>);
NativeRow OnText(Action<string>); OnDropdown(Action<int>);  OnButton(Action);
```

### 4.4 `NativeTheme`

```csharp
static float  Scale { get; }                    // Screen.height / 1080（最小 0.5）
static Vector2 Scaled(float x, float y);
static float  Scaled(float v);
static int    FontSize(float mult = 1f);        // 20 * Scale * mult

const float TitleHeight = 30f, RowHeight = 30f, ButtonHeight = 34f,
            ControlWidth = 150f, Padding = 10f, Spacing = 6f;   // 基准 1080p，用前乘 Scale

static Color Text, TextDim, Accent(金色), Danger, Panel, TitleBar, Separator, Dim;
static Color NormalState, HoverState, PressedState, DisabledState;
static Color StateColor(float brightness, float alpha);
```

### 4.5 `NativeAssets`

```csharp
static void   Invalidate();                                  // 清缓存（场景切换时库会自动调）
static Sprite Find(string name, bool fuzzy = true);
static bool   Exists(string name);
static Sprite Panel;        // uiBlock
static Sprite PanelSmall;   // uiBlockSmall
static Sprite PanelNano;    // uiBlockNano
static Sprite Check;        // UICheckMark
static Sprite White;        // whitesquare（找不到就程序生成）
static Sprite Cursor, Gradient, BarFill;
static Sprite Solid(Color color, int size = 4);              // 程序生成纯色贴图
static TMP_FontAsset Font { get; }
static bool   IsNativeFont { get; }                          // 是否拿到了 Retro GamingPix
static Font   GUIFont { get; }                               // 给 IMGUI 用的旧版 Font（可能 null）
static bool   HasChar(char c);
static string Sanitize(string input);                        // 缺字替换成 '?'
static bool   RegisterFallback(TMP_FontAsset fallback);
static TMP_FontAsset RegisterSystemFontFallback(params string[] fontNames);
static TMP_FontAsset RegisterFontFallback(Font font, string assetName = null);
static bool   SupportsChinese { get; }
static Texture2D Process(Texture2D src, float scale = 1f, float brightness = 1f, float alpha = 1f);
static string[] DumpSpriteNames();
static string Describe();
```

### 4.6 `NativeDialog` / `NativeMenu` / `NativeGUISkin`

```csharp
// NativeDialog
struct Choice { string Text; Action Action; Choice(string text, Action action); }
static NativeWindow Message(string title, string message, string okText = "确定", Action onOk = null);
static NativeWindow Confirm(string title, string message, Action onYes, Action onNo = null,
                            string yesText = "是", string noText = "否");
static NativeWindow Choose (string title, string message, params Choice[] choices);

// NativeMenu
static GameObject AddMainMenuButton (string text, Action onClick, Action<RectTransform> tweak = null);
static GameObject AddPauseMenuButton(string text, Action onClick, Action<RectTransform> tweak = null);
static void       OpenSettings(int category = 2);

// NativeGUISkin（只给 OnGUI 模组用）
static bool  AutoInstall;                 // 默认 true
static float TextureScaleMultiplier;      // 默认 3
static int   SmallSourceBorder, NanoSourceBorder;   // 默认 4 / 2
static bool  Installed { get; }
static int   FocusedRects { get; }        // 上一帧鼠标命中 IMGUI 的数量
static void  Install(); MarkDirty(); Begin(); End();
static IDisposable Scope();               // using (NativeGUISkin.Scope()) { ... }
static void  WithSkin(Action action);
static GUIStyle Style(string name);
static bool  IsCursorOver(in Rect rect);
static void  Panel(Rect rect, float brightness = 1f, float alpha = 0.9f);
static void  BigLabel(string text, float multiplier = 1.3f);
static bool  Button(Rect rect, string text);
static bool  FocusableButton(Rect rect, string text, out bool focused, GUIStyle style = null);
static void  InstallInputBlockPatch();    // 让 IMGUI 命中时屏蔽世界点击（调一次）
```

---

## 5. 示例库（复制即用）

### 5.1 最小窗口

```csharp
var w = NativeUI.Window("标题");
w.AddLabel("内容");
w.AddButton("确定", () => w.Close());
```

### 5.2 所有控件一次看全

```csharp
var w = NativeUI.Window("控件演示");
w.AddHeader("全部控件");
w.AddLabel("说明文字，会自动换行。");
w.AddToggle("开关", true, v => Debug.Log("toggle=" + v), "悬停看到的描述");
w.AddSlider("滑条", 0f, 100f, 50f, v => Debug.Log("slider=" + v), "0.#", "悬停描述");
w.AddDropdown("下拉", new[] { "A", "B", "C" }, 1, i => Debug.Log("index=" + i));
w.AddInput("输入", "默认值", s => Debug.Log("text=" + s));
w.AddButtonRow("右侧按钮行", "点我", () => NativeUI.Toast("ok"));
w.AddInlineButton("自绘行内按钮", "点我", () => NativeUI.Toast("ok2"));
w.AddSeparator();
w.AddSpace(10f);
w.AddButton("关闭", () => w.Close());
```

### 5.3 保存/读取控件状态（注意 `notify`）

```csharp
// 初始化时必须 notify:false（否则会立刻触发你的回调）
w.AddToggle("启用", MyState.Enabled, v => MyState.Enabled = v);
w.AddSlider("强度", 0f, 1f, MyState.Power, v => MyState.Power = v, "0.00");

// 之后想程序化改值：
row.SetBool(false);          // 不触发 onChange
row.SetBool(false, true);    // 触发 onChange
row.SetFloat(0.8f);
row.SetDropdownIndex(2);
row.SetText("abc");
```

### 5.4 打开时就要读取值的行，用 `AddRow` 手动绑定

```csharp
var row = w.AddRow(NativeRowKind.Float, "音量");
if (row.Slider != null)
{
    row.Slider.minValue = 0f;
    row.Slider.maxValue = 1f;
    row.SetFloat(AudioListener.volume, false);
    row.OnFloat(v => AudioListener.volume = v);
}
```

### 5.5 确认框 / 多选项对话框

```csharp
NativeUI.Confirm("删除存档", "确定要删除吗？此操作不可撤销。",
    onYes: () => { DeleteSave(); NativeUI.Toast("已删除"); },
    onNo:  () => NativeUI.Toast("已取消"));

NativeDialog.Choose("选择方案", "请选择一种处理方式",
    new NativeDialog.Choice("方案 A", () => ApplyA()),
    new NativeDialog.Choice("方案 B", () => ApplyB()),
    new NativeDialog.Choice("取消", null));
```

### 5.6 提示条

```csharp
NativeUI.Toast("普通提示");            // 游戏内 → 原生 PlayerCamera.DoAlert
NativeUI.Toast("重点提示", true);      // important=true → 显示在屏幕正中
NativeUI.Toast("主菜单里的提示条");     // 主菜单 → 自绘原生面板，4 秒后消失
```

### 5.7 注入主菜单 / 暂停菜单按钮

```csharp
// 主菜单：克隆游戏原生按钮（外观一致）
NativeMenu.AddMainMenuButton("我的面板", () => OpenMyWindow());

// 暂停菜单：自动排在最后一个按钮下方，并尽量参与入场动画
NativeMenu.AddPauseMenuButton("我的面板", () => OpenMyWindow(), rt =>
{
    // 可选：自己微调
    rt.sizeDelta = new Vector2(rt.sizeDelta.x, rt.sizeDelta.y);
});

// 注意：场景加载后才存在 PreRunScript / PauseHandler
NativeUI.SceneChanged += () => { /* 重新注入 */ };
```

### 5.8 直接打开游戏原生设置菜单

```csharp
NativeUI.OpenNativeSettings();      // 默认 Game 页
NativeUI.OpenNativeSettings(3);     // Input 页
NativeUI.CloseNativeSettings();
```

### 5.9 游戏内 HUD（原生贴图 + TMP）

```csharp
var panel = NativeUI.Panel(null, "uiBlockNano", 190f, 96f);   // parent=null → 用默认父节点
var rt = panel.rectTransform;
rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);            // 右上角
rt.pivot = new Vector2(1f, 1f);
rt.anchoredPosition = new Vector2(-16f * NativeTheme.Scale, -16f * NativeTheme.Scale);

var go = new GameObject("Text", typeof(RectTransform));
go.transform.SetParent(panel.transform, false);
var text = go.AddComponent<TMPro.TextMeshProUGUI>();
if (NativeUI.Font != null) text.font = NativeUI.Font;         // ★ 必须用原生字体
text.fontSize = NativeTheme.FontSize(0.8f);
text.color = NativeTheme.Text;
text.text = "HUD 内容";
var trt = text.rectTransform;
trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
trt.offsetMin = new Vector2(8f, 6f);  trt.offsetMax = new Vector2(-8f, -6f);
```

> 想跟游戏 HUD 同一层级就 `NativeUI.SetParent(PlayerCamera.main.mainCanvas.transform)`；
> 想跨场景常驻就用默认 Overlay。

### 5.10 把自定义项加进**游戏原生设置菜单**

```csharp
[HarmonyPatch(typeof(Settings), "DefaultSettings")]
internal static class MySettings
{
    private static void Postfix(ref List<Setting> __result)
    {
        var s = new SettingBool();
        s.name = "mymod_enabled";                       // 本地化 key = "gameset" + name
        s.value = true;
        s.category = Setting.SettingCategory.Game;      // Video/Audio/Game/Input/Language
        s.apply = () => { /* 生效逻辑 */ };
        __result.Add(s);
    }
}
```

要点：
- 显示文字取 `Locale.GetOther("gameset" + name)`，描述取 `Locale.GetOther("gameset" + name + "dsc")`；
- 想让它们显示中文，需要游戏 `Lang/*.json` 里有对应 key，或自己往 Locale 表里塞；
- 值会自动持久化到 `persistentDataPath/settings.json`（游戏按 name 存/取）；
- 类型可选：`SettingBool` / `SettingFloat`（有 min/max/formatValue）/ `SettingInt` / `SettingDropdown`（有 choices）/ `SettingKeybind`（有 value: KeyCode）；
- 读回值用 `Settings.Get<SettingBool>("mymod_enabled")`（**注意：`GetSetting<T>` 是私有的，别用**）。

### 5.11 IMGUI 老模组套原生皮肤

```csharp
private void OnGUI()
{
    using (NativeGUISkin.Scope())          // 进入时切皮肤，退出时还原
    {
        GUILayout.Window(0, rect, DrawWindow, "标题");
    }
}

private void Awake()
{
    // 让"鼠标停在 IMGUI 上"时游戏不再攻击/挖方块（只需调一次）
    NativeGUISkin.InstallInputBlockPatch();
}

private void DrawWindow(int id)
{
    NativeGUISkin.BigLabel("大标题", 1.3f);
    MyBool = GUILayout.Toggle(MyBool, " 开关");
    MyFloat = GUILayout.HorizontalSlider(MyFloat, 0f, 1f);
    if (GUILayout.Button("按钮")) NativeUI.PlayClick();
    NativeGUISkin.Panel(new Rect(6, rect.height - 34, rect.width - 12, 28), 1f, 0.85f);
    GUI.DragWindow(new Rect(0, 0, rect.width, 24));
}
```

### 5.12 中文显示

```csharp
// 默认 AutoFontFallback = true：只要文本里出现像素字体没有的字（中文），
// 库会自动去找系统里的 微软雅黑/黑体/宋体 注册成 fallback。通常什么都不用做。

NativeUI.AutoFontFallback = false;                       // 想完全手动控制
NativeUI.AddSystemFontFallback("Microsoft YaHei");       // 指定字体
NativeUI.AddFontFallback(myOwnTmpFontAsset);             // 用你打包的字体
bool ok = NativeUI.ChineseAvailable;                     // 检查是否已能显示中文
```

### 5.13 排查"贴图/字体找不到"

```csharp
NativeUI.DumpAssets();          // 日志里打印：Sprite 数量、字体名、贴图着色可用性、关键贴图存在性、全部 Sprite 名
Debug.Log(NativeAssets.Describe());
```

---

## 6. 常见任务配方

### 6.1 快捷键开关窗口（防重复）

```csharp
private NativeWindow _w;
private void Update()
{
    if (!Input.GetKeyDown(KeyCode.F6)) return;
    if (_w != null) { _w.Close(); _w = null; return; }
    _w = NativeUI.Window("面板");
    _w.Closed += () => _w = null;       // ★ 用户点 X 关闭时也要清引用
    // ... Add*
}
```

### 6.2 只在游戏内显示的 HUD

```csharp
private void Update()
{
    if (_root == null) return;
    bool show = NativeUI.InWorld && PlayerCamera.main != null;
    if (_root.activeSelf != show) _root.SetActive(show);
}
```

### 6.3 想禁止玩家在窗口打开时操作世界

不需要额外代码：UGUI 元素在 `UI` 层 → 游戏的 `PlayerCamera.HandleAttacks()` 里
`UIUtil.IsPointerOverUIElement()` 会直接 return。

但如果你想**整个屏幕**都屏蔽（模态），用对话框即可（`NativeUI.Confirm` 自带全屏遮罩），
或者自己加一个全屏 `Image`（`raycastTarget=true`，颜色 `NativeTheme.Dim`）。

### 6.4 窗口内容的动态重建

```csharp
// 清空内容区
for (int i = w.Content.childCount - 1; i >= 0; i--)
    UnityEngine.Object.Destroy(w.Content.GetChild(i).gameObject);
w.RefreshLayout();
```

### 6.5 在窗口里放自己的 UGUI 控件

```csharp
var go = new GameObject("MySlider", typeof(RectTransform));
var slider = go.AddComponent<Slider>();       // 自己配好 fillRect / handleRect
...
w.Add(go);                                     // 挂进垂直流式布局（会设 parent）
```

---

## 7. 降级与错误行为（可以依赖的保证）

| 情况 | 库的行为 |
|---|---|
| `Resources.Load("Special/GameSettingBool")` 返回 null | 自绘一个原生贴图的 Toggle 行；`NativeRow.Native == false` |
| `TMP_Dropdown` 预制体缺失 | 降级成"点击循环切换选项"的按钮（`NativeRow.Cycler`），`SetOptions`/`SetIndex`/`OnDropdown` 依然可用 |
| `TMP_InputField` 手搓失败 | 该行显示"控件创建失败"，其它行不受影响 |
| 原生 Sprite 找不到 | 面板退回纯白 Image（仍然可见可点），日志一条警告 |
| 字体找不到 | 用 TMP 默认字体，日志一条警告 |
| 贴图不可读（`GetPixels` 抛异常） | IMGUI 皮肤退回"原图不着色"，只警告一次 |
| `NativeUI.Window` 在无 Canvas 场景调用 | 库自己创建 Overlay Canvas，不会抛 |
| 任何回调里抛异常 | 库捕获并写日志，不会中断游戏的 UI 循环 |

**唯一会真的失败返回**：`NativeMenu.AddMainMenuButton` / `AddPauseMenuButton` 找不到模板时返回 `null`
（并打警告）。所以调用它们要判空：

```csharp
var go = NativeMenu.AddMainMenuButton("x", () => { });
if (go == null) Logger.LogWarning("菜单按钮注入失败");
```

---

## 8. 交付前的自检清单

写完代码后，逐条确认（这些是最常见的错误来源）：

- [ ] **没有在插件 `Awake()` 里创建窗口**（改用 `Start()` + `NativeUI.RunNextFrame`）。
- [ ] 所有 `NativeUI.Window` 的引用都有对应的置空逻辑（`Closed += () => _w = null;`），否则第二次按快捷键会操作已销毁对象。
- [ ] 用 `NativeUI.InWorld` / `PlayerCamera.main != null` 判空后才访问游戏对象。
- [ ] 播放音效用的是 `NativeUI.PlayClick()` 等静态方法（不是 `PlayerCamera.main.PlayUISound`）。
- [ ] 给 TMP 文本赋值时用了 `NativeUI.Font`（`if (NativeUI.Font != null) text.font = NativeUI.Font;`）。
- [ ] 数值初始化用 `notify:false` 语义（`AddToggle/AddSlider` 的 `value` 参数会自动用 `SetIsOnWithoutNotify`/`SetValueWithoutNotify`，无需额外处理）。
- [ ] 访问了 `Setting`/`Settings` 的地方用的是 `Settings.Get<T>(name)`，不是 `Settings.GetSetting<T>`（后者是 `private`）。
- [ ] 若调用了 `NativeMenu.Add*`，做了 null 检查。
- [ ] 若窗口回调引用了场景对象，订阅了 `NativeUI.SceneChanged` 做处理。
- [ ] 引用 `NativeUILib` 的 `<Private>false</Private>` 已设置（避免出现两份库副本导致类型冲突）。
- [ ] 若用 IMGUI，`InstallInputBlockPatch()` 只调用一次（可以在 `Awake` 里）。

---

## 9. 反模式（不要这么做）

| ❌ 不要 | ✅ 应该 |
|---|---|
| `new GameObject` + `AddComponent<Image>` + 手搓 `TMP_Dropdown` 模板 | 用 `w.AddDropdown(...)`（它复用游戏预制体） |
| `Resources.Load<Sprite>("uiBlockSmall")` | `NativeAssets.PanelSmall` / `NativeAssets.Find("uiBlockSmall")`（前者不在 Resources） |
| `Resources.Load<TMP_FontAsset>("Retro GamingPix")` | `NativeUI.Font` |
| 自己写 `OnGUI` 画新界面 | `NativeUI.Window`（IMGUI 只服务老代码） |
| `TMP_Settings.defaultFontAsset` 直接拿来用 | `NativeUI.Font`（会优先给游戏像素字体） |
| 每帧 `Resources.FindObjectsOfTypeAll<Sprite>()` | 库已缓存；要用就 `NativeAssets.Find` / `Exists` |
| 用 `Time.deltaTime` 驱动 UI 动画 | `Time.unscaledDeltaTime` |
| 在 UI 上硬编码像素尺寸（1080p 假设） | 乘 `NativeTheme.Scale`，或用 `NativeTheme.Scaled(...)` |
| 把 UI 放在 `Default` 层 | 让库的工厂方法创建（它们会自动设 `UI` 层） |
| 在 `Assets` 里放一大堆自绘控件去凑原生外观 | 先试 `AddRow(NativeRowKind.…)`，游戏预制体是最终答案 |

---

## 10. 排障表

| 症状 | 原因 / 解决 |
|---|---|
| 窗口是纯白色的方块，没有九宫格边框 | `uiBlock*` 贴图没拿到 → 场景还没加载完。用 `NativeUI.DumpAssets()` 确认；确认 `NativeAssets.Exists("uiBlockSmall")` |
| 文字全是方块 / 空白 | 字体缺字。检查 `NativeUI.ChineseAvailable`；手动 `NativeUI.AddSystemFontFallback("Microsoft YaHei")` |
| **某个控件里没有文字（比如按钮里空着）** | 容器太窄，像素字体的字形接近 1:1 宽高比放不下。把 `TextMeshProUGUI.overflowMode` 设成 `Overflow`，或缩小 `fontSize` / 加大容器。**不要用 `Ellipsis`**——该字体很可能没有 `…`(U+2026) 字形，TMP 会干脆什么都不画（库的默认值因此是 `Truncate`） |
| 按钮点不动 | `NativeUI.HasEventSystem == false`（有别的模组禁用了 EventSystem）；或你的 UI 不在 `UI` 层；或某个全屏 `Image` 挡住了（检查对话框遮罩没被销毁） |
| 点 UI 的同时角色也攻击了 | 你的 UI 不在 `UI` 层；或你用的是 IMGUI 但没调 `InstallInputBlockPatch()` |
| 中文模组里 tooltip 没反应 | `UITooltip` 需要挂在**有 `Graphic` 且 `raycastTarget=true`** 的对象上，并且该对象在 `UI` 层 |
| 窗口高度不对 / 内容被裁 | `AutoSize` 默认 true 且上限 `Screen.height * 0.75`；内容太多会自动滚动。也可 `w.AutoSize = false; w.SetSize(w,h)` |
| 暂停菜单新增的按钮不动画 | `buttonPos` 反射失败（游戏版本字段改名）→ 按钮仍可用。日志里会有警告 |
| 场景切换后窗口操作报 NullReference | 窗口常驻，但回调里的场景对象已销毁 → 订阅 `NativeUI.SceneChanged` 关闭/重建 |
| 出现两份 NativeUILib 导致类型冲突 | 消费者没设 `<Private>false</Private>`，或把 DLL 复制进了子目录 |

---

## 11. 内部实现要点（改库代码时看）

- **控件来源**：`NativeRow.ResourcePath(kind)` → `Special/GameSetting{Bool,Float,Int,Dropdown,Input}`。
  子节点约定（**游戏自己就是这么用的**）：`Child0`=名称文本、`Child1`=控件、`Child2`=数值文本。
- **布局**：内容区 = `VerticalLayoutGroup(childControlWidth/Height=true, forceExpandWidth=true)` +
  `ContentSizeFitter(vertical=PreferredSize)`，外面套 `ScrollRect` + `RectMask2D`。
  窗口 `AutoSize` 时在 `LateUpdate` / `Add*` 后调 `RefreshLayout()`。
- **拖动**：标题栏挂 `NativeDragger`，用 `RectTransformUtility.ScreenPointToLocalPointInRectangle`
  算局部坐标（对任何 Canvas 缩放都正确）。
- **滚动**：`Body` 上放了一个 **alpha=0 但 raycastTarget=true** 的 `Image`，否则滚轮命中会落到父级、
  `ScrollRect` 收不到事件。
- **场景钩子**：`NativeBootstrap`（`DontDestroyOnLoad`）监听 `sceneLoaded` → `NativeAssets.Invalidate()`
  + `NativeGUISkin.MarkDirty()` + 触发 `NativeUI.SceneChanged`。
- **IMGUI 焦点计数**：`NativeGUISkin.Begin/End` 只负责切皮肤；命中计数写在 `_pendingFocused`，
  由 `NativeBootstrap.LateUpdate()` 每帧结算成 `FocusedRects`（这样下一帧的 `Update` 能读到，与 KrokMP 做法一致）。

---

## 12. 参考

- 库源码：`src/`（每个文件头部都有注释说明职责）
- 可编译示例：`samples/SampleAddon/`（`SampleWindow` / `SampleHud` / `SampleImGuiWindow` / `SampleSettingsModule`）
- 游戏原生 UI 逆向报告：[docs/native-ui-research.zh-CN.md](docs/native-ui-research.zh-CN.md)
  （含每个资源路径、贴图名、注入点的出处与实机资产核对）
- 英文版说明：[README.md](README.md)
- 真实世界参考实现：第三方联机模组 **KrokoshaCasualtiesMP（Casualties: Together）**，其
  `UIBullshit.cs` 展示了"用游戏贴图手搓 IMGUI 原生皮肤"的完整做法，
  `KrokoshaMainmenuBackground.cs` 展示了主菜单注入 + `Resources.FindObjectsOfTypeAll` 搜素材的做法。
  （该模组不在本仓库内，可从其发布页获取）


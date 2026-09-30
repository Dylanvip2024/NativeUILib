> **本文是 [NativeUILib](../README.md) 背后的逆向研究记录。**
>
> 它记录了游戏原生 UI（UGUI + TextMeshPro）的资源路径、九宫格贴图名、字体名、组件契约与
> Harmony 注入点的**出处**：每条结论都标注了游戏反编译源码的行号，并对照实机资产文件
> （`CasualtiesUnknown_Data\*.assets`）核对过存在性。
>
> 语言：中文 ｜ English entry point: [README.md](../README.md)

---
# Casualties Unknown 7.0.1 —— 游戏「原生样式 UI」调用指南

> 研究范围：
> - `Assembly-CSharp\`（游戏反编译源码，265 个类型文件）
> - `KrokoshaCasualtiesMP\`（Coop/MP 模组，含 `UIBullshit.cs` / `UIMainMenu.cs` / `UIInGame.cs` / `UIServerBrowser.cs` / `UIChoicePrompt.cs`）
> - 实机资产文件 `D:\ruanjian\steam\steamapps\common\Casualties Unknown Demo\CasualtiesUnknown_Data\*.assets`（用于验证贴图名 / 字体名真的存在）
>
> 结论全部有出处，末尾附速查表。

---

## 0. 一句话结论

**游戏的原生 UI = Unity UGUI（`UnityEngine.UI`）+ TextMeshPro，全部由 `Resources` 下的预制体生成；入口是 `Utils.Create("<资源路径>", 父Transform)`。游戏自身一帧 IMGUI 都没用过。**

所以「调用原生样式 UI」有三条正道，按保真度排序：

| 方式 | 做法 | 保真度 | 适用场景 |
|---|---|---|---|
| **A** | `Utils.Create("Special/xxx", parent)` 直接生成游戏自带 UI 预制体 | ★★★★★（就是本体） | 设置菜单、物品标签、配方、交易、MP3、剧情文本… |
| **B** | `Instantiate` 场景里已有的 UI 对象当模板，改文字/换监听 | ★★★★★（连动画/布局一起继承） | 主菜单加按钮、暂停菜单加按钮 |
| **C** | 自己 `new GameObject` + `Image` / `TextMeshProUGUI`，但复用游戏的 sprite（`uiBlockSmall`/`uiBlockNano`）与字体（`Retro GamingPix`） | ★★★★ | 自定义布局的面板、HUD 挂件 |
| D（**不是**原生） | `OnGUI` + 克隆 `GUI.skin` 再贴游戏贴图 | ★★ | 模组 KrokoshaCasualtiesMP 用的就是这条，属"仿原生" |

---

## 1. 技术栈判定（附证据）

### 1.1 游戏没有 IMGUI

对 `Assembly-CSharp\` 全目录 grep：

```
pattern: OnGUI|GUI\.skin|GUISkin|GUIStyle
结果: No matches found
```

也就是说游戏从不设置 `GUI.skin`、从不写 `OnGUI`。反过来看模组的 `UIBullshit.cs`：

- `_GUI_CaptureOGUITextures()`（第 794 行）保存的是 **Unity 内置默认皮肤**（`_og_skin = GUI.skin`、`_og_fontsize`、`_b_ogfont`）；
- `_GUI_DoTexturesForMPUserInterface()`（第 765 行）自己 `Object.Instantiate<GUISkin>(GUI.skin)` 造一份新皮肤，再逐项贴游戏贴图；
- `_GUI_MPUISetSkinValues()`（第 708 行）命名就叫 "MP UI Set Skin Values"，说明皮肤是模组**自己搭**的；
- `UnfuckFonts()` / `DoFontThings()`（第 655、667 行）在还原字体，也说明默认皮肤不是游戏风格。

**推论：模组的 IMGUI 界面是"用游戏素材手工复刻的仿原生皮肤"，不是游戏原生调用路径。** 想要真原生，请走方式 A/B/C。

### 1.2 原生 UI 一律来自 `Resources` 预制体

```csharp
// Assembly-CSharp\Utils.cs:10-19  —— 唯一的创建入口
public static GameObject Create(string id, Vector2 pos, float rot)
    => Object.Instantiate(Resources.Load(id), pos, Quaternion.Euler(0,0,rot)) as GameObject;

public static GameObject Create(string id, Transform trans)
    => Object.Instantiate(Resources.Load(id), trans) as GameObject;
```

设置界面的整行控件就是运行时按类型创建的（`SettingsMenu.cs:59-167`）：`Special/GameSettingFloat`、`Special/GameSettingInt`、`Special/GameSettingDropdown`、`Special/GameSettingBool`、`Special/GameSettingInput`、`Special/GameSettingLanguage`。

### 1.3 四个 Canvas（父节点选择表）

| Canvas | 取法 | 生命周期 | 用途 |
|---|---|---|---|
| 主菜单 | `PreRunScript.instance.mainCanvas` （或 `PreRunScript.instance.GetComponent<Canvas>()`） | 主菜单场景 | 主菜单 / 存档 / 运行设置 |
| 游戏内 HUD | `PlayerCamera.main.mainCanvas` | 游戏场景 | 背包、伤口视图、交易、警报 |
| 世界空间 | `PlayerCamera.main.worldCanvas` | 游戏场景 | 跟随世界坐标的东西 |
| 常驻 | `GlobalDark.main.canvas` | `DontDestroyOnLoad` | 跨场景存活（tooltip/过场都在这里） |

模组自己的取法可以直接抄（`UIBullshit.cs:238-248`）：

```csharp
public static Canvas canvas => Util.IsInMainMenu()
    ? PreRunScript.instance.GetComponent<Canvas>()
    : PlayerCamera.main.mainCanvas;
// Util.IsInMainMenu() 的判定就是 WorldGeneration.world == null
```

判断环境：

```csharp
bool inMenu  = WorldGeneration.world == null;                 // 主菜单
bool inWorld = WorldGeneration.world != null;                 // 游戏内
bool paused  = PauseHandler.main && PauseHandler.main.isPaused;
```

---

## 2. 方式 A：直接生成游戏自带预制体（最推荐）

### 2.1 已验证可用的 Resources 路径

下列路径全部来自游戏自身代码调用（**已在 `resources.assets` 里验证对象名存在**），可直接 `Resources.Load`：

| 资源路径 | 组件 | 出处 |
|---|---|---|
| `Special/SettingsMenu` | `SettingsMenu` | `SettingsMenu.cs:18` |
| `Special/GameSettingBool` / `Float` / `Int` / `Dropdown` / `Input` / `Language` | 行控件 | `SettingsMenu.cs:59-167` |
| `Special/RunSettingBool` / `Float` / `Dropdown` | `RunSettingDisplay` | `PreRunScript.cs:101-113` |
| `Special/ItemLabel` | `ItemLabel`（图标+tooltip） | `AltHoverScript.cs:34` |
| `Special/RecipeBox` / `Special/RecipeItemPreview` | 配方列表 | `PlayerCamera.cs:239,395` |
| `Special/LiquidTransfer` | `LiquidTransfer` | `PlayerCamera.cs:1543` |
| `Special/TraderInvSplit` / `Special/TraderItemPanel` | 交易面板 | `PlayerCamera.cs:2576-2589` |
| `Special/MP3SongSelect` | `MP3Menu` | `Item.cs:6045`、`ConsoleScript.cs:1293` |
| `Special/ClickableText` | `ScrollableText`（打字机剧情） | `ScrollableText.cs:28` |
| `Special/ScannerUI` | `ScannerScript` | `Item.cs:5827` |
| `Special/MindwipeVignette` | 暗角 | `MindwipeScript.cs:22,85` |
| `Special/LimbArmorNum` | 护甲数字 | `WoundView.cs:375` |
| `Special/Console` | 控制台 | `ConsoleScript.cs:1873` |
| `Special/PickupLine` | 拾取提示线 | `Body.cs:1378` |
| `Special/HitFlash` | 受击闪白 | `WorldGeneration.cs:294` |
| `Special/GlobalDark` | `GlobalDark`（含 tooltip 层） | `GlobalDark.cs:189` |
| `GraphicWearDropButton` | 拖出装备按钮 | `PlayerCamera.cs:2816` |
| `ContainerItem` | 容器内物品行 | `PlayerCamera.cs:2871` |
| `DamageText` | 伤害飘字 | `WorldGeneration.cs:3997` |

### 2.2 最小可用示例：任何场景打开原生设置菜单

```csharp
using UnityEngine;

public static class NativeUI
{
    // 游戏自己就是这么开的（PauseHandler.Settings() / AdaptiveButton.Clicked()）
    public static void OpenSettings()
    {
        if (SettingsMenu.instance) return;                 // 单例，别开两份
        var parent = WorldGeneration.world == null
            ? PreRunScript.instance.mainCanvas.transform
            : (PauseHandler.main ? PauseHandler.main.transform : PlayerCamera.main.mainCanvas.transform);
        SettingsMenu.OpenMenu(parent);                     // 内部 = Utils.Create("Special/SettingsMenu", parent)
    }
}
```

`SettingsMenu` 的关键成员（`SettingsMenu.cs`）：

```csharp
public static SettingsMenu instance;
public RectTransform content;              // 行都塞这里
public List<Button> buttons;               // 左侧分类页签
public Sprite buttonOpen, buttonClosed;    // 选中/未选中贴图
public void SelectTab(Setting.SettingCategory category);  // Video/Audio/Game/Input/Language
public void Close();                       // Destroy + SaveSettings
private List<GameObject> spawnedSettings;  // 注意：私有的，模组用反射/属性改
```

### 2.3 把自定义项混进原生设置菜单

游戏支持扩展 `Settings.DefaultSettings()`，之后设置菜单会**自动**为它生成一行原生控件：

```csharp
[HarmonyPatch(typeof(Settings), nameof(Settings.DefaultSettings))]
static class AddMySetting
{
    static void Postfix(ref List<Setting> __result)
    {
        var s = new SettingBool {
            name = "mymod_enable",          // 本地化 key = "gameset" + name
            value = true,
            apply = () => { /* 生效逻辑 */ },
            category = Setting.SettingCategory.Game,
        };
        __result.Add(s);
    }
}
```

- 显示文字取 `Locale.GetOther("gameset" + name)`
- tooltip 描述取 `Locale.GetOther("gameset" + name + "dsc")`
- 想加键位就 `new SettingKeybind { name=..., value=KeyCode.F5, category = Setting.SettingCategory.Input }`（模组 `Settings_DefaultSettings_Patch.cs` 就是这个套路）

### 2.4 行控件的子节点约定（务必照抄）

原生行预制体的层级是固定的，游戏代码依赖 `GetChild(i)`：

**`Special/GameSetting*` / `Special/RunSetting*`**

```
Child 0 : TextMeshProUGUI        ← 设置名（也是 tooltip 挂载点）
Child 1 : 控件本体
          Slider / Toggle / TMP_Dropdown / TMP_InputField / Button(键位)
          bool → Toggle.isOn
          float → Slider.minValue / maxValue / value
          dropdown → TMP_Dropdown.AddOptions(List<OptionData>) / SetValueWithoutNotify
          int → TMP_InputField.SetTextWithoutNotify / onEndEdit
Child 2 : TextMeshProUGUI        ← 数值显示（仅 float 行有）
```

范例（`SettingsMenu.cs:59-70` 精简）：

```csharp
var row = Utils.Create("Special/GameSettingFloat", menu.content);
var slider = row.transform.GetChild(1).GetComponent<Slider>();
slider.minValue = 0; slider.maxValue = 1;
slider.SetValueWithoutNotify(0.5f);
row.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = "50%";
slider.onValueChanged.AddListener(v => { /* 应用 */ });
row.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "我的滑条";

// 想要原生 tooltip：
var tip = row.transform.GetChild(0).gameObject.AddComponent<UITooltip>();
tip.skipLocale = true;                 // true = 不查本地化表，直接用 tipName/tipDesc
tip.tipName = "我的滑条";
tip.tipDesc = "说明文字";
```

`Special/GameSettingLanguage` 有点特殊：**根节点自己就是 Button**，`Child 0` 是文字（`SettingsMenu.cs:167-176`）。

### 2.5 生成后的摆放

预制体自带 `RectTransform`，但位置要自己算 —— 游戏用的是"从 0 往下堆 + 撑高父容器"：

```csharp
float y = 0f;
foreach (var item in myItems) {
    var go = Utils.Create("Special/GameSettingBool", content);
    var rt = go.GetComponent<RectTransform>();
    rt.anchoredPosition = new Vector2(0f, -y - rt.sizeDelta.y * 0.5f);
    y += rt.sizeDelta.y;
}
content.sizeDelta = new Vector2(content.sizeDelta.x, y);
```

（出自 `SettingsMenu.cs:154-155,187`、`PreRunScript.cs:111-118`）

---

## 3. 方式 B：克隆场景已有 UI 当模板（保真度最高）

模组 `KrokoshaMainmenuBackground.CreateBackground()`（`KrokoshaMainmenuBackground.cs`）是最佳范例：它**不手搓**按钮，而是从主菜单里偷一个现成按钮来复制。

```csharp
PreRunScript prerun = Object.FindObjectOfType<PreRunScript>();

// 1) 偷一个原生按钮当模板（RunSettings/Presets/Cancel 是标准小按钮）
Transform t = prerun.transform.Find("RunSettings/Presets/Cancel");
template_button = Object.Instantiate(t.gameObject, prerun.transform, false);
template_button.name = "MY_BUTTON";
var btn = template_button.GetComponent<Button>();
btn.GetComponentInChildren<TMP_Text>(true).color = Color.white;  // 清掉原按钮的颜色状态
btn.onClick.RemoveAllListeners();                                // 关键：清掉 persistent listener
btn.onClick.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.Off);
template_button.SetActive(false);                                // 存成模板，藏起来

// 2) 每个按钮 = 再 Instantiate 一份模板
var mine = Object.Instantiate(template_button, prerun.transform, false);
mine.SetActive(true);
mine.name = "MY_REAL_BUTTON";
mine.GetComponentInChildren<TextMeshProUGUI>().text = "我的按钮";
mine.GetComponent<Button>().onClick.AddListener(() => { /* ... */ });

// 3) 用 anchor 定位，别用 anchoredPosition 硬怼
var rt = mine.GetComponent<RectTransform>();
rt.anchorMin = new Vector2(0.3f, 0f);
rt.anchorMax = new Vector2(0.6f, 0.1f);
rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
rt.SetSiblingIndex(prerun.transform.childCount);  // 控制显示层级
```

要点：

1. **`onClick.RemoveAllListeners()` 必须调**，否则新按钮会连带触发原按钮的逻辑。
2. **`SetPersistentListenerState(0, Off)`** —— 原生按钮的事件是"持久监听器"（Inspector 里连的），`RemoveAllListeners()` 不一定清得掉。
3. 换图标：`GetComponent<Image>().sprite = mySprite;`
4. 主菜单里可当模板的现成节点（用 `prerun.transform.Find(...)`）：
   - `Credits/TranslationRepo` —— 链接型按钮（模组用它做 MP 图标按钮）
   - `RunSettings/Presets/Cancel` —— 标准按钮（模组用它做所有按钮）
   - `VersionWarning` —— 两行文字（模组用它改版本文案）
5. **暂停菜单**同理：`PauseHandler.main` 上挂着 `buttons: List<RectTransform>`、`center`、`background`、`pauseText`；给它加按钮后记得考虑它的入场动画（`PauseHandler.Update()` 会按 `buttonPos` 列表插值缩放，新增按钮不会被自动收录，需要自己补进 `buttons` 或另做动画）。

---

## 4. 方式 C：自己用 UGUI 拼，但复用游戏素材

游戏自己在运行时**就是这么创建** UI 元素的 —— `WoundView.AddImageToLimb()`（`WoundView.cs`）：

```csharp
GameObject go = new GameObject(sprite.name, new Type[] { typeof(Image) });
go.transform.SetParent(parentRectTransform);            // Image 会自动带 RectTransform/CanvasRenderer
go.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
go.GetComponent<Image>().sprite = sprite;
go.GetComponent<Image>().SetNativeSize();               // 像素完美尺寸
```

### 4.1 拿到游戏的原生九宫格贴图

**这批 sprite 不在 Resources 里**（我在 `resources.assets` 里搜不到 `uiBlockSmall`），它们作为场景资源被打进 `sharedassets0/1.assets`，所以只能等场景加载后用 `Resources.FindObjectsOfTypeAll<Sprite>()` 按名字捞 —— 这正是模组的做法（`KrokoshaMainmenuBackground.cs:27-91`）：

```csharp
Sprite FindNativeSprite(string name)
{
    var all = Resources.FindObjectsOfTypeAll<Sprite>();
    var s = all.FirstOrDefault(x => x.name == name)
         ?? all.FirstOrDefault(x => x.name.Contains(name));   // 退化成包含匹配
    return s;
}
```

**已实机验证存在的贴图名**（扫 `*.assets` 得到，和模组的 `sprites_to_find` 完全吻合）：

| 贴图名 | 所在文件 | 说明 |
|---|---|---|
| `uiBlock` | sharedassets0 | 主面板九宫格 |
| `uiBlockSmall` | sharedassets0 | 小面板 / 大按钮（模组主要用它） |
| `uiBlockNano` | sharedassets0 | 最小面板 / 小按钮 / 滑条 / 输入框 |
| `uiBlockMini` | sharedassets1 | 迷你块 |
| `whitesquare` | sharedassets0 / 1 | 纯白 1px（tooltip 底、遮罩） |
| `uigradientblack` | sharedassets1 | 黑渐变 |
| `uicursor` / `uicursorpressed` | sharedassets1 | 光标 |
| `uiPoint` | sharedassets1 | 指针 |
| `userbarfill` | sharedassets1 | 条状填充 |
| `UICheckMark` | resources.assets | 勾 |
| `UIElement8px` | resources.assets | 8px 元素 |

> `UIFoldoutClosed` / `UIFoldoutOpened` 是模组列表里也有的，但我在 assets 里没扫到（可能被剥离或改名），用之前先判空。

### 4.2 另一条捷径：`PlayerCamera.main.uiNano`

`PlayerCamera` 自己就序列化了两个原生 sprite 字段（`PlayerCamera.cs:3652,3655`）：

```csharp
public Sprite uiNano;            // 原生"小面板"贴图
public Sprite darkenedUiNano;    // 它的暗色版（用于选中态）
```

游戏用它做配方分类页签的选中/未选中（`PlayerCamera.cs:91-95`）。这是**最省事**的原生外观来源，缺点是需要 `PlayerCamera.main`（仅游戏内）。

### 4.3 拿到原生 TMP 字体

游戏 UI 的 TMP 字体资产名是 **`Retro GamingPix`**（已验证存在于 `sharedassets0.assets`）。它同样不在 Resources 里，模组是这样捞的（`NetPlayer.cs:499-506`）：

```csharp
static TMP_FontAsset s_font;
TMP_FontAsset GetNativeFont()
{
    if (s_font == null)
        s_font = (from x in Object.FindObjectsOfType<TextMeshProUGUI>()
                  select x.font)
                 .FirstOrDefault(f => f != null && f.name == "Retro GamingPix");
    return s_font;
}
```

补充：

- `resources.assets`（真·Resources 目录）里有 `LiberationSans SDF`、`LiquidCrystal-BoldItalic SDF` 两个 TMP 字体资产，可以尝试 `Resources.Load<TMP_FontAsset>(...)`。
- **IMGUI 用的旧版 `Font`**（`GUI.skin.font`）不能直接从游戏拿，游戏资产里没有独立 ttf 可 `Resources.Load`；模组是把自己打包的 `retrogaming_200.ttf` 塞进 AssetBundle 再 `AssetBundle.LoadFromMemory` 取的（`CoopModAssets.cs:98-103`）。
- **缺字处理必须做**，否则中文/特殊字符会画成空白或方块：用 `font.HasCharacter(c, true, false)` 遍历替换（模组 `Together\FontUtils.cs` 就是干这个的）。

### 4.4 手工造一个原生风格面板（完整示例）

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class NativePanel : MonoBehaviour
{
    static Sprite Find(string n) =>
        Resources.FindObjectsOfTypeAll<Sprite>()
            .FirstOrDefault(s => s.name == n || s.name.Contains(n));

    public static GameObject Create(Transform parent, string title)
    {
        // 1) 根：Image 九宫格面板
        var root = new GameObject("MyNativePanel", typeof(Image), typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var img = root.GetComponent<Image>();
        img.sprite = Find("uiBlockSmall");
        img.type   = Image.Type.Sliced;         // 九宫格
        img.color  = new Color(1f, 1f, 1f, 0.9f);
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(420f, 300f);
        rt.anchoredPosition = Vector2.zero;

        // 2) 标题
        var t = new GameObject("Title", typeof(TextMeshProUGUI));
        t.transform.SetParent(root.transform, false);
        var tmp = t.GetComponent<TextMeshProUGUI>();
        tmp.font = GetNativeFont();
        tmp.text = title;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 20f * (Screen.height / 1080f);   // 和 GlobalDark.uiScale 同款缩放
        tmp.color = Color.white;
        t.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 110f);

        // 3) 原生按钮：偷模板最稳，这里演示自建
        var b = new GameObject("OK", typeof(Image), typeof(Button));
        b.transform.SetParent(root.transform, false);
        var bi = b.GetComponent<Image>();
        bi.sprite = Find("uiBlockNano"); bi.type = Image.Type.Sliced;
        b.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 40f);
        b.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -110f);
        b.GetComponent<Button>().onClick.AddListener(() => {
            PlayerCamera.PlayUISound("click", 1f);       // 原生点击音
            Object.Destroy(root);
        });

        // 4) 原生 tooltip 支持：随便找个 Graphics 挂 UITooltip 即可
        var tip = b.AddComponent<UITooltip>();
        tip.skipLocale = true;
        tip.tipName = "确定"; tip.tipDesc = "关掉这个面板";

        return root;
    }

    static TMP_FontAsset cached;
    static TMP_FontAsset GetNativeFont() => cached ??= Object
        .FindObjectsOfType<TextMeshProUGUI>()
        .Select(x => x.font)
        .FirstOrDefault(f => f && f.name == "Retro GamingPix");
}
```

### 4.5 九宫格 border 参数（想像素级还原时用）

模组从游戏面板尺寸反推出来的值（`UIBullshit.cs:1003-1006`、`279-280`）：

```csharp
int uiBlockSmallBorderSize = 3;   // uiBlockSmall 的 4 边 border
int uiBlockNanoBorderSize  = 2;   // uiBlockNano 的 4 边 border
// 模组在运行时按缩放放大：
uiBlockSmallBorderSize = Mathf.CeilToInt(uiScaleForMostUI * 4f);
uiBlockNanoBorderSize  = Mathf.CeilToInt(uiScaleForMostUI * 2f);
```

UGUI 里对应 `Image.type = Sliced` + `Image.pixelsPerUnitMultiplier`（或直接改 sprite 的 border）。

---

## 5. 必须知道的原生组件契约

| 组件 | 文件 | 作用 | 用法 |
|---|---|---|---|
| `UITooltip` | `UITooltip.cs` | 原生 tooltip 数据源 | 挂任意 GameObject；`skipLocale=true` 时直接用 `tipName`/`tipDesc`，否则 `Start()` 里查 `Locale.GetOther(localeName/localeDesc)` |
| `UILocalizer` | `UILocalizer.cs` | 文本本地化 | `key` 字段，`Start()` 里 `GetComponent<TextMeshProUGUI>().text = Locale.GetOther(key)`；`upper` 转大写 |
| `UIHoverDescription` | `UIHoverDescription.cs` | 悬停钩子（**空实现**） | 只有接口，游戏内实际逻辑靠 `GlobalDark` 的 raycast 流程；可当标记接口用 |
| `UIUtil` | `UIUtil.cs` | 指针判定 | `IsPointerOverUIElement()` / `IsPointerOverContextMenu()` / `GetUITooltip(raycastResults)` |
| `ScrollableText` | `ScrollableText.cs` | 打字机剧情 | `ScrollableText.CreateText(text, cover, sprites, font)` / `ForceClose()` / `textExists` |
| `AdaptiveButton` | `AdaptiveButton.cs` | **旧式主菜单按钮** | 自己调 `Input.GetMouseButtonDown` + `GetDistanceToCursor()`，鼠标靠近才亮；`overlayActive` 为 true 时禁用。**新 UI 别学它**，用 `Button` |
| `Moodle` / `BonusMoodleShowScript` | `Moodle.cs` 等 | HUD 状态图标 | 依赖 `Image` + 子节点 `Image`/`TextMeshProUGUI` 的固定层级 |

### 5.1 让原生 tooltip 自动弹出来

游戏每帧的 tooltip 流程（`GlobalDark.cs:136-170`）：

```
EventSystem.RaycastAll(mouse)
  → UIUtil.GetUITooltip(results)   // 找第一个带 UITooltip 的射线命中对象
  → GlobalDark.main.SetTooltip((name, desc))
  → GlobalDark.HandleTooltip()      // 画在 GlobalDark.main.tooltipRect 上，鼠标跟随 + 屏幕边缘夹取
```

所以只要满足：

1. 对象在 **`UI` 层**（`UIUtil` 用 `LayerMask.NameToLayer("UI")` 过滤）；
2. 有 `Graphic`（`Image`/`TMP_Text`）且 `raycastTarget = true`；
3. 挂 `UITooltip` 并填好 `tipName`/`tipDesc`（`skipLocale = true`）；

tooltip 就会用游戏原生外观弹出来。**也可以手动触发**：

```csharp
GlobalDark.main.SetTooltip(new ValueTuple<string,string>("名字", "描述"));
```

### 5.2 原生提示条（游戏内）

```csharp
PlayerCamera.main.DoAlert(Locale.GetOther("..."),important: false);   // 屏幕下方滑出提示条
```
（`PlayerCamera.cs:2070` 附近；`important:true` 显示在正中）

---

## 6. 输入 / 遮挡 / 穿透

- 游戏判断"鼠标是否在 UI 上"用的是 `UIUtil.IsPointerOverUIElement()`（`PlayerCamera.cs:1481,1853`）。**你自己的 UGUI 元素在 `UI` 层就自动被算进去**，不需要额外处理。
- 如果你（像模组一样）用 **IMGUI** 画界面，就必须自己记账，否则点击会穿透到世界（挖方块/开枪）。模组的方案（`UIBullshit.cs`）：
  - `CheckCursorOverlap(rect)` 统计 `TOTAL_INTERACTIVE_UI` / `TOTAL_FOCUSED_INTERACTIVE_UI`；
  - `LateUpdate` 里 `IS_CURSOR_OVERLAPPING = TOTAL_FOCUSED_INTERACTIVE_UI > 0`；
  - `Update` 里若重叠则 `GlobalDark.main.SetTooltip(("",""))` 抑制原生 tooltip。
- **UGUI 和 IMGUI 混用时**：UGUI 的点击由 `EventSystem` 处理，IMGUI 由 `GUI.Button` 处理，两者互不知情，一定要保证"同一块屏幕区域只由一套系统消费"。
- 模组的控制台 `Con.IsConsoleOpen()` 会屏蔽大部分 UI；如果你也在控制台上叠加东西，注意这个开关。

---

## 7. 原生 UI 音效

```csharp
// 两种重载（PlayerCamera.cs:32,38）
PlayerCamera.main.PlayUISound(PlayerCamera.UISoundType.Click, 1f);   // 实例版
PlayerCamera.PlayUISound("click", 1f);                               // 静态版（主菜单没有 PlayerCamera.main 时用这个）
```

`UISoundType` → 实际音效名对照（枚举顺序即索引，音效名由模组 `Util.cs:1026-1060` 还原，并已核对代码里的字面量用法）：

| 枚举 | 值 | 音效名 | 语义 |
|---|---|---|---|
| `Click` | 0 | `click` | 确定/主按钮 |
| `MiniClick` | 1 | `miniClick` | 页签切换/小操作 |
| `Close` | 2 | `close` | 关闭面板 |
| `HealthPanelOn` | 3 | `woundon` | 伤口视图打开 |
| `InventoryOpen` | 4 | `menuOpen` | 背包/暂停打开 |
| `InventoryClose` | 5 | `menuClose` | 背包/暂停关闭 |
| `Deny` | 6 | `warning` | 拒绝/失败 |
| `Time` | 7 | `time` | 时间缩放 |

底层是 `Sound.Play(clipName, Vector2.zero, twoDimensional:true, ...)` —— 想直接按名字播也行：`Sound.Play("click", Vector2.zero, true, false, null, 1f, 1f, true, true)`。

---

## 8. 尺度与动画规范（"原生手感"）

### 8.1 缩放

| 量 | 公式 | 出处 |
|---|---|---|
| 全局 UI 缩放 | `Screen.height / 1080f` | `GlobalDark.uiScale` |
| 游戏内 Canvas 缩放 | `PlayerCamera.main.mainCanvas.transform.localScale.y` | `PlayerCamera.uiScale` |
| 背包/伤口视图图标缩放 | `PlayerCamera.ImageSizeDelta(tex, pixelSize, maxSize)` | `PlayerCamera.cs:997` |
| 模组字号 | `DEFAULT_RETROGAME_FONTSIZE(20) * uiScale * 自定义倍率` | `UIBullshit.GetCurFontSize()` |
| 模组 tooltip 字号 | `label.fontSize * 0.78f` | `UIBullshit.TOOLTIP_SCALE` |

`ImageSizeDelta` 就是一个"按像素比例缩放并夹到 maxSize"的工具：

```csharp
Vector2 size = PlayerCamera.ImageSizeDelta(sprite.texture, 5f, 40f);
image.rectTransform.sizeDelta = size;   // ItemLabel.SetItem 就是这么干的
```

### 8.2 动画 / 缓动

- 原生 UI 动画**一律用 `Time.unscaledDeltaTime`**（暂停时 `timeScale = 0`，UI 还得动）。例：`AdaptiveButton.Update()`、`Moodle.Update()`、`PauseHandler.Update()`、`PreRunScript.Update()`。
- `AdaptiveButton` 的"靠近才亮"公式：
  ```csharp
  float d = GetDistanceToCursor();                        // 鼠标在 rect 内 = 0
  float a = Mathf.Max(1f - d * 0.005f, 0.025f);           // 越远越淡
  overlay.color = Color.Lerp(overlay.color, new Color(1,1,1,a), Time.unscaledDeltaTime * 3f);
  text.color    = Color.Lerp(text.color,    new Color(1,1,1, d <= 0 ? 1 : 0), Time.unscaledDeltaTime * 3f);
  ```
- `PauseHandler.Update()` 的入场：容器 `OutCubic` 缩放 + 每个按钮 `InOutQuint` 递增延迟 + `localPosition` 从半程补间。
- 缓动函数全在 `Utils.cs`：`InOutQuad/OutCubic/InOutQuint/OutElastic/...`，别自己再写一套。
- 颜色透明常用扩展：`color.WithAlpha(a)`（`Utils.cs:57`）。

### 8.3 配色约定

- 面板：白 + `alpha 0.8~0.9`（模组贴图处理也是 `brightness=1f, alpha=0.8f`）。
- 按钮态：normal `brightness 1.0 / alpha 0.8`，hover `0.65/0.7`，active/focus `0.4/0.7`（`UIBullshit.cs:870-946`）。
- 文本：`Color.white`；禁用/次要 `0.9f` 灰。
- 强调色：金色 `new Color(1f, 0.765f, 0f)`（`PreRunScript.cs:226` 预设选中）。
- 危险：`Color.red`；警告闪烁用 `Mathf.PingPong(Time.unscaledTime * 2.5f, 1f)` 做 alpha（`Moodle.Update()`）。

---

## 9. 把入口塞进原生 UI —— Harmony 注入点清单

| 想做的事 | Patch 目标 | 说明 |
|---|---|---|
| 主菜单加按钮 | `PreRunScript.Start` Postfix，或 `PreRunScript.Awake` 后 | 模组在 `KrokoshaMainmenuBackground.CreateBackground()` 里做 |
| 暂停菜单加按钮 | `PauseHandler.Start` / `PauseHandler.Update` | 注意 `buttons` 动画列表 |
| 设置菜单加页签/项 | `Settings.DefaultSettings` Postfix（加 `Setting`） | 最干净，UI 会自动生成 |
| 强制设置菜单打开某页 | `SettingsMenu.Start` Prefix（返回 false）+ 自己 `SelectTab` | 模组 `SettingsMenu_Start_Patch.cs` 的做法 |
| 想在菜单展开时禁用原生按钮 | `AdaptiveButton.overlayActive` Postfix（改 `__result = true`） | 模组 `AdaptiveButton_overlayActive_MultiplayerPatch.cs` |
| 世界内新增 UI 容器 | `WorldGeneration.FinishWorldGeneration` 之后 | 此时 `PlayerCamera.main` 已存在 |
| 跨场景常驻 UI | `GlobalDark.Awake` Postfix，挂到 `GlobalDark.main.canvas` | 该对象 `DontDestroyOnLoad` |

模组里可当模板的文件：

- `KrokoshaMainmenuBackground.cs` —— 主菜单注入 + 原生 sprite 搜索 + 模板克隆（**最值得读**）
- `UIBullshit.cs` —— IMGUI 仿原生皮肤全套
- `UIChoicePrompt.cs` —— 自己画的模态确认框（IMGUI 版）
- `PlayerCamera_ToggleTradeMenu_MultiplayerPatch.cs` / `WoundView_*` —— 对原生 UGUI 的增删改
- `KrokoshaMP_CharacterStatusIcon_*.cs` + `CharStatusVisuals.cs` —— 用 `SpriteRenderer` 做世界内状态图标

---

## 10. 坑清单

1. **`Resources.Load` 只能拿 Resources 目录的东西。** `uiBlockSmall`/`Retro GamingPix` 都在场景资产（sharedassets）里，**必须**用 `Resources.FindObjectsOfTypeAll` / `FindObjectsOfType` 搜，而且要等对应场景加载完（主菜单场景加载后才有 `Retro GamingPix`）。
2. **`Resources.FindObjectsOfTypeAll` 有性能成本**，别每帧调。缓存结果（模组就是静态字典 + 缓存 Texture2D）。
3. **`onClick.RemoveAllListeners()` 之外还要 `SetPersistentListenerState(0, Off)`**，否则克隆按钮会带上原逻辑。
4. **不要用 `new GameObject()` 直接加 `TextMeshProUGUI` 却忘了 `font`** —— 默认字体可能不是像素字体，会跟游戏风格完全不一样。一定要赋 `Retro GamingPix`。
5. **中文字形缺失**：`Retro GamingPix` 基本没有 CJK。要么做字符 fallback（`TMP_FontAsset.fallbackFontAssetTable`），要么用 `HasCharacter` 检测后替换，否则全是方块。
6. **`AdaptiveButton` 是主菜单特例**：它绕开 `EventSystem`，自己读 `Input.GetMouseButtonDown(0)`。你的 UGUI 元素如果盖在它上面，两边会同时响应 —— 记得通过 `overlayActive` 关掉它。
7. **`SettingsMenu` 是半单例**：`instance` 是 public static，但 `spawnedSettings` 是 private。模组用反射/属性访问；你也可以直接 `SelectTab` 让它自己重建。
8. **暂停菜单里开 UI 会卡住时间**：`Time.timeScale = 0` 时所有 `Time.deltaTime` 驱动的东西都不动，一律用 `unscaledDeltaTime`。
9. **`PlayerCamera.main` 在主菜单为 null**，所有用到它的调用都要判空（`PlayerCamera.PlayUISound(string)` 是静态版，主菜单可用）。
10. **场景切换会销毁 UI**：主菜单加的按钮在 `SceneManager.LoadScene("SampleScene")` 后没了。常驻请挂 `GlobalDark.main.canvas`。
11. **层级/显示顺序**：同 Canvas 内用 `SetSiblingIndex` / `SetAsLastSibling`；跨 Canvas 用 `Canvas.sortingOrder`。游戏的 tooltip 在 `GlobalDark` 的独立 Canvas 上，一般盖不住它。
12. **别把可交互 UI 放到非 `UI` 层**：`UIUtil` 的判定按层过滤，放错了就不算"指针在 UI 上"，点击会穿透到游戏世界。

---

## 附录 A：速查表

```csharp
// ── 创建原生预制体 ───────────────────────────────
GameObject go = Utils.Create("Special/GameSettingBool", parentTransform);
GameObject go2 = Utils.Create("primitivediggingtool", worldPos, 0f);   // 世界物件版

// ── 打开原生菜单 ────────────────────────────────
SettingsMenu.OpenMenu(PreRunScript.instance.mainCanvas.transform);
ScrollableText.CreateText(Locale.GetOther("startlorenote"), cover:true, sprites, font);
if (ScrollableText.textExists) ScrollableText.ForceClose();

// ── 环境判定 ───────────────────────────────────
bool menu   = WorldGeneration.world == null;
bool paused = PauseHandler.main && PauseHandler.main.isPaused;
bool console= ConsoleScript.instance && ConsoleScript.instance.active;
bool pointerOverUI = UIUtil.IsPointerOverUIElement();

// ── 拿原生素材 ─────────────────────────────────
Sprite ui = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == "uiBlockSmall");
Sprite ui2 = PlayerCamera.main.uiNano;                       // 游戏内捷径
TMP_FontAsset font = Object.FindObjectsOfType<TextMeshProUGUI>()
                           .Select(x => x.font)
                           .FirstOrDefault(f => f && f.name == "Retro GamingPix");

// ── 原生交互 ───────────────────────────────────
PlayerCamera.PlayUISound("click", 1f);
GlobalDark.main.SetTooltip(new ValueTuple<string,string>("名字","描述"));
PlayerCamera.main.DoAlert("提示文字", false);
var tip = go.AddComponent<UITooltip>(); tip.skipLocale = true; tip.tipName = "..."; tip.tipDesc = "...";

// ── 本地化 ─────────────────────────────────────
string s = Locale.GetOther("key");     // 物品 Locale.GetItem / 建筑 GetBuilding / 状态 GetMoodle
var loc = go.AddComponent<UILocalizer>(); loc.key = "key"; loc.upper = false;

// ── 尺寸 ───────────────────────────────────────
float scale = Screen.height / 1080f;                         // GlobalDark.uiScale
Vector2 size = PlayerCamera.ImageSizeDelta(sprite.texture, 5f, 40f);
image.type = Image.Type.Sliced;                              // 九宫格
```

## 附录 B：Resources 路径速查

```
Special/  SettingsMenu
          GameSettingBool | GameSettingFloat | GameSettingInt | GameSettingDropdown
          GameSettingInput | GameSettingLanguage
          RunSettingBool | RunSettingFloat | RunSettingDropdown
          ItemLabel | RecipeBox | RecipeItemPreview
          LiquidTransfer | TraderInvSplit | TraderItemPanel
          MP3SongSelect | ClickableText | ScannerUI | MindwipeVignette
          LimbArmorNum | Console | PickupLine | HitFlash | GlobalDark
根路径    GraphicWearDropButton | ContainerItem | DamageText
Sprites/  （如 Sprites/haggleIcon、Sprites/droplet、Sprites/waterpouch1 …）
BiomeIcon/1..N
Moodles   （Resources.LoadAll<Sprite>("Moodles")）
```

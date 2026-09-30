# NativeUILib

[![Release](https://img.shields.io/github/v/release/Dylanvip2024/NativeUILib?include_prereleases&sort=semver)](https://github.com/Dylanvip2024/NativeUILib/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/BepInEx-5.4.x-blue)](https://github.com/BepInEx/BepInEx)

[English](README.md) | **中文**

给 **Casualties Unknown**（7.0.1 / Demo）做的**原生风格 UI 绘制库**，以 BepInEx 5 插件形式发布。
其他模组引用这一个 DLL，就能用几行代码画出和游戏本体一模一样的界面。

```csharp
NativeUI.Window("我的面板")
    .AddLabel("你好")
    .AddToggle("启用", true, v => Enabled = v)
    .AddSlider("强度", 0f, 1f, 0.5f, v => Power = v, "0.00")
    .AddButton("关闭", () => { });
```

---

## 它解决什么问题

游戏的原生 UI 是 **UGUI + TextMeshPro**（不是 IMGUI），所有控件都藏在 `Resources` 的
`Special/GameSetting*` 预制体里，九宫格贴图则只存在于场景资产（`sharedassets`）里，
像素字体 `Retro GamingPix` 同样是场景资产。自己从零复刻这套外观要踩很多坑：

- 贴图**不在** `Resources`（得用 `Resources.FindObjectsOfTypeAll` 捞）；
- 字体**也不在** `Resources`；
- 9 宫格 border 要按分辨率缩放；
- 原生音效、`GlobalDark` 的全局 tooltip 流程；
- UI 必须放 `UI` 层，否则世界点击会穿透；
- 暂停时 `Time.timeScale == 0`，动画必须用 `Time.unscaledDeltaTime` ……

**NativeUILib 把这些全部封好了。** 关键在于它不去"模仿"原生外观，而是**直接复用游戏自己的控件**：
设置行是从游戏的 `Special/GameSetting*` 预制体直接实例化的，所以保真度是 100%。
预制体/贴图/字体缺失时，所有部件都会自动降级，不会抛异常。

| 你会得到 | 实现方式 |
|---|---|
| 原生控件 | 直接实例化游戏自己的 `Special/GameSettingBool/Float/Int/Dropdown/Input` 预制体 |
| 原生面板 | `uiBlock` / `uiBlockSmall` / `uiBlockNano` 九宫格，`Image.Type.Sliced` |
| 原生字体 | 自动反查 `Retro GamingPix`；缺中文时自动挂系统中文字体 fallback |
| 原生音效 | `click` / `miniClick` / `close` / `menuOpen` / `menuClose` / `warning` / `time` |
| 原生 tooltip | 挂上 `UITooltip` 就由游戏统一绘制（跟随鼠标、屏幕边缘夹取、脑损伤时扭曲） |
| 原生弹窗 | `NativeUI.Confirm(...)` 全屏遮罩 + 居中面板 |
| 原生提示条 | 游戏内直接调 `PlayerCamera.DoAlert`；主菜单里自绘一个原生面板 |
| 菜单注入 | 克隆游戏自己的按钮塞进主菜单 / 暂停菜单 |
| 设置项注入 | 一行 Harmony Postfix，自定义设置就出现在**游戏原生设置菜单**里 |
| IMGUI 皮肤 | 老式 `OnGUI` 模组一行 `using (NativeGUISkin.Scope())` 也能用上原生贴图 |

---

## 环境要求

| | |
|---|---|
| 游戏 | Casualties Unknown（Unity **Mono**，`CasualtiesUnknown_Data`） |
| 模组加载器 | **BepInEx 5.4.x**（HarmonyX） |
| 编译目标 | `net48`，用 `dotnet build`（.NET SDK 6+；需要 .NET Framework 4.8 Targeting Pack） |

## 安装

**方式 A —— 直接下载**：从 [Releases](https://github.com/Dylanvip2024/NativeUILib/releases) 下载
`NativeUILib.dll`，丢进 `BepInEx\plugins\`。

**方式 B —— 自己编译**

```powershell
.\build.ps1 -Deploy            # 编译并复制到游戏的 BepInEx\plugins
.\build.ps1 -Deploy -WithSample   # 连示例 Addon 一起
```

> 游戏目录默认为 `D:\ruanjian\steam\steamapps\common\Casualties Unknown Demo`，
> 用 `-GameFolder "..."` 覆盖；也可以直接改 `NativeUILib.csproj` 里的 `<GameFolder>`。

BepInEx 会把它当插件加载（`com.mod.casualties.nativeuilib`），启动时打一条日志。

## 在你自己的模组里引用

```xml
<ItemGroup>
  <Reference Include="NativeUILib">
    <HintPath>..\NativeUILib\build\NativeUILib.dll</HintPath>
    <Private>false</Private>   <!-- 关键：不要复制第二份 -->
  </Reference>
</ItemGroup>
```

```csharp
[BepInPlugin("你的.guid", "你的模组", "1.0.0")]
[BepInDependency(NativeUILibPlugin.Guid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin { ... }
```

> 库是**懒初始化**的：即使不声明依赖、不调用 `NativeUI.Init()`，第一次用 API 时也会自动就绪。
> 声明依赖只是为了让加载顺序更明确。

## API 速查

```csharp
// ── 窗口（UGUI，可拖动、自动高度、超高自动滚动）──
var w = NativeUI.Window("标题");                 // 或 Window(标题, 父节点, 宽, 高, 自动高度)
w.RememberPosition("my.mod.panel");             // 记住用户拖动的位置（PlayerPrefs）
w.AddHeader("段落标题");                          // 居中 + 金色
w.AddLabel("会自动换行的说明文字");
w.AddToggle("开关", true, v => Enabled = v, "悬停提示");
w.AddSlider("强度", 0f, 1f, 0.5f, v => Power = v, "0.00", "悬停提示");
w.AddDropdown("模式", new[] { "A", "B" }, 0, i => Mode = i);
w.AddInput("名字", "幸存者", s => Name = s, freeText: true);
w.AddButtonRow("重置", "点我", () => Reset());
w.AddInlineButton("说明", "点我", () => { });
w.AddSeparator(); w.AddSpace(8f);
w.AddButton("关闭", () => w.Close());
w.Closed += () => _window = null;

// ── 弹窗 / 提示 ──
NativeUI.Toast("已保存", important: false, seconds: 4f);
NativeUI.Message("提示", "完成了。");
NativeUI.Confirm("删除存档", "确定吗？", onYes: Delete, onNo: null);
NativeDialog.Choose("选一个", "msg", new NativeDialog.Choice("方案 A", OnA), new NativeDialog.Choice("方案 B", OnB));

// ── 与游戏原生 UI 集成 ──
NativeMenu.AddMainMenuButton("我的面板", OpenMyPanel);
NativeMenu.AddPauseMenuButton("我的面板", OpenMyPanel);
NativeUI.OpenNativeSettings(category: 2);       // 0=Video 1=Audio 2=Game 3=Input 4=Language
NativeUI.ShowNativeText("打字机效果的剧情文本…");

// ── 素材 / 状态 ──
Sprite ui   = NativeUI.Sprite("uiBlockNano");   // 未加载完则返回 null
var font    = NativeUI.Font;                     // Retro GamingPix
NativeUI.PlayClick();                            // 另有 PlayMiniClick/Close/Open/Deny
bool overUI = NativeUI.IsPointerOverUI();
bool inGame = NativeUI.InWorld;
bool esOk   = NativeUI.HasEventSystem;
NativeUI.DumpAssets();                           // 诊断：贴图名、字体、运行环境

// ── 中文显示（像素字体没有 CJK 字形）──
NativeUI.AutoFontFallback = true;               // 默认开启：发现缺字自动挂系统字体
NativeUI.AddSystemFontFallback("Microsoft YaHei");
bool cjk = NativeUI.ChineseAvailable;
```

**更细粒度控制** —— `NativeRow`（一行原生控件）：

```csharp
var row = w.AddRow(NativeRowKind.Float, "音量");
row.Slider.minValue = 0f; row.Slider.maxValue = 1f;
row.SetFloat(0.8f, notify: false);              // notify:false → 不触发回调
row.OnFloat(v => AudioListener.volume = v);
```

**IMGUI（仅给既有老模组）**

```csharp
private void OnGUI()
{
    using (NativeGUISkin.Scope())
        GUILayout.Window(0, _rect, DrawWindow, "标题");
}
private void Awake() => NativeGUISkin.InstallInputBlockPatch();   // 防止点击穿透到世界
```

## 几个容易踩的点

- **中文** —— `Retro GamingPix` 没有中日文字形。库会在首次遇到缺字时自动注册系统中文字体 fallback，
  也可以手动指定；用 `NativeUI.ChineseAvailable` 检查。
- **时机** —— 原生贴图/字体要等场景加载完才存在。库会在每次场景切换后自动重新索引，
  在此之前会自动降级（纯色面板 + 默认字体）。
- **过早创建 UI** —— 在插件 `Awake()` 里开窗口时游戏的 `EventSystem` 可能还没出现（按钮暂时不响应）。
  建议用 `Start()` + `NativeUI.RunNextFrame(...)`。
- **场景切换** —— 库的 Overlay Canvas 是 `DontDestroyOnLoad`，窗口会跨场景存活；
  如果你的回调碰了场景对象，请订阅 `NativeUI.SceneChanged`。
- **只能有一份库** —— 引用时务必 `<Private>false</Private>`。

---

## 文档

| 文件 | 读者 | 内容 |
|---|---|---|
| **[AI_AGENT_GUIDE.md](AI_AGENT_GUIDE.md)** | **AI 智能体 / 需要完整参考的人** | 完整 API 签名、20+ 可复制示例、硬约束、交付前自检清单、反模式、排障表 |
| [docs/native-ui-research.zh-CN.md](docs/native-ui-research.zh-CN.md) | 想理解原理的人 | 游戏原生 UI 的逆向研究报告：每条资源路径 / 贴图名 / 注入点都有源码出处与实机资产核对 |
| [samples/SampleAddon/](samples/SampleAddon/) | 想抄代码的人 | 可编译的完整示例模组（窗口 / HUD / IMGUI / 设置注入 / 菜单注入） |
| [README.md](README.md) | 国际用户 | 英文版说明 |

## 目录结构

```
NativeUILib/
├─ NativeUILib.csproj          库工程（net48）
├─ NativeUILib.sln
├─ build.ps1                   编译 + 部署
├─ build/                      编译产物（已忽略，DLL 请从 Releases 下载）
├─ src/
│  ├─ Plugin.cs                BepInEx 入口 + 日志 + 常驻宿主（场景切换钩子）
│  ├─ NativeUI.cs              ★ 统一入口（窗口/提示/对话框/原生设置/音效/素材）
│  ├─ NativeWindow.cs          ★ 原生窗口（拖动、自动高度、滚动、位置记忆）
│  ├─ NativeRow.cs             ★ 原生设置行（Toggle/Slider/Input/Dropdown/Button + 自绘降级）
│  ├─ NativeAssets.cs          素材服务（Sprite 索引 / 字体 / fallback / 贴图加工）
│  ├─ NativeTheme.cs           尺寸、字号、颜色规范
│  ├─ NativeBuild.cs           内部构建原语
│  ├─ NativeDialog.cs          模态对话框
│  ├─ NativeMenu.cs            主菜单 / 暂停菜单注入
│  └─ NativeGUISkin.cs         IMGUI 原生皮肤 + 世界输入屏蔽补丁
├─ samples/SampleAddon/        可编译示例模组
└─ docs/                       逆向研究记录
```

## 许可

[MIT](LICENSE) © 2026 Dylanvip2024

与 Casualties Unknown 的开发团队无关。

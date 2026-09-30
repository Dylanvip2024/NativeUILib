# NativeUILib

给 **Casualties Unknown**（7.0.1 / Demo）做的**原生风格 UI 绘制库**。
其他 BepInEx 模组引用它，就能用几行代码画出和游戏本体一模一样的界面。

```
NativeUI.Window("我的面板")
    .AddLabel("你好")
    .AddToggle("启用", true, v => Enabled = v)
    .AddSlider("强度", 0f, 1f, 0.5f, v => Power = v, "0.00")
    .AddButton("关闭", () => { });
```

---

## 它解决什么问题

游戏的原生 UI 是 **UGUI + TextMeshPro**（不是 IMGUI），所有控件都藏在 `Resources` 的
`Special/GameSetting*` 预制体里，九宫格贴图则只存在于场景资产（`sharedassets`）里。
自己从零复刻这套外观要踩很多坑：贴图不在 Resources、字体是场景资产、9 宫格 border 要按分辨率缩放、
按钮有原生音效、tooltip 要走 `GlobalDark` 的全局流程、UI 必须放 "UI" 层才不会被世界点击穿透……

**NativeUILib 把这些全部封好了**，对外只暴露 `NativeUI.*` / `NativeWindow` / `NativeRow` 三层 API。

| 你会得到 | 说明 |
|---|---|
| 原生控件 | 直接实例化 `Special/GameSettingBool/Float/Int/Dropdown/Input`，外观 100% 一致 |
| 原生面板 | `uiBlock` / `uiBlockSmall` / `uiBlockNano` 九宫格，`Image.Type.Sliced` |
| 原生字体 | 自动反查场景里的 `Retro GamingPix`；缺中文时自动挂系统中文字体 fallback |
| 原生音效 | `click` / `miniClick` / `close` / `warning` … |
| 原生 tooltip | 挂上 `UITooltip` 就由游戏统一绘制（自动跟随鼠标、屏幕边缘夹取、被眩晕扭曲） |
| 原生弹窗 | `NativeUI.Confirm(...)` 全屏遮罩 + 居中面板 |
| 原生提示条 | 游戏内直接调 `PlayerCamera.DoAlert` |
| 菜单注入 | 克隆游戏自己的按钮塞进主菜单 / 暂停菜单 |
| 设置项注入 | 一行 Harmony Postfix，自定义设置就出现在**游戏原生设置菜单**里 |
| IMGUI 皮肤 | 老式 `OnGUI` 模组一行 `using (NativeGUISkin.Scope())` 也能用上原生贴图 |

---

## 安装

1. 编译（或直接拿 `build\NativeUILib.dll`）：

   ```powershell
   .\build.ps1 -Deploy            # 编译并复制到游戏的 BepInEx\plugins
   .\build.ps1 -Deploy -WithSample   # 连示例 Addon 一起
   ```

   > 游戏目录默认为 `D:\ruanjian\steam\steamapps\common\Casualties Unknown Demo`，
   > 用 `-GameFolder "..."` 覆盖；也可以直接改 `NativeUILib.csproj` 里的 `<GameFolder>`。

2. 确保 `NativeUILib.dll` 在 `BepInEx\plugins\` 下（BepInEx 会把它当插件加载并写一条日志）。

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

---

## 文档

| 文件 | 读者 | 内容 |
|---|---|---|
| **[AI_AGENT_GUIDE.md](AI_AGENT_GUIDE.md)** | **AI 智能体 / 需要完整参考的人** | 完整 API 签名、20+ 可复制示例、硬约束、自检清单、反模式、排障表 |
| `samples/SampleAddon/` | 想抄代码的人 | 可编译的完整示例模组（窗口 / HUD / IMGUI / 设置注入 / 菜单注入） |
| `../逆向源码/7.0.1/游戏原生UI调用指南.md` | 想理解原理的人 | 游戏原生 UI 的逆向研究结论（含出处与实机验证） |

---

## 环境要求

- 游戏：Casualties Unknown（Unity Mono，`CasualtiesUnknown_Data`）
- BepInEx 5.4.x（HarmonyX）
- 编译目标：`net48`，`dotnet build`（.NET SDK 6+ 均可；需要 .NET Framework 4.8 Targeting Pack）

## 目录结构

```
NativeUILib/
├─ NativeUILib.csproj          库工程（net48）
├─ NativeUILib.sln
├─ build.ps1                   编译 + 部署
├─ build/NativeUILib.dll       产物
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
└─ samples/SampleAddon/        可编译示例模组
```

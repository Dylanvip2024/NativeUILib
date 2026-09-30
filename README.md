# NativeUILib

[![Release](https://img.shields.io/github/v/release/Dylanvip2024/NativeUILib?include_prereleases&sort=semver)](https://github.com/Dylanvip2024/NativeUILib/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/BepInEx-5.4.x-blue)](https://github.com/BepInEx/BepInEx)

**English** | [中文](README.zh-CN.md)

A **native-look UI toolkit** for **Casualties Unknown** (7.0.1 / Demo), shipped as a BepInEx 5 plugin.
Other mods reference one DLL and get pixel-perfect, game-identical UI in a few lines of code.

```csharp
var w = NativeUI.Window("My Panel");
w.AddLabel("Hello");
w.AddToggle("Enable", true, v => Enabled = v);
w.AddSlider("Power", 0f, 1f, 0.5f, v => Power = v, "0.00");
w.AddButton("Close", () => w.Close());
```

---

## Why this exists

The game's UI is **UGUI + TextMeshPro** — *not* IMGUI. Every control lives in `Resources` prefabs
(`Special/GameSetting*`), while the 9-slice sprites only exist inside scene assets (`sharedassets`) and the
pixel font (`Retro GamingPix`) is a scene-loaded TMP asset. Reproducing that look from scratch means fighting:

- sprites that are **not** in `Resources` (must be found via `Resources.FindObjectsOfTypeAll`),
- a font that is **not** in `Resources` either,
- 9-slice borders that must be rescaled per resolution,
- native button sounds, the global tooltip pipeline (`GlobalDark`), the `UI` layer requirement (so world
  clicks don't leak through), `Time.unscaledDeltaTime` for paused menus, and more.

**NativeUILib wraps all of it.** Instead of *imitating* the native look, it **reuses the game's own widgets**:
row controls are instantiated straight from the game's `Special/GameSetting*` prefabs, so fidelity is 100% by
construction. Everything degrades gracefully when a prefab/sprite/font is unavailable.

| What you get | How |
|---|---|
| Native controls | Instantiates the game's own `Special/GameSettingBool/Float/Int/Dropdown/Input` prefabs |
| Native panels | `uiBlock` / `uiBlockSmall` / `uiBlockNano` 9-slice, `Image.Type.Sliced` |
| Native font | Auto-resolves `Retro GamingPix`; optional OS-font fallback for CJK |
| Native sounds | `click` / `miniClick` / `close` / `menuOpen` / `menuClose` / `warning` / `time` |
| Native tooltips | Attach a `UITooltip`; the game draws it (follows cursor, clamps to screen, distorted when concussed) |
| Native modal dialogs | `NativeUI.Confirm(...)` — full-screen dimmer + centered panel |
| Native toasts | Calls `PlayerCamera.DoAlert` in-game; self-drawn panel in the main menu |
| Menu injection | Clones real game buttons into the main menu / pause menu |
| Settings injection | One Harmony postfix → your settings appear in the **game's own settings menu** |
| IMGUI skin | Legacy `OnGUI` mods: `using (NativeGUISkin.Scope())` gets the native sprites too |

---

## Requirements

| | |
|---|---|
| Game | Casualties Unknown (Unity **Mono**, `CasualtiesUnknown_Data`) |
| Mod loader | **BepInEx 5.4.x** (HarmonyX) |
| Build target | `net48`, built with `dotnet build` (.NET SDK 6+; needs the .NET Framework 4.8 targeting pack) |

## Installation

**Option A — download** the prebuilt `NativeUILib.dll` from
[Releases](https://github.com/Dylanvip2024/NativeUILib/releases) and drop it into `BepInEx\plugins\`.

**Option B — build from source**

```powershell
.\build.ps1 -Deploy                 # build + copy into the game's BepInEx\plugins
.\build.ps1 -Deploy -WithSample     # also build the sample addon
```

> The game folder defaults to `D:\ruanjian\steam\steamapps\common\Casualties Unknown Demo`.
> Override with `-GameFolder "..."` or edit `<GameFolder>` in `NativeUILib.csproj`.

BepInEx picks the DLL up as a plugin (`com.mod.casualties.nativeuilib`) and logs one line at startup.

## Using it from your own mod

```xml
<ItemGroup>
  <Reference Include="NativeUILib">
    <HintPath>..\NativeUILib\build\NativeUILib.dll</HintPath>
    <Private>false</Private>   <!-- important: never ship a second copy -->
  </Reference>
</ItemGroup>
```

```csharp
[BepInPlugin("your.guid", "Your Mod", "1.0.0")]
[BepInDependency(NativeUILibPlugin.Guid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin { /* ... */ }
```

> The library is **lazily initialised**: even without the dependency attribute or a call to `NativeUI.Init()`,
> the first API call brings everything up. Declaring the dependency just makes load order explicit.

## API cheat sheet

```csharp
// ── Windows (UGUI, draggable, auto-height, scrolls when too tall) ──
var w = NativeUI.Window("Title");               // or Window(title, parent, width, height, autoSize)
w.RememberPosition("my.mod.panel");             // persist drag position (PlayerPrefs)
w.AddHeader("Section");                         // gold, centered
w.AddLabel("Wrapped text", sizeMult: 1f);
w.AddToggle("Enable", true, v => Enabled = v, "tooltip");
w.AddSlider("Power", 0f, 1f, 0.5f, v => Power = v, "0.00", "tooltip");
w.AddDropdown("Mode", new[] { "A", "B" }, 0, i => Mode = i);
w.AddInput("Name", "Survivor", s => Name = s, freeText: true);
w.AddButtonRow("Reset", "Click me", () => Reset());
w.AddInlineButton("Label", "Click", () => { });
w.AddSeparator(); w.AddSpace(8f);
w.AddButton("Close", () => w.Close());
w.Closed += () => _window = null;

// ── Popups ──
NativeUI.Toast("Saved", important: false, seconds: 4f);
NativeUI.Message("Info", "Done.");
NativeUI.Confirm("Delete save", "Are you sure?", onYes: Delete, onNo: null);
NativeDialog.Choose("Pick one", "msg", new NativeDialog.Choice("A", OnA), new NativeDialog.Choice("B", OnB));

// ── Integrate with the game's own UI ──
NativeMenu.AddMainMenuButton("My Panel", OpenMyPanel);
NativeMenu.AddPauseMenuButton("My Panel", OpenMyPanel);
NativeUI.OpenNativeSettings(category: 2);       // 0=Video 1=Audio 2=Game 3=Input 4=Language
NativeUI.ShowNativeText("Typewriter lore text…");

// ── Assets / misc ──
Sprite ui   = NativeUI.Sprite("uiBlockNano");   // null if not loaded yet
var font    = NativeUI.Font;                     // Retro GamingPix
NativeUI.PlayClick();                            // also PlayMiniClick/Close/Open/Deny
bool overUI = NativeUI.IsPointerOverUI();
bool inGame = NativeUI.InWorld;
bool esOk   = NativeUI.HasEventSystem;
NativeUI.DumpAssets();                           // diagnostics: sprite names, font, environment

// ── CJK text (the pixel font has no CJK glyphs) ──
NativeUI.AutoFontFallback = true;               // default: auto-register an OS font when a glyph is missing
NativeUI.AddSystemFontFallback("Microsoft YaHei");
bool cjk = NativeUI.ChineseAvailable;
```

**Raw controls** — `NativeRow` (one native settings row) is exposed when you need more control:

```csharp
var row = w.AddRow(NativeRowKind.Float, "Volume");
row.Slider.minValue = 0f; row.Slider.maxValue = 1f;
row.SetFloat(0.8f, notify: false);              // notify:false → no callback fired
row.OnFloat(v => AudioListener.volume = v);
```

**IMGUI (legacy mods only)**

```csharp
private void OnGUI()
{
    using (NativeGUISkin.Scope())
        GUILayout.Window(0, _rect, DrawWindow, "Title");
}
private void Awake() => NativeGUISkin.InstallInputBlockPatch();   // stop world clicks leaking through
```

## Gotchas worth knowing

- **CJK text** – `Retro GamingPix` has no Chinese/Japanese glyphs. The library auto-registers an OS font
  fallback the first time a glyph is missing; you can also set one explicitly.
- **Timing** – native sprites/fonts only exist once the scene is loaded. The library re-indexes automatically on
  every scene load, and degrades to plain panels/default fonts before that.
- **Creating UI too early** – if you open a window from plugin `Awake()`, the game's `EventSystem` may not exist
  yet (buttons won't react until it does). Prefer `Start()` + `NativeUI.RunNextFrame(...)`.
- **Scene changes** – the library's overlay canvas is `DontDestroyOnLoad`, so windows survive; subscribe to
  `NativeUI.SceneChanged` if your callbacks touch scene objects.
- **One copy only** – reference with `<Private>false</Private>`.

## Documentation

| File | Audience | Contents |
|---|---|---|
| **[AI_AGENT_GUIDE.md](AI_AGENT_GUIDE.md)** | AI coding agents / anyone wanting the full reference | Complete API signatures, 20+ copy-paste examples, hard constraints, pre-flight checklist, anti-patterns, troubleshooting (Chinese) |
| [docs/native-ui-research.zh-CN.md](docs/native-ui-research.zh-CN.md) | Anyone curious about the internals | Reverse-engineering report: every resource path, sprite name and injection point, with source-line citations and asset verification (Chinese) |
| [samples/SampleAddon/](samples/SampleAddon/) | People who learn by copying | A complete, compilable sample mod (window / HUD / IMGUI / settings injection / menu injection) |

## Repository layout

```
NativeUILib/
├─ NativeUILib.csproj           library project (net48)
├─ NativeUILib.sln
├─ build.ps1                    build + deploy
├─ build/                       build output (git-ignored — grab the DLL from Releases)
├─ src/
│  ├─ Plugin.cs                 BepInEx entry + logging + persistent host (scene hooks)
│  ├─ NativeUI.cs               ★ facade (windows / toasts / dialogs / native settings / sounds / assets)
│  ├─ NativeWindow.cs           ★ native window (drag, auto-height, scroll, position memory)
│  ├─ NativeRow.cs              ★ native settings row (Toggle/Slider/Input/Dropdown/Button + fallback)
│  ├─ NativeAssets.cs           asset service (sprite index / font / fallback / texture processing)
│  ├─ NativeTheme.cs            sizes, font sizes, colours
│  ├─ NativeBuild.cs            internal construction primitives
│  ├─ NativeDialog.cs           modal dialogs
│  ├─ NativeMenu.cs             main-menu / pause-menu injection
│  └─ NativeGUISkin.cs          IMGUI native skin + world-input blocking patch
├─ samples/SampleAddon/         compilable sample mod
└─ docs/                        reverse-engineering notes
```

## License

[MIT](LICENSE) © 2026 Dylanvip2024

Not affiliated with the developers of Casualties Unknown.

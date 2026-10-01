# IL2CPP IMGUI Dictionary Translator / IL2CPP IMGUI 字典翻译器

[English](#english) | [中文](#中文)

A BepInEx 6 (IL2CPP) plugin that translates IMGUI texts — such as the ConfigurationManager (F1 menu) — on IL2CPP-based Illusion games, using a static `key=value` dictionary.

一个 BepInEx 6 (IL2CPP) 插件，通过静态 `键=值` 字典翻译 IL2CPP 游戏（ILLGAMES 系）中 IMGUI 绘制的界面文本——例如按 F1 打开的 ConfigurationManager（配置管理器）。

> Why: XUnity.AutoTranslator's IMGUI hooks are not available on IL2CPP (`NotImplementedException` in its source), so plugin-manager texts stay untranslated on IL2CPP games. This plugin fills that gap with a lightweight dictionary interceptor.
>
> 原因：XUnity.AutoTranslator 的 IMGUI 挂钩在 IL2CPP 上不可用（源码中为 `NotImplementedException`），导致 IL2CPP 游戏的插件管理器文本无法翻译。本插件用轻量的字典拦截器填补了这个空缺。

<a name="english"></a>
## English

### Supported games (tested)

| Game | BepInEx | Plugin file |
|---|---|---|
| HoneyCome | 6.0.0-be.752 (IL2CPP) | `HC/IL2CPPImguiDictTranslator.dll` |
| SamabakeScramble (SVS) | 6.0.0-be.752 (IL2CPP) | `SVS/IL2CPPImguiDictTranslator.dll` |
| Amaloca (AL) | 6.0.0-be.788 (IL2CPP) | `AL/IL2CPPImguiDictTranslator.dll` |
| Aicomi (AC) | 6.0.0-dev (IL2CPP) | `AC/IL2CPPImguiDictTranslator.dll` |

Probably works on any BepInEx 6 IL2CPP game — the hook targets `UnityEngine.GUI` / `UnityEngine.GUILayout` interop proxies.

### How it works

At startup the plugin:

1. Loads every `*.txt` in `BepInEx/Translation/zh-CN/Text/` (skipping `_`-prefixed system files) into a dictionary (`key=value` lines).
2. Finds all static methods on the interop proxies `UnityEngine.GUI` and `UnityEngine.GUILayout` that take a `GUIContent` parameter, plus common `string text` overloads.
3. Harmony-patches them with a prefix that looks up `GUIContent.text` in the dictionary and rewrites it in place.

Template keys are supported: `{{A}}`, `{{B}}`… map to number capture groups in occurrence order.

```
HC_Ahegao {{A}}=HC颜高潮插件 {{A}}          → HC_Ahegao 1.0.0 becomes HC颜高潮插件 1.0.0
BepInEx {{A}}-be.{{A}}=BepInEx框架 {{A}}-be.{{A}}  → BepInEx 6.0.0-be.691 becomes BepInEx框架 6.0.0-be.691
```

Install: drop the game-matching `IL2CPPImguiDictTranslator.dll` into `BepInEx/plugins/`, and put your dictionary at `BepInEx/Translation/zh-CN/Text/` (any `*.txt`). Toggle in F1 → *IL2CPP IMGUI Dictionary Translator*.

Build: open the per-game `.csproj` and point the `HintPath`s at your game's `BepInEx/core`, `BepInEx/interop`, then `dotnet build -c Release`.

<a name="中文"></a>
## 中文

### 已测试游戏

HoneyCome、SamabakeScramble (SVS)、Amaloca、Aicomi（BepInEx 6 IL2CPP）。理论上适用于所有 BepInEx 6 IL2CPP 游戏。

### 工作原理

1. 启动时加载 `BepInEx/Translation/zh-CN/Text/` 下所有 `*.txt`（跳过 `_` 开头的系统文件）到字典
2. 反射找出互操作程序集 `UnityEngine.GUI` / `UnityEngine.GUILayout` 中所有带 `GUIContent` 参数的静态方法及常见 `string text` 重载
3. 用 Harmony 打前缀补丁：调用前查字典，命中则就地改写 `GUIContent.text`

模板键：`{{A}}`、`{{B}}`… 按出现顺序映射数字捕获组（见上方示例）。

安装：把对应游戏的 `IL2CPPImguiDictTranslator.dll` 放进 `BepInEx/plugins/`，字典放 `BepInEx/Translation/zh-CN/Text/`。F1 里可开关。

编译：修改各游戏 `.csproj` 里的 `HintPath` 指向你的游戏目录后 `dotnet build -c Release`。

### 字典编写注意事项

- **含数字的键必须用 `{{A}}` 模板**：`FPS Counter {{A}}=FPS帧率计数器 {{A}}` ✓；`FPS Counter 3.3.1=...` ✗（永远失配）
- 模板按出现顺序映射：`BepInEx {{A}}-be.{{A}}` 的第一个 `{{A}}` 对应 `6.0.0`、第二个对应 `691`
- 日文键请注意假名/汉字字形陷阱（アヘ/アへ、ロ/口、まゆげ/まゆ等）——建议从插件 DLL 的 UTF-16 元数据提取原文

## Repository layout / 仓库结构

```
Shared/ImguiDictPlugin.cs        # 共享源码（四游戏通用）
HC_ImguiDictTranslator/          # HoneyCome 项目
AL_ImguiDictTranslator/          # Amaloca 项目
AC_ImguiDictTranslator/          # Aicomi 项目
SVS_ImguiDictTranslator/         # SamabakeScramble 项目
dist/                            # 已编译 DLL（可直接使用）
```

## License / 许可

MIT

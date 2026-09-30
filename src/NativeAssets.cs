using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace NativeUILib
{
    /// <summary>
    /// 游戏原生素材（Sprite / TMP 字体 / 旧版 Font）的获取与缓存。
    ///
    /// 关键事实：
    /// <list type="bullet">
    /// <item>游戏的九宫格 UI 贴图（uiBlock / uiBlockSmall / uiBlockNano / UICheckMark …）**不在 Resources 目录**，
    /// 它们是场景资产（sharedassets），只能用 <see cref="UnityEngine.Resources.FindObjectsOfTypeAll{T}"/> 按名字找。</item>
    /// <item>原生 TMP 字体 <c>Retro GamingPix</c> 同理，靠扫描场景里已有的 TMP 文本反查。</item>
    /// <item>两种查找都必须在对应场景加载之后（主菜单加载完才有字体）。因此本类不缓存"找不到"的结果，
    /// 且场景切换时会 <see cref="Invalidate"/> 重来。</item>
    /// </list>
    /// </summary>
    public static class NativeAssets
    {
        private static Dictionary<string, Sprite> _index;
        private static readonly Dictionary<string, bool> _missing = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, Sprite> _solids = new Dictionary<int, Sprite>();
        private static readonly Dictionary<Texture2D, Dictionary<string, Texture2D>> _processed =
            new Dictionary<Texture2D, Dictionary<string, Texture2D>>();

        private static TMP_FontAsset _font;
        private static Font _guiFont;
        private static Sprite _white;
        private static bool _warnedFont;
        private static bool _textureTintBroken;

        /// <summary>场景切换 / 需要重新扫描时调用。会清空所有缓存（包括"找不到"的记录）。</summary>
        public static void Invalidate()
        {
            _index = null;
            _missing.Clear();
            _solids.Clear();
            _processed.Clear();
            _font = null;
            _guiFont = null;
            _white = null;
            _warnedFont = false;
            _textureTintBroken = false;
        }

        // ─────────────────────────────────────────────────────────
        // Sprite
        // ─────────────────────────────────────────────────────────

        private static void EnsureIndex()
        {
            if (_index != null) return;
            var dict = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Sprite>();
                for (int i = 0; i < all.Length; i++)
                {
                    var s = all[i];
                    if (s == null) continue;
                    string n = s.name;
                    if (string.IsNullOrEmpty(n)) continue;
                    if (!dict.ContainsKey(n)) dict[n] = s;
                }
            }
            catch (Exception e)
            {
                NativeLog.Warn("构建 Sprite 索引失败：" + e.Message);
            }
            _index = dict;
            NativeLog.Info("已索引 " + dict.Count + " 个 Sprite（来自当前已加载资源）");
        }

        /// <summary>
        /// 按名字找游戏原生 Sprite。先精确匹配 → Resources.Load → 包含匹配（fuzzy）。
        /// 找不到返回 null（不会抛异常）。
        /// </summary>
        public static Sprite Find(string name, bool fuzzy = true)
        {
            if (string.IsNullOrEmpty(name)) return null;
            EnsureIndex();

            Sprite s;
            if (_index.TryGetValue(name, out s) && s != null) return s;

            try
            {
                s = Resources.Load<Sprite>(name);
                if (s == null) s = Resources.Load<Sprite>("Sprites/" + name);
            }
            catch { s = null; }
            if (s != null)
            {
                _index[name] = s;
                return s;
            }

            if (fuzzy)
            {
                foreach (var kv in _index)
                {
                    if (kv.Key.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                        return kv.Value;
                }
            }

            if (!_missing.ContainsKey(name))
            {
                _missing[name] = true;
                NativeLog.Warn("找不到原生 Sprite \"" + name + "\"（可能尚未加载完场景，或该版本已改名）");
            }
            return null;
        }

        public static bool Exists(string name) => Find(name, false) != null;

        private static Sprite FirstOf(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                var s = Find(names[i]);
                if (s != null) return s;
            }
            return null;
        }

        /// <summary>主面板九宫格（uiBlock）。</summary>
        public static Sprite Panel { get { var s = FirstOf("uiBlock", "uiBlockSmall"); return s != null ? s : White; } }

        /// <summary>小面板 / 大按钮（uiBlockSmall）。</summary>
        public static Sprite PanelSmall { get { var s = FirstOf("uiBlockSmall", "uiBlock"); return s != null ? s : White; } }

        /// <summary>最小面板 / 小按钮 / 滑条（uiBlockNano）。</summary>
        public static Sprite PanelNano { get { var s = FirstOf("uiBlockNano", "uiBlockSmall", "uiBlock"); return s != null ? s : White; } }

        /// <summary>勾选标记（UICheckMark）。</summary>
        public static Sprite Check { get { var s = FirstOf("UICheckMark", "UICheckmark"); return s != null ? s : White; } }

        /// <summary>纯白 1px（whitesquare），找不到就程序生成。</summary>
        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                var s = Find("whitesquare", false) ?? Find("white", false);
                _white = s != null ? s : Solid(Color.white);
                return _white;
            }
        }

        public static Sprite Cursor { get { return FirstOf("uicursor", "uicursorpressed"); } }
        public static Sprite Gradient { get { return FirstOf("uigradientblack", "uigradient"); } }
        public static Sprite BarFill { get { var s = FirstOf("userbarfill", "userbar"); return s != null ? s : White; } }

        /// <summary>程序生成一张纯色贴图并包成 Sprite（自带缓存）。</summary>
        public static Sprite Solid(Color color, int size = 4)
        {
            int key = (Mathf.RoundToInt(color.r * 255f) << 24) | (Mathf.RoundToInt(color.g * 255f) << 16)
                      | (Mathf.RoundToInt(color.b * 255f) << 8) | Mathf.RoundToInt(color.a * 255f);
            Sprite cached;
            if (_solids.TryGetValue(key, out cached) && cached != null) return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.hideFlags = HideFlags.HideAndDontSave;
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = color;
            tex.SetPixels(px);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "NativeUILib_Solid";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _solids[key] = sprite;
            return sprite;
        }

        // ─────────────────────────────────────────────────────────
        // 字体
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// 游戏原生 TMP 字体（Retro GamingPix）。取不到时退回 TMP 默认字体。
        /// 需要"像素级原生"时自己判断 <see cref="IsNativeFont"/>。
        /// </summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (_font != null) return _font;

                TMP_FontAsset found = null;
                try
                {
                    var texts = UnityEngine.Object.FindObjectsOfType<TMP_Text>();
                    for (int i = 0; i < texts.Length; i++)
                    {
                        var f = texts[i] != null ? texts[i].font : null;
                        if (f != null && f.name == NativeTheme.NativeFontName) { found = f; break; }
                    }
                    if (found == null)
                    {
                        for (int i = 0; i < texts.Length; i++)
                        {
                            var f = texts[i] != null ? texts[i].font : null;
                            if (f != null) { found = f; break; }
                        }
                    }
                }
                catch (Exception e) { NativeLog.Warn("扫描 TMP 字体失败：" + e.Message); }

                if (found == null)
                {
                    try { found = TMP_Settings.defaultFontAsset; } catch { found = null; }
                }

                if (found == null)
                {
                    if (!_warnedFont)
                    {
                        _warnedFont = true;
                        NativeLog.Warn("未能获取任何 TMP 字体（场景可能还没加载完），本次使用不设置字体");
                    }
                    return null;
                }

                _font = found;
                _warnedFont = false;
                NativeLog.Info("原生 TMP 字体：" + found.name + (found.name == NativeTheme.NativeFontName ? "（命中）" : "（回退）"));
                return _font;
            }
        }

        /// <summary>是否拿到了真正的游戏原生像素字体。</summary>
        public static bool IsNativeFont
        {
            get { var f = Font; return f != null && f.name == NativeTheme.NativeFontName; }
        }

        /// <summary>
        /// 旧版 <see cref="Font"/>（给 IMGUI 用）。从 TMP 字体资产的 sourceFontFile 反查，
        /// 可能为 null（此时 IMGUI 皮肤会沿用 Unity 默认字体）。
        /// </summary>
        public static Font GUIFont
        {
            get
            {
                if (_guiFont != null) return _guiFont;
                var tmp = Font;
                if (tmp == null) return null;
                try { _guiFont = tmp.sourceFontFile; }
                catch (Exception e) { NativeLog.Warn("读取 TMP sourceFontFile 失败：" + e.Message); }
                return _guiFont;
            }
        }

        /// <summary>检查字体是否包含某字符（做回退/替换用）。</summary>
        public static bool HasChar(char c)
        {
            var f = Font;
            if (f == null) return false;
            try { return f.HasCharacter(c, true, false); } catch { return false; }
        }

        // ─────────────────────────────────────────────────────────
        // 字体回退（解决 Retro GamingPix 没有中文字形）
        // ─────────────────────────────────────────────────────────

        private static readonly List<TMP_FontAsset> _registeredFallbacks = new List<TMP_FontAsset>();

        /// <summary>把一个 TMP 字体注册成原生字体的 fallback。</summary>
        public static bool RegisterFallback(TMP_FontAsset fallback)
        {
            if (fallback == null) return false;
            var main = Font;
            if (main == null || main == fallback) return false;
            try
            {
                if (main.fallbackFontAssetTable == null)
                    main.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!main.fallbackFontAssetTable.Contains(fallback))
                    main.fallbackFontAssetTable.Add(fallback);
                if (!_registeredFallbacks.Contains(fallback)) _registeredFallbacks.Add(fallback);
                NativeLog.Info("已注册字体 fallback：" + fallback.name);
                return true;
            }
            catch (Exception e)
            {
                NativeLog.Warn("注册字体 fallback 失败：" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 从**系统已安装字体**创建动态 TMP 字体并注册为 fallback。
        /// Windows 中文环境下默认会依次尝试 微软雅黑 → 黑体 → 宋体 → Arial Unicode MS。
        /// 返回成功注册的字体资产，失败返回 null。
        /// </summary>
        public static TMP_FontAsset RegisterSystemFontFallback(params string[] fontNames)
        {
            if (fontNames == null || fontNames.Length == 0)
                fontNames = new[] { "Microsoft YaHei", "SimHei", "SimSun", "Noto Sans CJK SC", "Arial Unicode MS" };

            for (int i = 0; i < fontNames.Length; i++)
            {
                string name = fontNames[i];
                if (string.IsNullOrEmpty(name)) continue;
                try
                {
                    var osFont = UnityEngine.Font.CreateDynamicFontFromOSFont(name, 32);
                    if (osFont == null) continue;
                    var asset = CreateDynamicFontAsset(osFont, name);
                    if (asset == null) continue;
                    if (RegisterFallback(asset)) return asset;
                }
                catch (Exception e)
                {
                    NativeLog.Warn("尝试系统字体 \"" + name + "\" 失败：" + e.Message);
                }
            }
            NativeLog.Warn("未能注册任何系统中文字体 fallback；中文可能显示为方块。可改用 AddFontFallback(你自己的 TMP_FontAsset)。");
            return null;
        }

        /// <summary>把任意 <see cref="Font"/>（例如你自己 AssetBundle 里的 ttf）包成动态 TMP 字体并注册 fallback。</summary>
        public static TMP_FontAsset RegisterFontFallback(Font font, string assetName = null)
        {
            if (font == null) return null;
            var asset = CreateDynamicFontAsset(font, assetName);
            if (asset == null) return null;
            return RegisterFallback(asset) ? asset : null;
        }

        private static TMP_FontAsset CreateDynamicFontAsset(Font font, string name)
        {
            try
            {
                var asset = TMP_FontAsset.CreateFontAsset(font);
                if (asset == null) return null;
                asset.name = string.IsNullOrEmpty(name) ? "NativeUILib_Dynamic" : "NativeUILib_" + name;
                asset.hideFlags = HideFlags.HideAndDontSave;
                asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                return asset;
            }
            catch (Exception e)
            {
                NativeLog.Warn("创建动态 TMP 字体失败：" + e.Message);
                return null;
            }
        }

        /// <summary>原生字体是否已能显示中文（含 fallback）。</summary>
        public static bool SupportsChinese
        {
            get
            {
                var f = Font;
                if (f == null) return false;
                try { return f.HasCharacter('的', true, false) && f.HasCharacter('伤', true, false); }
                catch { return false; }
            }
        }

        /// <summary>把字体不支持的字符替换成 '?'（避免显示成方块/空白）。</summary>
        public static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var f = Font;
            if (f == null) return input;
            var sb = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '\n' || c == '\r' || c == '\t') { sb.Append(c); continue; }
                try { sb.Append(f.HasCharacter(c, true, false) ? c : '?'); }
                catch { sb.Append(c); }
            }
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────
        // 贴图加工（给 IMGUI 皮肤 / 需要着色的场合）
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// 缩放 + 亮度/透明度着色，结果带缓存。
        /// 若贴图不可读（Unity 常见），会自动退化为"原图"并只警告一次，不会抛异常。
        /// </summary>
        public static Texture2D Process(Texture2D source, float scale = 1f, float brightness = 1f, float alpha = 1f)
        {
            if (source == null) return null;
            bool noScale = Mathf.Abs(scale - 1f) < 0.001f;
            bool noTint = Mathf.Abs(brightness - 1f) < 0.001f && Mathf.Abs(alpha - 1f) < 0.001f;
            if (noScale && noTint) return source;
            if (_textureTintBroken) return source;

            string key = scale.ToString("0.###") + "|" + brightness.ToString("0.###") + "|" + alpha.ToString("0.###");
            Dictionary<string, Texture2D> perTexture;
            if (!_processed.TryGetValue(source, out perTexture))
            {
                perTexture = new Dictionary<string, Texture2D>();
                _processed[source] = perTexture;
            }
            Texture2D cached;
            if (perTexture.TryGetValue(key, out cached) && cached != null) return cached;

            try
            {
                Color[] src = source.GetPixels();
                int w = noScale ? source.width : Mathf.Max(1, Mathf.CeilToInt(source.width * scale));
                int h = noScale ? source.height : Mathf.Max(1, Mathf.CeilToInt(source.height * scale));
                var dst = new Texture2D(w, h, TextureFormat.RGBA32, source.mipmapCount > 1);
                dst.filterMode = FilterMode.Point;
                dst.hideFlags = HideFlags.HideAndDontSave;

                var outPixels = new Color[w * h];
                if (noScale)
                {
                    for (int i = 0; i < src.Length && i < outPixels.Length; i++)
                    {
                        var c = src[i];
                        float a = c.a;
                        c *= brightness;
                        c.a = a * alpha;
                        outPixels[i] = c;
                    }
                }
                else
                {
                    float sx = (float)source.width / w;
                    float sy = (float)source.height / h;
                    for (int y = 0; y < h; y++)
                    {
                        int srcY = Mathf.FloorToInt(y * sy);
                        for (int x = 0; x < w; x++)
                        {
                            int srcX = Mathf.FloorToInt(x * sx);
                            var c = src[srcX + srcY * source.width];
                            float a = c.a;
                            c *= brightness;
                            c.a = a * alpha;
                            outPixels[x + y * w] = c;
                        }
                    }
                }
                dst.SetPixels(outPixels);
                dst.Apply();
                perTexture[key] = dst;
                return dst;
            }
            catch (Exception e)
            {
                _textureTintBroken = true;
                NativeLog.Warn("贴图不可读，已禁用贴图缩放/着色（外观会退化为原图）：" + e.Message);
                return source;
            }
        }

        // ─────────────────────────────────────────────────────────
        // 诊断
        // ─────────────────────────────────────────────────────────

        /// <summary>列出当前索引里所有 Sprite 名字（给 Mod 作者 / AI 探查用）。</summary>
        public static string[] DumpSpriteNames()
        {
            EnsureIndex();
            var names = _index.Keys.ToArray();
            Array.Sort(names, StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>一行式诊断信息，写进日志或返回给调用方。</summary>
        public static string Describe()
        {
            EnsureIndex();
            var f = Font;
            return "NativeUILib 素材状态：Sprite=" + _index.Count
                   + "，字体=" + (f != null ? f.name : "<null>")
                   + "，GUI字体=" + (GUIFont != null ? GUIFont.name : "<null>")
                   + "，贴图着色=" + (_textureTintBroken ? "不可用" : "可用")
                   + "，结果=" + (DescribeProbe());
        }

        private static string DescribeProbe()
        {
            var sb = new StringBuilder();
            string[] probe = { "uiBlock", "uiBlockSmall", "uiBlockNano", "UICheckMark", "whitesquare" };
            for (int i = 0; i < probe.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(probe[i]).Append(Exists(probe[i]) ? "=OK" : "=缺失");
            }
            return sb.ToString();
        }
    }
}

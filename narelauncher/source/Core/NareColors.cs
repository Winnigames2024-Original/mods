using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NareLauncher.Core
{
    /// <summary>
    /// Цветные коды в стиле Minecraft для имён/тегов: «&red:текст», «&blue:текст»,
    /// «&random:текст» (переливается радугой и анимируется). Несколько кодов подряд:
    /// «&red:ВЛАДЕЛЕЦ &blue:Лилия». Неизвестный код выводится как обычный текст.
    /// </summary>
    public static class NareColors
    {
        public struct Seg { public string Text; public Color Color; public bool Rainbow; }

        private static readonly Dictionary<string, Color> Map = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "red", Color.FromArgb(255, 85, 85) },
            { "dark_red", Color.FromArgb(170, 0, 0) },
            { "blue", Color.FromArgb(85, 120, 255) },
            { "dark_blue", Color.FromArgb(40, 60, 200) },
            { "aqua", Color.FromArgb(85, 255, 255) },
            { "cyan", Color.FromArgb(85, 255, 255) },
            { "green", Color.FromArgb(85, 220, 90) },
            { "dark_green", Color.FromArgb(40, 150, 40) },
            { "lime", Color.FromArgb(120, 255, 90) },
            { "yellow", Color.FromArgb(255, 220, 70) },
            { "gold", Color.FromArgb(255, 170, 40) },
            { "orange", Color.FromArgb(255, 140, 40) },
            { "purple", Color.FromArgb(180, 90, 255) },
            { "dark_purple", Color.FromArgb(140, 50, 220) },
            { "magenta", Color.FromArgb(255, 90, 220) },
            { "pink", Color.FromArgb(255, 120, 190) },
            { "white", Color.FromArgb(240, 240, 245) },
            { "gray", Color.FromArgb(160, 165, 175) },
            { "grey", Color.FromArgb(160, 165, 175) },
            { "black", Color.FromArgb(20, 22, 28) },
        };

        /// <summary>Есть ли в строке хоть один цветовой код.</summary>
        public static bool HasCodes(string s)
        {
            return !string.IsNullOrEmpty(s) && s.IndexOf('&') >= 0;
        }

        /// <summary>Есть ли анимированный радужный код (чтобы включить таймер перерисовки).</summary>
        public static bool HasRainbow(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            return s.IndexOf("&random:", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("&rainbow:", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static List<Seg> Parse(string text, Color def)
        {
            var segs = new List<Seg>();
            if (string.IsNullOrEmpty(text)) return segs;

            int i = 0, n = text.Length;
            // текст до первого '&'
            int first = text.IndexOf('&');
            if (first < 0) { segs.Add(new Seg { Text = text, Color = def }); return segs; }
            if (first > 0) segs.Add(new Seg { Text = text.Substring(0, first), Color = def });
            i = first;

            while (i < n)
            {
                // здесь text[i] == '&'
                int colon = text.IndexOf(':', i + 1);
                int nextAmp = text.IndexOf('&', i + 1);
                if (colon < 0 || (nextAmp >= 0 && nextAmp < colon))
                {
                    // нет кода — выводим как обычный текст до следующего '&' (или до конца)
                    int end = nextAmp < 0 ? n : nextAmp;
                    segs.Add(new Seg { Text = text.Substring(i, end - i), Color = def });
                    i = end;
                    continue;
                }
                string code = text.Substring(i + 1, colon - i - 1);
                Color c; bool rainbow = false;
                if (code.Equals("random", StringComparison.OrdinalIgnoreCase) ||
                    code.Equals("rainbow", StringComparison.OrdinalIgnoreCase)) { rainbow = true; c = def; }
                else if (!Map.TryGetValue(code, out c))
                {
                    // неизвестный код — '&code:' как обычный текст до следующего '&'
                    int end = nextAmp < 0 ? n : nextAmp;
                    segs.Add(new Seg { Text = text.Substring(i, end - i), Color = def });
                    i = end;
                    continue;
                }
                int contentEnd = text.IndexOf('&', colon + 1);
                if (contentEnd < 0) contentEnd = n;
                segs.Add(new Seg { Text = text.Substring(colon + 1, contentEnd - colon - 1), Color = c, Rainbow = rainbow });
                i = contentEnd;
            }
            return segs;
        }

        /// <summary>Нарисовать строку с кодами слева направо, по вертикали — по центру rect.</summary>
        public static void Draw(Graphics g, string raw, Font font, Color def, Rectangle rect, int phaseMs)
        {
            var segs = Parse(raw, def);
            const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            int x = rect.X;
            int right = rect.Right;
            foreach (var seg in segs)
            {
                if (string.IsNullOrEmpty(seg.Text)) continue;
                if (seg.Rainbow)
                {
                    for (int k = 0; k < seg.Text.Length && x < right; k++)
                    {
                        string ch = seg.Text[k].ToString();
                        var sz = TextRenderer.MeasureText(g, ch, font, new Size(9999, rect.Height), Flags);
                        int cy = rect.Y + (rect.Height - sz.Height) / 2;
                        float hue = ((phaseMs / 18f) + k * 24f) % 360f;
                        TextRenderer.DrawText(g, ch, font, new Rectangle(x, cy, sz.Width + 2, rect.Height), Hsv(hue), Flags);
                        x += sz.Width;
                    }
                }
                else
                {
                    if (x >= right) break;
                    var sz = TextRenderer.MeasureText(g, seg.Text, font, new Size(Math.Max(1, right - x), rect.Height), Flags);
                    int cy = rect.Y + (rect.Height - sz.Height) / 2;
                    TextRenderer.DrawText(g, seg.Text, font, new Rectangle(x, cy, right - x, rect.Height), seg.Color, Flags);
                    x += sz.Width;
                }
            }
        }

        private static Color Hsv(float h)
        {
            float s = 0.75f, v = 1f;
            float c = v * s;
            float hh = h / 60f;
            float xx = c * (1 - Math.Abs(hh % 2 - 1));
            float r = 0, g = 0, b = 0;
            if (hh < 1) { r = c; g = xx; }
            else if (hh < 2) { r = xx; g = c; }
            else if (hh < 3) { g = c; b = xx; }
            else if (hh < 4) { g = xx; b = c; }
            else if (hh < 5) { r = xx; b = c; }
            else { r = c; b = xx; }
            float mm = v - c;
            return Color.FromArgb((int)((r + mm) * 255), (int)((g + mm) * 255), (int)((b + mm) * 255));
        }
    }
}

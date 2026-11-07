using System;
using System.Collections.Generic;
using System.Drawing;

namespace NareLauncher.Core
{
    /// <summary>Правки целого слоя: шум, яркость, насыщенность, оттенок, отражение.</summary>
    public static class SkinFilters
    {
        private static readonly Random Rng = new Random();

        public static void Noise(Bitmap image, int amount)
        {
            Each(image, c =>
            {
                int d = Rng.Next(-amount, amount + 1);
                return Color.FromArgb(c.A, Clamp(c.R + d), Clamp(c.G + d), Clamp(c.B + d));
            });
        }

        public static void Brightness(Bitmap image, int delta)
        {
            Each(image, c => Color.FromArgb(c.A, Clamp(c.R + delta), Clamp(c.G + delta), Clamp(c.B + delta)));
        }

        public static void Saturation(Bitmap image, float k)
        {
            Each(image, c =>
            {
                float gray = c.R * 0.299f + c.G * 0.587f + c.B * 0.114f;
                return Color.FromArgb(c.A,
                    Clamp((int)(gray + (c.R - gray) * k)),
                    Clamp((int)(gray + (c.G - gray) * k)),
                    Clamp((int)(gray + (c.B - gray) * k)));
            });
        }

        public static void HueShift(Bitmap image, float degrees)
        {
            Each(image, c =>
            {
                float h, s, v;
                ToHsv(c, out h, out s, out v);
                h = (h + degrees) % 360f;
                if (h < 0f) h += 360f;
                return FromHsv(c.A, h, s, v);
            });
        }

        public static void Contrast(Bitmap image, float k)
        {
            Each(image, c => Color.FromArgb(c.A,
                Clamp((int)((c.R - 128) * k + 128)),
                Clamp((int)((c.G - 128) * k + 128)),
                Clamp((int)((c.B - 128) * k + 128))));
        }

        /// <summary>Отражает слой по горизонтали — каждую часть развёртки отдельно.</summary>
        public static void FlipX(Bitmap image, SkinArms arms)
        {
            foreach (var r in SkinLayout.Regions(arms))
            {
                for (int y = r.Y; y < r.Bottom; y++)
                {
                    for (int x = 0; x < r.Width / 2; x++)
                    {
                        int left = r.X + x, right = r.Right - 1 - x;
                        var a = image.GetPixel(left, y);
                        var b = image.GetPixel(right, y);
                        image.SetPixel(left, y, b);
                        image.SetPixel(right, y, a);
                    }
                }
            }
        }

        public static void Clear(Bitmap image)
        {
            Each(image, c => Color.Transparent);
        }

        /// <summary>Самые частые цвета текстуры — готовая палитра по скину.</summary>
        public static List<Color> TopColors(Bitmap image, int max)
        {
            var counts = new Dictionary<int, int>();

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var c = image.GetPixel(x, y);
                    if (c.A < 32) continue;

                    int key = Color.FromArgb(255, c.R, c.G, c.B).ToArgb();
                    int n;
                    counts[key] = counts.TryGetValue(key, out n) ? n + 1 : 1;
                }
            }

            var pairs = new List<KeyValuePair<int, int>>(counts);
            pairs.Sort((a, b) => b.Value.CompareTo(a.Value));

            var result = new List<Color>();
            foreach (var p in pairs)
            {
                result.Add(Color.FromArgb(p.Key));
                if (result.Count >= max) break;
            }
            return result;
        }

        private static void Each(Bitmap image, Func<Color, Color> map)
        {
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    var c = image.GetPixel(x, y);
                    if (c.A == 0) continue;
                    image.SetPixel(x, y, map(c));
                }
        }

        private static int Clamp(int v) { return v < 0 ? 0 : v > 255 ? 255 : v; }

        // ------------------------------------------------------------ цветовая модель

        public static void ToHsv(Color c, out float h, out float s, out float v)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float d = max - min;

            v = max;
            s = max <= 0f ? 0f : d / max;

            if (d <= 0f) { h = 0f; return; }

            if (max == r) h = 60f * (((g - b) / d) % 6f);
            else if (max == g) h = 60f * ((b - r) / d + 2f);
            else h = 60f * ((r - g) / d + 4f);

            if (h < 0f) h += 360f;
        }

        public static Color FromHsv(int alpha, float h, float s, float v)
        {
            float c = v * s;
            float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
            float m = v - c;

            float r, g, b;
            if (h < 60f) { r = c; g = x; b = 0f; }
            else if (h < 120f) { r = x; g = c; b = 0f; }
            else if (h < 180f) { r = 0f; g = c; b = x; }
            else if (h < 240f) { r = 0f; g = x; b = c; }
            else if (h < 300f) { r = x; g = 0f; b = c; }
            else { r = c; g = 0f; b = x; }

            return Color.FromArgb(alpha,
                Clamp((int)((r + m) * 255f)),
                Clamp((int)((g + m) * 255f)),
                Clamp((int)((b + m) * 255f)));
        }
    }
}

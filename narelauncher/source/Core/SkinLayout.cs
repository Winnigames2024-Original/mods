using System.Collections.Generic;
using System.Drawing;

namespace NareLauncher.Core
{
    /// <summary>
    /// Разметка скина: какие прямоугольники текстуры на какие грани модели идут.
    /// Используется и редактором (подсветка областей), и трёхмерным просмотром.
    /// </summary>
    public static class SkinLayout
    {
        // порядок граней: 0 верх, 1 низ, 2 правый бок (-X), 3 перёд (-Z), 4 левый бок (+X), 5 зад (+Z)
        public const int Up = 0, Down = 1, Right = 2, Front = 3, Left = 4, Back = 5;

        /// <summary>Шесть прямоугольников для коробки W×H×D с левым верхним углом развёртки (u,v).</summary>
        public static Rectangle[] BoxUv(int u, int v, int w, int h, int d)
        {
            return new[]
            {
                new Rectangle(u + d,         v,     w, d),   // верх
                new Rectangle(u + d + w,     v,     w, d),   // низ
                new Rectangle(u,             v + d, d, h),   // правый бок
                new Rectangle(u + d,         v + d, w, h),   // перёд
                new Rectangle(u + d + w,     v + d, d, h),   // левый бок
                new Rectangle(u + d + w + d, v + d, w, h)    // зад
            };
        }

        public class Part
        {
            public string Title;
            public float Cx, Cy, Cz;     // центр коробки в единицах модели
            public float W, H, D;        // размеры
            public Rectangle[] Uv;
            public bool Overlay;         // второй слой: шляпа, куртка, рукава
            public float Inflate;        // насколько второй слой больше основного
        }

        /// <summary>Модель игрока: восемь коробок основы и восемь коробок второго слоя.</summary>
        public static List<Part> Model(SkinArms arms)
        {
            int a = arms == SkinArms.Slim ? 3 : 4;        // ширина руки
            float ax = arms == SkinArms.Slim ? 5.5f : 6f; // центр руки по X

            var parts = new List<Part>
            {
                Make("Голова",      0,   28, 0, 8,  8,  8, BoxUv(0,  0,  8, 8,  8), false, 0),
                Make("Туловище",    0,   18, 0, 8, 12,  4, BoxUv(16, 16, 8, 12, 4), false, 0),
                Make("Правая рука", -ax, 18, 0, a, 12,  4, BoxUv(40, 16, a, 12, 4), false, 0),
                Make("Левая рука",   ax, 18, 0, a, 12,  4, BoxUv(32, 48, a, 12, 4), false, 0),
                Make("Правая нога", -2,   6, 0, 4, 12,  4, BoxUv(0,  16, 4, 12, 4), false, 0),
                Make("Левая нога",   2,   6, 0, 4, 12,  4, BoxUv(16, 48, 4, 12, 4), false, 0),

                Make("Шляпа",       0,   28, 0, 8,  8,  8, BoxUv(32, 0,  8, 8,  8), true, 0.5f),
                Make("Куртка",      0,   18, 0, 8, 12,  4, BoxUv(16, 32, 8, 12, 4), true, 0.25f),
                Make("Правый рукав",-ax, 18, 0, a, 12,  4, BoxUv(40, 32, a, 12, 4), true, 0.25f),
                Make("Левый рукав",  ax, 18, 0, a, 12,  4, BoxUv(48, 48, a, 12, 4), true, 0.25f),
                Make("Правая штанина", -2, 6, 0, 4, 12, 4, BoxUv(0,  32, 4, 12, 4), true, 0.25f),
                Make("Левая штанина",   2, 6, 0, 4, 12, 4, BoxUv(0,  48, 4, 12, 4), true, 0.25f)
            };

            return parts;
        }

        private static Part Make(string title, float cx, float cy, float cz,
                                 float w, float h, float d, Rectangle[] uv, bool overlay, float inflate)
        {
            return new Part
            {
                Title = title, Cx = cx, Cy = cy, Cz = cz,
                W = w, H = h, D = d, Uv = uv, Overlay = overlay, Inflate = inflate
            };
        }

        /// <summary>Все занятые области развёртки — для сетки в редакторе.</summary>
        public static List<Rectangle> Regions(SkinArms arms)
        {
            var list = new List<Rectangle>();
            foreach (var part in Model(arms))
                foreach (var r in part.Uv)
                    if (r.Width > 0 && r.Height > 0) list.Add(r);
            return list;
        }

        /// <summary>Крупные блоки развёртки с подписями — чтобы было понятно, где что.</summary>
        public static List<KeyValuePair<Rectangle, string>> Groups(SkinArms arms)
        {
            int a = arms == SkinArms.Slim ? 3 : 4;
            var list = new List<KeyValuePair<Rectangle, string>>
            {
                new KeyValuePair<Rectangle, string>(new Rectangle(0, 0, 32, 16), "Голова"),
                new KeyValuePair<Rectangle, string>(new Rectangle(32, 0, 32, 16), "Шляпа"),
                new KeyValuePair<Rectangle, string>(new Rectangle(16, 16, 24, 16), "Туловище"),
                new KeyValuePair<Rectangle, string>(new Rectangle(40, 16, 8 + a * 2, 16), "Правая рука"),
                new KeyValuePair<Rectangle, string>(new Rectangle(0, 16, 16, 16), "Правая нога"),
                new KeyValuePair<Rectangle, string>(new Rectangle(16, 32, 24, 16), "Куртка"),
                new KeyValuePair<Rectangle, string>(new Rectangle(40, 32, 8 + a * 2, 16), "Правый рукав"),
                new KeyValuePair<Rectangle, string>(new Rectangle(0, 32, 16, 16), "Правая штанина"),
                new KeyValuePair<Rectangle, string>(new Rectangle(32, 48, 8 + a * 2, 16), "Левая рука"),
                new KeyValuePair<Rectangle, string>(new Rectangle(48, 48, 8 + a * 2, 16), "Левый рукав"),
                new KeyValuePair<Rectangle, string>(new Rectangle(16, 48, 16, 16), "Левая нога"),
                new KeyValuePair<Rectangle, string>(new Rectangle(0, 48, 16, 16), "Левая штанина")
            };
            return list;
        }
    }
}

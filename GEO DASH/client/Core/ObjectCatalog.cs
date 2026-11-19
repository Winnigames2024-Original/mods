namespace GEO_DASH.Core
{
    public enum DrawStyle
    {
        Block, Slab, Platform, Brick, Glass, SlopeUp, SlopeDown,
        Spike, SpikeSmall, SpikeHalf, Saw, SawBig, SpikeRow, HazardBlock,
        Orb, Pad, Portal, Deco, DecoCircle, DecoTri, Glow, Text,
        Coin, Pickup, Finish, Checkpoint, Trigger, Teleport, Water, Lava, Image
    }

    public class ObjectDef
    {
        public int Id;
        public string Name = "";
        public string Category = "Блоки";
        public ObjKind Kind = ObjKind.Block;
        public DrawStyle Style = DrawStyle.Block;
        public double W = 30, H = 30;
        /// <summary>Хитбокс внутри клетки (в единицах, от левого-нижнего угла объекта).</summary>
        public double Hx = 0, Hy = 0, Hw = 30, Hh = 30;
        public bool Solid;
        public bool Deadly;
        /// <summary>Сквозь объект можно пройти в режиме «червяк» (обычные блоки — да, шипы — нет).</summary>
        public bool WormPhase;
        public bool TopOnly;       // платформа: стоять можно только сверху
        public bool Invisible;     // триггеры и служебные объекты не рисуются в игре

        public ObjectDef() { }
    }

    public static class ObjectCatalog
    {
        public static readonly Dictionary<int, ObjectDef> All = new();

        static void Add(ObjectDef d) => All[d.Id] = d;

        static ObjectCatalog()
        {
            // ---------- блоки ----------
            Add(new ObjectDef { Id = 1, Name = "Блок", Category = "Блоки", Kind = ObjKind.Block, Style = DrawStyle.Block, Solid = true, WormPhase = true });
            Add(new ObjectDef { Id = 2, Name = "Плита (низ)", Category = "Блоки", Kind = ObjKind.Block, Style = DrawStyle.Slab, H = 15, Hh = 15, Solid = true, WormPhase = true });
            Add(new ObjectDef { Id = 3, Name = "Плита (верх)", Category = "Блоки", Kind = ObjKind.Block, Style = DrawStyle.Slab, H = 15, Hy = 15, Hh = 15, Solid = true, WormPhase = true });
            Add(new ObjectDef { Id = 4, Name = "Тонкая плита", Category = "Блоки", Kind = ObjKind.Block, Style = DrawStyle.Slab, H = 8, Hh = 8, Solid = true, WormPhase = true });
            Add(new ObjectDef { Id = 5, Name = "Склон вверх", Category = "Блоки", Kind = ObjKind.Slope, Style = DrawStyle.SlopeUp, Solid = true, WormPhase = true });
            Add(new ObjectDef { Id = 6, Name = "Склон вниз", Category = "Блоки", Kind = ObjKind.Slope, Style = DrawStyle.SlopeDown, Solid = true, WormPhase = true });
            Add(new ObjectDef { Id = 7, Name = "Платформа", Category = "Блоки", Kind = ObjKind.Block, Style = DrawStyle.Platform, H = 10, Hh = 10, Solid = true, TopOnly = true, WormPhase = true });
            Add(new ObjectDef { Id = 8, Name = "Кирпич", Category = "Блоки", Kind = ObjKind.Block, Style = DrawStyle.Brick, Solid = true, WormPhase = true });
            Add(new ObjectDef { Id = 9, Name = "Стекло", Category = "Блоки", Kind = ObjKind.Block, Style = DrawStyle.Glass, Solid = true, WormPhase = true });

            // ---------- шипы и пилы ----------
            Add(new ObjectDef { Id = 20, Name = "Шип", Category = "Шипы", Kind = ObjKind.Hazard, Style = DrawStyle.Spike, Deadly = true, Hx = 11, Hy = 2, Hw = 8, Hh = 16 });
            Add(new ObjectDef { Id = 21, Name = "Малый шип", Category = "Шипы", Kind = ObjKind.Hazard, Style = DrawStyle.SpikeSmall, Deadly = true, H = 15, Hx = 12, Hy = 2, Hw = 6, Hh = 8 });
            Add(new ObjectDef { Id = 22, Name = "Шип с потолка", Category = "Шипы", Kind = ObjKind.Hazard, Style = DrawStyle.Spike, Deadly = true, Hx = 11, Hy = 12, Hw = 8, Hh = 16 });
            Add(new ObjectDef { Id = 23, Name = "Пила", Category = "Шипы", Kind = ObjKind.Hazard, Style = DrawStyle.Saw, Deadly = true, Hx = 5, Hy = 5, Hw = 20, Hh = 20 });
            Add(new ObjectDef { Id = 24, Name = "Большая пила", Category = "Шипы", Kind = ObjKind.Hazard, Style = DrawStyle.SawBig, Deadly = true, W = 60, H = 60, Hx = 10, Hy = 10, Hw = 40, Hh = 40 });
            Add(new ObjectDef { Id = 25, Name = "Три шипа", Category = "Шипы", Kind = ObjKind.Hazard, Style = DrawStyle.SpikeRow, Deadly = true, W = 90, Hx = 6, Hy = 2, Hw = 78, Hh = 14 });
            Add(new ObjectDef { Id = 26, Name = "Опасный блок", Category = "Шипы", Kind = ObjKind.Hazard, Style = DrawStyle.HazardBlock, Deadly = true, Solid = false });
            Add(new ObjectDef { Id = 27, Name = "Половина шипа", Category = "Шипы", Kind = ObjKind.Hazard, Style = DrawStyle.SpikeHalf, Deadly = true, H = 15, Hx = 10, Hy = 1, Hw = 10, Hh = 9 });

            // ---------- шары (орбы) ----------
            Add(new ObjectDef { Id = 40, Name = "Шар (орб)", Category = "Шары и пады", Kind = ObjKind.Orb, Style = DrawStyle.Orb, Hx = -3, Hy = -3, Hw = 36, Hh = 36 });

            // ---------- пады ----------
            Add(new ObjectDef { Id = 50, Name = "Пад", Category = "Шары и пады", Kind = ObjKind.Pad, Style = DrawStyle.Pad, H = 10, Hy = 0, Hh = 10 });

            // ---------- порталы ----------
            Add(new ObjectDef { Id = 60, Name = "Портал", Category = "Порталы", Kind = ObjKind.Portal, Style = DrawStyle.Portal, W = 30, H = 90, Hx = 0, Hy = 0, Hw = 30, Hh = 90 });
            Add(new ObjectDef { Id = 61, Name = "Телепорт", Category = "Порталы", Kind = ObjKind.Portal, Style = DrawStyle.Teleport, W = 30, H = 90, Hw = 30, Hh = 90 });

            // ---------- декор ----------
            Add(new ObjectDef { Id = 70, Name = "Квадрат (декор)", Category = "Декор", Kind = ObjKind.Deco, Style = DrawStyle.Deco });
            Add(new ObjectDef { Id = 71, Name = "Круг (декор)", Category = "Декор", Kind = ObjKind.Deco, Style = DrawStyle.DecoCircle });
            Add(new ObjectDef { Id = 72, Name = "Треугольник (декор)", Category = "Декор", Kind = ObjKind.Deco, Style = DrawStyle.DecoTri });
            Add(new ObjectDef { Id = 73, Name = "Свечение", Category = "Декор", Kind = ObjKind.Deco, Style = DrawStyle.Glow, W = 60, H = 60 });
            Add(new ObjectDef { Id = 75, Name = "Текст", Category = "Декор", Kind = ObjKind.Text, Style = DrawStyle.Text, W = 120, H = 30 });

            // ---------- сбор ----------
            Add(new ObjectDef { Id = 80, Name = "Секретная монета", Category = "Сбор", Kind = ObjKind.Coin, Style = DrawStyle.Coin, Hx = -2, Hy = -2, Hw = 34, Hh = 34 });
            Add(new ObjectDef { Id = 82, Name = "Пикап (предмет)", Category = "Сбор", Kind = ObjKind.Coin, Style = DrawStyle.Pickup, Hx = -2, Hy = -2, Hw = 34, Hh = 34 });

            // ---------- служебное ----------
            // ---------- картинки с сайта ----------
            Add(new ObjectDef { Id = 98, Name = "Картинка", Category = "Картинки", Kind = ObjKind.Deco,
                Style = DrawStyle.Image, W = 120, H = 120, Hw = 120, Hh = 120 });

            // ---------- жидкости ----------
            Add(new ObjectDef { Id = 94, Name = "Вода", Category = "Жидкости", Kind = ObjKind.Liquid, Style = DrawStyle.Water,
                W = 30, H = 30, Hw = 30, Hh = 30, WormPhase = true });
            Add(new ObjectDef { Id = 95, Name = "Вода (широкая)", Category = "Жидкости", Kind = ObjKind.Liquid, Style = DrawStyle.Water,
                W = 300, H = 120, Hw = 300, Hh = 120, WormPhase = true });
            Add(new ObjectDef { Id = 96, Name = "Лава", Category = "Жидкости", Kind = ObjKind.Liquid, Style = DrawStyle.Lava,
                W = 30, H = 30, Hw = 30, Hh = 30, WormPhase = true });
            Add(new ObjectDef { Id = 97, Name = "Лава (широкая)", Category = "Жидкости", Kind = ObjKind.Liquid, Style = DrawStyle.Lava,
                W = 300, H = 120, Hw = 300, Hh = 120, WormPhase = true });

            Add(new ObjectDef { Id = 90, Name = "Финиш", Category = "Служебное", Kind = ObjKind.Finish, Style = DrawStyle.Finish, W = 30, H = 300, Hw = 30, Hh = 300 });
            Add(new ObjectDef { Id = 92, Name = "Чекпоинт", Category = "Служебное", Kind = ObjKind.Deco, Style = DrawStyle.Checkpoint, W = 30, H = 120, Hw = 30, Hh = 120 });

            // ---------- триггеры ----------
            Add(new ObjectDef { Id = 100, Name = "Триггер", Category = "Триггеры", Kind = ObjKind.Trigger, Style = DrawStyle.Trigger, Invisible = true, Hw = 30, Hh = 30 });
        }

        public static ObjectDef Get(int id) => All.TryGetValue(id, out var d) ? d : All[1];

        public static IEnumerable<string> Categories()
        {
            var seen = new List<string>();
            foreach (var d in All.Values) if (!seen.Contains(d.Category)) seen.Add(d.Category);
            return seen;
        }

        public static RectangleF Hitbox(LevelObject o)
        {
            var d = Get(o.TypeId);
            double w = d.Hw * Math.Abs(o.ScaleX), h = d.Hh * Math.Abs(o.ScaleY);
            double x = o.X + d.Hx * o.ScaleX, y = o.Y + d.Hy * o.ScaleY;
            return new RectangleF((float)x, (float)y, (float)w, (float)h);
        }

        public static RectangleF Bounds(LevelObject o)
        {
            var d = Get(o.TypeId);
            return new RectangleF((float)o.X, (float)o.Y, (float)(d.W * o.ScaleX), (float)(d.H * o.ScaleY));
        }
    }
}

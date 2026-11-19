namespace GEO_DASH.Core
{
    /// <summary>Основные уровни игры: Easy / Hard / Insane. Генерируются кодом и лежат в assets/official.</summary>
    public static class OfficialLevels
    {
        public class Builder
        {
            public LevelData L = new();
            public double X;              // текущая позиция в клетках

            public Builder(string name, Difficulty d, int songId, string songName)
            {
                L.Settings.Name = name;
                L.Settings.Author = AppConfig.Brand;
                L.Settings.Difficulty = d;
                L.Settings.SongId = songId;
                L.Settings.SongName = songName;
                L.Settings.SongArtist = AppConfig.Brand;
                L.Settings.LocalId = "official_" + Math.Abs(songId);
                L.Settings.Password = "";
            }

            public void Obj(int type, double gx, double gy, Action<LevelObject> cfg = null)
            {
                var o = new LevelObject { TypeId = type, X = gx * 30, Y = gy * 30 };
                cfg?.Invoke(o);
                L.Add(o);
            }

            public void Block(double gx, double gy, int n = 1, int type = 1)
            {
                for (int i = 0; i < n; i++) Obj(type, gx + i, gy);
            }
            public void Column(double gx, double gy, int n, int type = 1)
            {
                for (int i = 0; i < n; i++) Obj(type, gx, gy + i);
            }
            public void Spike(double gx, double gy = 0) => Obj(20, gx, gy);
            public void SmallSpike(double gx, double gy = 0) => Obj(21, gx, gy);
            public void CeilSpike(double gx, double gy) => Obj(22, gx, gy);
            public void Saw(double gx, double gy) => Obj(23, gx, gy);
            public void Orb(double gx, double gy, OrbKind k) => Obj(40, gx, gy, o => o.Orb = k);
            public void Pad(double gx, double gy, PadKind k) => Obj(50, gx, gy, o => o.Pad = k);
            public void Portal(double gx, double gy, PortalKind k) => Obj(60, gx, gy, o => o.Portal = k);
            public void Coin(double gx, double gy, int idx) => Obj(80, gx, gy, o => o.CoinIndex = idx);
            public void Deco(double gx, double gy, int type = 70, double sx = 1, double sy = 1, int ch = 2)
                => Obj(type, gx, gy, o => { o.ScaleX = sx; o.ScaleY = sy; o.BaseColorChannel = ch; o.Opacity = 0.5; });
            public void Text(double gx, double gy, string t)
                => Obj(75, gx, gy, o => { o.Text = t; o.BaseColorChannel = 2; });
            public void Finish(double gx) => Obj(90, gx, 0);

            /// <summary>Коридор для корабля/волны: пол и потолок.</summary>
            public void Corridor(double gx, int len, int floorY, int ceilY)
            {
                for (int i = 0; i < len; i++)
                {
                    Obj(1, gx + i, floorY);
                    Obj(1, gx + i, ceilY);
                }
            }

            public LevelData Build()
            {
                L.Settings.LengthUnits = L.RightEdge();
                return L;
            }
        }

        public static LevelData Easy()
        {
            var b = new Builder("Neon Start", Difficulty.Easy, -1, "Neon Start");
            b.L.Settings.BgColor = unchecked((int)0xFF2A2060);
            b.L.Settings.GroundColor = unchecked((int)0xFF151038);
            b.L.Settings.StartSpeed = SpeedTier.Normal;
            b.Text(6, 4, "GEO DASH");

            // 1. знакомство: одиночные шипы
            for (int i = 0; i < 5; i++) b.Spike(16 + i * 7);
            b.Deco(14, 5, 71, 2, 2);

            // 2. ступеньки
            b.Block(56, 0, 4); b.Block(62, 1, 4); b.Block(68, 2, 4);
            b.Spike(60); b.Spike(66, 1);
            b.Coin(70, 5, 1);

            // 3. шары
            b.Orb(80, 3, OrbKind.Yellow);
            b.Spike(82); b.Spike(83);
            b.Orb(88, 4, OrbKind.Yellow);
            b.Block(92, 0, 6);
            b.Pad(98, 1, PadKind.Yellow);
            b.Spike(102); b.Spike(103);

            // 4. корабль
            b.Portal(110, 1, PortalKind.ModeShip);
            b.Corridor(112, 40, -1, 9);
            for (int i = 0; i < 6; i++) { b.Obj(1, 118 + i * 6, 2 + (i % 2) * 3); }
            b.Coin(134, 7, 2);
            b.Portal(154, 1, PortalKind.ModeCube);

            // 5. скорость 2x и пилы
            b.Portal(160, 1, PortalKind.SpeedFast);
            for (int i = 0; i < 4; i++) { b.Spike(166 + i * 9); b.Saw(170 + i * 9, 3); }
            b.Block(204, 0, 8);
            b.Orb(210, 4, OrbKind.Pink);

            // 6. шар
            b.Portal(216, 1, PortalKind.ModeBall);
            for (int i = 0; i < 14; i++) b.Obj(1, 218 + i, 6);
            b.Spike(224); b.CeilSpike(228, 5);
            b.Coin(236, 3, 3);
            b.Portal(236, 1, PortalKind.ModeCube);

            // финал
            b.Portal(242, 1, PortalKind.SpeedNormal);
            for (int i = 0; i < 3; i++) b.Spike(248 + i * 8);
            b.Text(268, 5, "GG!");
            b.Finish(278);
            return b.Build();
        }

        public static LevelData Hard()
        {
            var b = new Builder("Static Rush", Difficulty.Hard, -2, "Static Rush");
            b.L.Settings.BgColor = unchecked((int)0xFF35105A);
            b.L.Settings.GroundColor = unchecked((int)0xFF1B0836);
            b.L.Settings.StartSpeed = SpeedTier.Fast;

            // двойные шипы и короткие площадки
            for (int i = 0; i < 6; i++) { b.Spike(14 + i * 10); b.Spike(15 + i * 10); }
            b.Block(80, 1, 3); b.Block(88, 2, 3); b.Block(96, 3, 3);
            b.Spike(84); b.Spike(92, 1);
            b.Coin(98, 7, 1);

            // мини-куб
            b.Portal(106, 1, PortalKind.Mini);
            for (int i = 0; i < 5; i++) { b.Spike(112 + i * 6); b.SmallSpike(114 + i * 6); }
            b.Portal(144, 1, PortalKind.Big);

            // НЛО
            b.Portal(150, 1, PortalKind.ModeUfo);
            b.Corridor(152, 46, -1, 11);
            for (int i = 0; i < 9; i++) b.Obj(1, 158 + i * 5, 1 + (i * 3) % 7);
            b.Coin(180, 9, 2);
            b.Portal(200, 1, PortalKind.ModeCube);

            // волна (широкая)
            b.Portal(208, 1, PortalKind.ModeWave);
            b.Portal(208, 4, PortalKind.SpeedFaster);
            b.Corridor(210, 44, -1, 10);
            for (int i = 0; i < 10; i++)
            {
                b.Obj(1, 216 + i * 4, i % 2 == 0 ? 0 : 8);
                b.Obj(1, 216 + i * 4, i % 2 == 0 ? 1 : 9);
            }
            b.Portal(256, 1, PortalKind.ModeCube);
            b.Portal(256, 4, PortalKind.SpeedFast);

            // паук
            b.Portal(264, 1, PortalKind.ModeSpider);
            b.Corridor(266, 40, -1, 9);
            for (int i = 0; i < 8; i++) b.Obj(20, 272 + i * 5, (i % 2 == 0) ? 0 : 8);
            b.Coin(294, 4, 3);
            b.Portal(308, 1, PortalKind.ModeCube);

            // робот и финал
            b.Portal(316, 1, PortalKind.ModeRobot);
            b.Block(322, 0, 5); b.Block(330, 2, 5); b.Block(340, 4, 5);
            b.Spike(328); b.Spike(338, 2);
            b.Orb(348, 7, OrbKind.Red);
            for (int i = 0; i < 4; i++) b.Spike(356 + i * 7);
            b.Finish(392);
            return b.Build();
        }

        public static LevelData Insane()
        {
            var b = new Builder("Hyperdrive", Difficulty.Insane, -3, "Hyperdrive");
            b.L.Settings.BgColor = unchecked((int)0xFF4A0A2E);
            b.L.Settings.GroundColor = unchecked((int)0xFF220518);
            b.L.Settings.StartSpeed = SpeedTier.Faster;

            // плотный куб
            for (int i = 0; i < 8; i++) { b.Spike(12 + i * 8); b.Spike(13 + i * 8); b.SmallSpike(16 + i * 8); }
            b.Orb(78, 3, OrbKind.Yellow); b.Spike(80); b.Spike(81); b.Spike(82);
            b.Orb(86, 5, OrbKind.Pink);
            b.Coin(92, 8, 1);

            // 5x и корабль с пилами
            b.Portal(96, 1, PortalKind.SpeedHyper);
            b.Portal(100, 1, PortalKind.ModeShip);
            b.Corridor(102, 56, -1, 10);
            for (int i = 0; i < 12; i++)
            {
                b.Obj(23, 108 + i * 4, 2 + (i * 5) % 6);
                if (i % 3 == 0) b.Obj(1, 110 + i * 4, 0);
            }
            b.Portal(160, 1, PortalKind.ModeCube);

            // червяк — новый режим: сквозь блоки, клик меняет гравитацию
            b.Portal(168, 1, PortalKind.ModeWorm);
            b.Text(168, 9, "WORM");
            for (int i = 0; i < 60; i++) { b.Obj(1, 172 + i, 0); b.Obj(1, 172 + i, 7); }
            for (int i = 0; i < 24; i++) b.Obj(1, 176 + i * 2, 1);     // «грунт», сквозь который ползём
            for (int i = 0; i < 10; i++) b.Obj(20, 180 + i * 5, 2);
            for (int i = 0; i < 8; i++) b.Obj(22, 184 + i * 6, 5);
            b.Coin(210, 4, 2);
            b.Portal(234, 1, PortalKind.ModeCube);

            // дуал + 6x
            b.Portal(242, 1, PortalKind.Dual);
            b.Portal(246, 1, PortalKind.SpeedUltra);
            b.Corridor(248, 40, -1, 14);
            for (int i = 0; i < 10; i++)
            {
                b.Obj(20, 252 + i * 4, 0);
                b.Obj(22, 254 + i * 4, 13);
            }
            b.Portal(290, 1, PortalKind.Single);
            b.Portal(290, 4, PortalKind.SpeedFastest);

            // мини-волна
            b.Portal(296, 1, PortalKind.ModeWave);
            b.Portal(296, 4, PortalKind.Mini);
            b.Corridor(298, 48, -1, 8);
            for (int i = 0; i < 16; i++)
            {
                b.Obj(1, 302 + i * 3, i % 2 == 0 ? 0 : 6);
                b.Obj(1, 302 + i * 3, i % 2 == 0 ? 1 : 7);
            }
            b.Coin(330, 3, 3);
            b.Portal(348, 1, PortalKind.ModeCube);
            b.Portal(348, 4, PortalKind.Big);

            // финальный рывок
            b.Portal(354, 1, PortalKind.SpeedHyper);
            for (int i = 0; i < 7; i++) { b.Spike(360 + i * 6); b.Spike(361 + i * 6); }
            b.Orb(404, 3, OrbKind.Red);
            b.Text(412, 6, "HYPERDRIVE");
            b.Finish(424);
            return b.Build();
        }

        public static LevelData[] All() => new[] { Easy(), Hard(), Insane() };

        /// <summary>Создаёт файлы основных уровней в assets/official, если их нет.</summary>
        public static List<LevelData> EnsureFiles()
        {
            var res = new List<LevelData>();
            foreach (var lv in All())
            {
                var path = Path.Combine(AppConfig.OfficialDir, lv.Settings.LocalId + ".gdl");
                try
                {
                    if (!File.Exists(path)) lv.Save(path);
                    res.Add(LevelData.Load(path));
                }
                catch { res.Add(lv); }
            }
            return res;
        }
    }
}

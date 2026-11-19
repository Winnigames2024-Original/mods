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
                L.Settings.PlatformerFreeMove = false;   // основные уровни — обычный режим, прыжок пробелом и мышью
                L.Settings.BgId = 1 + (Math.Abs(songId) * 6) % 20;
                L.Settings.GroundId = 1 + (Math.Abs(songId) * 3) % 10;
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


        /// <summary>
        /// «Око» — наш собственный демон-уровень в духе глазастых уровней игры:
        /// узкие коридоры, зрачки-пилы и много смены режимов.
        /// </summary>
        public static LevelData Eye()
        {
            var b = new Builder("Oko", Difficulty.HardDemon, -4, "Violet Sky");
            b.L.Settings.BgColor = unchecked((int)0xFF101A52);
            b.L.Settings.GroundColor = unchecked((int)0xFF070B28);
            b.L.Settings.StartSpeed = SpeedTier.Fast;
            b.L.Settings.Description = "Смотри в оба: зрачки следят за тобой.";

            b.Text(6, 6, "OKO");

            // 1. разминка: шипы парами и ступеньки
            for (int i = 0; i < 6; i++) { b.Spike(16 + i * 9); b.SmallSpike(18 + i * 9); }
            b.Block(72, 0, 4); b.Block(78, 2, 4); b.Block(84, 4, 4);
            b.Spike(76); b.Spike(82, 2);
            b.Coin(88, 8, 1);

            // 2. первый «глаз»: кольцо из блоков с пилой-зрачком
            void Eye(double gx, int cy2, int rad)
            {
                for (int a = 0; a < 16; a++)
                {
                    double ang = a * Math.PI * 2 / 16;
                    b.Obj(1, Math.Round(gx + Math.Cos(ang) * rad), Math.Round(cy2 + Math.Sin(ang) * rad));
                }
                b.Saw(gx, cy2);
            }

            b.Portal(96, 1, PortalKind.ModeShip);
            b.Corridor(98, 52, -1, 13);
            Eye(112, 6, 3);
            Eye(132, 8, 3);
            Eye(152, 5, 3);
            b.Coin(142, 11, 2);
            b.Portal(152, 1, PortalKind.ModeCube);

            // 3. мини-волна между зрачками
            b.Portal(160, 1, PortalKind.ModeWave);
            b.Portal(160, 4, PortalKind.Mini);
            b.Corridor(162, 46, -1, 9);
            for (int i = 0; i < 14; i++)
            {
                b.Obj(1, 166 + i * 3, i % 2 == 0 ? 0 : 7);
                b.Obj(1, 166 + i * 3, i % 2 == 0 ? 1 : 8);
            }
            b.Portal(208, 1, PortalKind.ModeCube);
            b.Portal(208, 4, PortalKind.Big);

            // 4. паук по стенам, зрачки сверху и снизу
            b.Portal(216, 1, PortalKind.ModeSpider);
            b.Corridor(218, 44, -1, 10);
            for (int i = 0; i < 9; i++) b.Obj(20, 224 + i * 5, i % 2 == 0 ? 0 : 9);
            for (int i = 0; i < 4; i++) b.Saw(230 + i * 11, 5);
            b.Coin(252, 5, 3);
            b.Portal(262, 1, PortalKind.ModeCube);

            // 5. НЛО сквозь «ресницы»
            b.Portal(270, 1, PortalKind.ModeUfo);
            b.Corridor(272, 42, -1, 12);
            for (int i = 0; i < 10; i++)
            {
                b.Column(278 + i * 4, 1, 3);
                b.Column(280 + i * 4, 8, 3);
            }
            b.Portal(314, 1, PortalKind.ModeCube);

            // 6. финальный рывок с шарами
            b.Portal(322, 1, PortalKind.SpeedFaster);
            b.Orb(330, 4, OrbKind.Yellow);
            b.Spike(332); b.Spike(333);
            b.Orb(340, 5, OrbKind.Pink);
            b.Orb(348, 6, OrbKind.Red);
            for (int i = 0; i < 6; i++) { b.Spike(356 + i * 7); b.CeilSpike(359 + i * 7, 6); }
            b.Text(400, 7, "OKO");
            b.Finish(406);
            return b.Build();
        }


        /// <summary>
        /// «Eye Spy» — большой демон про глаза: кольца-зрачки, узкие проходы,
        /// постоянная смена режимов и финальный рывок. Уровень наш собственный.
        /// </summary>
        public static LevelData EyeSpy()
        {
            var b = new Builder("Eye Spy", Difficulty.InsaneDemon, -5, "Afterburner");
            b.L.Settings.BgColor = unchecked((int)0xFF0B1038);
            b.L.Settings.GroundColor = unchecked((int)0xFF050722);
            b.L.Settings.StartSpeed = SpeedTier.Faster;
            b.L.Settings.Description = "Глаза повсюду. Смотри в оба и не моргай.";

            // глаз: кольцо из блоков, пила вместо зрачка и шипы-ресницы
            void Eye(double gx, int cy, int rad, bool lashes = true)
            {
                for (int a = 0; a < 20; a++)
                {
                    double ang = a * Math.PI * 2 / 20;
                    b.Obj(1, Math.Round(gx + Math.Cos(ang) * rad), Math.Round(cy + Math.Sin(ang) * rad));
                }
                b.Saw(gx, cy);
                if (!lashes) return;
                b.Obj(20, gx - 1, cy + rad + 1);
                b.Obj(20, gx + 1, cy + rad + 1);
            }

            b.Text(6, 7, "EYE SPY");

            // 1. куб: зрачки между шипами
            for (int i = 0; i < 7; i++) { b.Spike(14 + i * 8); b.Spike(15 + i * 8); b.SmallSpike(18 + i * 8); }
            b.Orb(72, 4, OrbKind.Yellow);
            b.Spike(74); b.Spike(75); b.Spike(76);
            b.Orb(82, 5, OrbKind.Pink);
            b.Block(88, 0, 6);
            b.Coin(94, 8, 1);
            b.Pad(96, 1, PadKind.Pink);
            for (int i = 0; i < 4; i++) b.Spike(100 + i * 7);

            // 2. корабль сквозь зрачки
            b.Portal(132, 1, PortalKind.ModeShip);
            b.Corridor(134, 60, -1, 14);
            Eye(146, 7, 4);
            Eye(168, 5, 3);
            Eye(188, 9, 3);
            for (int i = 0; i < 6; i++) b.Saw(152 + i * 8, 1 + (i * 3) % 10);
            b.Portal(194, 1, PortalKind.ModeCube);

            // 3. мини-волна в узком глазу
            b.Portal(202, 1, PortalKind.ModeWave);
            b.Portal(202, 4, PortalKind.Mini);
            b.Portal(202, 7, PortalKind.SpeedFastest);
            b.Corridor(204, 54, -1, 8);
            for (int i = 0; i < 17; i++)
            {
                b.Obj(1, 208 + i * 3, i % 2 == 0 ? 0 : 6);
                b.Obj(1, 208 + i * 3, i % 2 == 0 ? 1 : 7);
            }
            b.Coin(232, 4, 2);
            b.Portal(258, 1, PortalKind.ModeCube);
            b.Portal(258, 4, PortalKind.Big);
            b.Portal(258, 7, PortalKind.SpeedFaster);

            // 4. НЛО между ресницами
            b.Portal(266, 1, PortalKind.ModeUfo);
            b.Corridor(268, 48, -1, 13);
            for (int i = 0; i < 11; i++)
            {
                b.Column(274 + i * 4, 1, 4);
                b.Column(276 + i * 4, 8, 4);
            }
            Eye(300, 6, 2, false);
            b.Portal(316, 1, PortalKind.ModeCube);

            // 5. дуал: два глаза сразу
            b.Portal(324, 1, PortalKind.Dual);
            b.Corridor(326, 46, -1, 15);
            for (int i = 0; i < 11; i++)
            {
                b.Obj(20, 330 + i * 4, 0);
                b.Obj(22, 332 + i * 4, 14);
            }
            b.Coin(352, 7, 3);
            b.Portal(372, 1, PortalKind.Single);

            // 6. паук по стенам
            b.Portal(380, 1, PortalKind.ModeSpider);
            b.Corridor(382, 42, -1, 10);
            for (int i = 0; i < 9; i++) b.Obj(20, 388 + i * 5, i % 2 == 0 ? 0 : 9);
            for (int i = 0; i < 4; i++) b.Saw(392 + i * 10, 5);
            b.Portal(424, 1, PortalKind.ModeCube);

            // 7. робот и финальный рывок
            b.Portal(432, 1, PortalKind.ModeRobot);
            b.Block(438, 0, 5); b.Block(446, 2, 5); b.Block(456, 4, 5);
            b.Spike(444); b.Spike(454, 2);
            b.Orb(466, 7, OrbKind.Red);
            b.Portal(472, 1, PortalKind.ModeCube);
            b.Portal(472, 4, PortalKind.SpeedHyper);
            for (int i = 0; i < 8; i++) { b.Spike(478 + i * 6); b.CeilSpike(481 + i * 6, 7); }
            Eye(532, 6, 4);
            b.Text(544, 8, "I SEE YOU");
            b.Finish(552);
            return b.Build();
        }


        /// <summary>
        /// «Night Ride» — наш демон под трек BIKE с Newgrounds (#1191782).
        /// Уровень собран по самой песне: смена скорости, режимов и экранных
        /// эффектов стоит там, где в треке подъёмы и сбросы.
        /// Трек не вшит в игру — он качается по ID, как любая музыка с сайта.
        /// </summary>
        public static LevelData Bike()
        {
            const int SongId = 1191782;
            var b = new Builder("Night Ride", Difficulty.InsaneDemon, SongId, "BIKE");
            b.L.Settings.SongArtist = "Newgrounds #1191782";
            b.L.Settings.BgColor = unchecked((int)0xFF0A0D2E);
            b.L.Settings.GroundColor = unchecked((int)0xFF05061A);
            b.L.Settings.BgId = 13;
            b.L.Settings.GroundId = 6;
            b.L.Settings.StartSpeed = SpeedTier.Normal;
            b.L.Settings.Description = "Ночная поездка под BIKE. Трек тянется с сайта по ID 1191782.";

            // ---- разметка трека: с какой секунды какая скорость ----
            var plan = new (double t, SpeedTier sp)[]
            {
                (  0, SpeedTier.Normal ),   // вступление
                ( 10, SpeedTier.Fast   ),   // разгон
                ( 20, SpeedTier.Faster ),   // первый сброс
                ( 30, SpeedTier.Faster ),
                ( 40, SpeedTier.Fastest),   // второй сброс
                ( 50, SpeedTier.Fast   ),   // передышка
                ( 60, SpeedTier.Fastest),   // третий сброс
                ( 70, SpeedTier.Faster ),
                ( 80, SpeedTier.Hyper  ),   // пик
                ( 90, SpeedTier.Faster )    // выезд
            };
            const double End = 99.6;

            // позиция в клетках к секунде трека
            double Gx(double t)
            {
                double x = 0;
                for (int i = 0; i < plan.Length; i++)
                {
                    double a = plan[i].t, c = i + 1 < plan.Length ? plan[i + 1].t : End;
                    if (t <= a) break;
                    double dt = Math.Min(t, c) - a;
                    x += Speeds.Mul[(int)plan[i].sp] * 311.58 * dt;
                    if (t <= c) break;
                }
                return x / 30.0;
            }

            // ---- служебное: ставим объекты и эффекты по времени ----
            void Trig(double t, double gy, Action<TriggerData> cfg)
            {
                b.Obj(100, Math.Round(Gx(t)), gy, o =>
                {
                    o.Trigger = new TriggerData();
                    cfg(o.Trigger);
                });
            }

            void Fx(double t, TriggerType type, double strength, double dur)
                => Trig(t, 9, tr => { tr.Type = type; tr.FxStrength = strength; tr.Duration = dur; });

            void Shake(double t, double power, double dur)
                => Trig(t, 10, tr =>
                {
                    tr.Type = TriggerType.Shake;
                    tr.ShakeStrength = power;
                    tr.ShakeInterval = 0.04;
                    tr.Duration = dur;
                });

            void Bg(double t, int color, double fade = 1.2)
                => Trig(t, 11, tr => { tr.Type = TriggerType.BgColor; tr.Color = color; tr.FadeTime = fade; });

            void Grad(double t, int top, int bottom, double dur = 1.0)
                => Trig(t, 12, tr =>
                {
                    tr.Type = TriggerType.Gradient;
                    tr.Color = top; tr.Color2 = bottom;
                    tr.Duration = dur; tr.FxStrength = 0.75;
                });

            void Speed(double t, SpeedTier sp) => b.Portal(Math.Round(Gx(t)), 1, SpeedPortal(sp));
            void Mode(double t, PortalKind k) => b.Portal(Math.Round(Gx(t)), 1, k);

            // пол и потолок на отрезке времени
            void Tunnel(double t0, double t1, int floorY, int ceilY)
            {
                int a = (int)Math.Round(Gx(t0)), c = (int)Math.Round(Gx(t1));
                for (int x = a; x <= c; x++) { b.Obj(1, x, floorY); b.Obj(1, x, ceilY); }
            }

            // ------------------------------------------------ 0:00 вступление
            b.Text(Gx(0.9), 8, "NIGHT RIDE");
            Grad(0.2, unchecked((int)0x8820306A), unchecked((int)0x00000000), 2.0);
            for (double t = 2.0; t < 9.6; t += 1.15)
            {
                b.Spike(Math.Round(Gx(t)));
                if (t > 5) b.SmallSpike(Math.Round(Gx(t)) + 2);
            }
            b.Coin(Math.Round(Gx(8.4)), 6, 1);
            Fx(9.4, TriggerType.Vignette, 0.5, 1.2);

            // ------------------------------------------------ 0:10 разгон
            Speed(10, SpeedTier.Fast);
            Bg(10, unchecked((int)0xFF14215E));
            Shake(10, 9, 0.5);
            for (double t = 10.6; t < 19.4; t += 0.95)
            {
                double gx = Math.Round(Gx(t));
                b.Block(gx, 0, 3);
                b.Spike(gx + 4);
                if ((int)(t * 10) % 3 == 0) b.Orb(gx + 6, 4, OrbKind.Yellow);
            }
            Fx(18.0, TriggerType.Pixelate, 0.35, 1.6);

            // ------------------------------------------------ 0:20 первый сброс: корабль
            Speed(20, SpeedTier.Faster);
            Mode(20, PortalKind.ModeShip);
            Shake(20, 20, 0.9);
            Fx(20, TriggerType.Flash, 0.9, 0.5);
            Fx(20.1, TriggerType.Chromatic, 0.8, 1.4);
            Bg(20, unchecked((int)0xFF2A1060));
            Grad(20, unchecked((int)0x993C1CFF), unchecked((int)0x11000000), 9.0);
            Tunnel(20.3, 29.6, -1, 13);
            for (double t = 21; t < 29.4; t += 0.62)
            {
                double gx = Math.Round(Gx(t));
                int y = 2 + (int)((t * 7) % 9);
                b.Obj(1, gx, y);
                if ((int)(t * 3) % 4 == 0) b.Saw(gx + 2, y + 2);
            }
            b.Coin(Math.Round(Gx(26.5)), 11, 2);

            // ------------------------------------------------ 0:30 куб и шар
            Mode(30, PortalKind.ModeCube);
            Fx(30, TriggerType.Shockwave, 0.7, 0.8);
            for (double t = 30.4; t < 33.4; t += 0.7)
            {
                double gx = Math.Round(Gx(t));
                b.Spike(gx); b.Spike(gx + 1);
                b.Orb(gx + 4, 4, OrbKind.Pink);
            }
            // удары 33.7 / 35.7 / 38.0 / 40.4 — на каждый вспышка и тряска
            foreach (var hit in new[] { 33.7, 35.7, 38.0 })
            {
                Fx(hit, TriggerType.Flash, 0.65, 0.3);
                Shake(hit, 14, 0.4);
                double gx = Math.Round(Gx(hit));
                b.Pad(gx, 1, PadKind.Yellow);
                b.Spike(gx + 3); b.Spike(gx + 4); b.Spike(gx + 5);
            }
            Mode(36.2, PortalKind.ModeBall);
            Tunnel(36.4, 39.6, -1, 8);
            for (double t = 36.8; t < 39.4; t += 0.5)
            {
                b.Obj(20, Math.Round(Gx(t)), 0);
                b.Obj(22, Math.Round(Gx(t)) + 2, 7);
            }
            Mode(39.7, PortalKind.ModeCube);

            // ------------------------------------------------ 0:40 второй сброс: мини-волна
            Speed(40, SpeedTier.Fastest);
            Mode(40, PortalKind.ModeWave);
            b.Portal(Math.Round(Gx(40)), 4, PortalKind.Mini);
            Shake(40, 22, 1.0);
            Fx(40, TriggerType.Flash, 1.0, 0.45);
            Fx(40.1, TriggerType.Glitch, 0.75, 1.2);
            Bg(40, unchecked((int)0xFF5A1038));
            Grad(40, unchecked((int)0x99FF2E6E), unchecked((int)0x11000000), 9.5);
            Tunnel(40.4, 49.6, -1, 9);
            {
                int a = (int)Math.Round(Gx(40.6)), c = (int)Math.Round(Gx(49.4));
                for (int x = a, k = 0; x < c; x += 5, k++)
                {
                    int lo = k % 2 == 0 ? 0 : 6;
                    b.Obj(1, x, lo); b.Obj(1, x, lo + 1);
                }
            }
            b.Coin(Math.Round(Gx(45.0)), 4, 3);

            // ------------------------------------------------ 0:50 передышка: НЛО
            Speed(50, SpeedTier.Fast);
            Mode(50, PortalKind.ModeCube);
            b.Portal(Math.Round(Gx(50)), 4, PortalKind.Big);
            Mode(50.6, PortalKind.ModeUfo);
            Bg(50, unchecked((int)0xFF0D2A4E));
            Grad(50, unchecked((int)0x6616C8FF), unchecked((int)0x00000000), 9.0);
            Fx(50, TriggerType.Blur, 0.4, 1.5);
            Tunnel(50.8, 59.4, -1, 12);
            for (double t = 51.4; t < 59.2; t += 0.78)
            {
                double gx = Math.Round(Gx(t));
                b.Column(gx, 1, 3);
                b.Column(gx + 3, 8, 3);
            }

            // ------------------------------------------------ 1:00 третий сброс: дуал
            Speed(60, SpeedTier.Fastest);
            Mode(60, PortalKind.ModeCube);
            b.Portal(Math.Round(Gx(60)), 4, PortalKind.Dual);
            Shake(60, 24, 1.1);
            Fx(60, TriggerType.Flash, 1.0, 0.5);
            Fx(60.1, TriggerType.Chromatic, 0.9, 1.6);
            Fx(60.1, TriggerType.Shockwave, 0.9, 0.9);
            Bg(60, unchecked((int)0xFF3A0A52));
            Grad(60, unchecked((int)0xAA9B2BFF), unchecked((int)0x11000000), 9.5);
            Tunnel(60.4, 69.6, -1, 15);
            for (double t = 60.8; t < 69.4; t += 0.55)
            {
                double gx = Math.Round(Gx(t));
                b.Obj(20, gx, 0);
                b.Obj(22, gx + 2, 14);
                if ((int)(t * 2) % 5 == 0) b.Orb(gx + 1, 7, OrbKind.Blue);
            }

            // ------------------------------------------------ 1:10 паук
            Speed(70, SpeedTier.Faster);
            b.Portal(Math.Round(Gx(70)), 1, PortalKind.Single);
            Mode(70.4, PortalKind.ModeSpider);
            Fx(70, TriggerType.Pixelate, 0.4, 1.4);
            Bg(70, unchecked((int)0xFF102A22));
            Tunnel(70.6, 79.6, -1, 10);
            for (double t = 71.2; t < 79.2; t += 0.62)
            {
                double gx = Math.Round(Gx(t));
                b.Obj(20, gx, ((int)(t * 2)) % 2 == 0 ? 0 : 9);
                if ((int)(t * 4) % 7 == 0) b.Saw(gx + 2, 5);
            }

            // ------------------------------------------------ 1:20 пик
            Speed(80, SpeedTier.Hyper);
            Mode(80, PortalKind.ModeCube);
            Shake(80, 26, 1.2);
            Fx(80, TriggerType.Flash, 1.0, 0.5);
            Fx(80.1, TriggerType.Glitch, 0.85, 1.4);
            Bg(80, unchecked((int)0xFF5E1A08));
            Grad(80, unchecked((int)0xAAFF7A1E), unchecked((int)0x11000000), 9.5);
            Mode(80.6, PortalKind.ModeRobot);
            for (double t = 81; t < 87.6; t += 0.8)
            {
                double gx = Math.Round(Gx(t));
                b.Block(gx, (int)((t * 3) % 5), 4);
                b.Spike(gx + 6);
            }
            // удар 87.9 — всё трясётся и вспыхивает
            Shake(87.9, 30, 1.0);
            Fx(87.9, TriggerType.Flash, 1.0, 0.4);
            Fx(87.9, TriggerType.Shockwave, 1.0, 1.0);
            Mode(88.0, PortalKind.ModeCube);
            for (double t = 88.3; t < 89.6; t += 0.42)
            {
                double gx = Math.Round(Gx(t));
                b.Spike(gx); b.CeilSpike(gx + 2, 7);
            }

            // ------------------------------------------------ 1:30 выезд
            Speed(90, SpeedTier.Faster);
            Bg(90, unchecked((int)0xFF0A0D2E));
            Grad(90, unchecked((int)0x6616C8FF), unchecked((int)0x00000000), 9.0);
            Fx(90, TriggerType.Vignette, 0.6, 2.0);
            for (double t = 90.6; t < 97.6; t += 0.9)
            {
                double gx = Math.Round(Gx(t));
                b.Spike(gx);
                if ((int)(t * 2) % 3 == 0) b.Orb(gx + 3, 4, OrbKind.Yellow);
            }
            b.Text(Gx(98.2), 7, "RIDE ON");
            b.Finish(Math.Round(Gx(End)) - 2);
            return b.Build();
        }

        /// <summary>Портал нужной скорости.</summary>
        static PortalKind SpeedPortal(SpeedTier sp) => sp switch
        {
            SpeedTier.Slow => PortalKind.SpeedSlow,
            SpeedTier.Normal => PortalKind.SpeedNormal,
            SpeedTier.Fast => PortalKind.SpeedFast,
            SpeedTier.Faster => PortalKind.SpeedFaster,
            SpeedTier.Fastest => PortalKind.SpeedFastest,
            SpeedTier.Hyper => PortalKind.SpeedHyper,
            _ => PortalKind.SpeedUltra
        };

        public static LevelData[] All() => new[] { Easy(), Hard(), Insane(), Eye(), EyeSpy(), Bike() };

        /// <summary>
        /// Кладёт файлы основных уровней в assets/official. Уровни собираются кодом,
        /// поэтому файл всегда переписывается — иначе старая версия игры оставляла
        /// на диске свои уровни и все три выглядели одинаково. Прогресс хранится
        /// отдельно и не теряется.
        /// </summary>
        public static List<LevelData> EnsureFiles()
        {
            var res = new List<LevelData>();
            foreach (var lv in All())
            {
                var path = Path.Combine(AppConfig.OfficialDir, lv.Settings.LocalId + ".gdl");
                try { lv.Save(path); } catch { }
                res.Add(lv);
            }
            return res;
        }
    }
}

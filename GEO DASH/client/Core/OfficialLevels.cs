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

            /// <summary>Парящий «мост»: сквозь него прорывается червяк.</summary>
            public void Bridge(double gx, int len, int y, int thick = 1)
            {
                for (int i = 0; i < len; i++)
                    for (int k = 0; k < thick; k++) Obj(1, gx + i, y + k);
            }

            /// <summary>Слоёная платформа: два моста друг над другом.</summary>
            public void Layered(double gx, int len, int lowY, int highY, int thick = 1)
            {
                Bridge(gx, len, lowY, thick);
                Bridge(gx, len, highY, thick);
            }

            /// <summary>Глаз: кольцо из блоков с пилой-зрачком.</summary>
            public void Eye(double gx, int cy, int rad, bool pupil = true)
            {
                for (int a = 0; a < 20; a++)
                {
                    double ang = a * Math.PI * 2 / 20;
                    Obj(1, Math.Round(gx + Math.Cos(ang) * rad), Math.Round(cy + Math.Sin(ang) * rad));
                }
                if (pupil) Saw(gx, cy);
            }

            public LevelData Build()
            {
                L.Settings.LengthUnits = L.RightEdge();
                return L;
            }
        }

        // =================================================================
        //  Пять уровней игры. Все нарисованы для GEO DASH, не копии чужих.
        // =================================================================

        /// <summary>1. «Neon Tunnel» — знакомство с прыжками и червяком.</summary>
        public static LevelData NeonTunnel()
        {
            var b = new Builder("Neon Tunnel", Difficulty.Easy, -1, "Neon Start");
            b.L.Settings.LocalId = "gd_neon_tunnel";
            b.L.Settings.BgColor = unchecked((int)0xFF0B1646);
            b.L.Settings.GroundColor = unchecked((int)0xFF050A26);
            b.L.Settings.BgId = 3; b.L.Settings.GroundId = 2;
            b.L.Settings.StartSpeed = SpeedTier.Normal;
            b.L.Settings.Description = "Неоновый тоннель: первые прыжки и первый червяк.";

            b.Text(6, 6, "NEON TUNNEL");

            // 1. одиночные шипы — просто прыгать
            for (int i = 0; i < 5; i++) b.Spike(16 + i * 8);
            b.Block(58, 0, 5);
            b.Spike(64);
            b.Pad(70, 1, PadKind.Yellow);

            // 2. ступеньки и шар
            b.Block(78, 1, 4); b.Block(85, 2, 4); b.Block(92, 3, 4);
            b.Spike(83); b.Spike(90, 1);
            b.Orb(100, 5, OrbKind.Yellow);
            b.Coin(104, 7, 1);

            // 3. первый червяк: широкая безопасная платформа
            b.Portal(112, 1, PortalKind.ModeWorm);
            b.Text(112, 7, "WORM: КЛИК = СКВОЗЬ БЛОК");
            b.Bridge(116, 26, 3, 2);            // мост, по которому едем сверху
            for (int i = 0; i < 4; i++) b.Obj(20, 122 + i * 7, 5);   // шипы сверху — ныряем под мост
            b.Coin(138, 8, 2);

            // 4. мосты один за другим: ныряем туда-сюда
            b.Bridge(148, 18, 3, 2);
            for (int i = 0; i < 3; i++) b.Obj(22, 152 + i * 6, 2);   // шипы снизу — выходим наверх
            b.Bridge(172, 18, 3, 2);
            for (int i = 0; i < 3; i++) b.Obj(20, 176 + i * 6, 5);

            b.Portal(196, 1, PortalKind.ModeCube);

            // 5. спокойный финал
            for (int i = 0; i < 4; i++) b.Spike(204 + i * 9);
            b.Orb(240, 4, OrbKind.Pink);
            b.Coin(246, 6, 3);
            b.Text(252, 6, "GG!");
            b.Finish(260);
            return b.Build();
        }

        /// <summary>2. «Subterranean Pulse» — корабль, куб и слоёные платформы.</summary>
        public static LevelData SubterraneanPulse()
        {
            var b = new Builder("Subterranean Pulse", Difficulty.Normal, -2, "Static Rush");
            b.L.Settings.LocalId = "gd_subterranean_pulse";
            b.L.Settings.BgColor = unchecked((int)0xFF06352C);
            b.L.Settings.GroundColor = unchecked((int)0xFF031A16);
            b.L.Settings.BgId = 9; b.L.Settings.GroundId = 4;
            b.L.Settings.StartSpeed = SpeedTier.Fast;
            b.L.Settings.Description = "Подземный импульс: два слоя подряд — два прорыва.";

            b.Text(6, 7, "SUBTERRANEAN PULSE");

            for (int i = 0; i < 6; i++) { b.Spike(14 + i * 8); b.SmallSpike(17 + i * 8); }
            b.Orb(64, 4, OrbKind.Yellow);
            b.Block(70, 0, 5);
            b.Pad(76, 1, PadKind.Pink);
            b.Coin(82, 8, 1);

            // корабль
            b.Portal(90, 1, PortalKind.ModeShip);
            b.Corridor(92, 40, -1, 12);
            for (int i = 0; i < 7; i++) b.Obj(1, 98 + i * 5, 2 + (i * 3) % 8);
            b.Portal(132, 1, PortalKind.ModeCube);

            // червяк: слоёные платформы — прорываемся дважды подряд
            b.Portal(140, 1, PortalKind.ModeWorm);
            for (int s = 0; s < 4; s++)
            {
                double gx = 146 + s * 26;
                b.Layered(gx, 16, 2, 6);                 // нижний и верхний слой
                b.Obj(20, gx + 4, 8);                    // сверху шип — надо вниз
                b.Obj(22, gx + 10, 1);                   // снизу шип — надо вверх
            }
            b.Coin(200, 10, 2);
            b.Portal(254, 1, PortalKind.ModeCube);

            // куб с орбами
            b.Orb(262, 4, OrbKind.Yellow);
            b.Spike(264); b.Spike(265);
            b.Orb(272, 5, OrbKind.Pink);
            b.Block(280, 0, 6);
            for (int i = 0; i < 3; i++) b.Spike(290 + i * 8);
            b.Coin(312, 6, 3);
            b.Text(318, 6, "PULSE");
            b.Finish(326);
            return b.Build();
        }

        /// <summary>3. «Phase Shift» — паук, червяк и шар вперемешку.</summary>
        public static LevelData PhaseShift()
        {
            var b = new Builder("Phase Shift", Difficulty.Hard, -3, "Hyperdrive");
            b.L.Settings.LocalId = "gd_phase_shift";
            b.L.Settings.BgColor = unchecked((int)0xFF3A0A4E);
            b.L.Settings.GroundColor = unchecked((int)0xFF1B0426);
            b.L.Settings.BgId = 16; b.L.Settings.GroundId = 7;
            b.L.Settings.StartSpeed = SpeedTier.Faster;
            b.L.Settings.Description = "Фазовый сдвиг: паук прыгает через пустоту, червяк ныряет сквозь блок.";

            b.Text(6, 7, "PHASE SHIFT");

            for (int i = 0; i < 7; i++) { b.Spike(14 + i * 7); b.Spike(15 + i * 7); }
            b.Orb(66, 5, OrbKind.Pink);
            b.Coin(72, 8, 1);

            // паук: телепорт через пустоту
            b.Portal(80, 1, PortalKind.ModeSpider);
            b.Corridor(82, 34, -1, 10);
            for (int i = 0; i < 7; i++) b.Obj(20, 88 + i * 5, i % 2 == 0 ? 0 : 9);

            // сразу червяк: ныряем сквозь толщу
            b.Portal(118, 1, PortalKind.ModeWorm);
            for (int s = 0; s < 3; s++)
            {
                double gx = 124 + s * 22;
                b.Bridge(gx, 14, 3, 3);                  // толстая плита
                b.Obj(20, gx + 5, 6);
                b.Obj(22, gx + 10, 2);
            }
            b.Coin(160, 9, 2);

            // снова паук, потом шар
            b.Portal(192, 1, PortalKind.ModeSpider);
            b.Corridor(194, 30, -1, 9);
            for (int i = 0; i < 6; i++) b.Saw(200 + i * 5, i % 2 == 0 ? 2 : 7);
            b.Portal(224, 1, PortalKind.ModeBall);
            b.Corridor(226, 28, -1, 8);
            for (int i = 0; i < 6; i++) { b.Obj(20, 232 + i * 4, 0); b.Obj(22, 234 + i * 4, 7); }
            b.Portal(254, 1, PortalKind.ModeCube);

            // финал
            b.Portal(262, 1, PortalKind.SpeedFastest);
            for (int i = 0; i < 6; i++) { b.Spike(268 + i * 7); b.SmallSpike(271 + i * 7); }
            b.Orb(312, 4, OrbKind.Red);
            b.Coin(318, 7, 3);
            b.Text(324, 7, "SHIFT");
            b.Finish(332);
            return b.Build();
        }

        /// <summary>4. «Core Overload» — скорость, волна, робот и слалом червяком.</summary>
        public static LevelData CoreOverload()
        {
            var b = new Builder("Core Overload", Difficulty.Insane, -5, "Afterburner");
            b.L.Settings.LocalId = "gd_core_overload";
            b.L.Settings.BgColor = unchecked((int)0xFF521405);
            b.L.Settings.GroundColor = unchecked((int)0xFF260903);
            b.L.Settings.BgId = 18; b.L.Settings.GroundId = 9;
            b.L.Settings.StartSpeed = SpeedTier.Fastest;
            b.L.Settings.Description = "Перегрузка ядра: узкие коридоры и слалом сквозь парящие кубы.";

            b.Text(6, 8, "CORE OVERLOAD");

            for (int i = 0; i < 8; i++) { b.Spike(14 + i * 6); b.SmallSpike(16 + i * 6); }
            b.Orb(66, 4, OrbKind.Yellow);
            b.Coin(72, 9, 1);

            // узкая волна
            b.Portal(80, 1, PortalKind.ModeWave);
            b.Portal(80, 4, PortalKind.Mini);
            b.Corridor(82, 40, -1, 8);
            for (int i = 0; i < 13; i++)
            {
                b.Obj(1, 86 + i * 3, i % 2 == 0 ? 0 : 6);
                b.Obj(1, 86 + i * 3, i % 2 == 0 ? 1 : 7);
            }
            b.Portal(122, 1, PortalKind.ModeCube);
            b.Portal(122, 4, PortalKind.Big);

            // робот по ступеням
            b.Portal(130, 1, PortalKind.ModeRobot);
            for (int i = 0; i < 6; i++)
            {
                b.Block(136 + i * 9, i % 3, 5);
                b.Spike(142 + i * 9);
            }
            b.Portal(192, 1, PortalKind.ModeCube);

            // слалом червяком: шахматка парящих кубов с шипами
            b.Portal(200, 1, PortalKind.ModeWorm);
            for (int i = 0; i < 14; i++)
            {
                double gx = 206 + i * 8;
                int y = i % 2 == 0 ? 2 : 5;
                b.Bridge(gx, 5, y, 2);
                b.Obj(i % 2 == 0 ? 20 : 22, gx + 2, i % 2 == 0 ? y + 2 : y - 1);
            }
            b.Coin(260, 9, 2);
            b.Portal(320, 1, PortalKind.ModeCube);

            // финальный рывок
            b.Portal(328, 1, PortalKind.SpeedHyper);
            for (int i = 0; i < 8; i++) { b.Spike(334 + i * 6); b.CeilSpike(337 + i * 6, 7); }
            b.Orb(386, 4, OrbKind.Red);
            b.Coin(392, 7, 3);
            b.Text(398, 8, "OVERLOAD");
            b.Finish(406);
            return b.Build();
        }

        /// <summary>
        /// 5. «iSpyWithMyLittleEye» — наш демон под трек BIKE (Newgrounds #1191782).
        /// Геометрия и эффекты сделаны для GEO DASH; с чужого уровня ничего не скопировано.
        /// Скорости и эффекты расставлены по самой песне (99,6 секунды).
        /// </summary>
        public static LevelData ISpy()
        {
            const int SongId = 1191782;
            var b = new Builder("iSpyWithMyLittleEye", Difficulty.EasyDemon, SongId, "BIKE");
            b.L.Settings.LocalId = "gd_ispy";
            b.L.Settings.SongArtist = "Tangermusic (Newgrounds #1191782)";
            b.L.Settings.BgColor = unchecked((int)0xFF101010);
            b.L.Settings.GroundColor = unchecked((int)0xFF050505);
            b.L.Settings.BgId = 13; b.L.Settings.GroundId = 6;
            b.L.Settings.EyesBackground = true;
            b.L.Settings.StartSpeed = SpeedTier.Normal;
            b.L.Settings.Description = "Наш демон под BIKE. Глаза на фоне следят за тобой. Трек качается по ID 1191782.";

            var plan = new (double t, SpeedTier sp)[]
            {
                (  0, SpeedTier.Normal ), ( 10, SpeedTier.Fast   ),
                ( 20, SpeedTier.Faster ), ( 30, SpeedTier.Faster ),
                ( 40, SpeedTier.Fastest), ( 50, SpeedTier.Fast   ),
                ( 60, SpeedTier.Fastest), ( 70, SpeedTier.Faster ),
                ( 80, SpeedTier.Hyper  ), ( 90, SpeedTier.Faster )
            };
            const double End = 99.6;

            double Gx(double t)
            {
                double x = 0;
                for (int i = 0; i < plan.Length; i++)
                {
                    double a = plan[i].t, c = i + 1 < plan.Length ? plan[i + 1].t : End;
                    if (t <= a) break;
                    x += Speeds.Mul[(int)plan[i].sp] * 311.58 * (Math.Min(t, c) - a);
                    if (t <= c) break;
                }
                return x / 30.0;
            }

            void Trig(double t, double gy, Action<TriggerData> cfg)
                => b.Obj(100, Math.Round(Gx(t)), gy, o => { o.Trigger = new TriggerData(); cfg(o.Trigger); });

            void Fx(double t, TriggerType type, double str, double dur)
                => Trig(t, 9, tr => { tr.Type = type; tr.FxStrength = str; tr.Duration = dur; });
            void Shake(double t, double power, double dur)
                => Trig(t, 10, tr => { tr.Type = TriggerType.Shake; tr.ShakeStrength = power; tr.ShakeInterval = 0.04; tr.Duration = dur; });
            void Bg(double t, int color)
                => Trig(t, 11, tr => { tr.Type = TriggerType.BgColor; tr.Color = color; tr.FadeTime = 0.8; });
            void Mode(double t, PortalKind k) => b.Portal(Math.Round(Gx(t)), 1, k);
            void Speed(double t, SpeedTier sp) => b.Portal(Math.Round(Gx(t)), 1, SpeedPortal(sp));
            void Tunnel(double t0, double t1, int floorY, int ceilY)
            {
                int a = (int)Math.Round(Gx(t0)), c = (int)Math.Round(Gx(t1));
                for (int x = a; x <= c; x++) { b.Obj(1, x, floorY); b.Obj(1, x, ceilY); }
            }

            // 0:00 куб, спокойно
            b.Text(Gx(0.9), 8, "I SPY");
            for (double t = 2.0; t < 9.6; t += 1.2) b.Spike(Math.Round(Gx(t)));
            b.Coin(Math.Round(Gx(8.4)), 6, 1);

            // 0:10 паук
            Speed(10, SpeedTier.Fast);
            Mode(10.2, PortalKind.ModeSpider);
            Bg(10, unchecked((int)0xFF1A1A1A));
            Shake(10, 8, 0.4);
            Tunnel(10.4, 19.4, -1, 10);
            for (double t = 11; t < 19; t += 0.9)
                b.Obj(20, Math.Round(Gx(t)), ((int)(t * 2)) % 2 == 0 ? 0 : 9);

            // 0:20 первый дроп: червяк сквозь глаза
            Speed(20, SpeedTier.Faster);
            Mode(20.2, PortalKind.ModeWorm);
            Shake(20, 20, 0.9);
            Fx(20, TriggerType.Flash, 0.9, 0.5);
            Fx(20.1, TriggerType.Chromatic, 0.8, 1.4);
            Bg(20, unchecked((int)0xFF3A0606));
            for (int s = 0; s < 4; s++)
            {
                double t0 = 21.2 + s * 2.1;
                double gx = Math.Round(Gx(t0));
                b.Bridge(gx, 10, 3, 3);                       // толстый «глаз»-блок
                b.Eye(gx + 5, 7, 3, false);
                b.Obj(20, gx + 3, 6);
                b.Obj(22, gx + 7, 2);
            }
            b.Coin(Math.Round(Gx(27.5)), 10, 2);

            // ---------------- 0:32–0:48 пред-дроп: цвета переворачиваются ----------------
            // Такт трека — 141,3 удара в минуту. По сбивкам барабанов палитра
            // резко перекидывается с чёрного на белый и обратно, всё чаще.
            const double Beat = 60.0 / 141.3;

            Mode(30, PortalKind.ModeBall);
            Tunnel(30.3, 35.6, -1, 9);
            for (double t = 30.6; t < 35.4; t += Beat)
            {
                double gx = Math.Round(Gx(t));
                b.Obj(20, gx, 0);
                b.Obj(22, gx + 2, 8);
            }
            Mode(35.8, PortalKind.ModeCube);

            for (int k = 0; k < 10; k++)
            {
                double t = 36.0 + k * Beat * 2;
                if (t > 47.6) break;
                // чем ближе дроп, тем короче вспышка инверсии
                Fx(t, TriggerType.InvertColor, 1.0, Math.Max(0.10, 0.42 - k * 0.03));
                Bg(t, k % 2 == 0 ? unchecked((int)0xFFF2F2F2) : unchecked((int)0xFF101010));
                if (k % 2 == 0) Shake(t, 6 + k, 0.18);

                double gx = Math.Round(Gx(t));
                b.Spike(gx); b.Spike(gx + 1);
                if (k % 3 == 0) b.Orb(gx + 4, 4, OrbKind.Pink);
            }

            // ---------------- 0:48–1:20 дроп: цепочки прорывов ----------------
            Speed(48, SpeedTier.Fastest);
            Mode(48.2, PortalKind.ModeWorm);
            Bg(48, unchecked((int)0xFF140000));
            Fx(48, TriggerType.Flash, 1.0, 0.45);
            Fx(48.1, TriggerType.Chromatic, 0.9, 1.6);
            Shake(48, 26, 1.2);

            // красная вспышка и микротряска на каждый сильный удар бочки
            for (double t = 48; t < 80; t += Beat * 2)
            {
                Fx(t, TriggerType.Flash, 0.42, 0.12);
                Shake(t, 4, 0.1);
            }

            // 0:48–0:56 — лестница из слоёных платформ: 3–4 клика подряд
            for (int chain = 0; chain < 4; chain++)
            {
                double t0 = 48.6 + chain * 2.0;
                for (int step = 0; step < 4; step++)
                {
                    double t = t0 + step * Beat;
                    double gx = Math.Round(Gx(t));
                    int y = 2 + step * 2;                       // ступенька выше предыдущей
                    b.Bridge(gx, 7, y, 2);
                    // шип с обратной стороны: выйти нужно именно прорывом
                    b.Obj(step % 2 == 0 ? 20 : 22, gx + 3, step % 2 == 0 ? y + 2 : y - 1);
                }
            }
            b.Coin(Math.Round(Gx(55.0)), 11, 2);

            // 0:57–1:02 — узкий зигзаг на волне
            Mode(56.8, PortalKind.ModeWave);
            b.Portal(Math.Round(Gx(56.8)), 4, PortalKind.Mini);
            Tunnel(57.0, 62.4, -1, 9);
            {
                int a = (int)Math.Round(Gx(57.2)), c = (int)Math.Round(Gx(62.2));
                for (int x = a, k = 0; x < c; x += 4, k++)
                {
                    int lo = k % 2 == 0 ? 0 : 6;
                    b.Obj(1, x, lo); b.Obj(1, x, lo + 1);
                }
            }

            // 1:02–1:07 — паук по стенам
            Mode(62.6, PortalKind.ModeSpider);
            Tunnel(62.8, 67.4, -1, 10);
            for (double t = 63.2; t < 67.2; t += Beat)
                b.Obj(20, Math.Round(Gx(t)), ((int)(t / Beat)) % 2 == 0 ? 0 : 9);

            // 1:07–1:20 — снова червяк: два больших глаза из блоков, сквозь них прорыв
            Mode(67.6, PortalKind.ModeWorm);
            Shake(67.6, 18, 0.6);
            for (int e = 0; e < 2; e++)
            {
                double gx = Math.Round(Gx(68.4 + e * 5.2));
                b.Eye(gx + 6, 6, 5, false);        // кольцо-глаз из блоков
                b.Bridge(gx + 2, 9, 5, 3);         // толща, сквозь которую идём
                b.Obj(20, gx + 4, 8);
                b.Obj(22, gx + 9, 4);
            }
            for (int chain = 0; chain < 3; chain++)
            {
                double t0 = 74.4 + chain * 1.8;
                for (int step = 0; step < 3; step++)
                {
                    double t = t0 + step * Beat;
                    double gx = Math.Round(Gx(t));
                    int y = 6 - step * 2;
                    b.Bridge(gx, 6, y, 2);
                    b.Obj(step % 2 == 0 ? 22 : 20, gx + 2, step % 2 == 0 ? y - 1 : y + 2);
                }
            }
            b.Coin(Math.Round(Gx(78.4)), 10, 3);

            // ---------------- 1:20–1:30 финальный рывок ----------------
            Speed(80, SpeedTier.Hyper);
            Mode(80.2, PortalKind.ModeRobot);
            Shake(80, 28, 1.2);
            Fx(80, TriggerType.Flash, 1.0, 0.5);
            Fx(80.1, TriggerType.Glitch, 0.85, 1.4);
            Bg(80, unchecked((int)0xFF8A0A0A));
            for (double t = 80.8; t < 85.6; t += Beat * 2)
            {
                double gx = Math.Round(Gx(t));
                b.Block(gx, (int)((t * 3) % 4), 5);
                b.Spike(gx + 6);
            }
            Mode(85.8, PortalKind.ModeWorm);
            b.Eye(Math.Round(Gx(87.0)), 6, 6, false);
            b.Bridge(Math.Round(Gx(86.6)), 12, 5, 3);
            b.Obj(20, Math.Round(Gx(87.2)), 8);
            Shake(87.9, 30, 1.0);
            Fx(87.9, TriggerType.Shockwave, 1.0, 1.0);
            Mode(88.4, PortalKind.ModeCube);

            // 1:30 выезд
            Speed(90, SpeedTier.Faster);
            Bg(90, unchecked((int)0xFF101010));
            Fx(90, TriggerType.Vignette, 0.6, 2.0);
            for (double t = 90.6; t < 97.6; t += 0.95) b.Spike(Math.Round(Gx(t)));
            b.Eye(Math.Round(Gx(98.0)), 6, 4);
            b.Text(Gx(98.6), 9, "I SEE YOU");
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

        public static LevelData[] All() => new[]
        {
            NeonTunnel(), SubterraneanPulse(), PhaseShift(), CoreOverload(), ISpy()
        };

        /// <summary>
        /// Кладёт файлы основных уровней в assets/official. Уровни собираются кодом,
        /// поэтому файл всегда переписывается. Прогресс хранится отдельно и не теряется.
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

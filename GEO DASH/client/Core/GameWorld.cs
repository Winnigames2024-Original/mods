namespace GEO_DASH.Core
{
    /// <summary>Рантайм-состояние объекта (триггеры меняют его, сам уровень не портится).</summary>
    public class ObjState
    {
        public double Dx, Dy;
        public double Rot;
        public double SX = 1, SY = 1;
        public double Alpha = 1;
        public bool Enabled = true;
        public bool Collected;
        public bool Fired;
        public bool Dynamic;
        public double PulseScale = 1;
        public Color? PulseColor;
        public double PulseT;
    }

    public class Checkpoint
    {
        public double X, Y, VY;
        public int GravityDir;
        public GameMode Mode;
        public SpeedTier Speed;
        public bool Mini, Mirror, Dual;
        public double Time;
    }

    public class GameWorld
    {
        public LevelData Level;
        public ObjState[] St = Array.Empty<ObjState>();
        public Player P1 = new();
        public Player P2 = new() { PlayerIndex = 1 };
        public bool DualMode;
        public bool Reverse;
        /// <summary>Платформер: игрок сам ходит влево-вправо.</summary>
        public bool Platformer;
        /// <summary>Сколько шаров (orbs) использовано за попытку — идёт в рекорды.</summary>
        public int OrbsUsed;
        /// <summary>Экранные эффекты 2.2.</summary>
        public readonly ScreenFx Fx = new();
        /// <summary>События, включённые триггером Event.</summary>
        public readonly HashSet<int> Events = new();
        /// <summary>Счётчики, которые не сбрасываются при смерти (2.2).</summary>
        public readonly HashSet<int> PersistentItems = new();
        /// <summary>Сдвиг точки игрока по экрану и поворот поля (2.2).</summary>
        public double GameplayOffsetX, GameplayOffsetY, GameplayAngle;
        public ColorSystem Colors = new();
        public Camera Cam = new();
        public TriggerEngine Trig;

        public double Time;
        public double Progress;
        public int Attempts = 1;
        public bool Finished;
        public bool Dead;
        public bool Practice;
        public bool NoClipCheat;
        public List<Checkpoint> Checkpoints = new();
        public readonly Dictionary<int, int> Items = new();
        public int CoinsGot;
        public readonly HashSet<int> CoinsCollected = new();

        public double GroundY = 0;
        /// <summary>Потолка у уровня нет — вверх можно улетать, но не бесконечно.</summary>
        public double RoofY = 1e7;

        /// <summary>С какой высоты над уровнем начинается отсчёт.</summary>
        public const double SkyHeight = 30 * 26;
        /// <summary>Сколько можно лететь там, наверху, прежде чем разбиться.</summary>
        public const double SkySeconds = 2.0;
        public double RightEdge = 1200;

        public Action<string> Sfx;                       // "jump","orb","death","portal","coin","win"
        public Action<int, double, double, bool> OnSongTrigger;  // id, volume, start, loop
        public Action OnDeathEvent;
        public Action OnFinishEvent;

        readonly Dictionary<int, List<int>> _grid = new();
        readonly List<int> _dynamic = new();
        readonly Dictionary<int, List<int>> _groups = new();
        List<int> _triggerOrder = new();
        int _trigPtr;
        SpiderRequest _spider1, _spider2;
        readonly List<int> _scratch = new();
        public double DeathTimer;

        const int Cell = 120;

        public void Init(LevelData lv, bool practice = false)
        {
            Level = lv;
            Practice = practice;
            St = new ObjState[lv.Objects.Count];
            for (int i = 0; i < St.Length; i++) St[i] = new ObjState();
            Colors.Init(lv);
            Trig = new TriggerEngine(this);
            BuildIndex();
            RightEdge = lv.RightEdge();
            Reverse = lv.Settings.ReverseGameplay;
            Platformer = lv.Settings.PlatformerFreeMove;
            Restart(true);
        }

        /// <summary>
        /// Быстрое обновление для редактора: уровень тот же, просто стало больше или меньше
        /// объектов. Полную пересборку (Init) делаем только когда поменялись настройки уровня.
        /// </summary>
        public void RefreshEditor()
        {
            if (Level == null) return;
            int n = Level.Objects.Count;
            if (St == null || St.Length != n)
            {
                var old = St;
                St = new ObjState[n];
                for (int i = 0; i < n; i++)
                    St[i] = (old != null && i < old.Length && old[i] != null) ? old[i] : new ObjState();
            }
            RightEdge = Level.RightEdge();
        }

        void BuildIndex()
        {
            _grid.Clear(); _dynamic.Clear(); _groups.Clear(); _triggerOrder.Clear();

            for (int i = 0; i < Level.Objects.Count; i++)
            {
                var o = Level.Objects[i];
                foreach (var g in o.Groups)
                {
                    if (!_groups.TryGetValue(g, out var l)) _groups[g] = l = new List<int>();
                    l.Add(i);
                }
                var def = ObjectCatalog.Get(o.TypeId);
                if (def.Kind == ObjKind.Trigger) _triggerOrder.Add(i);
            }

            // объекты, которые триггеры могут двигать — проверяем каждый кадр
            var movingGroups = new HashSet<int>();
            foreach (var o in Level.Objects)
            {
                if (o.Trigger == null) continue;
                var t = o.Trigger;
                if (t.Type is TriggerType.Move or TriggerType.Rotate or TriggerType.Scale
                    or TriggerType.Follow or TriggerType.FollowPlayerY or TriggerType.Keyframe)
                {
                    if (t.TargetGroup != 0) movingGroups.Add(t.TargetGroup);
                    if (t.CenterGroup != 0) movingGroups.Add(t.CenterGroup);
                }
            }

            for (int i = 0; i < Level.Objects.Count; i++)
            {
                var o = Level.Objects[i];
                bool dyn = false;
                foreach (var g in o.Groups) if (movingGroups.Contains(g)) { dyn = true; break; }
                if (dyn) { _dynamic.Add(i); St[i].Dynamic = true; continue; }

                var r = ObjectCatalog.Bounds(o);
                int c0 = (int)Math.Floor(r.Left / Cell), c1 = (int)Math.Floor(r.Right / Cell);
                for (int c = c0; c <= c1; c++)
                {
                    if (!_grid.TryGetValue(c, out var l)) _grid[c] = l = new List<int>();
                    l.Add(i);
                }
            }

            _triggerOrder.Sort((a, b) => Level.Objects[a].X.CompareTo(Level.Objects[b].X));
        }

        public List<int> GroupIndices(int g) => _groups.TryGetValue(g, out var l) ? l : null;

        public void Restart(bool full = false)
        {
            Dead = false; Finished = false; Time = 0; Progress = 0; DeathTimer = 0;
            for (int i = 0; i < St.Length; i++)
            {
                var s = St[i];
                s.Dx = s.Dy = 0; s.Rot = 0; s.SX = s.SY = 1; s.Alpha = 1;
                s.Enabled = true; s.Fired = false; s.PulseColor = null; s.PulseScale = 1;
                s.Collected = false;
            }
            var keep = new Dictionary<int, int>();
            foreach (var id in PersistentItems)
                if (Items.TryGetValue(id, out var v)) keep[id] = v;
            Items.Clear();
            foreach (var kv in keep) Items[kv.Key] = kv.Value;
            Fx.Reset();
            Events.Clear();
            GameplayOffsetX = GameplayOffsetY = GameplayAngle = 0;
            Colors.Init(Level);
            Trig.Reset();
            _trigPtr = 0;
            CoinsGot = 0; CoinsCollected.Clear();
            OrbsUsed = 0;
            DualMode = Level.Settings.StartDual;
            // двойной прыжок выдаётся только триггером — на старте его нет
            P1.ExtraJumps = P2.ExtraJumps = 0;
            P1.AirJumpsLeft = P2.AirJumpsLeft = 0;

            double startY = 15;
            double startX = 0;
            if (Practice && Checkpoints.Count > 0)
            {
                var cp = Checkpoints[^1];
                P1.Reset(cp.X, cp.Y, Level.Settings);
                P1.VY = cp.VY; P1.GravityDir = cp.GravityDir; P1.Mode = cp.Mode;
                P1.Speed = cp.Speed; P1.Mini = cp.Mini; P1.Mirror = cp.Mirror;
                DualMode = cp.Dual; Time = cp.Time;
                // триггеры до этой точки считаем уже сработавшими
                while (_trigPtr < _triggerOrder.Count && Level.Objects[_triggerOrder[_trigPtr]].X < cp.X) _trigPtr++;
            }
            else
            {
                P1.Reset(startX, startY, Level.Settings);
            }
            P2.Reset(P1.X, P1.Y + (DualMode ? 240 : 0), Level.Settings);
            P2.GravityDir = -P1.GravityDir;
            P2.PlayerIndex = 1;

            Cam.Reset(P1.X, P1.Y);
            if (full) Attempts = 1;
        }

        public void Die(Player p)
        {
            if (Dead || Finished) return;
            if (NoClipCheat) return;
            Dead = true;
            DeathTimer = 0;
            P1.LavaTime = 0; P1.InLava = false; P1.InLiquid = false;
            P2.LavaTime = 0; P2.InLava = false; P2.InLiquid = false;
            Sfx?.Invoke("death");
            Trig.FireOnDeath();
            OnDeathEvent?.Invoke();
        }

        public void Finish()
        {
            if (Finished) return;
            Finished = true;
            Progress = 1;
            Sfx?.Invoke("win");
            OnFinishEvent?.Invoke();
        }

        // ---------------------------------------------------------------- update

        public void Update(double dt, PlayerInput i1, PlayerInput i2)
        {
            // TimeWarp: ускоряем или замедляем всё, кроме эффектов
            Fx.Update(dt);
            dt *= Math.Clamp(Fx.TimeScale, 0.1, 4.0);
            if (Dead) { DeathTimer += dt; return; }
            if (Finished) { Time += dt; return; }

            Time += dt;
            Colors.Update(dt);
            Trig.Update(dt);

            StepPlayer(P1, dt, i1, ref _spider1);
            if (DualMode) StepPlayer(P2, dt, DualInput(i1, i2), ref _spider2);

            FireTriggersByX(P1.X);

            Progress = Math.Clamp(P1.X / Math.Max(1, RightEdge), 0, 1);
            Cam.Follow(P1, dt, GroundY, RoofY);

            foreach (var s in St)
                if (s.PulseT > 0) { s.PulseT -= dt; if (s.PulseT <= 0) { s.PulseColor = null; s.PulseScale = 1; } }
        }

        PlayerInput DualInput(PlayerInput i1, PlayerInput i2)
        {
            // в 2-player режиме второй игрок управляется своей кнопкой, иначе — общей
            return Level.Settings.TwoPlayer ? i2 : i1;
        }

        void StepPlayer(Player p, double dt, PlayerInput inp, ref SpiderRequest sreq)
        {
            // платформер: едем туда, куда жмём, а не вперёд сами
            double vx = Platformer
                ? p.SpeedUnits * Math.Clamp(inp.MoveX, -1, 1)
                : p.SpeedUnits * (Reverse ? -1 : 1) * (p.Mirror ? 1 : 1);
            double prevX = p.X, prevY = p.Y;

            p.X += vx * dt;

            sreq = new SpiderRequest();
            p.NoClipWorm = p.Mode == GameMode.Worm && OverlapSolid(p, true);
            p.Integrate(dt, inp, ref sreq);
            p.Y += p.VY * dt;

            if (sreq.Requested) SpiderTeleport(p);
            if (p.WormDigRequest) { p.WormDigRequest = false; WormDig(p); }

            // Потолка у уровня нет, но улететь вверх навсегда нельзя:
            // высоко над уровнем идёт отсчёт, и через пару секунд игрок разбивается.
            if (p.Y > GroundY + SkyHeight)
            {
                p.SkyTime += dt;
                if (p.SkyTime > SkySeconds) { Die(p); return; }
            }
            else p.SkyTime = 0;

            // след телепорта гаснет сам
            if (p.TeleT > 0) p.TeleT = Math.Max(0, p.TeleT - dt);
            if (p.SpiderFlash > 0) p.SpiderFlash = Math.Max(0, p.SpiderFlash - dt);

            ResolveSolids(p, prevX, prevY);
            ResolveBounds(p);
            TouchObjects(p, inp);

            // в лаве можно пробыть пять секунд, потом сгораешь
            if (p.InLava)
            {
                bool first = p.LavaTime <= 0;
                p.LavaTime += dt;
                if (first) { Sfx?.Invoke("orb"); OrbsUsed++; }
                if (p.LavaTime >= Phys.LavaSeconds) { Die(p); return; }
            }
            else if (p.LavaTime > 0)
            {
                p.LavaTime = Math.Max(0, p.LavaTime - dt * 0.8);
            }
            p.UpdateRotation(dt);

            if (p.Mode == GameMode.Wave && p.TrailOn)
            {
                p.WaveTrail.Add(new PointF((float)p.X, (float)p.Y));
                if (p.WaveTrail.Count > 900) p.WaveTrail.RemoveRange(0, 200);
            }
        }

        // ------------------------------------------------- геометрия объектов

        public RectangleF ObjHit(int i)
        {
            var o = Level.Objects[i];
            var s = St[i];
            var d = ObjectCatalog.Get(o.TypeId);
            double sx = o.ScaleX * s.SX, sy = o.ScaleY * s.SY;
            double x = o.X + s.Dx + d.Hx * sx;
            double y = o.Y + s.Dy + d.Hy * sy;
            return new RectangleF((float)x, (float)y, (float)(d.Hw * Math.Abs(sx)), (float)(d.Hh * Math.Abs(sy)));
        }

        public RectangleF ObjBounds(int i)
        {
            var o = Level.Objects[i];
            var s = St[i];
            var d = ObjectCatalog.Get(o.TypeId);
            return new RectangleF((float)(o.X + s.Dx), (float)(o.Y + s.Dy),
                                  (float)(d.W * o.ScaleX * s.SX), (float)(d.H * o.ScaleY * s.SY));
        }

        public List<int> Candidates(RectangleF r)
        {
            _scratch.Clear();
            int c0 = (int)Math.Floor(r.Left / Cell) - 1, c1 = (int)Math.Floor(r.Right / Cell) + 1;
            for (int c = c0; c <= c1; c++)
                if (_grid.TryGetValue(c, out var l))
                    foreach (var i in l) if (St[i].Enabled) _scratch.Add(i);
            foreach (var i in _dynamic) if (St[i].Enabled) _scratch.Add(i);
            return _scratch;
        }

        bool OverlapSolid(Player p, bool onlyPhase)
        {
            var box = p.Box;
            box.Inflate(-2, -2);
            foreach (var i in Candidates(box))
            {
                var d = ObjectCatalog.Get(Level.Objects[i].TypeId);
                if (!d.Solid) continue;
                if (onlyPhase && !d.WormPhase) continue;
                if (ObjHit(i).IntersectsWith(box)) return true;
            }
            return false;
        }

        void ResolveSolids(Player p, double prevX, double prevY)
        {
            bool worm = p.Mode == GameMode.Worm;
            p.OnGround = false; p.OnCeiling = false;

            var box = p.Box;
            var list = new List<int>(Candidates(box));

            foreach (var i in list)
            {
                var o = Level.Objects[i];
                if (o.NoTouch) continue;                        // «без касания» — игрок проходит насквозь
                var d = ObjectCatalog.Get(o.TypeId);
                if (!d.Solid) continue;
                var r = ObjHit(i);
                box = p.Box;
                if (!r.IntersectsWith(box)) continue;

                // Червяк ездит ПО блокам и проходит их только прорывом по клику.
                // Насквозь пропускаем лишь когда он уже оказался внутри блока,
                // чтобы не застрял намертво.
                if (worm && d.WormPhase && p.NoClipWorm) continue;

                // склон не убивает: по нему заезжают наверх, как в игре
                if (d.Kind == ObjKind.Slope)
                {
                    double lo = Math.Min(r.Top, r.Bottom), hi = Math.Max(r.Top, r.Bottom);
                    double left = Math.Min(r.Left, r.Right), wide = Math.Max(1, Math.Abs(r.Right - r.Left));
                    double t = Math.Clamp((p.X - left) / wide, 0, 1);
                    bool up = d.Style is DrawStyle.SlopeUp or DrawStyle.SlopeUpHalf or DrawStyle.SlopeUpLong;
                    double frac = up ? t : 1 - t;
                    double surf = lo + (hi - lo) * frac;      // высота склона под игроком

                    // Склон не просто держит — он разгоняет вверх, как трамплин:
                    // чем круче и чем быстрее едем, тем сильнее подкидывает.
                    double slope = (hi - lo) / wide;
                    double lift = up ? Math.Min(p.SpeedUnits * slope, Phys.CubeJump * 1.35) : 0;

                    if (p.GravityDir > 0)
                    {
                        double want = surf + p.Size / 2;
                        if (p.Y <= want + 4)
                        {
                            p.Y = want;
                            if (p.VY < lift) p.VY = lift;
                            p.OnGround = lift <= 0;
                            p.RobotJumping = false;
                        }
                    }
                    else
                    {
                        double want = hi - (surf - lo) - p.Size / 2;
                        if (p.Y >= want - 4)
                        {
                            p.Y = want;
                            if (p.VY > -lift) p.VY = -lift;
                            p.OnGround = lift <= 0;
                            p.RobotJumping = false;
                        }
                    }
                    continue;
                }

                double overlapL = box.Right - r.Left;
                double overlapR = r.Right - box.Left;
                double overlapT = box.Top - r.Bottom;   // Y вверх: Top > Bottom
                double overlapB = r.Top - box.Bottom;

                double halfW = Math.Min(overlapL, overlapR);
                double prevBottom = prevY - p.Size / 2, prevTop = prevY + p.Size / 2;

                bool fromAbove = prevBottom >= r.Top - 0.6;
                bool fromBelow = prevTop <= r.Bottom + 0.6;

                // на земле стоим со стороны притяжения; удар другой стороной — смерть, как в игре
                bool groundMode = p.Mode is GameMode.Cube or GameMode.Ball or GameMode.Robot or GameMode.Spider;

                if (fromAbove && p.VY <= 0.0001)
                {
                    if (p.GravityDir < 0 && groundMode && Math.Abs(p.VY) > 40) { Die(p); return; }
                    p.Y = r.Top + p.Size / 2;
                    p.VY = 0;
                    if (p.GravityDir > 0) { p.OnGround = true; p.RobotJumping = false; }
                    else p.OnCeiling = true;
                    continue;
                }
                if (fromBelow && p.VY >= -0.0001)
                {
                    if (d.TopOnly) continue;              // платформа: снизу проходим
                    // прыгнул, а сверху блок — разбился
                    if (p.GravityDir > 0 && groundMode && Math.Abs(p.VY) > 40) { Die(p); return; }
                    p.Y = r.Bottom - p.Size / 2;
                    p.VY = 0;
                    if (p.GravityDir < 0) { p.OnGround = true; p.RobotJumping = false; }
                    else p.OnCeiling = true;
                    continue;
                }

                if (d.TopOnly) continue;

                // удар в стену
                if (halfW > 1.5)
                {
                    if (p.Mode is GameMode.Ship or GameMode.Ufo or GameMode.Wave)
                    {
                        Die(p); return;
                    }
                    Die(p); return;
                }
            }
        }

        void ResolveBounds(Player p)
        {
            double half = p.Size / 2;
            if (p.Y - half < GroundY)
            {
                p.Y = GroundY + half;
                p.VY = 0;
                if (p.GravityDir > 0) { p.OnGround = true; p.RobotJumping = false; }
                else p.OnCeiling = true;
                if (p.Mode == GameMode.Wave || p.Mode == GameMode.Ship || p.Mode == GameMode.Ufo)
                { if (p.Mode == GameMode.Wave) Die(p); }
            }
            if (p.Y + half > RoofY)
            {
                p.Y = RoofY - half;
                p.VY = 0;
                if (p.GravityDir < 0) { p.OnGround = true; p.RobotJumping = false; }
                else p.OnCeiling = true;
            }
        }

        void SpiderTeleport(Player p)
        {
            // ищем ближайшую поверхность в направлении, противоположном текущей гравитации
            int dir = -p.GravityDir;           // куда летим
            double half = p.Size / 2;
            double from = p.Y;
            double best = dir > 0 ? RoofY - half : GroundY + half;

            var probe = new RectangleF((float)(p.X - half + 2), (float)Math.Min(from, best) - 2,
                                       (float)(p.Size - 4), (float)Math.Abs(best - from) + 4);
            foreach (var i in Candidates(probe))
            {
                var d = ObjectCatalog.Get(Level.Objects[i].TypeId);
                if (!d.Solid) continue;
                var r = ObjHit(i);
                if (r.Right <= p.X - half + 1 || r.Left >= p.X + half - 1) continue;
                if (dir > 0)
                {
                    double y = r.Bottom - half;         // садимся под блоком
                    if (y > from + 1 && y < best) best = y;
                }
                else
                {
                    double y = r.Top + half;            // садимся на блок
                    if (y < from - 1 && y > best) best = y;
                }
            }
            p.TeleFromY = p.Y;
            p.TeleT = 0.2;
            p.Y = best;
            p.VY = 0;
            p.GravityDir = -p.GravityDir;
            p.SpiderFlash = 0.18;
            Sfx?.Invoke("orb");
        }

        /// <summary>
        /// Червяк прорывается сквозь блок, на котором стоит: проходит всю сплошную
        /// толщу и выходит с другой стороны, гравитация переворачивается.
        /// Если с той стороны нет места — остаёмся на месте.
        /// </summary>
        void WormDig(Player p)
        {
            double half = p.Size / 2;
            // роем в сторону притяжения: стоим сверху — уходим вниз, и наоборот
            int dir = p.GravityDir > 0 ? -1 : 1;
            double face = p.GravityDir > 0 ? p.Y - half : p.Y + half;   // грань, на которой стоим

            // собираем сплошную толщу блоков под игроком
            var probe = new RectangleF((float)(p.X - half + 2), (float)(face - 4000),
                                       (float)(p.Size - 4), 8000f);
            var spans = new List<(double lo, double hi)>();
            foreach (var i in Candidates(probe))
            {
                var o = Level.Objects[i];
                if (o.NoTouch) continue;
                var d = ObjectCatalog.Get(o.TypeId);
                if (!d.Solid) continue;
                var r = ObjHit(i);
                if (r.Right <= p.X - half + 1 || r.Left >= p.X + half - 1) continue;
                spans.Add((Math.Min(r.Top, r.Bottom), Math.Max(r.Top, r.Bottom)));
            }
            if (spans.Count == 0) return;

            // идём от грани вглубь, склеивая блоки, стоящие вплотную
            double edge = face;
            bool moved = true;
            int guard = 0;
            while (moved && guard++ < 64)
            {
                moved = false;
                foreach (var (lo, hi) in spans)
                {
                    if (dir < 0)
                    {
                        // роем вниз: блок должен начинаться у самой грани
                        if (hi >= edge - 1.5 && lo < edge) { edge = lo; moved = true; }
                    }
                    else
                    {
                        if (lo <= edge + 1.5 && hi > edge) { edge = hi; moved = true; }
                    }
                }
            }
            if (Math.Abs(edge - face) < 1) return;        // под нами ничего нет

            double targetY = dir < 0 ? edge - half : edge + half;

            // за блоком должно быть место: иначе никуда не лезем
            if (targetY - half < GroundY || targetY + half > RoofY) return;
            var test = new RectangleF((float)(p.X - half + 2), (float)(targetY - half + 2),
                                      (float)(p.Size - 4), (float)(p.Size - 4));
            foreach (var i in Candidates(test))
            {
                var o = Level.Objects[i];
                if (o.NoTouch) continue;
                var d = ObjectCatalog.Get(o.TypeId);
                if (!d.Solid) continue;
                var r = ObjHit(i);
                if (r.Right <= test.Left || r.Left >= test.Right) continue;
                double lo = Math.Min(r.Top, r.Bottom), hi = Math.Max(r.Top, r.Bottom);
                if (hi > targetY - half + 1 && lo < targetY + half - 1) return;   // занято
            }

            p.TeleFromY = p.Y;
            p.TeleT = 0.22;
            p.Y = targetY;
            p.VY = 0;
            p.GravityDir = -p.GravityDir;
            p.OnGround = true;
            p.OnCeiling = false;
            p.WormBurrow = 1;
            Cam.Shake(7, 0.03, 0.14);      // лёгкий толчок камеры на прорыв
            Sfx?.Invoke("portal");
        }

        void TouchObjects(Player p, PlayerInput inp)
        {
            var box = p.Box;
            var inner = p.InnerBox;
            p.InLiquid = false;
            p.InLava = false;
            foreach (var i in new List<int>(Candidates(box)))
            {
                var o = Level.Objects[i];
                var s = St[i];
                if (!s.Enabled || s.Collected) continue;
                if (o.NoTouch) continue;
                var d = ObjectCatalog.Get(o.TypeId);
                var r = ObjHit(i);

                switch (d.Kind)
                {
                    case ObjKind.Liquid:
                        // вода и лава: плаваем; в лаве ещё и горим
                        if (r.IntersectsWith(inner))
                        {
                            p.InLiquid = true;
                            if (d.Style == DrawStyle.Lava) p.InLava = true;
                        }
                        break;

                    case ObjKind.Hazard:
                        if (r.IntersectsWith(inner)) { Die(p); return; }
                        break;

                    case ObjKind.Portal:
                        if (r.IntersectsWith(box)) ApplyPortal(p, o, i);
                        break;

                    case ObjKind.Orb:
                        if (inp.Pressed && r.IntersectsWith(box)) ApplyOrb(p, o, i);
                        break;

                    case ObjKind.Pad:
                        if (r.IntersectsWith(box)) ApplyPad(p, o, i);
                        break;

                    case ObjKind.Coin:
                        if (r.IntersectsWith(box))
                        {
                            s.Collected = true;
                            if (o.TypeId == 80) { if (CoinsCollected.Add(o.Id)) CoinsGot++; Sfx?.Invoke("coin"); }
                            else { AddItem(o.CoinIndex == 0 ? 1 : o.CoinIndex, 1); Sfx?.Invoke("coin"); }
                        }
                        break;

                    case ObjKind.Finish:
                        if (r.IntersectsWith(box)) { Finish(); return; }
                        break;

                    case ObjKind.Deco:
                        if (d.Style == DrawStyle.Checkpoint && Practice && r.IntersectsWith(box))
                            AddCheckpoint(p);
                        break;

                    case ObjKind.Trigger:
                        if (o.Trigger != null && o.Trigger.TouchTriggered && r.IntersectsWith(box))
                        {
                            if (!s.Fired || o.Trigger.MultiTrigger)
                            {
                                s.Fired = true;
                                Trig.Fire(o, i);
                            }
                        }
                        break;
                }
            }
        }

        void ApplyPortal(Player p, LevelObject o, int idx)
        {
            var k = o.Portal;
            bool changed = true;
            switch (k)
            {
                case PortalKind.ModeCube: p.Mode = GameMode.Cube; break;
                case PortalKind.ModeShip: p.Mode = GameMode.Ship; break;
                case PortalKind.ModeBall: p.Mode = GameMode.Ball; break;
                case PortalKind.ModeUfo: p.Mode = GameMode.Ufo; break;
                case PortalKind.ModeWave: p.Mode = GameMode.Wave; p.WaveTrail.Clear(); break;
                case PortalKind.ModeRobot: p.Mode = GameMode.Robot; break;
                case PortalKind.ModeSpider: p.Mode = GameMode.Spider; break;
                case PortalKind.ModeWorm: p.Mode = GameMode.Worm; p.WormBurrow = 0; break;

                case PortalKind.GravityDown: if (p.GravityDir != 1) p.FlipGravity(); break;
                case PortalKind.GravityUp: if (p.GravityDir != -1) p.FlipGravity(); break;
                case PortalKind.GravityToggle: p.FlipGravity(); break;

                case PortalKind.Mini: p.Mini = true; break;
                case PortalKind.Big: p.Mini = false; break;
                case PortalKind.Mirror: p.Mirror = true; break;
                case PortalKind.Unmirror: p.Mirror = false; break;

                case PortalKind.Dual:
                    if (!DualMode)
                    {
                        DualMode = true;
                        P2.Reset(P1.X, P1.Y + 150, Level.Settings);
                        P2.Mode = P1.Mode; P2.Speed = P1.Speed; P2.Mini = P1.Mini;
                        P2.GravityDir = -P1.GravityDir;
                        P2.PlayerIndex = 1;
                    }
                    break;
                case PortalKind.Single: DualMode = false; break;

                case PortalKind.SpeedSlow: p.Speed = SpeedTier.Slow; break;
                case PortalKind.SpeedNormal: p.Speed = SpeedTier.Normal; break;
                case PortalKind.SpeedFast: p.Speed = SpeedTier.Fast; break;
                case PortalKind.SpeedFaster: p.Speed = SpeedTier.Faster; break;
                case PortalKind.SpeedFastest: p.Speed = SpeedTier.Fastest; break;
                case PortalKind.SpeedHyper: p.Speed = SpeedTier.Hyper; break;
                case PortalKind.SpeedUltra: p.Speed = SpeedTier.Ultra; break;

                case PortalKind.Teleport:
                    p.Y = o.TeleportTargetY != 0 ? o.TeleportTargetY : p.Y;
                    p.VY = 0;
                    break;

                default: changed = false; break;
            }
            if (changed && !St[idx].Fired)
            {
                St[idx].Fired = true;
                Sfx?.Invoke("portal");
            }
        }

        void ApplyOrb(Player p, LevelObject o, int idx)
        {
            switch (o.Orb)
            {
                case OrbKind.Yellow: p.Jump(Phys.OrbYellow); break;
                case OrbKind.Pink: p.Jump(Phys.OrbPink); break;
                case OrbKind.Red: p.Jump(Phys.OrbRed); break;
                case OrbKind.Blue: p.FlipGravity(); p.VY = 0; p.Jump(240); break;
                case OrbKind.Green: p.FlipGravity(); p.Jump(Phys.OrbYellow); break;
                case OrbKind.Black: p.VY = -Phys.OrbRed * p.GravityDir; break;
                case OrbKind.Dash: p.VY = 0; p.Jump(0); break;
                case OrbKind.GravityDash: p.FlipGravity(); p.VY = 0; break;
                case OrbKind.Spider:
                    {
                        var req = new SpiderRequest { Requested = true };
                        SpiderTeleport(p);
                        break;
                    }
                case OrbKind.Toggle: p.FlipGravity(); break;
            }
            Sfx?.Invoke("orb");
            St[idx].PulseT = 0.25;
            St[idx].PulseScale = 1.3;
        }

        void ApplyPad(Player p, LevelObject o, int idx)
        {
            if (St[idx].PulseT > 0.1) return;
            switch (o.Pad)
            {
                case PadKind.Yellow: p.Jump(Phys.PadYellow); break;
                case PadKind.Pink: p.Jump(Phys.PadPink); break;
                case PadKind.Red: p.Jump(Phys.PadRed); break;
                case PadKind.Blue: p.FlipGravity(); p.Jump(200); break;
                case PadKind.Spider: SpiderTeleport(p); break;
            }
            St[idx].PulseT = 0.3;
            St[idx].PulseScale = 1.25;
            Sfx?.Invoke("orb");
        }

        void FireTriggersByX(double x)
        {
            while (_trigPtr < _triggerOrder.Count)
            {
                int i = _triggerOrder[_trigPtr];
                var o = Level.Objects[i];
                if (o.X > x) break;
                _trigPtr++;
                if (o.Trigger == null) continue;
                if (o.Trigger.TouchTriggered || o.Trigger.SpawnTriggered) continue;
                if (!St[i].Enabled) continue;
                St[i].Fired = true;
                Trig.Fire(o, i);
            }
        }

        public void AddItem(int id, int delta)
        {
            Items.TryGetValue(id, out int v);
            Items[id] = v + delta;
            Trig.OnItemChanged(id, Items[id]);
        }

        public int GetItem(int id) { Items.TryGetValue(id, out int v); return v; }

        public void AddCheckpoint(Player p)
        {
            if (Checkpoints.Count > 0 && Math.Abs(Checkpoints[^1].X - p.X) < 40) return;
            Checkpoints.Add(new Checkpoint
            {
                X = p.X, Y = p.Y, VY = p.VY, GravityDir = p.GravityDir,
                Mode = p.Mode, Speed = p.Speed, Mini = p.Mini, Mirror = p.Mirror,
                Dual = DualMode, Time = Time
            });
        }

        public void RemoveCheckpoint()
        {
            if (Checkpoints.Count > 0) Checkpoints.RemoveAt(Checkpoints.Count - 1);
        }
    }
}

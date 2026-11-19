namespace GEO_DASH.Core
{
    /// <summary>Исполнитель всех триггеров уровня.</summary>
    public class TriggerEngine
    {
        readonly GameWorld W;
        readonly Random _rnd = new();

        class ActMove
        {
            public List<int> Targets;
            public double[] FromX, FromY;
            public double Dx, Dy, Dur, T;
            public EasingType Ease;
            public double Rate;
            public int TargetGroup;
        }
        class ActRot
        {
            public List<int> Targets;
            public double[] From;
            public double Deg, Dur, T;
            public EasingType Ease;
            public PointF Center;
            public bool AroundCenter;
            public double[] Cx, Cy;
            public int TargetGroup;
        }
        class ActScale
        {
            public List<int> Targets;
            public double[] FromX, FromY;
            public double ToX, ToY, Dur, T;
            public EasingType Ease;
            public int TargetGroup;
        }
        class ActAlpha
        {
            public List<int> Targets;
            public double[] From;
            public double To, Dur, T;
            public int TargetGroup;
        }
        class ActFollow
        {
            public List<int> Targets;
            public double Xm, Ym, Dur, T;
            public double LastPx, LastPy;
            public bool PlayerY;
            public double Delay, MaxSpeed;
            public int TargetGroup;
        }
        class ActArea
        {
            public TriggerType Type;
            public List<int> Targets;
            public double[] FromX, FromY, Power;
            public double Dx, Dy, Deg, ScaleX, ScaleY, Alpha;
            public Color Tint;
            public double Dur, T, Rate;
            public EasingType Ease;
        }
        class ActSpawn
        {
            public int Group;
            public double Delay;
        }
        class ActTimer
        {
            public double T;
            public int Group;
        }
        class PendingCount
        {
            public TriggerData T;
            public bool Instant;
        }

        readonly List<ActMove> _moves = new();
        readonly List<ActRot> _rots = new();
        readonly List<ActScale> _scales = new();
        readonly List<ActAlpha> _alphas = new();
        readonly List<ActFollow> _follows = new();
        readonly List<ActArea> _areas = new();
        readonly List<ActSpawn> _spawns = new();
        readonly List<ActTimer> _timers = new();
        readonly List<TriggerData> _onDeath = new();
        readonly List<PendingCount> _counts = new();

        public TriggerEngine(GameWorld w) { W = w; }

        public void Reset()
        {
            _moves.Clear(); _rots.Clear(); _scales.Clear(); _alphas.Clear();
            _follows.Clear(); _spawns.Clear(); _timers.Clear(); _onDeath.Clear(); _counts.Clear();
            _areas.Clear();
        }

        // ------------------------------------------------------------- запуск

        public void Fire(LevelObject obj, int selfIndex)
        {
            var t = obj.Trigger;
            if (t == null) return;
            if (t.SpawnDelay > 0.0001 && t.Type != TriggerType.Spawn)
            {
                // задержка через очередь спавна самого себя
                _spawns.Add(new ActSpawn { Group = -selfIndex - 1, Delay = t.SpawnDelay });
                return;
            }
            Execute(t, obj);
        }

        void FireGroup(int group)
        {
            var list = W.GroupIndices(group);
            if (list == null) return;
            foreach (var i in list)
            {
                var o = W.Level.Objects[i];
                if (o.Trigger == null) continue;
                if (!W.St[i].Enabled) continue;
                if (W.St[i].Fired && !o.Trigger.MultiTrigger) { }
                W.St[i].Fired = true;
                if (o.Trigger.SpawnDelay > 0.0001)
                    _spawns.Add(new ActSpawn { Group = -i - 1, Delay = o.Trigger.SpawnDelay });
                else
                    Execute(o.Trigger, o);
            }
        }

        public void FireOnDeath()
        {
            foreach (var t in _onDeath.ToArray()) if (t.TargetGroup != 0) FireGroup(t.TargetGroup);
        }

        public void OnItemChanged(int id, int value)
        {
            foreach (var pc in _counts.ToArray())
            {
                if (pc.T.ItemId != id) continue;
                if (value == pc.T.TargetCount || (pc.T.CountMultiActivate && value >= pc.T.TargetCount))
                {
                    FireGroup(pc.T.TargetGroup);
                    if (!pc.T.CountMultiActivate) _counts.Remove(pc);
                }
            }
        }

        void Execute(TriggerData t, LevelObject src)
        {
            switch (t.Type)
            {
                case TriggerType.Move:
                case TriggerType.Keyframe:
                    {
                        var list = W.GroupIndices(t.TargetGroup);
                        if (list == null) break;
                        var a = new ActMove
                        {
                            Targets = list, Dx = t.MoveX, Dy = t.MoveY,
                            Dur = Math.Max(0, t.Duration), Ease = t.Easing, Rate = t.EaseRate,
                            TargetGroup = t.TargetGroup
                        };
                        a.FromX = new double[list.Count]; a.FromY = new double[list.Count];
                        for (int k = 0; k < list.Count; k++) { a.FromX[k] = W.St[list[k]].Dx; a.FromY[k] = W.St[list[k]].Dy; }
                        if (a.Dur <= 0) { for (int k = 0; k < list.Count; k++) { W.St[list[k]].Dx += a.Dx; W.St[list[k]].Dy += a.Dy; } }
                        else _moves.Add(a);
                        break;
                    }

                case TriggerType.Rotate:
                    {
                        var list = W.GroupIndices(t.TargetGroup);
                        if (list == null) break;
                        var a = new ActRot
                        {
                            Targets = list, Deg = t.Degrees, Dur = Math.Max(0, t.Duration),
                            Ease = t.Easing, TargetGroup = t.TargetGroup
                        };
                        a.From = new double[list.Count];
                        a.Cx = new double[list.Count]; a.Cy = new double[list.Count];
                        for (int k = 0; k < list.Count; k++) { a.From[k] = W.St[list[k]].Rot; a.Cx[k] = W.St[list[k]].Dx; a.Cy[k] = W.St[list[k]].Dy; }
                        if (t.CenterGroup != 0)
                        {
                            var cl = W.GroupIndices(t.CenterGroup);
                            if (cl != null && cl.Count > 0)
                            {
                                var r = W.ObjBounds(cl[0]);
                                a.Center = new PointF(r.Left + r.Width / 2, r.Top + r.Height / 2);
                                a.AroundCenter = true;
                            }
                        }
                        if (a.Dur <= 0)
                        {
                            for (int k = 0; k < list.Count; k++) W.St[list[k]].Rot += a.Deg;
                        }
                        else _rots.Add(a);
                        break;
                    }

                case TriggerType.Scale:
                    {
                        var list = W.GroupIndices(t.TargetGroup);
                        if (list == null) break;
                        var a = new ActScale
                        {
                            Targets = list, ToX = t.ScaleX, ToY = t.ScaleY,
                            Dur = Math.Max(0, t.Duration), Ease = t.Easing, TargetGroup = t.TargetGroup
                        };
                        a.FromX = new double[list.Count]; a.FromY = new double[list.Count];
                        for (int k = 0; k < list.Count; k++) { a.FromX[k] = W.St[list[k]].SX; a.FromY[k] = W.St[list[k]].SY; }
                        if (a.Dur <= 0) foreach (var i in list) { W.St[i].SX = a.ToX; W.St[i].SY = a.ToY; }
                        else _scales.Add(a);
                        break;
                    }

                case TriggerType.Alpha:
                    {
                        var list = W.GroupIndices(t.TargetGroup);
                        if (list == null) break;
                        var a = new ActAlpha { Targets = list, To = t.Opacity, Dur = Math.Max(0, t.Duration), TargetGroup = t.TargetGroup };
                        a.From = new double[list.Count];
                        for (int k = 0; k < list.Count; k++) a.From[k] = W.St[list[k]].Alpha;
                        if (a.Dur <= 0) foreach (var i in list) W.St[i].Alpha = a.To;
                        else _alphas.Add(a);
                        break;
                    }

                case TriggerType.Toggle:
                    {
                        var list = W.GroupIndices(t.TargetGroup);
                        if (list == null) break;
                        foreach (var i in list) W.St[i].Enabled = t.Activate;
                        break;
                    }

                case TriggerType.Spawn:
                    _spawns.Add(new ActSpawn { Group = t.TargetGroup, Delay = t.SpawnDelay });
                    break;

                case TriggerType.Stop:
                    StopGroup(t.TargetGroup);
                    break;

                case TriggerType.Pulse:
                    {
                        var col = Color.FromArgb(t.Color);
                        if (t.PulseToGroup)
                        {
                            var list = W.GroupIndices(t.TargetGroup);
                            if (list != null)
                                foreach (var i in list)
                                {
                                    W.St[i].PulseColor = col;
                                    W.St[i].PulseT = t.PulseFadeIn + t.PulseHold + t.PulseFadeOut;
                                    W.St[i].PulseScale = 1;
                                }
                        }
                        else W.Colors.Pulse(t.ColorChannel, col, t.PulseFadeIn, t.PulseHold, t.PulseFadeOut);
                        break;
                    }

                case TriggerType.Color:
                    W.Colors.FadeTo(t.ColorChannel, Color.FromArgb(t.Color), t.FadeTime, t.Blending, 1);
                    break;
                case TriggerType.BgColor:
                    W.Colors.FadeTo(ColorSystem.ChBg, Color.FromArgb(t.Color), t.FadeTime, false, 1);
                    break;
                case TriggerType.GroundColor:
                    W.Colors.FadeTo(ColorSystem.ChGround, Color.FromArgb(t.Color), t.FadeTime, false, 1);
                    break;

                case TriggerType.Shake:
                    W.Cam.Shake(t.ShakeStrength, t.ShakeInterval, Math.Max(0.05, t.Duration));
                    break;

                case TriggerType.Follow:
                case TriggerType.FollowPlayerY:
                    {
                        var list = W.GroupIndices(t.TargetGroup);
                        if (list == null) break;
                        _follows.Add(new ActFollow
                        {
                            Targets = list, Xm = t.FollowXMod, Ym = t.FollowYMod,
                            Dur = t.Duration <= 0 ? 999 : t.Duration,
                            PlayerY = t.Type == TriggerType.FollowPlayerY,
                            Delay = t.FollowDelay, MaxSpeed = t.FollowMaxSpeed,
                            LastPx = W.P1.X, LastPy = W.P1.Y, TargetGroup = t.TargetGroup
                        });
                        break;
                    }

                case TriggerType.Animate:
                    {
                        var list = W.GroupIndices(t.TargetGroup);
                        if (list != null) foreach (var i in list) W.St[i].PulseT = 0.3;
                        break;
                    }

                case TriggerType.Touch:
                    if (t.TargetGroup != 0) FireGroup(t.TargetGroup);
                    break;

                case TriggerType.Count:
                    _counts.Add(new PendingCount { T = t.Clone(), Instant = false });
                    break;

                case TriggerType.InstantCount:
                    {
                        int v = W.GetItem(t.ItemId);
                        bool ok = t.CountMultiActivate ? v >= t.TargetCount : v == t.TargetCount;
                        if (ok) FireGroup(t.TargetGroup);
                        else if (t.GroupB != 0) FireGroup(t.GroupB);
                        break;
                    }

                case TriggerType.Pickup:
                    W.AddItem(t.ItemId, t.Count);
                    break;

                case TriggerType.Collision:
                case TriggerType.InstantCollision:
                    {
                        bool hit = false;
                        var la = W.GroupIndices(t.BlockA);
                        if (la != null)
                        {
                            var pb = W.P1.Box;
                            foreach (var i in la) if (W.ObjHit(i).IntersectsWith(pb)) { hit = true; break; }
                        }
                        if (hit) { if (t.TargetGroup != 0) FireGroup(t.TargetGroup); }
                        else if (t.GroupB != 0) FireGroup(t.GroupB);
                        break;
                    }

                case TriggerType.Random:
                    {
                        bool a = _rnd.NextDouble() * 100 < t.Chance;
                        int g = a ? t.TargetGroup : t.GroupB;
                        if (g != 0) FireGroup(g);
                        break;
                    }

                case TriggerType.Timer:
                    _timers.Add(new ActTimer { T = Math.Max(0, t.TimerSeconds), Group = t.TargetGroup });
                    break;

                case TriggerType.Camera:
                    W.Cam.Static = t.CamStatic;
                    W.Cam.SetOffset(t.CamOffsetX, t.CamOffsetY, t.Duration);
                    break;

                case TriggerType.CameraZoom:
                    W.Cam.SetZoom(t.CamZoom, t.Duration);
                    break;

                case TriggerType.Song:
                    W.OnSongTrigger?.Invoke(t.SongId, t.SongVolume, t.SongStartSec, t.SongLoop);
                    break;

                case TriggerType.Gravity:
                    {
                        W.P1.GravityMul = t.GravityMul <= 0 ? 1 : t.GravityMul;
                        if (t.GravityDir == 0) W.P1.FlipGravity();
                        else if (W.P1.GravityDir != t.GravityDir) W.P1.FlipGravity();
                        break;
                    }

                case TriggerType.Teleport:
                    W.P1.Y = t.TeleportY;
                    if (!t.TeleportSmooth) W.P1.VY = 0;
                    break;

                case TriggerType.OnDeath:
                    _onDeath.Add(t.Clone());
                    break;

                case TriggerType.Trail:
                    W.P1.TrailOn = t.Activate;
                    break;

                case TriggerType.HidePlayer:
                    W.P1.Hidden = true; W.P2.Hidden = true;
                    break;
                case TriggerType.ShowPlayer:
                    W.P1.Hidden = false; W.P2.Hidden = false;
                    break;

                case TriggerType.Options:
                    if (t.OptChangeMode) W.P1.Mode = t.OptMode;
                    W.P1.Mini = t.OptMini;
                    W.P1.Mirror = t.OptMirror;
                    W.DualMode = t.OptDual;
                    break;

                case TriggerType.Checkpoint:
                    W.AddCheckpoint(W.P1);
                    break;

                case TriggerType.End:
                    W.Finish();
                    break;

                case TriggerType.Reverse:
                    W.Reverse = t.Activate;
                    break;

                case TriggerType.SpeedChange:
                    W.P1.Speed = t.Speed;
                    if (W.DualMode) W.P2.Speed = t.Speed;
                    break;

                // ======================= эффекты и возможности 2.2 =======================

                case TriggerType.Gradient:
                    W.Fx.GradientOn = t.Activate;
                    W.Fx.GradTop = Color.FromArgb(t.Color);
                    W.Fx.GradBottom = Color.FromArgb(t.Color2);
                    break;

                case TriggerType.Particles:
                    W.Fx.Burst(src?.X + 15 ?? W.P1.X, src?.Y + 15 ?? W.P1.Y,
                               Math.Clamp(t.Count, 1, 120), Color.FromArgb(t.Color), 120 + t.FxStrength * 400);
                    break;

                case TriggerType.Shockwave:
                    W.Fx.Shock.Set(Math.Max(0.15, t.Duration), Math.Clamp(t.FxStrength, 0, 1));
                    W.Fx.ShockX = src?.X ?? W.P1.X;
                    W.Fx.ShockY = src?.Y ?? W.P1.Y;
                    break;

                case TriggerType.Chromatic: W.Fx.Chromatic.Set(t.Duration, t.FxStrength); break;
                case TriggerType.Pixelate:  W.Fx.Pixelate.Set(t.Duration, t.FxStrength); break;
                case TriggerType.Glitch:    W.Fx.Glitch.Set(t.Duration, t.FxStrength); break;
                case TriggerType.InvertColor: W.Fx.Invert.Set(t.Duration, t.FxStrength); break;
                case TriggerType.Grayscale: W.Fx.Gray.Set(t.Duration, t.FxStrength); break;
                case TriggerType.Blur:      W.Fx.Blur.Set(t.Duration, t.FxStrength); break;
                case TriggerType.Vignette:  W.Fx.Vignette.Set(t.Duration, t.FxStrength); break;

                case TriggerType.Flash:
                    W.Fx.Flash.Set(Math.Max(0.1, t.Duration), Math.Clamp(t.FxStrength, 0, 1));
                    W.Fx.FlashColor = Color.FromArgb(t.Color);
                    break;

                case TriggerType.Bump:
                    W.Cam.Shake(t.ShakeStrength, t.ShakeInterval, Math.Max(0.12, t.Duration));
                    W.Fx.Shock.Set(Math.Max(0.2, t.Duration), 0.5);
                    W.Fx.ShockX = W.P1.X; W.Fx.ShockY = W.P1.Y;
                    break;

                case TriggerType.TimeWarp:
                    W.Fx.TimeScale = Math.Clamp(t.TimeScale <= 0 ? 1 : t.TimeScale, 0.1, 4);
                    break;

                // ---- площадные: действуют на всё вокруг себя, чем ближе — тем сильнее
                case TriggerType.AreaMove:
                case TriggerType.AreaRotate:
                case TriggerType.AreaScale:
                case TriggerType.AreaFade:
                case TriggerType.AreaTint:
                    Area(t, src);
                    break;

                case TriggerType.Sequence:
                    {
                        // по очереди включаем группы: цель, вторая, третья…
                        int[] groups = { t.TargetGroup, t.GroupB, t.CenterGroup, t.SecondaryGroup };
                        double delay = 0;
                        foreach (var g in groups)
                        {
                            if (g == 0) continue;
                            _spawns.Add(new ActSpawn { Group = g, Delay = delay });
                            delay += Math.Max(0.02, t.StepDelay);
                        }
                        break;
                    }

                case TriggerType.AdvancedFollow:
                    _follows.Add(new ActFollow
                    {
                        Targets = W.GroupIndices(t.TargetGroup) ?? new List<int>(),
                        Xm = t.FollowXMod, Ym = t.FollowYMod,
                        Dur = t.Duration <= 0 ? 9999 : t.Duration,
                        Delay = t.FollowDelay, MaxSpeed = t.FollowMaxSpeed,
                        LastPx = W.P1.X, LastPy = W.P1.Y,
                        TargetGroup = t.TargetGroup
                    });
                    break;

                case TriggerType.CameraEdge:
                case TriggerType.CameraGuide:
                    W.Cam.SetOffset(t.CamOffsetX, t.CamOffsetY, Math.Max(0.05, t.Duration));
                    break;

                case TriggerType.CameraStatic:
                    W.Cam.Static = t.CamStatic;
                    break;

                case TriggerType.CameraRotate:
                    W.GameplayAngle = t.Degrees;
                    break;

                case TriggerType.GameplayOffset:
                    W.GameplayOffsetX = t.MoveX;
                    W.GameplayOffsetY = t.MoveY;
                    break;

                case TriggerType.RotateGameplay:
                    W.GameplayAngle = t.Degrees;
                    break;

                case TriggerType.Event:
                    W.Events.Add(t.EventId);
                    if (t.TargetGroup != 0) FireGroup(t.TargetGroup);
                    break;

                case TriggerType.Reset:
                    W.Events.Remove(t.EventId);
                    if (t.TargetGroup != 0) StopGroup(t.TargetGroup);
                    break;

                case TriggerType.ItemEdit:
                    W.AddItem(t.ItemId, t.Count);
                    break;

                case TriggerType.PersistentItem:
                    // значение остаётся между попытками
                    W.PersistentItems.Add(t.ItemId);
                    break;

                case TriggerType.ItemCompare:
                    {
                        int have = W.Items.TryGetValue(t.ItemId, out var v) ? v : 0;
                        bool okCmp = t.Compare switch
                        {
                            1 => have > t.TargetCount,
                            2 => have < t.TargetCount,
                            3 => have >= t.TargetCount,
                            4 => have <= t.TargetCount,
                            5 => have != t.TargetCount,
                            _ => have == t.TargetCount
                        };
                        int g = okCmp ? t.TrueGroup : t.FalseGroup;
                        if (g != 0) FireGroup(g);
                        break;
                    }

                case TriggerType.ToggleBlock:
                    foreach (var i in W.GroupIndices(t.TargetGroup) ?? new List<int>())
                        W.St[i].Enabled = !W.St[i].Enabled;
                    break;

                case TriggerType.LinkVisible:
                    foreach (var i in W.GroupIndices(t.TargetGroup) ?? new List<int>())
                        W.St[i].Alpha = t.Activate ? 1 : 0;
                    break;

                case TriggerType.EnterEffect:
                    foreach (var i in W.GroupIndices(t.TargetGroup) ?? new List<int>())
                        W.St[i].Alpha = Math.Clamp(t.Opacity, 0, 1);
                    break;

                case TriggerType.Sfx:
                    W.Sfx?.Invoke(string.IsNullOrEmpty(t.SfxName) ? "click" : t.SfxName);
                    break;

                case TriggerType.EditSong:
                    W.OnSongTrigger?.Invoke(t.SongId != 0 ? t.SongId : W.Level.Settings.SongId,
                                            t.SongVolume, t.SongStartSec, t.SongLoop);
                    break;
            }
        }

        /// <summary>Площадной триггер: берём всё в радиусе и тянем тем сильнее, чем ближе.</summary>
        void Area(TriggerData t, LevelObject src)
        {
            if (src == null) return;
            double cx = src.X + 15, cy = src.Y + 15;
            double rad = Math.Max(30, t.AreaRadius);
            double dur = Math.Max(0.05, t.Duration);

            var targets = new List<int>();
            var fromX = new List<double>();
            var fromY = new List<double>();
            var power = new List<double>();

            for (int i = 0; i < W.Level.Objects.Count; i++)
            {
                var o = W.Level.Objects[i];
                if (o.Trigger != null) continue;
                double dx = o.X + 15 - cx, dy = o.Y + 15 - cy;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d > rad) continue;
                targets.Add(i);
                fromX.Add(W.St[i].Dx);
                fromY.Add(W.St[i].Dy);
                power.Add(1 - d / rad);          // в центре сильнее
            }
            if (targets.Count == 0) return;

            _areas.Add(new ActArea
            {
                Type = t.Type,
                Targets = targets,
                FromX = fromX.ToArray(), FromY = fromY.ToArray(), Power = power.ToArray(),
                Dx = t.MoveX, Dy = t.MoveY, Deg = t.Degrees,
                ScaleX = t.ScaleX, ScaleY = t.ScaleY, Alpha = t.Opacity,
                Tint = Color.FromArgb(t.Color),
                Dur = dur, Ease = t.Easing, Rate = t.EaseRate
            });
        }

        void StopGroup(int group)
        {
            _moves.RemoveAll(a => a.TargetGroup == group);
            _rots.RemoveAll(a => a.TargetGroup == group);
            _scales.RemoveAll(a => a.TargetGroup == group);
            _alphas.RemoveAll(a => a.TargetGroup == group);
            _follows.RemoveAll(a => a.TargetGroup == group);
            _spawns.RemoveAll(a => a.Group == group);
            _timers.RemoveAll(a => a.Group == group);
        }

        // ------------------------------------------------------------ update

        public void Update(double dt)
        {
            // площадные триггеры 2.2
            for (int i = _areas.Count - 1; i >= 0; i--)
            {
                var a = _areas[i];
                a.T += dt;
                double k = Ease(Math.Clamp(a.T / a.Dur, 0, 1), a.Ease, a.Rate);
                for (int j = 0; j < a.Targets.Count; j++)
                {
                    var st = W.St[a.Targets[j]];
                    double pw = a.Power[j];
                    switch (a.Type)
                    {
                        case TriggerType.AreaMove:
                            st.Dx = a.FromX[j] + a.Dx * k * pw;
                            st.Dy = a.FromY[j] + a.Dy * k * pw;
                            break;
                        case TriggerType.AreaRotate:
                            st.Rot = a.Deg * k * pw;
                            break;
                        case TriggerType.AreaScale:
                            st.SX = 1 + (a.ScaleX - 1) * k * pw;
                            st.SY = 1 + (a.ScaleY - 1) * k * pw;
                            break;
                        case TriggerType.AreaFade:
                            st.Alpha = 1 + (a.Alpha - 1) * k * pw;
                            break;
                        case TriggerType.AreaTint:
                            st.PulseColor = a.Tint;
                            st.PulseT = Math.Max(st.PulseT, a.Dur - a.T + 0.1);
                            break;
                    }
                }
                if (a.T >= a.Dur) _areas.RemoveAt(i);
            }

            for (int i = _moves.Count - 1; i >= 0; i--)
            {
                var a = _moves[i];
                a.T += dt;
                double k = Ease(Math.Clamp(a.T / a.Dur, 0, 1), a.Ease, a.Rate);
                for (int j = 0; j < a.Targets.Count; j++)
                {
                    var s = W.St[a.Targets[j]];
                    s.Dx = a.FromX[j] + a.Dx * k;
                    s.Dy = a.FromY[j] + a.Dy * k;
                }
                if (a.T >= a.Dur) _moves.RemoveAt(i);
            }

            for (int i = _rots.Count - 1; i >= 0; i--)
            {
                var a = _rots[i];
                a.T += dt;
                double k = Ease(Math.Clamp(a.T / a.Dur, 0, 1), a.Ease, 2);
                for (int j = 0; j < a.Targets.Count; j++)
                {
                    var idx = a.Targets[j];
                    var s = W.St[idx];
                    s.Rot = a.From[j] + a.Deg * k;
                    if (a.AroundCenter)
                    {
                        var b = ObjectCatalog.Bounds(W.Level.Objects[idx]);
                        double ox = b.Left + b.Width / 2, oy = b.Top + b.Height / 2;
                        double ang = a.Deg * k * Math.PI / 180.0;
                        double rx = ox - a.Center.X, ry = oy - a.Center.Y;
                        double nx = rx * Math.Cos(ang) - ry * Math.Sin(ang);
                        double ny = rx * Math.Sin(ang) + ry * Math.Cos(ang);
                        s.Dx = a.Cx[j] + (nx - rx);
                        s.Dy = a.Cy[j] + (ny - ry);
                    }
                }
                if (a.T >= a.Dur) _rots.RemoveAt(i);
            }

            for (int i = _scales.Count - 1; i >= 0; i--)
            {
                var a = _scales[i];
                a.T += dt;
                double k = Ease(Math.Clamp(a.T / a.Dur, 0, 1), a.Ease, 2);
                for (int j = 0; j < a.Targets.Count; j++)
                {
                    var s = W.St[a.Targets[j]];
                    s.SX = a.FromX[j] + (a.ToX - a.FromX[j]) * k;
                    s.SY = a.FromY[j] + (a.ToY - a.FromY[j]) * k;
                }
                if (a.T >= a.Dur) _scales.RemoveAt(i);
            }

            for (int i = _alphas.Count - 1; i >= 0; i--)
            {
                var a = _alphas[i];
                a.T += dt;
                double k = Math.Clamp(a.T / a.Dur, 0, 1);
                for (int j = 0; j < a.Targets.Count; j++)
                {
                    var s = W.St[a.Targets[j]];
                    s.Alpha = a.From[j] + (a.To - a.From[j]) * k;
                }
                if (a.T >= a.Dur) _alphas.RemoveAt(i);
            }

            for (int i = _follows.Count - 1; i >= 0; i--)
            {
                var a = _follows[i];
                a.T += dt;
                double px = W.P1.X, py = W.P1.Y;
                double ddx = (px - a.LastPx) * a.Xm;
                double ddy = (py - a.LastPy) * a.Ym;
                if (a.MaxSpeed > 0)
                {
                    double lim = a.MaxSpeed * dt;
                    ddx = Math.Clamp(ddx, -lim, lim);
                    ddy = Math.Clamp(ddy, -lim, lim);
                }
                foreach (var idx in a.Targets)
                {
                    var s = W.St[idx];
                    if (a.PlayerY) s.Dy += ddy;
                    else { s.Dx += ddx; s.Dy += ddy; }
                }
                a.LastPx = px; a.LastPy = py;
                if (a.T >= a.Dur) _follows.RemoveAt(i);
            }

            for (int i = _spawns.Count - 1; i >= 0; i--)
            {
                var a = _spawns[i];
                a.Delay -= dt;
                if (a.Delay <= 0)
                {
                    _spawns.RemoveAt(i);
                    if (a.Group < 0)
                    {
                        int idx = -a.Group - 1;
                        if (idx >= 0 && idx < W.Level.Objects.Count)
                        {
                            var o = W.Level.Objects[idx];
                            if (o.Trigger != null) ExecuteNoDelay(o.Trigger, o);
                        }
                    }
                    else FireGroup(a.Group);
                }
            }

            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                var a = _timers[i];
                a.T -= dt;
                if (a.T <= 0) { _timers.RemoveAt(i); if (a.Group != 0) FireGroup(a.Group); }
            }
        }

        void ExecuteNoDelay(TriggerData t, LevelObject o)
        {
            var c = t.Clone();
            c.SpawnDelay = 0;
            Execute(c, o);
        }

        // ------------------------------------------------------------ easing

        public static double Ease(double t, EasingType e, double rate)
        {
            if (rate <= 0) rate = 2;
            switch (e)
            {
                case EasingType.None: return t;
                case EasingType.EaseIn: return Math.Pow(t, rate);
                case EasingType.EaseOut: return 1 - Math.Pow(1 - t, rate);
                case EasingType.EaseInOut:
                    return t < 0.5 ? Math.Pow(t * 2, rate) / 2 : 1 - Math.Pow((1 - t) * 2, rate) / 2;
                case EasingType.SineInOut: return -(Math.Cos(Math.PI * t) - 1) / 2;
                case EasingType.ExponentialInOut:
                    if (t <= 0) return 0; if (t >= 1) return 1;
                    return t < 0.5 ? Math.Pow(2, 20 * t - 10) / 2 : (2 - Math.Pow(2, -20 * t + 10)) / 2;
                case EasingType.BackInOut:
                    {
                        double c1 = 1.70158, c2 = c1 * 1.525;
                        return t < 0.5
                            ? Math.Pow(2 * t, 2) * ((c2 + 1) * 2 * t - c2) / 2
                            : (Math.Pow(2 * t - 2, 2) * ((c2 + 1) * (t * 2 - 2) + c2) + 2) / 2;
                    }
                case EasingType.ElasticOut:
                    if (t <= 0) return 0; if (t >= 1) return 1;
                    return Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * (2 * Math.PI) / 3) + 1;
                case EasingType.ElasticIn:
                    if (t <= 0) return 0; if (t >= 1) return 1;
                    return -Math.Pow(2, 10 * t - 10) * Math.Sin((t * 10 - 10.75) * (2 * Math.PI) / 3);
                case EasingType.ElasticInOut:
                    if (t <= 0) return 0; if (t >= 1) return 1;
                    return t < 0.5
                        ? -(Math.Pow(2, 20 * t - 10) * Math.Sin((20 * t - 11.125) * (2 * Math.PI) / 4.5)) / 2
                        : (Math.Pow(2, -20 * t + 10) * Math.Sin((20 * t - 11.125) * (2 * Math.PI) / 4.5)) / 2 + 1;
                case EasingType.BounceOut: return BounceOut(t);
                case EasingType.BounceIn: return 1 - BounceOut(1 - t);
                case EasingType.BounceInOut:
                    return t < 0.5 ? (1 - BounceOut(1 - 2 * t)) / 2 : (1 + BounceOut(2 * t - 1)) / 2;
                default: return t;
            }
        }

        static double BounceOut(double t)
        {
            const double n1 = 7.5625, d1 = 2.75;
            if (t < 1 / d1) return n1 * t * t;
            if (t < 2 / d1) { t -= 1.5 / d1; return n1 * t * t + 0.75; }
            if (t < 2.5 / d1) { t -= 2.25 / d1; return n1 * t * t + 0.9375; }
            t -= 2.625 / d1; return n1 * t * t + 0.984375;
        }

        public static string Label(TriggerType t) => t switch
        {
            TriggerType.Move => "Move — двигать группу",
            TriggerType.Rotate => "Rotate — вращать",
            TriggerType.Scale => "Scale — масштаб",
            TriggerType.Alpha => "Alpha — прозрачность",
            TriggerType.Toggle => "Toggle — вкл/выкл группу",
            TriggerType.Spawn => "Spawn — запустить группу",
            TriggerType.Stop => "Stop — остановить группу",
            TriggerType.Pulse => "Pulse — пульс цветом",
            TriggerType.Color => "Color — цвет канала",
            TriggerType.BgColor => "BG — цвет фона",
            TriggerType.GroundColor => "Ground — цвет земли",
            TriggerType.Shake => "Shake — тряска камеры",
            TriggerType.Follow => "Follow — следовать за игроком",
            TriggerType.FollowPlayerY => "Follow Player Y",
            TriggerType.Animate => "Animate — анимация",
            TriggerType.Touch => "Touch — по касанию",
            TriggerType.Count => "Count — по счётчику",
            TriggerType.InstantCount => "Instant Count",
            TriggerType.Pickup => "Pickup — выдать предмет",
            TriggerType.Collision => "Collision — столкновение",
            TriggerType.InstantCollision => "Instant Collision",
            TriggerType.Random => "Random — случайно",
            TriggerType.Timer => "Timer — таймер",
            TriggerType.Camera => "Camera — смещение камеры",
            TriggerType.CameraZoom => "Camera Zoom",
            TriggerType.Song => "Song — музыка по ID",
            TriggerType.Gravity => "Gravity — гравитация",
            TriggerType.Teleport => "Teleport — телепорт игрока",
            TriggerType.OnDeath => "On Death — при смерти",
            TriggerType.Trail => "Trail — след",
            TriggerType.HidePlayer => "Hide Player",
            TriggerType.ShowPlayer => "Show Player",
            TriggerType.Options => "Options — режим/мини/дуал",
            TriggerType.Checkpoint => "Checkpoint",
            TriggerType.End => "End — конец уровня",
            TriggerType.Reverse => "Reverse — обратный ход",
            TriggerType.Keyframe => "Keyframe",
            TriggerType.SpeedChange => "Speed — сменить скорость",
            TriggerType.Gradient => "Gradient — градиент на фоне",
            TriggerType.Particles => "Particles — частицы",
            TriggerType.Shockwave => "Shock Wave — ударная волна",
            TriggerType.Chromatic => "Chromatic — цветные края",
            TriggerType.Pixelate => "Pixelate — крупные пиксели",
            TriggerType.Glitch => "Glitch — глюк картинки",
            TriggerType.InvertColor => "Invert — инверсия цветов",
            TriggerType.Grayscale => "Grayscale — чёрно-белое",
            TriggerType.Blur => "Blur — размытие",
            TriggerType.Vignette => "Vignette — тёмные края",
            TriggerType.Flash => "Flash — вспышка",
            TriggerType.Bump => "Bump — толчок камеры",
            TriggerType.TimeWarp => "Time Warp — скорость времени",
            TriggerType.AreaMove => "Area Move — двигать всё вокруг",
            TriggerType.AreaRotate => "Area Rotate — вращать вокруг",
            TriggerType.AreaScale => "Area Scale — масштаб вокруг",
            TriggerType.AreaFade => "Area Fade — прозрачность вокруг",
            TriggerType.AreaTint => "Area Tint — подкрасить вокруг",
            TriggerType.Sequence => "Sequence — цепочка групп",
            TriggerType.AdvancedFollow => "Adv. Follow — умное следование",
            TriggerType.CameraEdge => "Camera Edge — край камеры",
            TriggerType.CameraGuide => "Camera Guide — подсказка камере",
            TriggerType.CameraStatic => "Camera Static — камера стоит",
            TriggerType.CameraRotate => "Camera Rotate — поворот камеры",
            TriggerType.Event => "Event — событие",
            TriggerType.Reset => "Reset — сброс события",
            TriggerType.ItemEdit => "Item Edit — изменить счётчик",
            TriggerType.ItemCompare => "Item Compare — сравнить счётчик",
            TriggerType.PersistentItem => "Persistent — счётчик не сбрасывать",
            TriggerType.ToggleBlock => "Toggle Block — переключить блоки",
            TriggerType.LinkVisible => "Link Visible — показать связку",
            TriggerType.EnterEffect => "Enter Effect — появление",
            TriggerType.Sfx => "SFX — звук",
            TriggerType.EditSong => "Edit Song — правка музыки",
            TriggerType.GameplayOffset => "Gameplay Offset — сдвиг поля",
            TriggerType.RotateGameplay => "Rotate Gameplay — поворот поля",
            _ => t.ToString()
        };
    }
}

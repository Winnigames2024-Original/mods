using System.Drawing.Drawing2D;
using GEO_DASH.Core;
using GEO_DASH.UI;

namespace GEO_DASH.Render
{
    public class ViewTransform
    {
        public double CamX, CamY, Zoom = 1;
        public Size Client;

        public float Scale => (float)(Client.Height / (Camera.ViewH / Math.Max(0.05, Zoom)));

        public PointF W2S(double x, double y)
        {
            float s = Scale;
            return new PointF(
                (float)((x - CamX) * s + Client.Width / 2.0),
                (float)(Client.Height / 2.0 - (y - CamY) * s));
        }

        public PointF S2W(float sx, float sy)
        {
            float s = Scale;
            return new PointF(
                (float)((sx - Client.Width / 2.0) / s + CamX),
                (float)((Client.Height / 2.0 - sy) / s + CamY));
        }

        public RectangleF WorldRect(double x, double y, double w, double h)
        {
            var p = W2S(x, y + h);
            float s = Scale;
            return new RectangleF(p.X, p.Y, (float)(w * s), (float)(h * s));
        }

        public RectangleF VisibleWorld()
        {
            var a = S2W(0, Client.Height);
            var b = S2W(Client.Width, 0);
            return new RectangleF(a.X, a.Y, b.X - a.X, b.Y - a.Y);
        }
    }

    public static class Renderer
    {
        public static bool ShowHitboxes;
        public static bool ShowTriggers;

        public static void Draw(Graphics g, GameWorld w, ViewTransform v, bool editor)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.CompositingQuality = CompositingQuality.HighSpeed;

            var bg = w.Colors.Eff(ColorSystem.ChBg);
            DrawBackground(g, v, bg, w.Cam.CamX, w.Cam.CamY, w.Level.Settings);

            // градиент поверх фона (триггер Gradient из 2.2)
            if (w.Fx.GradientOn)
            {
                var rect = new Rectangle(0, 0, Math.Max(1, v.Client.Width), Math.Max(1, v.Client.Height));
                using var gb = new LinearGradientBrush(new Rectangle(rect.X, rect.Y - 1, rect.Width, rect.Height + 2),
                    w.Fx.GradTop, w.Fx.GradBottom, LinearGradientMode.Vertical);
                g.FillRectangle(gb, rect);
            }

            DrawGroundAndRoof(g, w, v);

            var vis = v.VisibleWorld();
            vis.Inflate(90, 240);

            // объекты
            for (int i = 0; i < w.Level.Objects.Count; i++)
            {
                var st = w.St.Length > i ? w.St[i] : null;
                if (st != null && !st.Enabled && !editor) continue;
                var o = w.Level.Objects[i];
                var def = ObjectCatalog.Get(o.TypeId);
                double dx = st?.Dx ?? 0, dy = st?.Dy ?? 0;
                double sx = o.ScaleX * (st?.SX ?? 1), sy = o.ScaleY * (st?.SY ?? 1);
                var r = new RectangleF((float)(o.X + dx), (float)(o.Y + dy),
                                       (float)(def.W * sx), (float)(def.H * sy));
                if (r.Right < vis.Left || r.Left > vis.Right) continue;

                if (def.Invisible && !editor && !ShowTriggers) continue;
                if (o.HideInGame && !editor) continue;          // «скрытый» блок — только в редакторе

                double alpha = (st?.Alpha ?? 1) * o.Opacity;
                if (alpha <= 0.01 && !editor) continue;

                Color baseCol = ObjColor(w, o, st);
                DrawObject(g, v, o, def, r, st, baseCol, alpha, editor);
            }

            // игроки
            if (!w.P1.Hidden) DrawPlayer(g, v, w.P1, w.Colors.Eff(ColorSystem.ChP1), w);
            if (w.DualMode && !w.P2.Hidden) DrawPlayer(g, v, w.P2, w.Colors.Eff(ColorSystem.ChP2), w);

            DrawFx(g, w, v);
        }

        static Color ObjColor(GameWorld w, LevelObject o, ObjState st)
        {
            if (st?.PulseColor != null) return st.PulseColor.Value;
            var def = ObjectCatalog.Get(o.TypeId);
            return def.Kind switch
            {
                ObjKind.Block => w.Colors.Eff(o.BaseColorChannel),
                ObjKind.Hazard => w.Colors.Eff(o.BaseColorChannel),
                _ => w.Colors.Eff(o.BaseColorChannel)
            };
        }

        // ------------------------------------------------------------ фон

        static void DrawBackground(Graphics g, ViewTransform v, Color bg, double camX, double camY, LevelSettings st)
        {
            var rect = new Rectangle(0, 0, Math.Max(1, v.Client.Width), Math.Max(1, v.Client.Height));
            float tex = Math.Clamp(v.Scale * 0.95f, 0.35f, 3f);
            // фон едет медленнее уровня — как в игре
            GdBackdrop.DrawBackground(g, rect, st?.BgId ?? 1, bg,
                camX * v.Scale * 0.26, camY * v.Scale * 0.16, tex);
        }

        static void DrawGroundAndRoof(Graphics g, GameWorld w, ViewTransform v)
        {
            var gc = w.Colors.Eff(ColorSystem.ChGround);
            var line = w.Colors.Eff(ColorSystem.ChLine);
            int gid = w.Level.Settings?.GroundId ?? 1;
            float tex = Math.Clamp(v.Scale * 0.95f, 0.35f, 3f);
            double scroll = w.Cam.CamX * v.Scale;

            var top = v.W2S(0, w.GroundY);
            if (top.Y < v.Client.Height)
                GdBackdrop.DrawGround(g,
                    new RectangleF(0, top.Y, v.Client.Width, v.Client.Height - top.Y + 2),
                    gid, gc, line, scroll, tex);

            var roof = v.W2S(0, w.RoofY);
            if (roof.Y > 0)
                GdBackdrop.DrawGround(g,
                    new RectangleF(0, -2, v.Client.Width, roof.Y + 2),
                    gid, gc, line, scroll, tex, true);
        }

        // ------------------------------------------------------- объекты

        static void DrawObject(Graphics g, ViewTransform v, LevelObject o, ObjectDef def,
                               RectangleF wr, ObjState st, Color col, double alpha, bool editor)
        {
            var r = v.WorldRect(wr.X, wr.Y, wr.Width, wr.Height);
            int a = (int)Math.Clamp(alpha * 255, 0, 255);
            Color c = Color.FromArgb(a, col);
            Color detail = Color.FromArgb(a, Blend(col, Color.White, 0.75));

            var state = g.Save();
            if (Math.Abs(o.Rotation + (st?.Rot ?? 0)) > 0.01)
            {
                g.TranslateTransform(r.X + r.Width / 2, r.Y + r.Height / 2);
                g.RotateTransform((float)-(o.Rotation + (st?.Rot ?? 0)));
                g.TranslateTransform(-(r.X + r.Width / 2), -(r.Y + r.Height / 2));
            }

            switch (def.Style)
            {
                case DrawStyle.Block:
                case DrawStyle.Slab:
                    using (var br = new SolidBrush(c)) g.FillRectangle(br, r);
                    using (var pen = new Pen(detail, Math.Max(1.5f, r.Height * 0.09f))) g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                    break;

                case DrawStyle.Brick:
                    using (var br = new SolidBrush(c)) g.FillRectangle(br, r);
                    using (var pen = new Pen(Color.FromArgb(a, Blend(col, Color.White, 0.45)), 1.4f))
                    {
                        g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                        g.DrawLine(pen, r.X, r.Y + r.Height / 2, r.Right, r.Y + r.Height / 2);
                        g.DrawLine(pen, r.X + r.Width / 2, r.Y, r.X + r.Width / 2, r.Y + r.Height / 2);
                        g.DrawLine(pen, r.X + r.Width / 4, r.Y + r.Height / 2, r.X + r.Width / 4, r.Bottom);
                    }
                    break;

                case DrawStyle.Glass:
                    using (var br = new SolidBrush(Color.FromArgb(a / 3, col))) g.FillRectangle(br, r);
                    using (var pen = new Pen(Color.FromArgb(a, Blend(col, Color.White, 0.8)), 2f)) g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                    break;

                case DrawStyle.Platform:
                    using (var br = new SolidBrush(c)) g.FillRectangle(br, r);
                    using (var pen = new Pen(detail, 2f)) g.DrawLine(pen, r.X, r.Y, r.Right, r.Y);
                    break;

                case DrawStyle.SlopeUp:
                case DrawStyle.SlopeDown:
                    {
                        PointF[] pts = def.Style == DrawStyle.SlopeUp
                            ? new[] { new PointF(r.X, r.Bottom), new PointF(r.Right, r.Bottom), new PointF(r.Right, r.Y) }
                            : new[] { new PointF(r.X, r.Bottom), new PointF(r.Right, r.Bottom), new PointF(r.X, r.Y) };
                        using (var br = new SolidBrush(c)) g.FillPolygon(br, pts);
                        using (var pen = new Pen(detail, 2f)) g.DrawPolygon(pen, pts);
                        break;
                    }

                case DrawStyle.Spike:
                case DrawStyle.SpikeSmall:
                case DrawStyle.SpikeHalf:
                    {
                        bool ceil = o.TypeId == 22;
                        PointF[] pts = ceil
                            ? new[] { new PointF(r.X, r.Y), new PointF(r.Right, r.Y), new PointF(r.X + r.Width / 2, r.Bottom) }
                            : new[] { new PointF(r.X, r.Bottom), new PointF(r.Right, r.Bottom), new PointF(r.X + r.Width / 2, r.Y) };
                        using (var br = new SolidBrush(c)) g.FillPolygon(br, pts);
                        using (var pen = new Pen(detail, 1.8f)) g.DrawPolygon(pen, pts);
                        break;
                    }

                case DrawStyle.SpikeRow:
                    {
                        float w3 = r.Width / 3f;
                        for (int k = 0; k < 3; k++)
                        {
                            var pts = new[]
                            {
                                new PointF(r.X + k * w3, r.Bottom),
                                new PointF(r.X + (k + 1) * w3, r.Bottom),
                                new PointF(r.X + (k + 0.5f) * w3, r.Y)
                            };
                            using var br = new SolidBrush(c); g.FillPolygon(br, pts);
                            using var pen = new Pen(detail, 1.4f); g.DrawPolygon(pen, pts);
                        }
                        break;
                    }

                case DrawStyle.Saw:
                case DrawStyle.SawBig:
                    {
                        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                        float rad = Math.Min(r.Width, r.Height) / 2;
                        int teeth = def.Style == DrawStyle.SawBig ? 12 : 8;
                        var pts = new PointF[teeth * 2];
                        double rot = Environment.TickCount / 1000.0 * 6;
                        for (int k = 0; k < teeth * 2; k++)
                        {
                            double ang = rot + k * Math.PI / teeth;
                            double rr = (k % 2 == 0) ? rad : rad * 0.62;
                            pts[k] = new PointF((float)(cx + Math.Cos(ang) * rr), (float)(cy + Math.Sin(ang) * rr));
                        }
                        using (var br = new SolidBrush(Color.FromArgb(a, Blend(col, Color.FromArgb(180, 180, 200), 0.5)))) g.FillPolygon(br, pts);
                        using (var pen = new Pen(detail, 1.6f)) g.DrawPolygon(pen, pts);
                        using (var br2 = new SolidBrush(Color.FromArgb(a, 40, 40, 50)))
                            g.FillEllipse(br2, cx - rad * 0.25f, cy - rad * 0.25f, rad * 0.5f, rad * 0.5f);
                        break;
                    }

                case DrawStyle.HazardBlock:
                    using (var br = new HatchBrush(HatchStyle.WideUpwardDiagonal,
                               Color.FromArgb(a, 255, 80, 80), Color.FromArgb(a, 60, 10, 10)))
                        g.FillRectangle(br, r);
                    break;

                case DrawStyle.Orb:
                    {
                        var oc = OrbColor(o.Orb);
                        float k = (float)(st?.PulseScale ?? 1);
                        var rr = Inflate(r, (k - 1) * r.Width * 0.5f);
                        using (var br = new SolidBrush(Color.FromArgb(a / 4, oc))) g.FillEllipse(br, Inflate(rr, r.Width * 0.3f));
                        using (var pen = new Pen(Color.FromArgb(a, oc), Math.Max(2f, r.Width * 0.14f))) g.DrawEllipse(pen, rr);
                        using (var br = new SolidBrush(Color.FromArgb((int)(a * 0.85), Blend(oc, Color.White, 0.5))))
                            g.FillEllipse(br, Inflate(rr, -r.Width * 0.3f));
                        break;
                    }

                case DrawStyle.Pad:
                    {
                        var pc = PadColor(o.Pad);
                        using (var br = new SolidBrush(Color.FromArgb(a, pc)))
                            g.FillRectangle(br, r.X, r.Y, r.Width, Math.Max(2f, r.Height));
                        using (var br = new SolidBrush(Color.FromArgb(a / 3, pc)))
                            g.FillRectangle(br, r.X, r.Y - r.Height, r.Width, r.Height);
                        break;
                    }

                case DrawStyle.Portal:
                case DrawStyle.Teleport:
                    DrawPortal(g, r, o, a);
                    break;

                case DrawStyle.Deco:
                    using (var br = new SolidBrush(Color.FromArgb(a, col))) g.FillRectangle(br, r);
                    break;
                case DrawStyle.DecoCircle:
                    using (var br = new SolidBrush(Color.FromArgb(a, col))) g.FillEllipse(br, r);
                    break;
                case DrawStyle.DecoTri:
                    using (var br = new SolidBrush(Color.FromArgb(a, col)))
                        g.FillPolygon(br, new[] { new PointF(r.X, r.Bottom), new PointF(r.Right, r.Bottom), new PointF(r.X + r.Width / 2, r.Y) });
                    break;
                case DrawStyle.Glow:
                    using (var br = new SolidBrush(Color.FromArgb(Math.Min(90, a), col))) g.FillEllipse(br, r);
                    break;

                case DrawStyle.Image:
                    {
                        var img = Net.ImageLibrary.Get(o.ImageId);
                        if (img != null)
                        {
                            var at = new System.Drawing.Imaging.ImageAttributes();
                            var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = (float)Math.Clamp(a / 255.0, 0, 1) };
                            at.SetColorMatrix(cm);
                            g.DrawImage(img, new[]
                            {
                                new PointF(r.X, r.Y),
                                new PointF(r.Right, r.Y),
                                new PointF(r.X, r.Bottom)
                            }, new RectangleF(0, 0, img.Width, img.Height), GraphicsUnit.Pixel, at);
                            at.Dispose();
                        }
                        else
                        {
                            using var br = new SolidBrush(Color.FromArgb(Math.Min(120, a), 120, 140, 220));
                            g.FillRectangle(br, r);
                            using var pen = new Pen(Color.FromArgb(a, 200, 215, 255), 2f) { DashStyle = DashStyle.Dash };
                            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                            using var f = Gd_Font();
                            using var tb = new SolidBrush(Color.FromArgb(a, 230, 240, 255));
                            g.DrawString("IMG #" + o.ImageId, f, tb, r.X + 4, r.Y + 4);
                        }
                        break;
                    }

                case DrawStyle.Water:
                case DrawStyle.Lava:
                    DrawLiquid(g, r, def.Style == DrawStyle.Lava, a);
                    break;

                case DrawStyle.Text:
                    using (var br = new SolidBrush(Color.FromArgb(a, col)))
                    using (var f = new Font("Segoe UI", Math.Max(6f, r.Height * 0.6f), FontStyle.Bold))
                        g.DrawString(o.Text ?? "", f, br, r.X, r.Y);
                    break;

                case DrawStyle.Coin:
                    {
                        bool got = st?.Collected ?? false;
                        var cc = got ? Color.FromArgb(60, 255, 215, 90) : Color.FromArgb(a, 255, 215, 90);
                        using (var pen = new Pen(cc, Math.Max(2f, r.Width * 0.12f))) g.DrawEllipse(pen, r);
                        if (!got) using (var br = new SolidBrush(Color.FromArgb(a / 2, 255, 230, 140))) g.FillEllipse(br, Inflate(r, -r.Width * 0.25f));
                        break;
                    }
                case DrawStyle.Pickup:
                    {
                        bool got = st?.Collected ?? false;
                        if (!got)
                        {
                            using var br = new SolidBrush(Color.FromArgb(a, 120, 255, 180));
                            g.FillPolygon(br, Diamond(r));
                        }
                        break;
                    }

                case DrawStyle.Finish:
                    using (var br = new HatchBrush(HatchStyle.LargeCheckerBoard, Color.FromArgb(a, 250, 250, 250), Color.FromArgb(a, 30, 30, 40)))
                        g.FillRectangle(br, r);
                    break;

                case DrawStyle.Checkpoint:
                    using (var pen = new Pen(Color.FromArgb(a, 120, 255, 160), 2f) { DashStyle = DashStyle.Dash })
                        g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                    break;

                case DrawStyle.Trigger:
                    if (editor || ShowTriggers)
                    {
                        using var br = new SolidBrush(Color.FromArgb(160, 70, 130, 255));
                        g.FillRectangle(br, r);
                        using var pen = new Pen(Color.White, 1.2f);
                        g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                        if (r.Width > 18 && o.Trigger != null)
                        {
                            using var f = new Font("Segoe UI", Math.Max(5f, r.Height * 0.3f), FontStyle.Bold);
                            using var tb = new SolidBrush(Color.White);
                            g.DrawString(ShortTrig(o.Trigger.Type), f, tb, r.X + 1, r.Y + 1);
                        }
                    }
                    break;
            }

            g.Restore(state);

            if (ShowHitboxes && (def.Deadly || def.Solid))
            {
                var hb = ObjectCatalog.Hitbox(o);
                var hr = v.WorldRect(hb.X + (st?.Dx ?? 0), hb.Y + (st?.Dy ?? 0), hb.Width, hb.Height);
                using var pen = new Pen(def.Deadly ? Color.Red : Color.Lime, 1f);
                g.DrawRectangle(pen, hr.X, hr.Y, hr.Width, hr.Height);
            }

            if (editor && o.Groups.Count > 0)
            {
                using var f = new Font("Segoe UI", 7f);
                using var br = new SolidBrush(Color.FromArgb(220, 255, 230, 120));
                g.DrawString("G" + string.Join(",", o.Groups), f, br, r.X, r.Y - 11);
            }
        }

        static string ShortTrig(TriggerType t) => t switch
        {
            TriggerType.Move => "MOV",
            TriggerType.Rotate => "ROT",
            TriggerType.Scale => "SCL",
            TriggerType.Alpha => "ALP",
            TriggerType.Toggle => "TGL",
            TriggerType.Spawn => "SPW",
            TriggerType.Stop => "STP",
            TriggerType.Pulse => "PLS",
            TriggerType.Color => "COL",
            TriggerType.BgColor => "BG",
            TriggerType.GroundColor => "GRD",
            TriggerType.Shake => "SHK",
            TriggerType.Follow => "FLW",
            TriggerType.FollowPlayerY => "FPY",
            TriggerType.Touch => "TCH",
            TriggerType.Count => "CNT",
            TriggerType.InstantCount => "ICN",
            TriggerType.Pickup => "PCK",
            TriggerType.Collision => "CLS",
            TriggerType.Random => "RND",
            TriggerType.Timer => "TMR",
            TriggerType.Camera => "CAM",
            TriggerType.CameraZoom => "ZOM",
            TriggerType.Song => "SNG",
            TriggerType.Gravity => "GRV",
            TriggerType.Teleport => "TPT",
            TriggerType.OnDeath => "DTH",
            TriggerType.End => "END",
            TriggerType.SpeedChange => "SPD",
            _ => "TRG"
        };

        static void DrawPortal(Graphics g, RectangleF r, LevelObject o, int a)
        {
            var (c1, label) = PortalLook(o.Portal);
            using (var br = new LinearGradientBrush(
                       new RectangleF(r.X, r.Y, Math.Max(2, r.Width), Math.Max(2, r.Height)),
                       Color.FromArgb(a, c1), Color.FromArgb(a / 2, Blend(c1, Color.White, 0.6)),
                       LinearGradientMode.Vertical))
                g.FillEllipse(br, r.X, r.Y, r.Width, r.Height);
            using (var pen = new Pen(Color.FromArgb(a, Blend(c1, Color.White, 0.5)), Math.Max(2f, r.Width * 0.12f)))
                g.DrawEllipse(pen, r.X, r.Y, r.Width, r.Height);
            if (r.Height > 26)
            {
                using var f = new Font("Segoe UI", Math.Max(6f, r.Width * 0.3f), FontStyle.Bold);
                using var br = new SolidBrush(Color.FromArgb(a, Color.White));
                var sz = g.MeasureString(label, f);
                g.DrawString(label, f, br, r.X + r.Width / 2 - sz.Width / 2, r.Y + r.Height / 2 - sz.Height / 2);
            }
        }

        public static (Color, string) PortalLook(PortalKind k) => k switch
        {
            PortalKind.ModeCube => (Color.FromArgb(90, 230, 120), "CUB"),
            PortalKind.ModeShip => (Color.FromArgb(255, 140, 220), "SHP"),
            PortalKind.ModeBall => (Color.FromArgb(255, 160, 60), "BAL"),
            PortalKind.ModeUfo => (Color.FromArgb(255, 230, 90), "UFO"),
            PortalKind.ModeWave => (Color.FromArgb(90, 220, 255), "WAV"),
            PortalKind.ModeRobot => (Color.FromArgb(170, 170, 190), "ROB"),
            PortalKind.ModeSpider => (Color.FromArgb(180, 80, 255), "SPD"),
            PortalKind.ModeWorm => (Color.FromArgb(120, 255, 170), "WRM"),
            PortalKind.GravityUp => (Color.FromArgb(90, 150, 255), "↑"),
            PortalKind.GravityDown => (Color.FromArgb(255, 240, 120), "↓"),
            PortalKind.GravityToggle => (Color.FromArgb(200, 120, 255), "⇅"),
            PortalKind.Mini => (Color.FromArgb(120, 255, 120), "MIN"),
            PortalKind.Big => (Color.FromArgb(255, 120, 120), "BIG"),
            PortalKind.Dual => (Color.FromArgb(255, 110, 180), "DUA"),
            PortalKind.Single => (Color.FromArgb(150, 200, 255), "SNG"),
            PortalKind.Mirror => (Color.FromArgb(200, 200, 255), "MIR"),
            PortalKind.Unmirror => (Color.FromArgb(160, 160, 200), "NOR"),
            PortalKind.SpeedSlow => (Color.FromArgb(120, 200, 255), "0.5"),
            PortalKind.SpeedNormal => (Color.FromArgb(120, 255, 160), "1x"),
            PortalKind.SpeedFast => (Color.FromArgb(255, 230, 120), "2x"),
            PortalKind.SpeedFaster => (Color.FromArgb(255, 170, 90), "3x"),
            PortalKind.SpeedFastest => (Color.FromArgb(255, 110, 110), "4x"),
            PortalKind.SpeedHyper => (Color.FromArgb(230, 90, 255), "5x"),
            PortalKind.SpeedUltra => (Color.FromArgb(255, 60, 160), "6x"),
            PortalKind.Teleport => (Color.FromArgb(140, 255, 255), "TP"),
            _ => (Color.Gray, "?")
        };

        public static Color OrbColor(OrbKind k) => k switch
        {
            OrbKind.Yellow => Color.FromArgb(255, 230, 90),
            OrbKind.Pink => Color.FromArgb(255, 120, 200),
            OrbKind.Blue => Color.FromArgb(90, 160, 255),
            OrbKind.Green => Color.FromArgb(100, 255, 130),
            OrbKind.Red => Color.FromArgb(255, 90, 90),
            OrbKind.Black => Color.FromArgb(60, 60, 70),
            OrbKind.Dash => Color.FromArgb(120, 255, 230),
            OrbKind.GravityDash => Color.FromArgb(190, 120, 255),
            OrbKind.Spider => Color.FromArgb(170, 70, 255),
            OrbKind.Toggle => Color.FromArgb(230, 230, 230),
            _ => Color.White
        };

        public static Color PadColor(PadKind k) => k switch
        {
            PadKind.Yellow => Color.FromArgb(255, 230, 90),
            PadKind.Pink => Color.FromArgb(255, 120, 200),
            PadKind.Blue => Color.FromArgb(90, 160, 255),
            PadKind.Red => Color.FromArgb(255, 90, 90),
            PadKind.Spider => Color.FromArgb(170, 70, 255),
            _ => Color.White
        };

        // ------------------------------------------------------------ игрок

        public static void DrawPlayer(Graphics g, ViewTransform v, Player p, Color col, GameWorld w)
        {
            if (p.Mode == GameMode.Wave && p.TrailOn && p.WaveTrail.Count > 1)
            {
                using var pen = new Pen(Color.FromArgb(150, col), Math.Max(2f, (float)(p.Size * 0.35 * v.Scale)));
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                var pts = new PointF[Math.Min(p.WaveTrail.Count, 600)];
                int start = p.WaveTrail.Count - pts.Length;
                for (int i = 0; i < pts.Length; i++) pts[i] = v.W2S(p.WaveTrail[start + i].X, p.WaveTrail[start + i].Y);
                if (pts.Length > 1) g.DrawLines(pen, pts);
            }

            var box = p.Box;
            var r = v.WorldRect(box.X, box.Y, box.Width, box.Height);
            var state = g.Save();
            g.TranslateTransform(r.X + r.Width / 2, r.Y + r.Height / 2);
            g.RotateTransform((float)p.Rotation);
            if (p.Mirror) g.ScaleTransform(-1, 1);
            var rr = new RectangleF(-r.Width / 2, -r.Height / 2, r.Width, r.Height);
            Color dark = Blend(col, Color.Black, 0.55);
            Color light = Blend(col, Color.White, 0.55);

            switch (p.Mode)
            {
                case GameMode.Cube:
                    using (var br = new SolidBrush(col)) g.FillRectangle(br, rr);
                    using (var pen = new Pen(dark, Math.Max(2f, rr.Width * 0.11f))) g.DrawRectangle(pen, rr.X, rr.Y, rr.Width, rr.Height);
                    using (var br = new SolidBrush(light)) g.FillRectangle(br, Inflate(rr, -rr.Width * 0.3f));
                    break;

                case GameMode.Ship:
                    using (var br = new SolidBrush(Blend(col, Color.White, 0.2)))
                        g.FillPolygon(br, new[]
                        {
                            new PointF(rr.X - rr.Width * 0.45f, rr.Bottom),
                            new PointF(rr.Right + rr.Width * 0.5f, rr.Y + rr.Height * 0.5f),
                            new PointF(rr.X - rr.Width * 0.45f, rr.Y)
                        });
                    using (var br = new SolidBrush(col)) g.FillRectangle(br, Inflate(rr, -rr.Width * 0.22f));
                    break;

                case GameMode.Ball:
                    using (var br = new SolidBrush(col)) g.FillEllipse(br, rr);
                    using (var pen = new Pen(dark, Math.Max(2f, rr.Width * 0.12f))) g.DrawEllipse(pen, rr);
                    using (var br = new SolidBrush(light))
                        g.FillRectangle(br, rr.X, rr.Y + rr.Height * 0.4f, rr.Width, rr.Height * 0.2f);
                    break;

                case GameMode.Ufo:
                    using (var br = new SolidBrush(Color.FromArgb(180, light)))
                        g.FillEllipse(br, rr.X + rr.Width * 0.18f, rr.Y, rr.Width * 0.64f, rr.Height * 0.62f);
                    using (var br = new SolidBrush(col))
                        g.FillEllipse(br, rr.X - rr.Width * 0.15f, rr.Y + rr.Height * 0.45f, rr.Width * 1.3f, rr.Height * 0.45f);
                    break;

                case GameMode.Wave:
                    using (var br = new SolidBrush(col)) g.FillPolygon(br, Diamond(rr));
                    using (var pen = new Pen(dark, 2f)) g.DrawPolygon(pen, Diamond(rr));
                    break;

                case GameMode.Robot:
                    using (var br = new SolidBrush(col))
                        g.FillRectangle(br, rr.X, rr.Y, rr.Width, rr.Height * 0.62f);
                    using (var br = new SolidBrush(dark))
                    {
                        g.FillRectangle(br, rr.X + rr.Width * 0.12f, rr.Y + rr.Height * 0.62f, rr.Width * 0.24f, rr.Height * 0.38f);
                        g.FillRectangle(br, rr.X + rr.Width * 0.64f, rr.Y + rr.Height * 0.62f, rr.Width * 0.24f, rr.Height * 0.38f);
                    }
                    using (var br = new SolidBrush(light))
                        g.FillRectangle(br, rr.X + rr.Width * 0.2f, rr.Y + rr.Height * 0.15f, rr.Width * 0.6f, rr.Height * 0.25f);
                    break;

                case GameMode.Spider:
                    using (var br = new SolidBrush(col)) g.FillRectangle(br, Inflate(rr, -rr.Width * 0.1f));
                    using (var pen = new Pen(dark, Math.Max(1.6f, rr.Width * 0.09f)))
                    {
                        for (int i = -1; i <= 1; i += 2)
                            for (int k = 0; k < 3; k++)
                            {
                                float y = rr.Y + rr.Height * (0.2f + k * 0.3f);
                                g.DrawLine(pen, rr.X + rr.Width / 2, y,
                                                rr.X + rr.Width / 2 + i * rr.Width * 0.75f, y + rr.Height * 0.25f);
                            }
                    }
                    break;

                case GameMode.Worm:
                    {
                        // червяк: голова + сегменты, при «бурении» внутри блока темнее
                        float seg = rr.Width * 0.46f;
                        for (int i = 3; i >= 0; i--)
                        {
                            float k = 1f - i * 0.16f;
                            var c2 = Blend(col, p.NoClipWorm ? Color.Black : Color.White, i * 0.12);
                            using var br = new SolidBrush(Color.FromArgb(i == 0 ? 255 : 210, c2));
                            float cx = rr.X + rr.Width / 2 - i * seg * 0.62f;
                            float cy = rr.Y + rr.Height / 2 + (float)Math.Sin(Environment.TickCount / 90.0 + i) * rr.Height * 0.08f;
                            g.FillEllipse(br, cx - seg * k / 2, cy - seg * k / 2, seg * k, seg * k);
                        }
                        using (var br = new SolidBrush(Color.White))
                        {
                            float ex = rr.X + rr.Width * 0.62f, ey = rr.Y + rr.Height * 0.38f;
                            g.FillEllipse(br, ex, ey, rr.Width * 0.14f, rr.Width * 0.14f);
                        }
                        if (p.NoClipWorm)
                        {
                            using var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1.5f) { DashStyle = DashStyle.Dot };
                            g.DrawEllipse(pen, Inflate(rr, rr.Width * 0.12f));
                        }
                        break;
                    }
            }

            g.Restore(state);

            if (ShowHitboxes)
            {
                var hr = v.WorldRect(p.InnerBox.X, p.InnerBox.Y, p.InnerBox.Width, p.InnerBox.Height);
                using var pen = new Pen(Color.Orange, 1f);
                g.DrawRectangle(pen, hr.X, hr.Y, hr.Width, hr.Height);
            }
        }

        // ------------------------------------------------------------ утилиты

        public static PointF[] Diamond(RectangleF r) => new[]
        {
            new PointF(r.X, r.Y + r.Height / 2),
            new PointF(r.X + r.Width / 2, r.Y),
            new PointF(r.Right, r.Y + r.Height / 2),
            new PointF(r.X + r.Width / 2, r.Bottom)
        };

        public static RectangleF Inflate(RectangleF r, float d)
        {
            var c = r; c.Inflate(d, d); return c;
        }

        static Font Gd_Font() => new("Segoe UI", 8f, FontStyle.Bold);

        /// <summary>Частицы, ударная волна, виньетка и вспышка — поверх мира.</summary>
        static void DrawFx(Graphics g, GameWorld w, ViewTransform v)
        {
            var fx = w.Fx;

            // частицы
            foreach (var p in fx.Particles)
            {
                var sp = v.W2S(p.X, p.Y);
                float sz = (float)(p.Size * Math.Max(0.4, v.Scale));
                int a = (int)Math.Clamp(255 * (p.Life / Math.Max(0.1, p.MaxLife)), 0, 255);
                using var br = new SolidBrush(Color.FromArgb(a, p.Color));
                g.FillEllipse(br, sp.X - sz / 2, sp.Y - sz / 2, sz, sz);
            }

            // ударная волна — расходящееся кольцо
            if (fx.Shock.On)
            {
                double k = 1 - fx.Shock.K / Math.Max(0.001, fx.Shock.Str);
                var c = v.W2S(fx.ShockX, fx.ShockY);
                float rad = (float)(40 + k * 900 * v.Scale);
                int a = (int)Math.Clamp(200 * fx.Shock.K / Math.Max(0.001, fx.Shock.Str), 0, 200);
                using var pen = new Pen(Color.FromArgb(a, 255, 255, 255), (float)(14 * fx.Shock.Str * (1 - k) + 2));
                g.DrawEllipse(pen, c.X - rad, c.Y - rad, rad * 2, rad * 2);
            }

            // виньетка
            if (fx.Vignette.On)
            {
                var r = new Rectangle(0, 0, Math.Max(1, v.Client.Width), Math.Max(1, v.Client.Height));
                using var path = new GraphicsPath();
                path.AddEllipse(-r.Width * 0.25f, -r.Height * 0.25f, r.Width * 1.5f, r.Height * 1.5f);
                using var br = new PathGradientBrush(path)
                {
                    CenterColor = Color.FromArgb(0, 0, 0, 0),
                    SurroundColors = new[] { Color.FromArgb((int)(220 * fx.Vignette.K), 0, 0, 0) }
                };
                g.FillRectangle(br, r);
            }

            // вспышка на весь экран
            if (fx.Flash.On)
            {
                var r = new Rectangle(0, 0, Math.Max(1, v.Client.Width), Math.Max(1, v.Client.Height));
                using var br = new SolidBrush(Color.FromArgb((int)Math.Clamp(255 * fx.Flash.K, 0, 255), fx.FlashColor));
                g.FillRectangle(br, r);
            }
        }

        /// <summary>Вода и лава: заливка, волна по поверхности и пузырьки.</summary>
        static void DrawLiquid(Graphics g, RectangleF r, bool lava, int alpha)
        {
            if (r.Width < 1 || r.Height < 1) return;
            double t = Environment.TickCount64 / 1000.0;

            Color top = lava ? Color.FromArgb(255, 150, 40) : Color.FromArgb(90, 190, 255);
            Color bottom = lava ? Color.FromArgb(170, 30, 10) : Color.FromArgb(20, 70, 180);
            int a = Math.Clamp(alpha, 0, 255);

            using (var br = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2),
                       Color.FromArgb(a * (lava ? 230 : 170) / 255, top),
                       Color.FromArgb(a * (lava ? 245 : 205) / 255, bottom), LinearGradientMode.Vertical))
                g.FillRectangle(br, r);

            // волна по верхней кромке
            float amp = Math.Min(6f, r.Height * 0.12f);
            var pts = new List<PointF>();
            int seg = Math.Clamp((int)(r.Width / 12), 4, 64);
            for (int i = 0; i <= seg; i++)
            {
                float x = r.X + r.Width * i / seg;
                float y = r.Y + (float)(Math.Sin(t * (lava ? 1.6 : 2.4) + i * 0.6) * amp);
                pts.Add(new PointF(x, y));
            }
            using (var pen = new Pen(Color.FromArgb(Math.Min(255, a), lava ? Color.FromArgb(255, 220, 120) : Color.FromArgb(200, 240, 255)),
                       Math.Max(2f, r.Height * 0.06f)))
                if (pts.Count > 1) g.DrawLines(pen, pts.ToArray());

            // пузырьки
            int bubbles = Math.Clamp((int)(r.Width / 40), 1, 10);
            for (int i = 0; i < bubbles; i++)
            {
                double ph = (t * (lava ? 0.35 : 0.6) + i * 0.27) % 1.0;
                float bx = r.X + r.Width * ((i * 0.37f + 0.12f) % 1f);
                float by = r.Bottom - (float)(r.Height * ph);
                float bs = Math.Max(2f, r.Height * (lava ? 0.09f : 0.06f));
                using var br = new SolidBrush(Color.FromArgb((int)(a * 0.5 * (1 - ph)),
                    lava ? Color.FromArgb(255, 230, 150) : Color.White));
                g.FillEllipse(br, bx, by, bs, bs);
            }
        }

        public static Color Blend(Color a, Color b, double k)
        {
            k = Math.Clamp(k, 0, 1);
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * k),
                (int)(a.G + (b.G - a.G) * k),
                (int)(a.B + (b.B - a.B) * k));
        }
    }
}

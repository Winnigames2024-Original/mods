using System.Drawing.Drawing2D;
using GEO_DASH.Core;

namespace GEO_DASH.UI
{
    /// <summary>Кнопка палитры редактора: рисует сам объект.</summary>
    public class ObjButton : Control
    {
        public LevelObject Model = new() { TypeId = 1 };
        /// <summary>Если задана — кнопка рисует целую конструкцию, а не один объект.</summary>
        public BuildPiece Piece;
        public string Caption = "";
        public bool Selected;
        bool _hover;

        public ObjButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(58, 58);
            MouseEnter += (s, e) => { _hover = true; Invalidate(); };
            MouseLeave += (s, e) => { _hover = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(2, 2, Width - 4, Height - 4);

            using (var path = Gd.Round(r, 10f))
            {
                using var br = new LinearGradientBrush(r,
                    Selected ? Color.FromArgb(70, 110, 220) : Color.FromArgb(40, 42, 86),
                    Selected ? Color.FromArgb(30, 60, 150) : Color.FromArgb(22, 22, 52),
                    LinearGradientMode.Vertical);
                g.FillPath(br, path);
                using var pen = new Pen(Selected ? Gd.Neon : Gd.Ink, Selected ? 3f : 2.4f);
                g.DrawPath(pen, path);
            }
            if (_hover)
                using (var path = Gd.Round(r, 10f))
                using (var br = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    g.FillPath(br, path);

            var inner = new RectangleF(r.X + 8, r.Y + 6, r.Width - 16, r.Height - (string.IsNullOrEmpty(Caption) ? 12 : 20));
            if (Piece != null) DrawPiece(g, inner, Piece);
            else DrawObject(g, inner, Model);

            if (!string.IsNullOrEmpty(Caption))
                using (var f = Gd.Bold(6.5f))
                    Gd.Outlined(g, Caption, f, new RectangleF(r.X, r.Bottom - 15, r.Width, 13),
                        Gd.Paper, thick: 2f);
        }

        /// <summary>Мини-превью целой конструкции: все объекты вписываются в кнопку.</summary>
        public static void DrawPiece(Graphics g, RectangleF r, BuildPiece piece)
        {
            if (piece == null || piece.Objects.Count == 0) return;
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var o in piece.Objects)
            {
                var b = ObjectCatalog.Bounds(o);
                minX = Math.Min(minX, b.X); minY = Math.Min(minY, b.Y);
                maxX = Math.Max(maxX, b.X + b.Width); maxY = Math.Max(maxY, b.Y + b.Height);
            }
            double w = Math.Max(1, maxX - minX), h = Math.Max(1, maxY - minY);
            float k = (float)Math.Min(r.Width / w, r.Height / h);
            float ox = r.X + (r.Width - (float)(w * k)) / 2;
            float oy = r.Y + (r.Height - (float)(h * k)) / 2;

            foreach (var o in piece.Objects)
            {
                var b = ObjectCatalog.Bounds(o);
                // мир Y вверх, экран Y вниз
                var cell = new RectangleF(
                    ox + (float)((b.X - minX) * k),
                    oy + (float)((maxY - (b.Y + b.Height)) * k),
                    Math.Max(2f, (float)(b.Width * k)),
                    Math.Max(2f, (float)(b.Height * k)));
                DrawObject(g, cell, o);
            }
        }

        /// <summary>Мини-превью объекта для палитры и панели правки.</summary>
        public static void DrawObject(Graphics g, RectangleF r, LevelObject o)
        {
            var d = ObjectCatalog.Get(o.TypeId);
            float side = Math.Min(r.Width, r.Height);
            var box = new RectangleF(r.X + (r.Width - side) / 2, r.Y + (r.Height - side) / 2, side, side);

            switch (d.Style)
            {
                case DrawStyle.Block:
                case DrawStyle.Brick:
                case DrawStyle.Glass:
                    using (var br = new SolidBrush(Color.FromArgb(225, 228, 248))) g.FillRectangle(br, box);
                    using (var pen = new Pen(Color.FromArgb(120, 125, 170), 2f)) g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
                    break;
                case DrawStyle.Slab:
                case DrawStyle.Platform:
                    using (var br = new SolidBrush(Color.FromArgb(225, 228, 248)))
                        g.FillRectangle(br, new RectangleF(box.X, box.Y + box.Height * 0.5f, box.Width, box.Height * 0.5f));
                    break;
                case DrawStyle.SlopeUp:
                case DrawStyle.SlopeDown:
                    using (var br = new SolidBrush(Color.FromArgb(225, 228, 248)))
                        g.FillPolygon(br, d.Style == DrawStyle.SlopeUp
                            ? new[] { new PointF(box.Left, box.Bottom), new PointF(box.Right, box.Bottom), new PointF(box.Right, box.Top) }
                            : new[] { new PointF(box.Left, box.Bottom), new PointF(box.Right, box.Bottom), new PointF(box.Left, box.Top) });
                    break;
                case DrawStyle.Spike:
                case DrawStyle.SpikeSmall:
                case DrawStyle.SpikeHalf:
                    using (var br = new SolidBrush(Color.FromArgb(255, 120, 120)))
                        g.FillPolygon(br, o.TypeId == 22
                            ? new[] { new PointF(box.Left, box.Top), new PointF(box.Right, box.Top), new PointF(box.X + box.Width / 2, box.Bottom) }
                            : new[] { new PointF(box.Left, box.Bottom), new PointF(box.Right, box.Bottom), new PointF(box.X + box.Width / 2, box.Top) });
                    break;
                case DrawStyle.SpikeRow:
                    using (var br = new SolidBrush(Color.FromArgb(255, 120, 120)))
                        for (int i = 0; i < 3; i++)
                        {
                            float w3 = box.Width / 3f;
                            g.FillPolygon(br, new[]
                            {
                                new PointF(box.X + i * w3, box.Bottom),
                                new PointF(box.X + (i + 1) * w3, box.Bottom),
                                new PointF(box.X + (i + 0.5f) * w3, box.Top)
                            });
                        }
                    break;
                case DrawStyle.Saw:
                case DrawStyle.SawBig:
                    using (var br = new SolidBrush(Color.FromArgb(200, 205, 225))) g.FillEllipse(br, box);
                    using (var pen = new Pen(Color.FromArgb(60, 62, 90), 2f))
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4;
                            g.DrawLine(pen,
                                (float)(box.X + box.Width / 2 + Math.Cos(a) * box.Width * 0.2),
                                (float)(box.Y + box.Height / 2 + Math.Sin(a) * box.Height * 0.2),
                                (float)(box.X + box.Width / 2 + Math.Cos(a) * box.Width * 0.5),
                                (float)(box.Y + box.Height / 2 + Math.Sin(a) * box.Height * 0.5));
                        }
                    break;
                case DrawStyle.HazardBlock:
                    using (var br = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.FromArgb(255, 90, 90), Color.FromArgb(70, 20, 20)))
                        g.FillRectangle(br, box);
                    break;
                case DrawStyle.Orb:
                    {
                        var c = Render.Renderer.OrbColor(o.Orb);
                        using var pen = new Pen(c, Math.Max(2.5f, box.Width * 0.16f));
                        g.DrawEllipse(pen, box);
                        using var br = new SolidBrush(Color.FromArgb(120, c));
                        g.FillEllipse(br, Render.Renderer.Inflate(box, -box.Width * 0.3f));
                        break;
                    }
                case DrawStyle.Pad:
                    {
                        var c = Render.Renderer.PadColor(o.Pad);
                        using var br = new SolidBrush(c);
                        g.FillRectangle(br, new RectangleF(box.X, box.Bottom - box.Height * 0.25f, box.Width, box.Height * 0.25f));
                        using var br2 = new SolidBrush(Color.FromArgb(70, c));
                        g.FillRectangle(br2, new RectangleF(box.X, box.Y + box.Height * 0.4f, box.Width, box.Height * 0.35f));
                        break;
                    }
                case DrawStyle.Portal:
                case DrawStyle.Teleport:
                    {
                        var (c, label) = Render.Renderer.PortalLook(o.Portal);
                        var pr = new RectangleF(box.X + box.Width * 0.18f, box.Y, box.Width * 0.64f, box.Height);
                        using (var br = new LinearGradientBrush(pr, c, Render.Renderer.Blend(c, Color.White, 0.6), LinearGradientMode.Vertical))
                            g.FillEllipse(br, pr);
                        using (var f = Gd.Bold(6.5f))
                            Gd.Outlined(g, label, f, pr, Color.White, thick: 2f);
                        break;
                    }
                case DrawStyle.Coin:
                    Gd.DrawCoin(g, Render.Renderer.Inflate(box, -box.Width * 0.12f), true);
                    break;
                case DrawStyle.Pickup:
                    using (var br = new SolidBrush(Color.FromArgb(120, 255, 180)))
                        g.FillPolygon(br, Render.Renderer.Diamond(box));
                    break;
                case DrawStyle.Deco:
                    using (var br = new SolidBrush(Color.FromArgb(150, 160, 230))) g.FillRectangle(br, box);
                    break;
                case DrawStyle.DecoCircle:
                    using (var br = new SolidBrush(Color.FromArgb(150, 160, 230))) g.FillEllipse(br, box);
                    break;
                case DrawStyle.DecoTri:
                    using (var br = new SolidBrush(Color.FromArgb(150, 160, 230)))
                        g.FillPolygon(br, new[] { new PointF(box.Left, box.Bottom), new PointF(box.Right, box.Bottom), new PointF(box.X + box.Width / 2, box.Top) });
                    break;
                case DrawStyle.Water:
                case DrawStyle.Lava:
                    {
                        bool lava = d.Style == DrawStyle.Lava;
                        using var br = new LinearGradientBrush(box,
                            lava ? Color.FromArgb(255, 150, 40) : Color.FromArgb(90, 190, 255),
                            lava ? Color.FromArgb(170, 30, 10) : Color.FromArgb(20, 70, 180),
                            LinearGradientMode.Vertical);
                        g.FillRectangle(br, box);
                        using var pen = new Pen(lava ? Color.FromArgb(255, 220, 120) : Color.FromArgb(200, 240, 255), 2.4f);
                        g.DrawLine(pen, box.Left, box.Top + 3, box.Right, box.Top + 3);
                        break;
                    }
                case DrawStyle.Glow:
                    using (var br = new SolidBrush(Color.FromArgb(90, 160, 200, 255))) g.FillEllipse(br, box);
                    break;
                case DrawStyle.Text:
                    using (var f = Gd.Title(9f))
                        Gd.Outlined(g, "Aa", f, box, Gd.Paper, thick: 2.4f);
                    break;
                case DrawStyle.Finish:
                    using (var br = new HatchBrush(HatchStyle.LargeCheckerBoard, Color.White, Color.FromArgb(30, 30, 45)))
                        g.FillRectangle(br, box);
                    break;
                case DrawStyle.Checkpoint:
                    using (var pen = new Pen(Color.FromArgb(120, 255, 160), 2f) { DashStyle = DashStyle.Dash })
                        g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
                    break;
                case DrawStyle.Trigger:
                    using (var br = new SolidBrush(Color.FromArgb(70, 130, 255))) g.FillRectangle(br, box);
                    using (var f = Gd.Bold(6f))
                        Gd.Outlined(g, o.Trigger == null ? "TRG" : ShortTrigger(o.Trigger.Type), f, box, Color.White, thick: 2f);
                    break;
                default:
                    using (var br = new SolidBrush(Color.FromArgb(200, 205, 235))) g.FillRectangle(br, box);
                    break;
            }
        }

        public static string ShortTrigger(TriggerType t) => t switch
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
            TriggerType.Count => "CNT",
            TriggerType.Pickup => "PCK",
            TriggerType.Random => "RND",
            TriggerType.Timer => "TMR",
            TriggerType.Camera => "CAM",
            TriggerType.CameraZoom => "ZOM",
            TriggerType.Song => "SNG",
            TriggerType.Gravity => "GRV",
            TriggerType.Teleport => "TPT",
            TriggerType.End => "END",
            TriggerType.SpeedChange => "SPD",
            TriggerType.Gradient => "GRD2",
            TriggerType.Particles => "PRT",
            TriggerType.Shockwave => "WAVE",
            TriggerType.Chromatic => "CHRM",
            TriggerType.Pixelate => "PIXL",
            TriggerType.Glitch => "GLCH",
            TriggerType.InvertColor => "INV",
            TriggerType.Grayscale => "GRAY",
            TriggerType.Blur => "BLUR",
            TriggerType.Vignette => "VIGN",
            TriggerType.Flash => "FLSH",
            TriggerType.Bump => "BUMP",
            TriggerType.TimeWarp => "TIME",
            TriggerType.AreaMove => "AMOV",
            TriggerType.AreaRotate => "AROT",
            TriggerType.AreaScale => "ASCL",
            TriggerType.AreaFade => "AFAD",
            TriggerType.AreaTint => "ATNT",
            TriggerType.Sequence => "SEQ",
            TriggerType.AdvancedFollow => "AFLW",
            TriggerType.CameraEdge => "CEDG",
            TriggerType.CameraGuide => "CGID",
            TriggerType.CameraStatic => "CSTA",
            TriggerType.CameraRotate => "CROT",
            TriggerType.Event => "EVNT",
            TriggerType.Reset => "RST",
            TriggerType.ItemEdit => "ITEM",
            TriggerType.ItemCompare => "ICMP",
            TriggerType.PersistentItem => "PERS",
            TriggerType.ToggleBlock => "TGLB",
            TriggerType.LinkVisible => "LINK",
            TriggerType.EnterEffect => "ENTR",
            TriggerType.Sfx => "SFX",
            TriggerType.EditSong => "ESNG",
            TriggerType.GameplayOffset => "GOFF",
            TriggerType.RotateGameplay => "RGPL",
            _ => "TRG"
        };
    }
}

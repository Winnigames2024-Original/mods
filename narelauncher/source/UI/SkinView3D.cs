using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    public enum SurfacePhase { Begin, Move, End }

    public class SurfacePaintEventArgs : EventArgs
    {
        public SurfacePhase Phase;
        public Point Texel;
        public bool Erase;
    }

    /// <summary>
    /// Трёхмерный просмотр скина. Каждая грань коробки — аффинное наложение
    /// куска развёртки, поэтому текстура ложится точно, без размытия.
    /// Накладка рисуется отдельными коробками чуть большего размера.
    ///
    /// В режиме рисования строится буфер выбора: каждая грань заливается своим
    /// номером, по нему мышь попадает ровно в тот пиксель развёртки, который
    /// виден под курсором.
    /// </summary>
    public class SkinView3D : BaseControl
    {
        public SkinDocument Doc;
        public bool ShowOverlay = true;
        public bool Spin = true;

        /// <summary>Левая кнопка рисует по модели, средняя — вращает.</summary>
        public bool PaintMode;

        public event EventHandler<SurfacePaintEventArgs> SurfacePaint;

        public float Yaw = 0.6f;
        public float Pitch = 0.18f;

        private readonly Timer _timer;
        private bool _drag;
        private bool _brush;
        private bool _brushErase;
        private Point _last;

        private class FaceDraw
        {
            public PointF[] Points;
            public Rectangle Uv;
            public float Depth;
            public float Shade;
            public bool Overlay;
        }

        private readonly List<FaceDraw> _faces = new List<FaceDraw>();
        private bool _overlayPart;

        public SkinView3D()
        {
            Cursor = Cursors.SizeAll;
            _timer = new Timer { Interval = 40 };
            _timer.Tick += (s, e) =>
            {
                if (Spin && !_drag && !_brush)
                {
                    Yaw += 0.02f;
                    Invalidate();
                }
            };
            _timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) _timer.Dispose();
            base.Dispose(disposing);
        }

        public void SetPaintMode(bool on)
        {
            PaintMode = on;
            Cursor = on ? Cursors.Cross : Cursors.SizeAll;
            Invalidate();
        }

        // ------------------------------------------------------------ мышь

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            _last = e.Location;

            bool wantPaint = PaintMode &&
                             (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right);

            if (wantPaint)
            {
                Point texel;
                if (!TryPick(e.Location, out texel)) return;

                _brush = true;
                _brushErase = e.Button == MouseButtons.Right;
                RaisePaint(SurfacePhase.Begin, texel, _brushErase);
                RaisePaint(SurfacePhase.Move, texel, _brushErase);
                return;
            }

            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
                _drag = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_brush)
            {
                Point texel;
                if (TryPick(e.Location, out texel))
                    RaisePaint(SurfacePhase.Move, texel, _brushErase);
                return;
            }

            if (!_drag) return;

            Yaw += (e.X - _last.X) * 0.02f;
            Pitch += (e.Y - _last.Y) * 0.012f;
            if (Pitch > 1.1f) Pitch = 1.1f;
            if (Pitch < -1.1f) Pitch = -1.1f;
            _last = e.Location;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_brush)
            {
                _brush = false;
                RaisePaint(SurfacePhase.End, Point.Empty, _brushErase);
            }
            _drag = false;
            base.OnMouseUp(e);
        }

        private void RaisePaint(SurfacePhase phase, Point texel, bool erase)
        {
            var h = SurfacePaint;
            if (h == null) return;
            h(this, new SurfacePaintEventArgs { Phase = phase, Texel = texel, Erase = erase });
        }

        // ------------------------------------------------------------ попадание в текстуру

        /// <summary>
        /// По точке на экране находит пиксель развёртки под курсором.
        /// Идём по граням спереди назад; пустые пиксели накладки пропускаем,
        /// чтобы кисть попадала в то тело, которое видно сквозь них.
        /// </summary>
        public bool TryPick(Point mouse, out Point texel)
        {
            texel = new Point(-1, -1);
            if (_faces.Count == 0) return false;

            var image = Doc == null || Doc.Flat == null ? null : Doc.Flat.Image;

            for (int i = _faces.Count - 1; i >= 0; i--)
            {
                var face = _faces[i];
                if (face.Uv.Width <= 0 || face.Uv.Height <= 0) continue;

                float a, b;
                if (!Solve(face, mouse, out a, out b)) continue;
                if (a < 0f || a >= 1f || b < 0f || b >= 1f) continue;

                var hit = new Point(face.Uv.X + (int)(a * face.Uv.Width),
                                    face.Uv.Y + (int)(b * face.Uv.Height));

                if (hit.X < 0 || hit.Y < 0 || hit.X >= SkinFile.Size || hit.Y >= SkinFile.Size) continue;

                // сквозь прозрачную накладку рисуем по тому, что под ней
                if (face.Overlay && image != null)
                {
                    try { if (image.GetPixel(hit.X, hit.Y).A == 0) continue; }
                    catch { }
                }

                texel = hit;
                return true;
            }

            return false;
        }

        /// <summary>Обратное аффинное преобразование: точка экрана -> доли вдоль граней.</summary>
        private static bool Solve(FaceDraw face, Point mouse, out float a, out float b)
        {
            a = 0f; b = 0f;

            var p0 = face.Points[0];
            var p1 = face.Points[1];
            var p2 = face.Points[2];

            float ux = p1.X - p0.X, uy = p1.Y - p0.Y;
            float vx = p2.X - p0.X, vy = p2.Y - p0.Y;

            float det = ux * vy - uy * vx;
            if (Math.Abs(det) < 0.0001f) return false;

            float dx = mouse.X - p0.X, dy = mouse.Y - p0.Y;
            a = (dx * vy - dy * vx) / det;
            b = (ux * dy - uy * dx) / det;
            return true;
        }

        // ------------------------------------------------------------ отрисовка

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);

            var box = new Rectangle(0, 0, Width - 1, Height - 1);
            if (box.Width <= 4 || box.Height <= 4) return;

            Theme.FillRound(g, box, 12, Color.FromArgb(14, 18, 26));
            Theme.DrawRound(g, box, 12, Theme.Border);

            var skin = Doc == null ? null : Doc.Flat;
            if (skin == null || skin.Image == null)
            {
                Theme.Text1(g, "Скин не загружен", Theme.F(9.5f), Theme.Dim, box,
                    StringAlignment.Center, StringAlignment.Center);
                return;
            }

            float scale = Math.Min(Width / 26f, Height / 42f);
            float ox = Width / 2f;
            float oy = Height / 2f + 16f * scale;

            _faces.Clear();
            foreach (var part in SkinLayout.Model(Doc.Arms))
            {
                if (part.Overlay && !ShowOverlay) continue;
                Collect(_faces, part, scale, ox, oy);
            }

            _faces.Sort((a, b) => b.Depth.CompareTo(a.Depth));

            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;

            using (var attrs = new ImageAttributes())
            {
                attrs.SetWrapMode(WrapMode.TileFlipXY);

                foreach (var face in _faces)
                {
                    if (face.Uv.Width <= 0 || face.Uv.Height <= 0) continue;
                    try
                    {
                        g.DrawImage(skin.Image, face.Points,
                                    new Rectangle(face.Uv.X, face.Uv.Y, face.Uv.Width, face.Uv.Height),
                                    GraphicsUnit.Pixel, attrs);

                        // лёгкое затенение боковых и нижних граней — модель читается объёмнее
                        if (face.Shade < 1f)
                        {
                            int alpha = (int)((1f - face.Shade) * 110);
                            using (var brush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                                g.FillPolygon(brush, Quad(face.Points));
                        }
                    }
                    catch { }
                }
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;

            string hint = Doc.Arms == SkinArms.Slim ? "тонкие руки" : "обычные руки";
            if (PaintMode) hint += " · рисуем по модели";
            Theme.Text1(g, hint, Theme.F(8.5f), Theme.Dim, new Rectangle(10, Height - 22, 220, 16));
        }

        /// <summary>Из трёх точек аффинного наложения получаем четырёхугольник для заливки.</summary>
        private static PointF[] Quad(PointF[] p)
        {
            var fourth = new PointF(p[1].X + p[2].X - p[0].X, p[1].Y + p[2].Y - p[0].Y);
            return new[] { p[0], p[1], fourth, p[2] };
        }

        private void Collect(List<FaceDraw> faces, SkinLayout.Part part, float scale, float ox, float oy)
        {
            _overlayPart = part.Overlay;

            float w = part.W / 2f + part.Inflate;
            float h = part.H / 2f + part.Inflate;
            float d = part.D / 2f + part.Inflate;

            float cx = part.Cx, cy = part.Cy, cz = part.Cz;

            // грань задаётся тремя точками: левый верх, правый верх, левый низ
            AddFace(faces, part.Uv[SkinLayout.Up], 1.00f,
                    cx - w, cy + h, cz + d, cx + w, cy + h, cz + d, cx - w, cy + h, cz - d, scale, ox, oy);

            AddFace(faces, part.Uv[SkinLayout.Down], 0.55f,
                    cx - w, cy - h, cz - d, cx + w, cy - h, cz - d, cx - w, cy - h, cz + d, scale, ox, oy);

            AddFace(faces, part.Uv[SkinLayout.Right], 0.80f,
                    cx - w, cy + h, cz + d, cx - w, cy + h, cz - d, cx - w, cy - h, cz + d, scale, ox, oy);

            AddFace(faces, part.Uv[SkinLayout.Front], 0.95f,
                    cx - w, cy + h, cz - d, cx + w, cy + h, cz - d, cx - w, cy - h, cz - d, scale, ox, oy);

            AddFace(faces, part.Uv[SkinLayout.Left], 0.80f,
                    cx + w, cy + h, cz - d, cx + w, cy + h, cz + d, cx + w, cy - h, cz - d, scale, ox, oy);

            AddFace(faces, part.Uv[SkinLayout.Back], 0.70f,
                    cx + w, cy + h, cz + d, cx - w, cy + h, cz + d, cx + w, cy - h, cz + d, scale, ox, oy);
        }

        private void AddFace(List<FaceDraw> faces, Rectangle uv, float shade,
                             float x0, float y0, float z0,
                             float x1, float y1, float z1,
                             float x2, float y2, float z2,
                             float scale, float ox, float oy)
        {
            float d0, d1, d2;
            var p0 = Project(x0, y0, z0, scale, ox, oy, out d0);
            var p1 = Project(x1, y1, z1, scale, ox, oy, out d1);
            var p2 = Project(x2, y2, z2, scale, ox, oy, out d2);

            // отбрасываем грани, повёрнутые от нас
            float cross = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
            if (cross <= 0) return;

            faces.Add(new FaceDraw
            {
                Points = new[] { p0, p1, p2 },
                Uv = uv,
                Depth = (d0 + d1 + d2) / 3f,
                Shade = shade,
                Overlay = _overlayPart
            });
        }

        private PointF Project(float x, float y, float z, float scale, float ox, float oy, out float depth)
        {
            float cosY = (float)Math.Cos(Yaw), sinY = (float)Math.Sin(Yaw);
            float rx = x * cosY + z * sinY;
            float rz = -x * sinY + z * cosY;

            float cosP = (float)Math.Cos(Pitch), sinP = (float)Math.Sin(Pitch);
            float ry = y * cosP - rz * sinP;
            depth = y * sinP + rz * cosP;

            return new PointF(ox + rx * scale, oy - ry * scale);
        }
    }
}

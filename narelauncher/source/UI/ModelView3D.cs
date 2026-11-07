using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Просмотр модели: те же кубы и те же числа uv, что уйдут в .json,
    /// поэтому на экране видно именно то, что покажет игра.
    ///
    /// Рисуем без OpenGL: каждая грань — аффинное наложение куска текстуры
    /// на треугольник из трёх точек, как в просмотре скина. Грани сортируем
    /// по глубине, повёрнутые от нас отбрасываем.
    /// </summary>
    public class ModelView3D : BaseControl
    {
        public BlockModel Model;
        public int Selected = -1;
        public bool Grid = true;

        /// <summary>Выбрали куб щелчком по нему.</summary>
        public event EventHandler Picked;

        public float Yaw = 0.7f;
        public float Pitch = 0.32f;
        public float Zoom = 1f;

        private bool _drag;
        private bool _moved;
        private Point _last;

        private class FaceDraw
        {
            public PointF[] Points;        // левый верх, правый верх, левый низ
            public Bitmap Image;
            public Rectangle Src;
            public float Depth;
            public float Shade;
            public int Cube;
            public bool Flip;              // uv задан «наоборот» — тогда без картинки
        }

        private readonly List<FaceDraw> _faces = new List<FaceDraw>();

        private static readonly Dictionary<string, float> Shades = new Dictionary<string, float>
        {
            { "up", 1.00f }, { "down", 0.55f },
            { "north", 0.95f }, { "south", 0.78f },
            { "east", 0.86f }, { "west", 0.72f }
        };

        public ModelView3D()
        {
            Cursor = Cursors.SizeAll;
        }

        // -------------------------------------------------------------- мышь

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            _last = e.Location;
            _moved = false;
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle) _drag = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_drag) return;

            int dx = e.X - _last.X;
            int dy = e.Y - _last.Y;
            if (Math.Abs(dx) > 2 || Math.Abs(dy) > 2) _moved = true;

            Yaw += dx * 0.012f;
            Pitch += dy * 0.010f;

            if (Pitch > 1.4f) Pitch = 1.4f;
            if (Pitch < -1.4f) Pitch = -1.4f;

            _last = e.Location;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            bool click = _drag && !_moved && e.Button == MouseButtons.Left;
            _drag = false;

            if (!click) return;

            int hit = Pick(e.Location);
            if (hit < 0) return;

            Selected = hit;
            Invalidate();

            var handler = Picked;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            Zoom *= e.Delta > 0 ? 1.12f : 1f / 1.12f;
            if (Zoom < 0.3f) Zoom = 0.3f;
            if (Zoom > 4f) Zoom = 4f;

            Invalidate();
        }

        /// <summary>Какой куб под курсором. Ближние грани проверяем первыми.</summary>
        private int Pick(Point mouse)
        {
            for (int i = _faces.Count - 1; i >= 0; i--)
            {
                float a, b;
                if (!Solve(_faces[i], mouse, out a, out b)) continue;
                if (a < 0f || a >= 1f || b < 0f || b >= 1f) continue;
                return _faces[i].Cube;
            }
            return -1;
        }

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

        // --------------------------------------------------------- отрисовка

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);

            var box = new Rectangle(0, 0, Width - 1, Height - 1);
            if (box.Width <= 8 || box.Height <= 8) return;

            Theme.FillRound(g, box, 12, Color.FromArgb(14, 18, 26));
            Theme.DrawRound(g, box, 12, Theme.Border);

            if (Model == null || Model.Cubes.Count == 0)
            {
                Theme.Text1(g, "Кубов нет — нажмите «Добавить куб»", Theme.F(9.5f), Theme.Dim, box,
                    StringAlignment.Center, StringAlignment.Center);
                return;
            }

            float scale = Math.Min(Width / 34f, Height / 34f) * Zoom;
            float ox = Width / 2f;
            float oy = Height / 2f + 2f * scale;

            if (Grid) PaintGrid(g, scale, ox, oy);

            _faces.Clear();
            for (int i = 0; i < Model.Cubes.Count; i++) Collect(i, scale, ox, oy);

            _faces.Sort((a, b) => a.Depth.CompareTo(b.Depth));

            var saveInterp = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;

            using (var attrs = new ImageAttributes())
            {
                attrs.SetWrapMode(WrapMode.TileFlipXY);

                foreach (var face in _faces)
                {
                    var quad = Quad(face.Points);

                    if (face.Image != null && !face.Flip &&
                        face.Src.Width > 0 && face.Src.Height > 0)
                    {
                        try
                        {
                            g.DrawImage(face.Image, face.Points, face.Src, GraphicsUnit.Pixel, attrs);
                        }
                        catch { Plain(g, quad, face.Shade); }
                    }
                    else Plain(g, quad, face.Shade);

                    if (face.Shade < 1f)
                        using (var veil = new SolidBrush(Color.FromArgb((int)((1f - face.Shade) * 110), 0, 0, 0)))
                            g.FillPolygon(veil, quad);
                }
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = saveInterp;

            // обводка выбранного куба — поверх всего, иначе её не видно
            if (Selected >= 0 && Selected < Model.Cubes.Count)
            {
                using (var pen = new Pen(Theme.Accent, 1.6f))
                    foreach (var face in _faces)
                        if (face.Cube == Selected) g.DrawPolygon(pen, Quad(face.Points));
            }

            Theme.Text1(g, "ЛКМ — повернуть · колесо — ближе · щелчок по кубу — выбрать",
                Theme.F(8.2f), Theme.Dim, new Rectangle(12, Height - 22, Width - 24, 16));
        }

        private static void Plain(Graphics g, PointF[] quad, float shade)
        {
            int tone = (int)(150 * shade) + 40;
            using (var brush = new SolidBrush(Color.FromArgb(255, tone, tone + 6, tone + 16)))
                g.FillPolygon(brush, quad);
        }

        /// <summary>Клетка 16x16 под моделью: видно, где границы блока.</summary>
        private void PaintGrid(Graphics g, float scale, float ox, float oy)
        {
            using (var thin = new Pen(Color.FromArgb(28, 255, 255, 255)))
            using (var edge = new Pen(Color.FromArgb(70, 255, 255, 255)))
            {
                for (int i = 0; i <= 16; i++)
                {
                    float d0, d1;
                    var a = Project(i - 8, -8, -8, scale, ox, oy, out d0);
                    var b = Project(i - 8, -8, 8, scale, ox, oy, out d1);
                    g.DrawLine(i == 0 || i == 16 ? edge : thin, a, b);

                    var c = Project(-8, -8, i - 8, scale, ox, oy, out d0);
                    var d = Project(8, -8, i - 8, scale, ox, oy, out d1);
                    g.DrawLine(i == 0 || i == 16 ? edge : thin, c, d);
                }
            }
        }

        private void Collect(int index, float scale, float ox, float oy)
        {
            var cube = Model.Cubes[index];

            float x1 = cube.Low(0), x2 = cube.High(0);
            float y1 = cube.Low(1), y2 = cube.High(1);
            float z1 = cube.Low(2), z2 = cube.High(2);

            foreach (var side in ModelCube.Sides)
            {
                var face = cube.Face(side);
                if (face.Hidden) continue;

                float[] a, b, c;
                Corners(side, x1, x2, y1, y2, z1, z2, out a, out b, out c);

                Turn(cube, a); Turn(cube, b); Turn(cube, c);

                float d0, d1, d2;
                var p0 = Project(a[0] - 8f, a[1] - 8f, a[2] - 8f, scale, ox, oy, out d0);
                var p1 = Project(b[0] - 8f, b[1] - 8f, b[2] - 8f, scale, ox, oy, out d1);
                var p2 = Project(c[0] - 8f, c[1] - 8f, c[2] - 8f, scale, ox, oy, out d2);

                // грань, повёрнутая от нас, не рисуется
                float cross = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
                if (cross <= 0) continue;

                var points = Spin(new[] { p0, p1, p2 }, face.Rotation);

                var slot = Model.Slot(face.Texture);
                var image = slot == null ? null : slot.Image;

                var uv = cube.UvOf(side);
                var src = Rectangle.Empty;
                bool flip = false;

                if (image != null)
                {
                    float k = image.Width / 16f;
                    float ky = image.Height / 16f;

                    int sx = (int)Math.Round(Math.Min(uv[0], uv[2]) * k);
                    int sy = (int)Math.Round(Math.Min(uv[1], uv[3]) * ky);
                    int sw = (int)Math.Round(Math.Abs(uv[2] - uv[0]) * k);
                    int sh = (int)Math.Round(Math.Abs(uv[3] - uv[1]) * ky);

                    if (sw <= 0 || sh <= 0) flip = true;
                    else src = new Rectangle(sx, sy, sw, sh);
                }

                _faces.Add(new FaceDraw
                {
                    Points = points,
                    Image = image,
                    Src = src,
                    Depth = (d0 + d1 + d2) / 3f,
                    Shade = Shades.ContainsKey(side) ? Shades[side] : 0.9f,
                    Cube = index,
                    Flip = flip
                });
            }
        }

        /// <summary>
        /// Три угла грани в том же порядке, в каком по ней ложится текстура:
        /// левый верх, правый верх, левый низ. Порядок подобран под развёртку
        /// Minecraft, поэтому картинка на кубе лежит как в игре.
        /// </summary>
        private static void Corners(string side,
                                    float x1, float x2, float y1, float y2, float z1, float z2,
                                    out float[] a, out float[] b, out float[] c)
        {
            switch (side)
            {
                case "north":
                    a = new[] { x2, y2, z1 }; b = new[] { x1, y2, z1 }; c = new[] { x2, y1, z1 }; break;
                case "south":
                    a = new[] { x1, y2, z2 }; b = new[] { x2, y2, z2 }; c = new[] { x1, y1, z2 }; break;
                case "west":
                    a = new[] { x1, y2, z1 }; b = new[] { x1, y2, z2 }; c = new[] { x1, y1, z1 }; break;
                case "east":
                    a = new[] { x2, y2, z2 }; b = new[] { x2, y2, z1 }; c = new[] { x2, y1, z2 }; break;
                case "up":
                    a = new[] { x1, y2, z1 }; b = new[] { x2, y2, z1 }; c = new[] { x1, y2, z2 }; break;
                default:    // down
                    a = new[] { x1, y1, z2 }; b = new[] { x2, y1, z2 }; c = new[] { x1, y1, z1 }; break;
            }
        }

        /// <summary>Поворот куба вокруг своей оси — как его понимает Minecraft.</summary>
        private static void Turn(ModelCube cube, float[] point)
        {
            if (string.IsNullOrEmpty(cube.Axis) || Math.Abs(cube.Angle) < 0.001f) return;

            double rad = cube.Angle * Math.PI / 180.0;
            float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);

            float px = point[0] - cube.Origin[0];
            float py = point[1] - cube.Origin[1];
            float pz = point[2] - cube.Origin[2];

            switch (cube.Axis)
            {
                case "x":
                {
                    float ny = py * cos - pz * sin;
                    float nz = py * sin + pz * cos;
                    py = ny; pz = nz;
                    break;
                }
                case "y":
                {
                    float nx = px * cos + pz * sin;
                    float nz = -px * sin + pz * cos;
                    px = nx; pz = nz;
                    break;
                }
                default:    // z
                {
                    float nx = px * cos - py * sin;
                    float ny = px * sin + py * cos;
                    px = nx; py = ny;
                    break;
                }
            }

            point[0] = px + cube.Origin[0];
            point[1] = py + cube.Origin[1];
            point[2] = pz + cube.Origin[2];
        }

        /// <summary>Поворот картинки на грани: просто берём углы по кругу.</summary>
        private static PointF[] Spin(PointF[] p, int rotation)
        {
            var fourth = new PointF(p[1].X + p[2].X - p[0].X, p[1].Y + p[2].Y - p[0].Y);

            switch (((rotation % 360) + 360) % 360)
            {
                case 90: return new[] { p[2], p[0], fourth };
                case 180: return new[] { fourth, p[2], p[1] };
                case 270: return new[] { p[1], fourth, p[0] };
                default: return p;
            }
        }

        private static PointF[] Quad(PointF[] p)
        {
            var fourth = new PointF(p[1].X + p[2].X - p[0].X, p[1].Y + p[2].Y - p[0].Y);
            return new[] { p[0], p[1], fourth, p[2] };
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

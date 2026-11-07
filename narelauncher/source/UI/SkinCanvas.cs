using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    public enum SkinTool
    {
        Pencil, Eraser, Fill, Picker, Lighten, Darken,
        Line, Rect, Ellipse, Replace, Spray
    }

    /// <summary>
    /// Попиксельный редактор развёртки скина. Рисует всегда в активный слой,
    /// а показывает склейку всех видимых слоёв.
    /// </summary>
    public class SkinCanvas : BaseControl
    {
        public SkinDocument Doc;
        public SkinTool Tool = SkinTool.Pencil;
        public Color Ink = Color.FromArgb(255, 220, 90, 90);
        public bool ShowGrid = true;
        public bool ShowRegions = true;

        /// <summary>Сторона квадратной кисти в пикселях развёртки.</summary>
        public int BrushSize = 1;

        /// <summary>Зеркалить мазок внутри своей части тела.</summary>
        public bool MirrorX;

        /// <summary>Заливать прямоугольник и эллипс, а не рисовать контур.</summary>
        public bool FillShapes;

        public event EventHandler Changed;
        public event EventHandler<Color> ColorPicked;

        private class UndoStep
        {
            public int Index;
            public Bitmap Image;
        }

        private readonly List<UndoStep> _undo = new List<UndoStep>();
        private readonly List<UndoStep> _redo = new List<UndoStep>();
        private const int UndoLimit = 40;

        private float _zoom = 8f;
        private PointF _offset;
        private bool _viewReady;
        private bool _painting;
        private bool _panning;
        private Point _lastMouse;
        private Point _lastPixel = new Point(-1, -1);
        private int _lineLimit = 64;

        private bool _shaping;
        private Point _shapeA, _shapeB;
        private bool _shapeErase;

        private static readonly Random Rng = new Random();

        public SkinCanvas()
        {
            Cursor = Cursors.Cross;
        }

        private SkinFile Skin { get { return Doc == null ? null : Doc.Flat; } }

        private Bitmap Target
        {
            get
            {
                if (Doc == null) return null;
                var layer = Doc.Current;
                return layer == null ? null : layer.Image;
            }
        }

        public bool CanUndo { get { return _undo.Count > 0; } }
        public bool CanRedo { get { return _redo.Count > 0; } }

        public static bool IsShape(SkinTool tool)
        {
            return tool == SkinTool.Line || tool == SkinTool.Rect || tool == SkinTool.Ellipse;
        }

        // ------------------------------------------------------------ история

        public void PushUndo()
        {
            if (Doc == null || Target == null) return;

            _undo.Add(new UndoStep { Index = Doc.Active, Image = new Bitmap(Target) });
            if (_undo.Count > UndoLimit)
            {
                _undo[0].Image.Dispose();
                _undo.RemoveAt(0);
            }

            Drop(_redo);
        }

        public void Undo() { Step(_undo, _redo); }
        public void Redo() { Step(_redo, _undo); }

        private void Step(List<UndoStep> from, List<UndoStep> to)
        {
            if (Doc == null || from.Count == 0) return;

            var step = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);

            if (step.Index >= 0 && step.Index < Doc.Layers.Count)
            {
                var layer = Doc.Layers[step.Index];
                to.Add(new UndoStep { Index = step.Index, Image = new Bitmap(layer.Image) });

                if (layer.Image != null) layer.Image.Dispose();
                layer.Image = step.Image;
                Doc.Active = step.Index;
            }
            else step.Image.Dispose();

            Doc.Rebuild();
            Invalidate();
            Raise();
        }

        public void ClearUndo()
        {
            Drop(_undo);
            Drop(_redo);
        }

        private static void Drop(List<UndoStep> list)
        {
            foreach (var s in list) s.Image.Dispose();
            list.Clear();
        }

        private void Raise()
        {
            var h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        /// <summary>Правка слоя снаружи (фильтр, очистка): сохраняем шаг и пересобираем.</summary>
        public void ApplyToLayer(Action<Bitmap> action)
        {
            if (Doc == null || Target == null || action == null) return;

            PushUndo();
            action(Target);
            Doc.Rebuild();
            Invalidate();
            Raise();
        }

        // ------------------------------------------------------------ рисование по модели

        public void BeginSurfaceStroke()
        {
            if (Doc == null) return;
            if (Tool != SkinTool.Picker) PushUndo();
            _lastPixel = new Point(-1, -1);
            _lineLimit = 3;       // соседние точки модели рядом и в развёртке
        }

        public void SurfaceStroke(Point texel, bool erase)
        {
            if (Doc == null || texel.X < 0) return;

            if (Tool == SkinTool.Picker)
            {
                var picked = Skin.Image.GetPixel(texel.X, texel.Y);
                var h = ColorPicked;
                if (h != null && picked.A > 0) h(this, picked);
                return;
            }

            if (Tool == SkinTool.Fill) { FloodFill(texel, erase); return; }
            if (Tool == SkinTool.Replace) { ReplaceColor(texel, erase); return; }

            Stroke(texel, erase);
        }

        public void EndSurfaceStroke()
        {
            _lineLimit = 64;
            _lastPixel = new Point(-1, -1);
            Raise();
        }

        // ------------------------------------------------------------ мышь

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            _lastMouse = e.Location;

            if (e.Button == MouseButtons.Middle)
            {
                _panning = true;
                return;
            }

            if (Doc == null) return;
            if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;

            var p = ToPixel(e.Location);
            if (p.X < 0) return;

            bool erase = e.Button == MouseButtons.Right;

            if (Tool == SkinTool.Picker)
            {
                var c = Skin.Image.GetPixel(p.X, p.Y);
                var h = ColorPicked;
                if (h != null && c.A > 0) h(this, c);
                return;
            }

            if (IsShape(Tool))
            {
                PushUndo();
                _shaping = true;
                _shapeErase = erase;
                _shapeA = p;
                _shapeB = p;
                Invalidate();
                return;
            }

            PushUndo();

            if (Tool == SkinTool.Fill) { FloodFill(p, erase); return; }
            if (Tool == SkinTool.Replace) { ReplaceColor(p, erase); return; }

            _painting = true;
            _lastPixel = new Point(-1, -1);
            Stroke(p, erase);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_panning)
            {
                _offset.X += e.X - _lastMouse.X;
                _offset.Y += e.Y - _lastMouse.Y;
                _lastMouse = e.Location;
                Invalidate();
                return;
            }

            if (_shaping)
            {
                var p = ToPixelClamped(e.Location);
                if (p != _shapeB) { _shapeB = p; Invalidate(); }
                return;
            }

            if (_painting)
            {
                var p = ToPixel(e.Location);
                if (p.X >= 0) Stroke(p, e.Button == MouseButtons.Right);
            }

            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_shaping)
            {
                _shaping = false;
                CommitShape();
            }
            else if (_painting) Raise();

            _painting = false;
            _panning = false;
            base.OnMouseUp(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            float before = _zoom;
            _zoom *= e.Delta > 0 ? 1.15f : 1f / 1.15f;
            if (_zoom < 2f) _zoom = 2f;
            if (_zoom > 42f) _zoom = 42f;

            // масштабируем вокруг курсора
            float k = _zoom / before;
            _offset.X = e.X - (e.X - _offset.X) * k;
            _offset.Y = e.Y - (e.Y - _offset.Y) * k;

            Invalidate();
        }

        public void ResetView()
        {
            if (Width < 10 || Height < 10) return;
            _zoom = Math.Max(2f, Math.Min((Width - 24) / 64f, (Height - 24) / 64f));
            _offset = new PointF((Width - 64 * _zoom) / 2f, (Height - 64 * _zoom) / 2f);
            _viewReady = true;
            Invalidate();
        }

        public void Zoom(float k)
        {
            float before = _zoom;
            _zoom = Math.Max(2f, Math.Min(42f, _zoom * k));

            float cx = Width / 2f, cy = Height / 2f;
            float ratio = _zoom / before;
            _offset.X = cx - (cx - _offset.X) * ratio;
            _offset.Y = cy - (cy - _offset.Y) * ratio;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!_viewReady) ResetView();
        }

        private Point ToPixel(Point mouse)
        {
            if (Skin == null || Skin.Image == null) return new Point(-1, -1);

            int x = (int)Math.Floor((mouse.X - _offset.X) / _zoom);
            int y = (int)Math.Floor((mouse.Y - _offset.Y) / _zoom);

            if (x < 0 || y < 0 || x >= 64 || y >= 64) return new Point(-1, -1);
            return new Point(x, y);
        }

        private Point ToPixelClamped(Point mouse)
        {
            int x = (int)Math.Floor((mouse.X - _offset.X) / _zoom);
            int y = (int)Math.Floor((mouse.Y - _offset.Y) / _zoom);
            return new Point(Math.Max(0, Math.Min(63, x)), Math.Max(0, Math.Min(63, y)));
        }

        // ------------------------------------------------------------ мазок

        private void Stroke(Point p, bool erase)
        {
            if (p == _lastPixel) return;

            // линия между кадрами, чтобы при быстром движении не было разрывов
            if (_lastPixel.X >= 0 &&
                Math.Abs(p.X - _lastPixel.X) <= _lineLimit &&
                Math.Abs(p.Y - _lastPixel.Y) <= _lineLimit)
            {
                var points = new List<Point>();
                LinePoints(_lastPixel, p, points);
                foreach (var q in points) Dab(q.X, q.Y, erase);
            }
            else Dab(p.X, p.Y, erase);

            _lastPixel = p;
            Doc.Rebuild();
            Invalidate();
            Raise();
        }

        /// <summary>Один отпечаток кисти: размер, симметрия, спрей.</summary>
        private void Dab(int x, int y, bool erase)
        {
            if (Tool == SkinTool.Spray && !erase)
            {
                int radius = Math.Max(2, BrushSize + 2);
                int shots = Math.Max(2, radius);
                for (int i = 0; i < shots; i++)
                {
                    int dx = Rng.Next(-radius, radius + 1);
                    int dy = Rng.Next(-radius, radius + 1);
                    if (dx * dx + dy * dy > radius * radius) continue;
                    Mirrored(x + dx, y + dy, false);
                }
                return;
            }

            int half = BrushSize / 2;
            for (int dy = 0; dy < BrushSize; dy++)
                for (int dx = 0; dx < BrushSize; dx++)
                    Mirrored(x - half + dx, y - half + dy, erase);
        }

        private void Mirrored(int x, int y, bool erase)
        {
            Plot(x, y, erase);
            if (!MirrorX) return;

            Rectangle region;
            if (!TryRegion(new Point(x, y), out region)) return;

            int mx = region.X + (region.Right - 1 - x);
            if (mx != x) Plot(mx, y, erase);
        }

        private void Plot(int x, int y, bool erase)
        {
            if (x < 0 || y < 0 || x >= 64 || y >= 64) return;

            var target = Target;
            if (target == null) return;

            if (erase || Tool == SkinTool.Eraser)
            {
                target.SetPixel(x, y, Color.Transparent);
                return;
            }

            if (Tool == SkinTool.Lighten || Tool == SkinTool.Darken)
            {
                var c = target.GetPixel(x, y);
                if (c.A == 0) return;
                int k = Tool == SkinTool.Lighten ? 18 : -18;
                target.SetPixel(x, y, Color.FromArgb(c.A,
                    Clamp(c.R + k), Clamp(c.G + k), Clamp(c.B + k)));
                return;
            }

            target.SetPixel(x, y, Ink);
        }

        private static int Clamp(int v) { return v < 0 ? 0 : v > 255 ? 255 : v; }

        // ------------------------------------------------------------ фигуры

        private void CommitShape()
        {
            foreach (var p in ShapePixels(_shapeA, _shapeB)) Dab(p.X, p.Y, _shapeErase);

            Doc.Rebuild();
            Invalidate();
            Raise();
        }

        private List<Point> ShapePixels(Point a, Point b)
        {
            var points = new List<Point>();

            switch (Tool)
            {
                case SkinTool.Line: LinePoints(a, b, points); break;
                case SkinTool.Rect: RectPoints(a, b, points); break;
                case SkinTool.Ellipse: EllipsePoints(a, b, points); break;
            }

            return points;
        }

        private static void LinePoints(Point a, Point b, List<Point> into)
        {
            int dx = Math.Abs(b.X - a.X), dy = Math.Abs(b.Y - a.Y);
            int sx = a.X < b.X ? 1 : -1, sy = a.Y < b.Y ? 1 : -1;
            int err = dx - dy;
            int x = a.X, y = a.Y;

            while (true)
            {
                into.Add(new Point(x, y));
                if (x == b.X && y == b.Y) break;
                int e2 = err * 2;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx) { err += dx; y += sy; }
            }
        }

        private void RectPoints(Point a, Point b, List<Point> into)
        {
            int x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X);
            int y0 = Math.Min(a.Y, b.Y), y1 = Math.Max(a.Y, b.Y);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (FillShapes || x == x0 || x == x1 || y == y0 || y == y1)
                        into.Add(new Point(x, y));
        }

        private void EllipsePoints(Point a, Point b, List<Point> into)
        {
            int x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X);
            int y0 = Math.Min(a.Y, b.Y), y1 = Math.Max(a.Y, b.Y);

            float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f;
            float rx = Math.Max(0.5f, (x1 - x0) / 2f + 0.25f);
            float ry = Math.Max(0.5f, (y1 - y0) / 2f + 0.25f);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (!Inside(x, y, cx, cy, rx, ry)) continue;

                    if (FillShapes) { into.Add(new Point(x, y)); continue; }

                    // контур — клетка внутри, у которой сосед снаружи
                    bool edge = !Inside(x + 1, y, cx, cy, rx, ry) || !Inside(x - 1, y, cx, cy, rx, ry) ||
                                !Inside(x, y + 1, cx, cy, rx, ry) || !Inside(x, y - 1, cx, cy, rx, ry);
                    if (edge) into.Add(new Point(x, y));
                }
            }
        }

        private static bool Inside(int x, int y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }

        // ------------------------------------------------------------ заливка и замена

        private void FloodFill(Point start, bool erase)
        {
            var target = Target;
            if (target == null) return;

            var seed = target.GetPixel(start.X, start.Y);
            var replacement = erase ? Color.Transparent : Ink;
            if (seed.ToArgb() == replacement.ToArgb()) return;

            // заливка не выходит за пределы своей области развёртки
            Rectangle bounds = RegionAt(start);

            var stack = new Stack<Point>();
            stack.Push(start);

            while (stack.Count > 0)
            {
                var p = stack.Pop();
                if (!bounds.Contains(p)) continue;
                if (target.GetPixel(p.X, p.Y).ToArgb() != seed.ToArgb()) continue;

                target.SetPixel(p.X, p.Y, replacement);

                stack.Push(new Point(p.X + 1, p.Y));
                stack.Push(new Point(p.X - 1, p.Y));
                stack.Push(new Point(p.X, p.Y + 1));
                stack.Push(new Point(p.X, p.Y - 1));
            }

            Doc.Rebuild();
            Invalidate();
            Raise();
        }

        /// <summary>Меняет все пиксели такого же цвета во всём слое.</summary>
        private void ReplaceColor(Point p, bool erase)
        {
            var target = Target;
            if (target == null) return;

            var from = target.GetPixel(p.X, p.Y);
            var to = erase ? Color.Transparent : Ink;
            if (from.ToArgb() == to.ToArgb()) return;

            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    if (target.GetPixel(x, y).ToArgb() == from.ToArgb())
                        target.SetPixel(x, y, to);

            Doc.Rebuild();
            Invalidate();
            Raise();
        }

        private Rectangle RegionAt(Point p)
        {
            Rectangle r;
            return TryRegion(p, out r) ? r : new Rectangle(0, 0, 64, 64);
        }

        private bool TryRegion(Point p, out Rectangle region)
        {
            foreach (var r in SkinLayout.Regions(Doc.Arms))
                if (r.Contains(p)) { region = r; return true; }

            region = new Rectangle(0, 0, 64, 64);
            return false;
        }

        // ------------------------------------------------------------ отрисовка

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var box = new Rectangle(0, 0, Width - 1, Height - 1);
            if (box.Width <= 4 || box.Height <= 4) return;

            Theme.Smooth(g);
            Theme.FillRound(g, box, 12, Color.FromArgb(14, 18, 26));
            Theme.DrawRound(g, box, 12, Theme.Border);

            if (Skin == null || Skin.Image == null)
            {
                Theme.Text1(g, "Скин не загружен", Theme.F(9.5f), Theme.Dim, box,
                    StringAlignment.Center, StringAlignment.Center);
                return;
            }

            if (!_viewReady) ResetView();

            var area = new RectangleF(_offset.X, _offset.Y, 64 * _zoom, 64 * _zoom);

            DrawChecker(g, area);

            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;
            g.DrawImage(Skin.Image, area);

            // предпросмотр фигуры прямо поверх развёртки
            if (_shaping)
            {
                var preview = _shapeErase ? Color.FromArgb(150, 255, 80, 80)
                                          : Color.FromArgb(190, Ink);
                using (var brush = new SolidBrush(preview))
                    foreach (var p in ShapePixels(_shapeA, _shapeB))
                        g.FillRectangle(brush, area.X + p.X * _zoom, area.Y + p.Y * _zoom, _zoom, _zoom);
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (ShowGrid && _zoom >= 6f)
            {
                using (var pen = new Pen(Color.FromArgb(28, 255, 255, 255)))
                {
                    for (int i = 0; i <= 64; i++)
                    {
                        float x = area.X + i * _zoom;
                        float y = area.Y + i * _zoom;
                        g.DrawLine(pen, x, area.Y, x, area.Bottom);
                        g.DrawLine(pen, area.X, y, area.Right, y);
                    }
                }
            }

            if (ShowRegions)
            {
                using (var pen = new Pen(Color.FromArgb(120, Theme.Accent), 1.4f))
                    foreach (var r in SkinLayout.Regions(Doc.Arms))
                        g.DrawRectangle(pen, area.X + r.X * _zoom, area.Y + r.Y * _zoom,
                                        r.Width * _zoom, r.Height * _zoom);
            }

            if (MirrorX)
            {
                using (var pen = new Pen(Color.FromArgb(110, Theme.Accent2), 1.2f))
                {
                    pen.DashStyle = DashStyle.Dash;
                    foreach (var r in SkinLayout.Regions(Doc.Arms))
                    {
                        float mx = area.X + (r.X + r.Width / 2f) * _zoom;
                        g.DrawLine(pen, mx, area.Y + r.Y * _zoom, mx, area.Y + r.Bottom * _zoom);
                    }
                }
            }

            using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255)))
                g.DrawRectangle(pen, area.X, area.Y, area.Width, area.Height);
        }

        private void DrawChecker(Graphics g, RectangleF area)
        {
            float cell = Math.Max(4f, _zoom);
            using (var dark = new SolidBrush(Color.FromArgb(26, 31, 42)))
            using (var light = new SolidBrush(Color.FromArgb(34, 40, 54)))
            {
                g.FillRectangle(dark, area);
                int rows = (int)Math.Ceiling(area.Height / cell);
                int cols = (int)Math.Ceiling(area.Width / cell);
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                        if (((x + y) & 1) == 0)
                            g.FillRectangle(light, area.X + x * cell, area.Y + y * cell,
                                            Math.Min(cell, area.Right - (area.X + x * cell)),
                                            Math.Min(cell, area.Bottom - (area.Y + y * cell)));
            }
        }
    }
}

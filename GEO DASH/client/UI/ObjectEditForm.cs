using GEO_DASH.Core;

namespace GEO_DASH.UI
{
    /// <summary>«Править объект»: группы, цвета, масштаб, поворот, слой, текст, вид портала/орба/пада.</summary>
    public class ObjectEditForm : Form
    {
        readonly Panel _p;
        int _y = 12;

        readonly TextBox _groups, _text;
        readonly NumericUpDown _rot, _sx, _sy, _z, _alpha, _c1, _c2, _tpY;
        readonly ComboBox _portal, _orb, _pad;
        readonly LevelObject _src;

        public ObjectEditForm(LevelObject src)
        {
            _src = src;
            Text = "GEO DASH — Объект";
            ClientSize = new Size(520, 620);
            BackColor = Gd.Deep;
            ForeColor = Gd.Paper;
            Font = Gd.Reg(10f);
            StartPosition = FormStartPosition.CenterParent;

            _p = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Gd.Deep };
            Controls.Add(_p);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Color.FromArgb(18, 16, 44) };
            Controls.Add(bottom);
            var ok = new GdButton
            {
                Text = "ПРИМЕНИТЬ", Icon = GdIcon.Check, Left = 200, Top = 7, Width = 160, Height = 50,
                Color1 = Color.FromArgb(120, 255, 170), Color2 = Color.FromArgb(20, 150, 100)
            };
            ok.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            var ca = new GdButton
            {
                Text = "ОТМЕНА", Icon = GdIcon.Cross, Left = 368, Top = 7, Width = 140, Height = 50,
                Color1 = Color.FromArgb(200, 120, 120), Color2 = Color.FromArgb(120, 40, 50)
            };
            ca.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            bottom.Controls.Add(ok); bottom.Controls.Add(ca);

            var def = ObjectCatalog.Get(src.TypeId);

            // превью
            var preview = new Panel { Left = 14, Top = 10, Width = 90, Height = 90, BackColor = Color.Transparent };
            preview.Paint += (s, e) => ObjButton.DrawObject(e.Graphics, new RectangleF(8, 8, 74, 74), src);
            _p.Controls.Add(preview);
            _p.Controls.Add(new Label
            {
                Text = def.Name, Left = 114, Top = 26, Width = 360, Font = Gd.Title(13f),
                ForeColor = Gd.Neon, BackColor = Color.Transparent
            });
            _y = 112;

            Label("Группы (через запятую)");
            _groups = Tb(string.Join(",", src.Groups));

            Label("Поворот, градусы");
            _rot = Num(-360, 360, src.Rotation, 0);

            Label("Масштаб X / Y");
            _sx = Num(-10, 10, src.ScaleX, 2, 140);
            _sy = Num(-10, 10, src.ScaleY, 2, 140, 168);

            Label("Канал цвета / деталей");
            _c1 = Num(1, 999, src.BaseColorChannel, 0, 140);
            _c2 = Num(1, 999, src.DetailColorChannel, 0, 140, 168);

            Label("Прозрачность (0..1)");
            _alpha = Num(0, 1, src.Opacity, 2);

            Label("Слой (Z)");
            _z = Num(-20, 20, src.ZOrder, 0);

            if (def.Kind == ObjKind.Portal)
            {
                Label("Вид портала");
                _portal = Cb(Enum.GetNames<PortalKind>(), (int)src.Portal);
            }
            if (def.Kind == ObjKind.Orb)
            {
                Label("Вид орба");
                _orb = Cb(Enum.GetNames<OrbKind>(), (int)src.Orb);
            }
            if (def.Kind == ObjKind.Pad)
            {
                Label("Вид пада");
                _pad = Cb(Enum.GetNames<PadKind>(), (int)src.Pad);
            }
            if (src.TypeId == 61 || src.Portal == PortalKind.Teleport)
            {
                Label("Телепорт: Y назначения");
                _tpY = Num(-3000, 3000, src.TeleportTargetY, 0);
            }
            if (def.Kind == ObjKind.Text)
            {
                Label("Текст");
                _text = Tb(src.Text);
            }
        }

        void Label(string t)
        {
            _p.Controls.Add(new Label
            {
                Left = 16, Top = _y, Width = 300, Height = 20, Text = t,
                ForeColor = Gd.Paper, Font = Gd.Bold(10f), BackColor = Color.Transparent
            });
            _y += 24;
        }

        TextBox Tb(string val)
        {
            var t = new TextBox
            {
                Left = 16, Top = _y, Width = 470, Text = val ?? "",
                BackColor = Color.FromArgb(22, 20, 52), ForeColor = Gd.Paper, BorderStyle = BorderStyle.FixedSingle
            };
            _p.Controls.Add(t);
            _y += 36;
            return t;
        }

        NumericUpDown Num(double min, double max, double val, int dec, int w = 470, int left = 16)
        {
            var n = new NumericUpDown
            {
                Left = left, Top = _y, Width = w, DecimalPlaces = dec,
                Increment = dec > 0 ? 0.1m : 1m, Minimum = (decimal)min, Maximum = (decimal)max,
                BackColor = Color.FromArgb(22, 20, 52), ForeColor = Gd.Paper, BorderStyle = BorderStyle.FixedSingle
            };
            n.Value = (decimal)Math.Clamp(val, min, max);
            _p.Controls.Add(n);
            if (left == 16) _y += 36;
            return n;
        }

        ComboBox Cb(string[] items, int sel)
        {
            var c = new ComboBox
            {
                Left = 16, Top = _y, Width = 470, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(22, 20, 52), ForeColor = Gd.Paper, FlatStyle = FlatStyle.Flat
            };
            c.Items.AddRange(items);
            c.SelectedIndex = Math.Clamp(sel, 0, items.Length - 1);
            _p.Controls.Add(c);
            _y += 36;
            return c;
        }

        /// <summary>Применить настройки к объекту.</summary>
        public void ApplyTo(LevelObject o)
        {
            o.Groups = _groups.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => int.TryParse(t.Trim(), out var g) ? g : 0)
                .Where(g => g > 0).Distinct().ToList();
            o.Rotation = (double)_rot.Value;
            o.ScaleX = (double)_sx.Value == 0 ? 1 : (double)_sx.Value;
            o.ScaleY = (double)_sy.Value == 0 ? 1 : (double)_sy.Value;
            o.BaseColorChannel = (int)_c1.Value;
            o.DetailColorChannel = (int)_c2.Value;
            o.Opacity = (double)_alpha.Value;
            o.ZOrder = (int)_z.Value;
            if (_portal != null) o.Portal = (PortalKind)_portal.SelectedIndex;
            if (_orb != null) o.Orb = (OrbKind)_orb.SelectedIndex;
            if (_pad != null) o.Pad = (PadKind)_pad.SelectedIndex;
            if (_tpY != null) o.TeleportTargetY = (double)_tpY.Value;
            if (_text != null) o.Text = _text.Text;
        }
    }
}

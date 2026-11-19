using GEO_DASH.Core;

namespace GEO_DASH.UI
{
    /// <summary>Настройки уровня: режимы игры, скорость, сложность, музыка по ID, защита от копирования.</summary>
    public class LevelSettingsForm : Form
    {
        readonly LevelSettings _s;
        readonly Panel _p;
        int _y = 12;

        public LevelSettingsForm(LevelSettings s)
        {
            _s = s;
            Text = "GEO DASH — Настройки уровня";
            ClientSize = new Size(620, 720);
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
                Text = "СОХРАНИТЬ", Icon = GdIcon.Save, Left = 300, Top = 6, Width = 150, Height = 50,
                Color1 = Color.FromArgb(120, 255, 170), Color2 = Color.FromArgb(20, 150, 100)
            };
            ok.Click += (a, b) => { Apply(); DialogResult = DialogResult.OK; Close(); };
            var ca = new GdButton
            {
                Text = "ОТМЕНА", Icon = GdIcon.Cross, Left = 458, Top = 6, Width = 140, Height = 50,
                Color1 = Color.FromArgb(200, 120, 120), Color2 = Color.FromArgb(120, 40, 50)
            };
            ca.Click += (a, b) => { DialogResult = DialogResult.Cancel; Close(); };
            bottom.Controls.Add(ok); bottom.Controls.Add(ca);

            Build();
        }

        Label L(string t, int w = 240)
        {
            var l = new Label { Left = 14, Top = _y + 4, Width = w, Text = t, ForeColor = Gd.Paper, Font = Gd.Bold(10f), BackColor = Color.Transparent };
            _p.Controls.Add(l);
            return l;
        }

        T Ctl<T>(T c) where T : Control
        {
            c.Left = 266; c.Top = _y; if (c.Width < 10) c.Width = 300;
            c.BackColor = Color.FromArgb(22, 20, 52); c.ForeColor = Gd.Paper;
            _p.Controls.Add(c);
            _y += Math.Max(28, c.Height + 8);
            return c;
        }

        void Section(string t)
        {
            _y += 10;
            var l = new Label
            {
                Left = 12, Top = _y, Width = 560, Text = "— " + t + " —",
                ForeColor = Gd.Neon, Font = Gd.Title(11f), BackColor = Color.Transparent
            };
            _p.Controls.Add(l);
            _y += 30;
        }

        TextBox _name, _desc, _copyPass, _pass, _songName;
        NumericUpDown _songId, _offset, _gravity;
        ComboBox _mode, _speed, _diff, _copyMode;
        CheckBox _mini, _dual, _mirror, _gravUp, _two, _reverse, _fadeIn;
        Button _bg, _ground, _obj;
        int _bgCol, _groundCol, _objCol;

        void Build()
        {
            Section("Общее");
            L("Название"); _name = Ctl(new TextBox { Text = _s.Name, Width = 300, BorderStyle = BorderStyle.FixedSingle });
            L("Описание"); _desc = Ctl(new TextBox { Text = _s.Description, Width = 300, Height = 60, Multiline = true, BorderStyle = BorderStyle.FixedSingle });
            L("Сложность (заявленная автором)");
            _diff = Ctl(new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat });
            foreach (Difficulty d in Enum.GetValues<Difficulty>()) _diff.Items.Add(Difficulties.Label(d));
            _diff.SelectedIndex = (int)_s.Difficulty;

            Section("Режимы игры (старт уровня)");
            L("Режим игрока");
            _mode = Ctl(new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat });
            foreach (GameMode m in Enum.GetValues<GameMode>()) _mode.Items.Add(Modes.Label(m));
            _mode.SelectedIndex = (int)_s.StartMode;

            L("Скорость");
            _speed = Ctl(new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat });
            foreach (SpeedTier t in Enum.GetValues<SpeedTier>()) _speed.Items.Add(Speeds.Label(t));
            _speed.SelectedIndex = (int)_s.StartSpeed;

            _mini = Ctl(new CheckBox { Text = "мини-размер", Checked = _s.StartMini, Width = 300, ForeColor = Gd.Paper, BackColor = Color.Transparent });
            _dual = Ctl(new CheckBox { Text = "дуал (два игрока-копии)", Checked = _s.StartDual, Width = 300, ForeColor = Gd.Paper, BackColor = Color.Transparent });
            _mirror = Ctl(new CheckBox { Text = "зеркальный режим", Checked = _s.StartMirror, Width = 300, ForeColor = Gd.Paper, BackColor = Color.Transparent });
            _gravUp = Ctl(new CheckBox { Text = "старт с перевёрнутой гравитацией", Checked = _s.StartGravityUp, Width = 300, ForeColor = Gd.Paper, BackColor = Color.Transparent });
            _two = Ctl(new CheckBox { Text = "режим на двоих (вторая кнопка — второй игрок)", Checked = _s.TwoPlayer, Width = 340, ForeColor = Gd.Paper, BackColor = Color.Transparent });
            _reverse = Ctl(new CheckBox { Text = "обратный ход уровня", Checked = _s.ReverseGameplay, Width = 300, ForeColor = Gd.Paper, BackColor = Color.Transparent });

            L("Множитель гравитации");
            _gravity = Ctl(new NumericUpDown { Width = 120, DecimalPlaces = 2, Increment = 0.1m, Minimum = 0.2m, Maximum = 3m, Value = (decimal)_s.GravityScale, BorderStyle = BorderStyle.FixedSingle });

            Section("Музыка");
            L("ID трека (Newgrounds; минус — встроенный)");
            _songId = Ctl(new NumericUpDown { Width = 160, Minimum = -99, Maximum = 99999999, Value = _s.SongId, BorderStyle = BorderStyle.FixedSingle });
            L("Название трека"); _songName = Ctl(new TextBox { Text = _s.SongName, Width = 300, BorderStyle = BorderStyle.FixedSingle });
            L("Смещение старта, сек");
            _offset = Ctl(new NumericUpDown { Width = 120, DecimalPlaces = 2, Increment = 0.5m, Minimum = 0, Maximum = 600, Value = (decimal)_s.SongOffsetSec, BorderStyle = BorderStyle.FixedSingle });
            _fadeIn = Ctl(new CheckBox { Text = "плавное появление музыки", Checked = _s.SongFadeIn, Width = 300, ForeColor = Gd.Paper, BackColor = Color.Transparent });

            var pick = new GdButton
            {
                Text = "НАЙТИ ТРЕК ПО ID", Icon = GdIcon.Music, Left = 266, Top = _y, Width = 240, Height = 44,
                Color1 = Color.FromArgb(200, 150, 255), Color2 = Color.FromArgb(100, 50, 180)
            };
            pick.Click += (s, e) =>
            {
                using var f = new MusicForm((int)_songId.Value);
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    _songId.Value = Math.Clamp(f.SongId, _songId.Minimum, _songId.Maximum);
                    _songName.Text = f.SongName;
                }
            };
            _p.Controls.Add(pick);
            _y += 54;

            Section("Защита уровня");
            L("Копирование уровня другими");
            _copyMode = Ctl(new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat });
            _copyMode.Items.AddRange(new object[] { "Запрещено (уровень закрыт)", "Только по паролю", "Свободное копирование" });
            _copyMode.SelectedIndex = Math.Clamp(_s.CopyMode, 0, 2);
            L("Пароль для копирования");
            _copyPass = Ctl(new TextBox { Text = _s.CopyPassword, Width = 300, BorderStyle = BorderStyle.FixedSingle });
            L("Пароль владельца (перезапись на сервере)");
            _pass = Ctl(new TextBox { Text = _s.Password, Width = 300, BorderStyle = BorderStyle.FixedSingle });

            Section("Оформление");
            _bgCol = _s.BgColor; _groundCol = _s.GroundColor; _objCol = _s.ObjColor;
            L("Цвет фона"); _bg = ColorBtn(() => _bgCol, v => _bgCol = v);
            L("Цвет земли"); _ground = ColorBtn(() => _groundCol, v => _groundCol = v);
            L("Цвет объектов (канал 1)"); _obj = ColorBtn(() => _objCol, v => _objCol = v);
            _y += 20;
        }

        Button ColorBtn(Func<int> get, Action<int> set)
        {
            var b = new Button
            {
                Width = 300, Height = 30, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(get()), Text = ""
            };
            b.Click += (s, e) =>
            {
                using var cd = new ColorDialog { Color = Color.FromArgb(get()), FullOpen = true };
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    set(cd.Color.ToArgb());
                    b.BackColor = cd.Color;
                }
            };
            return Ctl(b);
        }

        void Apply()
        {
            _s.Name = _name.Text.Trim();
            _s.Description = _desc.Text;
            _s.Difficulty = (Difficulty)_diff.SelectedIndex;
            _s.StartMode = (GameMode)_mode.SelectedIndex;
            _s.StartSpeed = (SpeedTier)_speed.SelectedIndex;
            _s.StartMini = _mini.Checked;
            _s.StartDual = _dual.Checked;
            _s.StartMirror = _mirror.Checked;
            _s.StartGravityUp = _gravUp.Checked;
            _s.TwoPlayer = _two.Checked;
            _s.ReverseGameplay = _reverse.Checked;
            _s.GravityScale = (double)_gravity.Value;
            _s.SongId = (int)_songId.Value;
            _s.SongName = _songName.Text.Trim();
            _s.SongOffsetSec = (double)_offset.Value;
            _s.SongFadeIn = _fadeIn.Checked;
            _s.CopyMode = _copyMode.SelectedIndex;
            _s.CopyPassword = _copyPass.Text.Trim();
            _s.Password = _pass.Text.Trim();
            _s.BgColor = _bgCol;
            _s.GroundColor = _groundCol;
            _s.ObjColor = _objCol;
        }
    }
}

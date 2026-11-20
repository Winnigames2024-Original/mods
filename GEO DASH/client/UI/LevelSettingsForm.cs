using GEO_DASH.Core;
using GEO_DASH.Net;
using GEO_DASH.Audio;

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

        /// <summary>Подпись под шкалой: скачан трек или нет.</summary>
        void UpdateSongState()
        {
            if (_songState == null) return;
            int id = (int)_songId.Value;
            if (id == 0) { _songState.Text = "Уровень без музыки"; return; }
            if (id < 0) { _songState.Text = "Встроенный трек — качать не нужно"; return; }
            _songState.Text = SongDownloader.Have(id)
                ? $"Трек #{id} уже скачан"
                : $"Трек #{id} ещё не скачан";
        }

        /// <summary>Скачать трек по номеру: сервер, потом Newgrounds, потом файл вручную.</summary>
        async Task GetSongAsync()
        {
            if (_songBusy) return;
            int id = (int)_songId.Value;
            if (id <= 0) { UpdateSongState(); return; }

            _songBusy = true;
            _songBar.Value = 0;
            var pct = new Progress<int>(v => { if (!IsDisposed) _songBar.Value = Math.Clamp(v, 0, 100); });
            var log = new Progress<string>(t => { if (!IsDisposed) _songState.Text = t; });

            string path = await SongDownloader.GetAsync(id, pct, log);
            _songBusy = false;

            if (path != null)
            {
                _songBar.Value = 100;
                _songState.Text = $"Трек #{id} готов";
                if (string.IsNullOrWhiteSpace(_songName.Text)) _songName.Text = "Трек #" + id;
                Toasts.Show("Трек скачан", "#" + id, Toasts.Kind.Download);
                return;
            }

            _songBar.Value = 0;
            string why = string.IsNullOrEmpty(SongDownloader.LastError) ? "не получилось" : SongDownloader.LastError;
            if (!GdDialog.Confirm(this, "Трек не скачался",
                    $"Не вышло забрать трек #{id}: {why}.\n\nВыбрать файл mp3 вручную?"))
            {
                _songState.Text = "Трек #" + id + " не скачан: " + why;
                return;
            }

            path = SongDownloader.PickManually(this, id);
            _songState.Text = path != null ? $"Трек #{id} взят из файла" : "Файл не выбран";
            if (path != null) _songBar.Value = 100;
        }

        /// <summary>Послушать трек прямо из настроек.</summary>
        void TestSong()
        {
            int id = (int)_songId.Value;
            if (id == 0) { _songState.Text = "Музыки у уровня нет"; return; }
            _ = MusicPlayer.PlayAsync(id, (double)_offset.Value, 0.8, false);
            _songState.Text = "Играю трек #" + id;
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
        ProgressBar _songBar;
        Label _songState;
        bool _songBusy;
        NumericUpDown _songId, _offset, _gravity;
        ComboBox _mode, _speed, _diff, _copyMode;
        CheckBox _mini, _dual, _mirror, _gravUp, _two, _reverse, _fadeIn, _platformer, _mp;
        NumericUpDown _maxPlayers;
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
            _platformer = Ctl(new CheckBox { Text = "платформер (ходишь влево-вправо сам)", Checked = _s.PlatformerFreeMove, Width = 340, ForeColor = Gd.Paper, BackColor = Color.Transparent });

            Section("Мультиплеер");
            _mp = Ctl(new CheckBox { Text = "тип уровня: мультиплеер (все на одной карте)", Checked = _s.Multiplayer, Width = 380, ForeColor = Gd.Paper, BackColor = Color.Transparent });
            L("Максимум игроков в одной карте");
            _maxPlayers = Ctl(new NumericUpDown { Width = 120, Minimum = 2, Maximum = 50, Value = Math.Clamp(_s.MaxPlayers, 2, 50), BorderStyle = BorderStyle.FixedSingle });

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

            // ---- скачивание трека по номеру прямо здесь ----
            _songBar = new ProgressBar
            {
                Left = 24, Top = _y, Width = 482, Height = 14, Style = ProgressBarStyle.Continuous
            };
            _p.Controls.Add(_songBar);
            _y += 22;

            _songState = new Label
            {
                Left = 24, Top = _y, Width = 482, Height = 20, BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(205, 220, 255), Font = Gd.Reg(9f)
            };
            _p.Controls.Add(_songState);
            _y += 26;

            var getSong = new GdButton
            {
                Text = "СКАЧАТЬ ТРЕК", Icon = GdIcon.Download, Left = 24, Top = _y, Width = 232, Height = 44,
                Color1 = Color.FromArgb(130, 220, 255), Color2 = Color.FromArgb(25, 95, 180)
            };
            getSong.Click += async (s, e) => await GetSongAsync();
            _p.Controls.Add(getSong);

            var testSong = new GdButton
            {
                Text = "ТЕСТ МУЗЫКИ", Icon = GdIcon.Play, Left = 266, Top = _y, Width = 240, Height = 44,
                Color1 = Color.FromArgb(120, 255, 170), Color2 = Color.FromArgb(20, 150, 100)
            };
            testSong.Click += (s, e) => TestSong();
            _p.Controls.Add(testSong);
            _y += 54;

            UpdateSongState();
            _songId.ValueChanged += (s, e) => UpdateSongState();

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
            _s.PlatformerFreeMove = _platformer.Checked;
            _s.Multiplayer = _mp.Checked;
            _s.MaxPlayers = (int)_maxPlayers.Value;
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

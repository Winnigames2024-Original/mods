using GEO_DASH.Core;

namespace GEO_DASH.UI
{
    /// <summary>Редактор любого триггера: поля подстраиваются под выбранный тип.</summary>
    public class TriggerEditForm : Form
    {
        public TriggerData Data { get; private set; }
        readonly Panel _p;
        readonly ComboBox _type;
        int _y;
        readonly List<Action> _apply = new();

        public TriggerEditForm(TriggerData src)
        {
            Data = src?.Clone() ?? new TriggerData();
            Text = "GEO DASH — Триггер";
            ClientSize = new Size(560, 680);
            BackColor = Gd.Deep; ForeColor = Gd.Paper; Font = Gd.Reg(10f);
            StartPosition = FormStartPosition.CenterParent;

            var top = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Color.FromArgb(18, 16, 44) };
            Controls.Add(top);
            _type = new ComboBox
            {
                Left = 14, Top = 12, Width = 520, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(22, 20, 52), ForeColor = Gd.Paper, FlatStyle = FlatStyle.Flat,
                Font = Gd.Bold(10f)
            };
            foreach (TriggerType t in Enum.GetValues<TriggerType>()) _type.Items.Add(TriggerEngine.Label(t));
            _type.SelectedIndex = (int)Data.Type;
            _type.SelectedIndexChanged += (s, e) => { Collect(); Data.Type = (TriggerType)_type.SelectedIndex; Build(); };
            top.Controls.Add(_type);

            _p = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Gd.Deep };
            Controls.Add(_p);
            _p.BringToFront();

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 62, BackColor = Color.FromArgb(18, 16, 44) };
            Controls.Add(bottom);
            var ok = new GdButton
            {
                Text = "OK", Icon = GdIcon.Check, Left = 270, Top = 6, Width = 130, Height = 48,
                Color1 = Color.FromArgb(120, 255, 170), Color2 = Color.FromArgb(20, 150, 100)
            };
            ok.Click += (s, e) => { Collect(); DialogResult = DialogResult.OK; Close(); };
            var ca = new GdButton
            {
                Text = "ОТМЕНА", Icon = GdIcon.Cross, Left = 408, Top = 6, Width = 130, Height = 48,
                Color1 = Color.FromArgb(200, 120, 120), Color2 = Color.FromArgb(120, 40, 50)
            };
            ca.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            bottom.Controls.Add(ok); bottom.Controls.Add(ca);

            Build();
        }

        void Collect() { foreach (var a in _apply) a(); }

        void Label(string t)
        {
            _p.Controls.Add(new Label
            {
                Left = 14, Top = _y + 4, Width = 250, Text = t,
                ForeColor = Gd.Paper, Font = Gd.Bold(10f), BackColor = Color.Transparent
            });
        }

        void NumF(string label, Func<double> get, Action<double> set, double min, double max, int dec = 2)
        {
            Label(label);
            var n = new NumericUpDown
            {
                Left = 276, Top = _y, Width = 240, DecimalPlaces = dec,
                Increment = dec > 0 ? 0.1m : 1m, Minimum = (decimal)min, Maximum = (decimal)max,
                BackColor = Color.FromArgb(22, 20, 52), ForeColor = Gd.Paper, BorderStyle = BorderStyle.FixedSingle
            };
            n.Value = (decimal)Math.Clamp(get(), min, max);
            _p.Controls.Add(n);
            _apply.Add(() => set((double)n.Value));
            _y += 32;
        }

        void NumI(string label, Func<int> get, Action<int> set, int min, int max)
            => NumF(label, () => get(), v => set((int)v), min, max, 0);

        void Chk(string label, Func<bool> get, Action<bool> set)
        {
            var c = new CheckBox
            {
                Left = 276, Top = _y, Width = 250, Text = label, Checked = get(),
                ForeColor = Gd.Paper, BackColor = Color.Transparent, Font = Gd.Bold(9.5f)
            };
            _p.Controls.Add(c);
            _apply.Add(() => set(c.Checked));
            _y += 30;
        }

        void Enum2<T>(string label, Func<T> get, Action<T> set) where T : struct, Enum
        {
            Label(label);
            var cb = new ComboBox
            {
                Left = 276, Top = _y, Width = 240, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(22, 20, 52), ForeColor = Gd.Paper, FlatStyle = FlatStyle.Flat
            };
            foreach (var v in Enum.GetValues<T>()) cb.Items.Add(v.ToString());
            cb.SelectedIndex = Convert.ToInt32(get());
            _p.Controls.Add(cb);
            _apply.Add(() => set((T)Enum.ToObject(typeof(T), cb.SelectedIndex)));
            _y += 32;
        }

        void ColorPick(string label, Func<int> get, Action<int> set)
        {
            Label(label);
            var b = new Button
            {
                Left = 276, Top = _y, Width = 240, Height = 28, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(get())
            };
            int cur = get();
            b.Click += (s, e) =>
            {
                using var cd = new ColorDialog { Color = Color.FromArgb(cur), FullOpen = true };
                if (cd.ShowDialog() == DialogResult.OK) { cur = cd.Color.ToArgb(); b.BackColor = cd.Color; }
            };
            _p.Controls.Add(b);
            _apply.Add(() => set(cur));
            _y += 34;
        }

        void Build()
        {
            _p.Controls.Clear();
            _apply.Clear();
            _y = 12;

            var d = Data;

            // общее для большинства
            if (d.Type is not (TriggerType.End or TriggerType.Checkpoint or TriggerType.HidePlayer or TriggerType.ShowPlayer))
                NumI("Целевая группа", () => d.TargetGroup, v => d.TargetGroup = v, 0, 9999);

            switch (d.Type)
            {
                case TriggerType.Move:
                case TriggerType.Keyframe:
                    NumF("Сдвиг X (единиц)", () => d.MoveX, v => d.MoveX = v, -9000, 9000, 1);
                    NumF("Сдвиг Y (единиц)", () => d.MoveY, v => d.MoveY = v, -9000, 9000, 1);
                    NumF("Длительность, сек", () => d.Duration, v => d.Duration = v, 0, 60);
                    Enum2("Плавность", () => d.Easing, v => d.Easing = v);
                    NumF("Сила плавности", () => d.EaseRate, v => d.EaseRate = v, 0.1, 10, 1);
                    break;

                case TriggerType.Rotate:
                    NumF("Градусы", () => d.Degrees, v => d.Degrees = v, -3600, 3600, 1);
                    NumF("Длительность, сек", () => d.Duration, v => d.Duration = v, 0, 60);
                    NumI("Группа центра вращения", () => d.CenterGroup, v => d.CenterGroup = v, 0, 9999);
                    Enum2("Плавность", () => d.Easing, v => d.Easing = v);
                    break;

                case TriggerType.Scale:
                    NumF("Масштаб X", () => d.ScaleX, v => d.ScaleX = v, 0.05, 20);
                    NumF("Масштаб Y", () => d.ScaleY, v => d.ScaleY = v, 0.05, 20);
                    NumF("Длительность, сек", () => d.Duration, v => d.Duration = v, 0, 60);
                    Enum2("Плавность", () => d.Easing, v => d.Easing = v);
                    break;

                case TriggerType.Alpha:
                    NumF("Прозрачность (0..1)", () => d.Opacity, v => d.Opacity = v, 0, 1);
                    NumF("Длительность, сек", () => d.Duration, v => d.Duration = v, 0, 60);
                    break;

                case TriggerType.Toggle:
                case TriggerType.Trail:
                case TriggerType.Reverse:
                    Chk("включить (иначе выключить)", () => d.Activate, v => d.Activate = v);
                    break;

                case TriggerType.Spawn:
                    NumF("Задержка, сек", () => d.SpawnDelay, v => d.SpawnDelay = v, 0, 120);
                    Chk("мульти-триггер (можно много раз)", () => d.MultiTrigger, v => d.MultiTrigger = v);
                    break;

                case TriggerType.Pulse:
                    ColorPick("Цвет пульса", () => d.Color, v => d.Color = v);
                    Chk("пульсировать группу (иначе канал)", () => d.PulseToGroup, v => d.PulseToGroup = v);
                    NumI("Канал цвета", () => d.ColorChannel, v => d.ColorChannel = v, 1, 999);
                    NumF("Нарастание, сек", () => d.PulseFadeIn, v => d.PulseFadeIn = v, 0, 10);
                    NumF("Удержание, сек", () => d.PulseHold, v => d.PulseHold = v, 0, 10);
                    NumF("Затухание, сек", () => d.PulseFadeOut, v => d.PulseFadeOut = v, 0, 10);
                    break;

                case TriggerType.Color:
                case TriggerType.BgColor:
                case TriggerType.GroundColor:
                    if (d.Type == TriggerType.Color) NumI("Канал цвета", () => d.ColorChannel, v => d.ColorChannel = v, 1, 999);
                    ColorPick("Цвет", () => d.Color, v => d.Color = v);
                    NumF("Время перехода, сек", () => d.FadeTime, v => d.FadeTime = v, 0, 30);
                    Chk("blending", () => d.Blending, v => d.Blending = v);
                    break;

                case TriggerType.Shake:
                    NumF("Сила", () => d.ShakeStrength, v => d.ShakeStrength = v, 0, 200, 1);
                    NumF("Интервал, сек", () => d.ShakeInterval, v => d.ShakeInterval = v, 0.01, 2);
                    NumF("Длительность, сек", () => d.Duration, v => d.Duration = v, 0.05, 30);
                    break;

                case TriggerType.Follow:
                case TriggerType.FollowPlayerY:
                    NumF("Коэффициент X", () => d.FollowXMod, v => d.FollowXMod = v, -10, 10);
                    NumF("Коэффициент Y", () => d.FollowYMod, v => d.FollowYMod = v, -10, 10);
                    NumF("Длительность, сек (0 = бесконечно)", () => d.Duration, v => d.Duration = v, 0, 600);
                    NumF("Макс. скорость", () => d.FollowMaxSpeed, v => d.FollowMaxSpeed = v, 0, 5000, 0);
                    break;

                case TriggerType.Count:
                case TriggerType.InstantCount:
                    NumI("ID предмета", () => d.ItemId, v => d.ItemId = v, 1, 9999);
                    NumI("Нужное количество", () => d.TargetCount, v => d.TargetCount = v, -999, 9999);
                    Chk("срабатывать при >= (а не ==)", () => d.CountMultiActivate, v => d.CountMultiActivate = v);
                    NumI("Группа «иначе»", () => d.GroupB, v => d.GroupB = v, 0, 9999);
                    break;

                case TriggerType.Pickup:
                    NumI("ID предмета", () => d.ItemId, v => d.ItemId = v, 1, 9999);
                    NumI("Сколько выдать", () => d.Count, v => d.Count = v, -999, 999);
                    break;

                case TriggerType.Collision:
                case TriggerType.InstantCollision:
                    NumI("Группа блока A", () => d.BlockA, v => d.BlockA = v, 0, 9999);
                    NumI("Группа «иначе»", () => d.GroupB, v => d.GroupB = v, 0, 9999);
                    break;

                case TriggerType.Random:
                    NumF("Шанс, %", () => d.Chance, v => d.Chance = v, 0, 100, 1);
                    NumI("Вторая группа", () => d.GroupB, v => d.GroupB = v, 0, 9999);
                    break;

                case TriggerType.Timer:
                    NumF("Время, сек", () => d.TimerSeconds, v => d.TimerSeconds = v, 0, 600);
                    break;

                case TriggerType.Camera:
                    NumF("Смещение X", () => d.CamOffsetX, v => d.CamOffsetX = v, -2000, 2000, 1);
                    NumF("Смещение Y", () => d.CamOffsetY, v => d.CamOffsetY = v, -2000, 2000, 1);
                    NumF("Длительность, сек", () => d.Duration, v => d.Duration = v, 0, 30);
                    Chk("статичная камера", () => d.CamStatic, v => d.CamStatic = v);
                    break;

                case TriggerType.CameraZoom:
                    NumF("Зум", () => d.CamZoom, v => d.CamZoom = v, 0.3, 3);
                    NumF("Длительность, сек", () => d.Duration, v => d.Duration = v, 0, 30);
                    break;

                case TriggerType.Song:
                    NumI("ID трека (минус — встроенный)", () => d.SongId, v => d.SongId = v, -99, 99999999);
                    NumF("Громкость", () => d.SongVolume, v => d.SongVolume = v, 0, 1);
                    NumF("Старт, сек", () => d.SongStartSec, v => d.SongStartSec = v, 0, 600);
                    Chk("зациклить", () => d.SongLoop, v => d.SongLoop = v);
                    break;

                case TriggerType.Gravity:
                    NumI("Направление (-1 вверх, 0 переключить, 1 вниз)", () => d.GravityDir, v => d.GravityDir = v, -1, 1);
                    NumF("Множитель гравитации", () => d.GravityMul, v => d.GravityMul = v, 0.1, 5);
                    break;

                case TriggerType.Teleport:
                    NumF("Y назначения", () => d.TeleportY, v => d.TeleportY = v, -3000, 3000, 1);
                    Chk("сохранить скорость", () => d.TeleportSmooth, v => d.TeleportSmooth = v);
                    break;

                case TriggerType.SpeedChange:
                    Enum2("Скорость", () => d.Speed, v => d.Speed = v);
                    break;

                case TriggerType.Options:
                    Chk("менять режим игры", () => d.OptChangeMode, v => d.OptChangeMode = v);
                    Enum2("Режим", () => d.OptMode, v => d.OptMode = v);
                    Chk("мини", () => d.OptMini, v => d.OptMini = v);
                    Chk("дуал", () => d.OptDual, v => d.OptDual = v);
                    Chk("зеркально", () => d.OptMirror, v => d.OptMirror = v);
                    break;
            }

            // общие флаги активации
            _y += 8;
            Label("— как срабатывает —");
            _y += 26;
            Chk("по касанию игрока (touch)", () => d.TouchTriggered, v => d.TouchTriggered = v);
            Chk("только по Spawn (не по X)", () => d.SpawnTriggered, v => d.SpawnTriggered = v);
            Chk("мульти-триггер", () => d.MultiTrigger, v => d.MultiTrigger = v);
            if (d.Type != TriggerType.Spawn)
                NumF("Задержка перед запуском, сек", () => d.SpawnDelay, v => d.SpawnDelay = v, 0, 120);
        }
    }
}

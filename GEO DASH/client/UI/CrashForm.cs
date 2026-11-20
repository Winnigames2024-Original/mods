using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>
    /// Что случилось с игрой. Показывается в двух случаях:
    ///   • прошлый запуск оборвался — игрок видит, на чём именно, и может
    ///     отправить отчёт ещё раз или открыть папку с логом;
    ///   • зашёл админ — он видит отчёты всех игроков, присланные игрой.
    /// Отчёт уходит на сервер сам, окно нужно, чтобы человек знал, что
    /// произошло, а не гадал, почему игра закрылась.
    /// </summary>
    public class CrashForm : GdForm
    {
        readonly TextBox _text;
        readonly ListBox _list;
        readonly Label _head;
        List<ReportDto> _reports = new();

        readonly string _stage, _tail;
        readonly bool _admin;

        public CrashForm(string stage, string tail, bool admin)
        {
            _stage = stage ?? "";
            _tail = tail ?? "";
            _admin = admin;

            Text = "GEO DASH — отчёт об ошибке";
            ClientSize = new Size(900, 620);
            BgA = Color.FromArgb(60, 40, 110);
            BgId = 6; GroundId = 4;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            _head = new Label
            {
                Left = 28, Top = 96, Width = 844, Height = 48, ForeColor = Gd.Paper,
                Font = Gd.Bold(11f), BackColor = Color.Transparent,
                Text = _stage.Length > 0
                    ? "Прошлый запуск закрылся сам. Игра была занята: " + _stage +
                      "\nОтчёт уже ушёл администратору — делать ничего не нужно."
                    : "Отчёты об ошибках, присланные игрой."
            };
            Host(_head);

            _list = new ListBox
            {
                Left = 28, Top = 150, Width = 300, Height = 380, Visible = _admin,
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, Font = Gd.Reg(9f)
            };
            _list.SelectedIndexChanged += (s, e) => ShowSelected();
            Host(_list);

            _text = new TextBox
            {
                Left = _admin ? 344 : 28, Top = 150, Width = _admin ? 528 : 844, Height = 380,
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9.5f),
                Text = _tail.Length > 0 ? _tail : "Подробностей нет."
            };
            Host(_text);

            Add("ЗАКРЫТЬ", GdIcon.Check, 690, 548, 182, 46,
                Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100), (s, e) => Close());

            Add("ПАПКА С ЛОГОМ", GdIcon.Save, 28, 548, 210, 46,
                Color.FromArgb(160, 190, 255), Color.FromArgb(40, 70, 160), (s, e) => OpenFolder());

            if (_admin)
            {
                Add("ОБНОВИТЬ", GdIcon.Refresh, 250, 548, 170, 46,
                    Color.FromArgb(140, 220, 255), Color.FromArgb(25, 100, 170),
                    async (s, e) => await LoadAsync());
                Add("ПРОЧИТАНО", GdIcon.Check, 432, 548, 180, 46,
                    Color.FromArgb(230, 200, 120), Color.FromArgb(150, 110, 20),
                    async (s, e) => { await ApiClient.ReportsSeenAsync(); await LoadAsync(); });

                Shown += async (s, e) => await LoadAsync();
            }
        }

        protected override void OnRender(Graphics g) =>
            Header(g, "ОТЧЁТ ОБ ОШИБКЕ", _admin ? "что присылают игроки" : "игра закрылась сама");

        void OpenFolder()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = AppConfig.DataDir,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        async Task LoadAsync()
        {
            var r = await ApiClient.ReportsAsync();
            if (!r.Ok || r.Data == null)
            {
                _text.Text = "Список отчётов не загрузился: " + r.Error;
                return;
            }

            _reports = r.Data.Reports ?? new();
            _list.Items.Clear();
            foreach (var x in _reports)
                _list.Items.Add((x.Seen ? "   " : "• ") + Short(x.At) + "  " + x.User + "  " + x.Platform);

            _head.Text = _reports.Count == 0
                ? "Отчётов пока нет — игра ни у кого не падала."
                : "Отчётов: " + r.Data.Total + ", новых: " + r.Data.Fresh +
                  (_stage.Length > 0 ? "\nВаш прошлый запуск оборвался на этапе: " + _stage : "");

            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        }

        static string Short(string iso) =>
            iso.Length >= 16 ? iso.Substring(5, 11).Replace('T', ' ') : iso;

        void ShowSelected()
        {
            int i = _list.SelectedIndex;
            if (i < 0 || i >= _reports.Count) return;
            var x = _reports[i];
            _text.Text =
                "Когда: " + x.At + "\r\n" +
                "Аккаунт: " + (x.User.Length > 0 ? x.User : "—") + "\r\n" +
                "Где: " + x.Platform + " " + x.Version + "\r\n" +
                "Что: " + x.Kind + "\r\n" +
                "На чём оборвалось: " + (x.Stage.Length > 0 ? x.Stage : "неизвестно") + "\r\n\r\n" +
                x.Text.Replace("\n", "\r\n");
        }
    }
}

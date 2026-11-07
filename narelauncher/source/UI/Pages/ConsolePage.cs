using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI.Pages
{
    public class ConsolePage : PageBase
    {
        private readonly TextBox _output = new TextBox();
        private readonly NareButton _clear = new NareButton { Text = "Очистить", Style = BtnStyle.Ghost, Icon = Ico.Trash, Size = new Size(150, 40) };
        private readonly NareButton _copy = new NareButton { Text = "Копировать всё", Style = BtnStyle.Ghost, Size = new Size(180, 40) };
        private readonly NareButton _openLog = new NareButton { Text = "Открыть launcher.log", Style = BtnStyle.Ghost, Icon = Ico.Folder, Size = new Size(210, 40) };
        private readonly NareButton _kill = new NareButton { Text = "Закрыть игру", Style = BtnStyle.Danger, Icon = Ico.Stop, Size = new Size(170, 40) };

        private readonly StringBuilder _buffer = new StringBuilder();
        private bool _dirty;
        private readonly Timer _flush = new Timer { Interval = 300 };

        public ConsolePage()
        {
            Padding = new Padding(0);

            _output.Multiline = true;
            _output.ReadOnly = true;
            _output.ScrollBars = ScrollBars.Vertical;
            _output.WordWrap = false;
            _output.BackColor = Color.FromArgb(12, 15, 21);
            _output.ForeColor = Color.FromArgb(190, 205, 225);
            _output.BorderStyle = BorderStyle.None;
            _output.Font = new Font("Consolas", 9f);
            _output.MaxLength = 0;

            Controls.Add(_output);
            Controls.Add(_clear);
            Controls.Add(_copy);
            Controls.Add(_openLog);
            Controls.Add(_kill);

            _clear.Click += (s, e) =>
            {
                lock (_buffer) _buffer.Clear();
                _output.Clear();
            };

            _copy.Click += (s, e) =>
            {
                try
                {
                    if (_output.TextLength > 0) Clipboard.SetText(_output.Text);
                }
                catch { }
            };

            _openLog.Click += (s, e) =>
            {
                try
                {
                    if (System.IO.File.Exists(Paths.LogFile))
                        System.Diagnostics.Process.Start("notepad.exe", "\"" + Paths.LogFile + "\"");
                    else
                        Host.ShowInfo("Файл журнала ещё не создан.");
                }
                catch (Exception ex) { Host.ShowError("Не удалось открыть журнал", ex); }
            };

            _kill.Click += (s, e) =>
            {
                if (!GameLauncher.IsRunning) { Host.ShowInfo("Игра не запущена."); return; }
                if (Host.Confirm("Принудительно закрыть Minecraft?")) GameLauncher.Kill();
            };

            Log.Line += OnLogLine;

            _flush.Tick += (s, e) => Flush();
            _flush.Start();
        }

        private void OnLogLine(string line)
        {
            lock (_buffer)
            {
                _buffer.AppendLine(line);
                if (_buffer.Length > 200000)
                    _buffer.Remove(0, _buffer.Length - 150000);
                _dirty = true;
            }
        }

        private void Flush()
        {
            if (!_dirty || !IsHandleCreated) return;

            string chunk;
            lock (_buffer)
            {
                chunk = _buffer.ToString();
                _buffer.Clear();
                _dirty = false;
            }
            if (chunk.Length == 0) return;

            // не даём текстовому полю разрастаться бесконечно
            if (_output.TextLength > 300000)
                _output.Text = _output.Text.Substring(_output.TextLength - 150000);

            _output.AppendText(chunk);
        }

        public override void PageShown()
        {
            _dirty = true;
            Flush();
            Layout1();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Log.Line -= OnLogLine;
                _flush.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private void Layout1()
        {
            int pad = 28;
            int top = 64;
            int bottomH = 64;

            _output.SetBounds(pad + 10, top + 10,
                Math.Max(100, Width - pad * 2 - 20),
                Math.Max(60, Height - top - bottomH - 20));

            int by = Height - bottomH + 10;
            _clear.Location = new Point(pad, by);
            _copy.Location = new Point(pad + 162, by);
            _openLog.Location = new Point(pad + 354, by);
            _kill.Location = new Point(Math.Max(pad, Width - pad - _kill.Width), by);

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            Theme.Text1(g, "Консоль", Theme.F(17f, FontStyle.Bold), Theme.Text,
                new Rectangle(28, 16, 400, 28));
            Theme.Text1(g, "Журнал лаунчера и вывод самой игры", Theme.F(9.5f), Theme.Muted,
                new Rectangle(28, 40, 700, 20));

            var box = new Rectangle(28, 64, Math.Max(50, Width - 56), Math.Max(40, Height - 64 - 64));
            Theme.FillRound(g, box, 12, Color.FromArgb(12, 15, 21));
            Theme.DrawRound(g, box, 12, Theme.Border);
        }
    }
}

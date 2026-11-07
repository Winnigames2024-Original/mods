using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI.Pages
{
    /// <summary>
    /// Страница скина: модель, краткая сводка и вход в большой редактор,
    /// который живёт в отдельном окне.
    /// </summary>
    public class SkinPage : PageBase
    {
        private const int Pad = 28;

        private readonly SkinView3D _view = new SkinView3D { Spin = true };

        private readonly NareButton _edit = new NareButton
        { Text = "Открыть редактор скина", Style = BtnStyle.Accent, Icon = Ico.Skin, Size = new Size(252, 44) };

        private readonly NareButton _web = new NareButton
        { Text = "Скин с сайта", Style = BtnStyle.Ghost, Icon = Ico.Search, Size = new Size(176, 40) };

        private readonly NareButton _open = new NareButton
        { Text = "Импорт", Style = BtnStyle.Ghost, Icon = Ico.Folder, Size = new Size(136, 40) };

        private readonly NareButton _save = new NareButton
        { Text = "Экспорт", Style = BtnStyle.Ghost, Icon = Ico.Download, Size = new Size(140, 40) };

        private readonly NareButton _apply = new NareButton
        { Text = "Применить к аккаунту", Style = BtnStyle.Success, Icon = Ico.Check, Size = new Size(224, 40) };

        private readonly NareToggle _spin = new NareToggle { Checked = true };
        private readonly NareToggle _overlay = new NareToggle { Checked = true };

        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private SkinDocument _doc;

        public SkinPage()
        {
            _edit.Click += (s, e) => OpenEditor();
            _web.Click += (s, e) => FromWeb();
            _open.Click += (s, e) => Import();
            _save.Click += (s, e) => Export();
            _apply.Click += (s, e) => ApplyToAccount();

            _spin.CheckedChanged += (s, e) => { _view.Spin = _spin.Checked; };
            _overlay.CheckedChanged += (s, e) => { _view.ShowOverlay = _overlay.Checked; _view.Invalidate(); };

            Controls.Add(_view);
            Controls.Add(_edit);
            Controls.Add(_web);
            Controls.Add(_open);
            Controls.Add(_save);
            Controls.Add(_apply);
            Controls.Add(_spin);
            Controls.Add(_overlay);
            Controls.Add(_status);
        }

        // ---------------------------------------------------------------- данные

        public override void PageShown()
        {
            if (_doc == null && !LoadFromAccount()) LoadDefault();

            Layout1();
            Invalidate();
        }

        private bool LoadFromAccount()
        {
            var account = AccountStore.Selected;
            if (account == null || string.IsNullOrEmpty(account.SkinPath)) return false;

            string layered = Path.ChangeExtension(account.SkinPath, SkinDocument.Extension);

            try
            {
                if (File.Exists(layered))
                {
                    SetDoc(SkinDocument.LoadNare(layered));
                    Say("Открыт ваш скин со слоями.", Theme.Muted);
                    return true;
                }

                if (File.Exists(account.SkinPath))
                {
                    SetDoc(SkinDocument.FromSkin(SkinFile.FromFile(account.SkinPath)));
                    Say("Открыт скин аккаунта «" + account.Name + "».", Theme.Muted);
                    return true;
                }
            }
            catch (Exception ex) { Log.Warn("Скин аккаунта не открылся: " + ex.Message); }

            return false;
        }

        private void LoadDefault()
        {
            try
            {
                SetDoc(SkinDocument.FromSkin(SkinFile.Default(false, Settings.Current.SelectedVersion)));
                Say("Показан стандартный Стив — откройте редактор, чтобы сделать свой.", Theme.Muted);
            }
            catch (Exception ex) { Host.ShowError("Не удалось загрузить стандартный скин", ex); }
        }

        private void SetDoc(SkinDocument doc)
        {
            _doc = doc;
            _view.Doc = doc;
            _view.Invalidate();
            Invalidate();
        }

        private void OpenEditor()
        {
            using (var form = new SkinEditorForm(Host, _doc))
            {
                form.ShowDialog(Host);
                if (form.Doc != null) SetDoc(form.Doc);
            }

            Say("Редактор закрыт. Слоёв: " + (_doc == null ? 0 : _doc.Layers.Count) + ".", Theme.Muted);
        }

        private void FromWeb()
        {
            var account = AccountStore.Selected;
            string nick = account == null ? "" : account.Name;

            using (var dlg = new SkinDownloadForm(nick))
            {
                if (dlg.ShowDialog(Host) != DialogResult.OK || dlg.Result == null) return;

                SetDoc(SkinDocument.FromSkin(dlg.Result));
                Say("Загружен скин «" + dlg.Result.Name + "» с сайта.", Theme.Good);
            }
        }

        private void Import()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Открыть скин";
                dlg.Filter = "Скин (*" + SkinDocument.Extension + ";*.png)|*" + SkinDocument.Extension + ";*.png|" +
                             "NareSkin со слоями (*" + SkinDocument.Extension + ")|*" + SkinDocument.Extension + "|" +
                             "PNG 64×64 (*.png)|*.png|Все файлы (*.*)|*.*";
                if (dlg.ShowDialog(Host) != DialogResult.OK) return;

                try
                {
                    SetDoc(SkinDocument.Open(dlg.FileName));
                    Say("Открыт " + Path.GetFileName(dlg.FileName) + " · слоёв: " + _doc.Layers.Count, Theme.Muted);
                }
                catch (Exception ex) { Host.ShowError("Не удалось открыть скин", ex); }
            }
        }

        private void Export()
        {
            if (_doc == null) return;

            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "Сохранить скин";
                dlg.Filter = "NareSkin со слоями (*" + SkinDocument.Extension + ")|*" + SkinDocument.Extension +
                             "|PNG 64×64 для игры (*.png)|*.png";
                dlg.FileName = Paths.SafeName(_doc.Name) + SkinDocument.Extension;
                if (dlg.ShowDialog(Host) != DialogResult.OK) return;

                try
                {
                    string file = dlg.FileName;
                    string ext = (Path.GetExtension(file) ?? "").ToLowerInvariant();

                    if (dlg.FilterIndex == 2 && ext != ".png")
                    {
                        file = Path.ChangeExtension(file, ".png");
                        ext = ".png";
                    }

                    if (ext == ".png") { _doc.SavePng(file); Say("Сохранена текстура: " + file, Theme.Good); }
                    else
                    {
                        if (ext != SkinDocument.Extension) file = Path.ChangeExtension(file, SkinDocument.Extension);
                        _doc.SaveNare(file);
                        Say("Сохранено со слоями: " + file, Theme.Good);
                    }
                }
                catch (Exception ex) { Host.ShowError("Не удалось сохранить скин", ex); }
            }
        }

        private void ApplyToAccount()
        {
            if (_doc == null) return;

            var account = AccountStore.Selected;
            if (account == null)
            {
                Host.ManageAccounts();
                account = AccountStore.Selected;
                if (account == null) return;
            }

            try
            {
                string dir = Path.Combine(Paths.DataDir, "skins");
                Paths.Ensure(dir);

                string png = Path.Combine(dir, Paths.SafeName(account.Name) + ".png");
                _doc.SavePng(png);
                _doc.SaveNare(Path.ChangeExtension(png, SkinDocument.Extension));

                account.SkinPath = png;
                AccountStore.Save();

                Say("Скин применён к «" + account.Name + "».", Theme.Good);
            }
            catch (Exception ex) { Host.ShowError("Не удалось применить скин", ex); }
        }

        private void Say(string text, Color color)
        {
            _status.Text = text;
            _status.ForeColor = color;
        }

        // ---------------------------------------------------------------- вёрстка

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private void Layout1()
        {
            int top = 96;
            int viewW = Math.Min(360, Math.Max(200, Width / 3));
            int viewH = Math.Max(220, Height - top - 70);

            _view.SetBounds(Pad, top, viewW, viewH);

            int x = Pad + viewW + 34;
            int y = top + 10;

            _edit.Location = new Point(x, y);
            y += _edit.Height + 18;

            _web.Location = new Point(x, y);
            _open.Location = new Point(x + _web.Width + 12, y);
            y += _web.Height + 12;

            _save.Location = new Point(x, y);
            _apply.Location = new Point(x + _save.Width + 12, y);
            y += _save.Height + 22;

            _overlay.Location = new Point(x, y);
            y += 32;
            _spin.Location = new Point(x, y);

            _status.SetBounds(Pad, Height - 28, Math.Max(100, Width - Pad * 2), 18);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            Theme.Text1(g, "Скин", Theme.F(17f, FontStyle.Bold), Theme.Text,
                new Rectangle(Pad, 14, 500, 28));
            Theme.Text1(g, "Рисование, слои, загрузка с сайта и свой формат " + SkinDocument.Extension +
                           " — в отдельном окне редактора",
                Theme.F(9.5f), Theme.Muted, new Rectangle(Pad, 44, Math.Max(100, Width - 56), 18));

            if (_doc != null)
            {
                int x = _edit.Left;
                Theme.Text1(g, _doc.Name, Theme.F(13f, FontStyle.Bold), Theme.Text,
                    new Rectangle(x, 56, 420, 22));

                Theme.Text1(g, "Слоёв: " + _doc.Layers.Count + " · " +
                               (_doc.Arms == SkinArms.Slim ? "тонкие руки (Алекс)" : "обычные руки (Стив)"),
                    Theme.F(9.5f), Theme.Muted, new Rectangle(x, 78, 420, 18));
            }

            Theme.Text1(g, "Накладка (шляпа, куртка)", Theme.F(9.5f), Theme.Muted,
                new Rectangle(_overlay.Right + 12, _overlay.Top - 2, 240, 26));
            Theme.Text1(g, "Вращение", Theme.F(9.5f), Theme.Muted,
                new Rectangle(_spin.Right + 12, _spin.Top - 2, 200, 26));
        }
    }
}

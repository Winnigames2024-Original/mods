using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Админ-управление эвентами: создать/изменить, запустить/остановить, удалить,
    /// задать монеты за заход, уведомление (title+текст) и картинки на главное меню.
    /// </summary>
    public class EventsAdminForm : Form
    {
        private static readonly Color CBg = Color.FromArgb(10, 13, 20);
        private static readonly Color CCard = Color.FromArgb(16, 20, 31);
        private static readonly Color CBorder = Color.FromArgb(27, 34, 50);
        private static readonly Color CText = Color.FromArgb(231, 234, 242);
        private static readonly Color CDim = Color.FromArgb(139, 147, 167);
        private static readonly Color CAccent = Color.FromArgb(91, 140, 255);
        private static readonly Color CGreen = Color.FromArgb(86, 211, 100);
        private static readonly Color CRed = Color.FromArgb(255, 107, 116);

        private readonly string _token;
        private readonly ListBox _list = new ListBox();
        private List<NareEvent> _events = new List<NareEvent>();
        private NareEvent _cur = new NareEvent();

        private readonly TextBox _name = new TextBox();
        private readonly TextBox _desc = new TextBox();
        private readonly TextBox _coins = new TextBox();
        private readonly TextBox _accent = new TextBox();
        private readonly TextBox _nTitle = new TextBox();
        private readonly TextBox _nText = new TextBox();
        private readonly ListBox _imgs = new ListBox();
        private readonly Label _status = new Label();
        private readonly Button _toggle = new Button();

        public EventsAdminForm(string token)
        {
            _token = token ?? "";
            Text = "Эвенты — управление";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(920, 640);
            BackColor = CBg; ForeColor = CText; Font = Theme.F(10f);

            Controls.Add(Lbl("Эвенты", 15f, FontStyle.Bold, CText, new Rectangle(18, 14, 300, 26)));

            _list.BorderStyle = BorderStyle.None; _list.BackColor = CCard; _list.ForeColor = CText;
            _list.Font = Theme.F(10f); _list.Bounds = new Rectangle(18, 50, 250, 470);
            _list.SelectedIndexChanged += (s, e) => { if (_list.SelectedIndex >= 0 && _list.SelectedIndex < _events.Count) LoadInto(_events[_list.SelectedIndex]); };
            Controls.Add(_list);

            var bNew = Btn("➕ Новый", CCard, CAccent, new Rectangle(18, 528, 120, 32), () => { LoadInto(new NareEvent()); });
            var bRefresh = Btn("⟳ Обновить", CCard, CDim, new Rectangle(146, 528, 122, 32), Reload);
            Controls.Add(bNew); Controls.Add(bRefresh);

            int x = 290, w = 590;
            Controls.Add(Lbl("Название", 9f, FontStyle.Bold, CDim, new Rectangle(x, 50, 200, 16)));
            Style(_name); _name.Bounds = new Rectangle(x, 70, w, 28); Controls.Add(_name);

            Controls.Add(Lbl("Описание (текст баннера на главном меню)", 9f, FontStyle.Bold, CDim, new Rectangle(x, 104, 400, 16)));
            Style(_desc); _desc.Multiline = true; _desc.Bounds = new Rectangle(x, 124, w, 54); Controls.Add(_desc);

            Controls.Add(Lbl("Монет за заход", 9f, FontStyle.Bold, CDim, new Rectangle(x, 186, 180, 16)));
            Style(_coins); _coins.Bounds = new Rectangle(x, 206, 180, 28); _coins.Text = "0"; Controls.Add(_coins);
            Controls.Add(Lbl("Акцент (HEX, напр. 5b8cff)", 9f, FontStyle.Bold, CDim, new Rectangle(x + 200, 186, 240, 16)));
            Style(_accent); _accent.Bounds = new Rectangle(x + 200, 206, 180, 28); Controls.Add(_accent);

            Controls.Add(Lbl("Уведомление — заголовок", 9f, FontStyle.Bold, CDim, new Rectangle(x, 244, 300, 16)));
            Style(_nTitle); _nTitle.Bounds = new Rectangle(x, 264, w, 28); Controls.Add(_nTitle);
            Controls.Add(Lbl("Уведомление — текст", 9f, FontStyle.Bold, CDim, new Rectangle(x, 298, 300, 16)));
            Style(_nText); _nText.Multiline = true; _nText.Bounds = new Rectangle(x, 318, w, 50); Controls.Add(_nText);

            Controls.Add(Lbl("Картинки на главное меню", 9f, FontStyle.Bold, CDim, new Rectangle(x, 376, 300, 16)));
            _imgs.BorderStyle = BorderStyle.None; _imgs.BackColor = CCard; _imgs.ForeColor = CText; _imgs.Font = Theme.F(9f);
            _imgs.Bounds = new Rectangle(x, 396, w - 150, 96); Controls.Add(_imgs);
            Controls.Add(Btn("Добавить", CCard, CText, new Rectangle(x + w - 140, 396, 140, 30), AddImage));
            Controls.Add(Btn("Удалить картинку", CCard, CRed, new Rectangle(x + w - 140, 430, 140, 30), DelImage));

            var bSave = Btn("💾 Сохранить", Color.FromArgb(27, 44, 82), Color.FromArgb(157, 180, 255), new Rectangle(x, 510, 150, 38), Save);
            _toggle.Bounds = new Rectangle(x + 160, 510, 170, 38); StyleBtnObj(_toggle, "▶ Запустить", Color.FromArgb(17, 54, 32), CGreen); _toggle.Click += (s, e) => Toggle();
            var bDel = Btn("🗑 Удалить эвент", Color.FromArgb(54, 20, 23), CRed, new Rectangle(x + 340, 510, 170, 38), DeleteEvent);
            Controls.Add(bSave); Controls.Add(_toggle); Controls.Add(bDel);

            _status.AutoSize = false; _status.Bounds = new Rectangle(x, 556, w, 20); _status.ForeColor = CDim; _status.BackColor = Color.Transparent;
            Controls.Add(_status);

            Load += (s, e) => Reload();
        }

        private void Reload()
        {
            Say("Загрузка…");
            new Thread(() =>
            {
                try
                {
                    var all = NareEvents.All(_token);
                    Ui(() =>
                    {
                        _events = all;
                        _list.Items.Clear();
                        foreach (var e in all) _list.Items.Add((e.Active ? "🟢 " : "⚪ ") + e.Name + "  (" + e.Coins + "💰, " + e.Joins + " заходов)");
                        Say(all.Count + " эвентов");
                    });
                }
                catch (Exception ex) { Ui(() => Say("Ошибка: " + ex.Message)); }
            })
            { IsBackground = true }.Start();
        }

        private void LoadInto(NareEvent e)
        {
            _cur = e;
            _name.Text = e.Name; _desc.Text = e.Desc; _coins.Text = e.Coins.ToString();
            _accent.Text = e.Accent; _nTitle.Text = e.NotifyTitle; _nText.Text = e.NotifyText;
            _imgs.Items.Clear(); foreach (var u in e.Images) _imgs.Items.Add(u);
            StyleBtnObj(_toggle, e.Active ? "⏹ Остановить" : "▶ Запустить",
                e.Active ? Color.FromArgb(54, 20, 23) : Color.FromArgb(17, 54, 32), e.Active ? CRed : CGreen);
            _toggle.Enabled = !string.IsNullOrEmpty(e.Id);
        }

        private void CollectInto()
        {
            _cur.Name = _name.Text.Trim();
            _cur.Desc = _desc.Text.Trim();
            int c; int.TryParse(_coins.Text.Trim(), out c); _cur.Coins = Math.Max(0, c);
            _cur.Accent = _accent.Text.Trim();
            _cur.NotifyTitle = _nTitle.Text.Trim();
            _cur.NotifyText = _nText.Text.Trim();
            _cur.Images = new List<string>();
            foreach (var it in _imgs.Items) _cur.Images.Add(it.ToString());
        }

        private void Save()
        {
            CollectInto();
            if (_cur.Name.Length == 0) { Say("Укажи название"); return; }
            Say("Сохранение…");
            new Thread(() =>
            {
                try { string id = NareEvents.Save(_token, _cur); _cur.Id = id; Ui(() => { Say("Сохранено"); Reload(); }); }
                catch (Exception ex) { Ui(() => Say("Ошибка: " + ex.Message)); }
            })
            { IsBackground = true }.Start();
        }

        private void Toggle()
        {
            if (string.IsNullOrEmpty(_cur.Id)) { Say("Сначала сохрани эвент"); return; }
            bool target = !_cur.Active;
            new Thread(() =>
            {
                try { NareEvents.SetState(_token, _cur.Id, target); _cur.Active = target; Ui(() => { Say(target ? "Запущен" : "Остановлен"); Reload(); LoadInto(_cur); }); }
                catch (Exception ex) { Ui(() => Say("Ошибка: " + ex.Message)); }
            })
            { IsBackground = true }.Start();
        }

        private void DeleteEvent()
        {
            if (string.IsNullOrEmpty(_cur.Id)) { LoadInto(new NareEvent()); return; }
            if (MessageBox.Show("Удалить эвент «" + _cur.Name + "»?", "Удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            string id = _cur.Id;
            new Thread(() =>
            {
                try { NareEvents.Delete(_token, id); Ui(() => { LoadInto(new NareEvent()); Reload(); }); }
                catch (Exception ex) { Ui(() => Say("Ошибка: " + ex.Message)); }
            })
            { IsBackground = true }.Start();
        }

        private void AddImage()
        {
            string path;
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Картинки (*.png;*.jpg;*.jpeg;*.webp;*.gif)|*.png;*.jpg;*.jpeg;*.webp;*.gif";
                ofd.Title = "Картинка для главного меню";
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                path = ofd.FileName;
            }
            Say("Загрузка картинки…");
            new Thread(() =>
            {
                try { string url = NareEvents.UploadImage(_token, path); Ui(() => { _imgs.Items.Add(url); Say("Картинка добавлена (не забудь «Сохранить»)"); }); }
                catch (Exception ex) { Ui(() => Say("Ошибка: " + ex.Message)); }
            })
            { IsBackground = true }.Start();
        }

        private void DelImage()
        {
            if (_imgs.SelectedIndex >= 0) _imgs.Items.RemoveAt(_imgs.SelectedIndex);
        }

        private void Say(string t) { _status.Text = t; }
        private void Ui(Action a) { try { if (IsHandleCreated) BeginInvoke(a); else a(); } catch { } }

        // helpers
        private static Label Lbl(string t, float sz, FontStyle st, Color fg, Rectangle b)
        {
            return new Label { Text = t, Font = Theme.F(sz, st), ForeColor = fg, BackColor = Color.Transparent, AutoSize = false, Bounds = b };
        }
        private void Style(TextBox tb)
        {
            tb.BorderStyle = BorderStyle.FixedSingle; tb.BackColor = CCard; tb.ForeColor = CText; tb.Font = Theme.F(10f);
        }
        private Button Btn(string t, Color bg, Color fg, Rectangle b, Action act)
        {
            var btn = new Button { Bounds = b };
            StyleBtnObj(btn, t, bg, fg);
            btn.Click += (s, e) => { try { act(); } catch (Exception ex) { Say(ex.Message); } };
            return btn;
        }
        private static void StyleBtnObj(Button b, string t, Color bg, Color fg)
        {
            b.Text = t; b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0;
            b.BackColor = bg; b.ForeColor = fg; b.Font = Theme.F(9.5f, FontStyle.Bold); b.Cursor = Cursors.Hand;
        }
    }
}

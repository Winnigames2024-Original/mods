namespace GEO_DASH.Core
{
    /// <summary>Элемент игрового интерфейса: доля экрана по ширине/высоте и размер.</summary>
    public class GuiItem
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Size { get; set; } = 0.09;
        public bool Visible { get; set; } = true;

        public GuiItem() { }
        public GuiItem(double x, double y, double size) { X = x; Y = y; Size = size; }
    }

    /// <summary>
    /// Расположение кнопок в игре: «=» слева сверху, полоса прогресса,
    /// кнопки платформера. Всё можно двигать прямо в игре.
    /// </summary>
    public class GuiLayout
    {
        public GuiItem Menu { get; set; } = new(0.035, 0.065, 0.085);
        public GuiItem Progress { get; set; } = new(0.5, 0.05, 0.46);
        public GuiItem Left { get; set; } = new(0.09, 0.84, 0.15);
        public GuiItem Right { get; set; } = new(0.235, 0.84, 0.15);
        public GuiItem Jump { get; set; } = new(0.90, 0.84, 0.17);
        public double Opacity { get; set; } = 0.85;

        public void Reset()
        {
            Menu = new GuiItem(0.035, 0.065, 0.085);
            Progress = new GuiItem(0.5, 0.05, 0.46);
            Left = new GuiItem(0.09, 0.84, 0.15);
            Right = new GuiItem(0.235, 0.84, 0.15);
            Jump = new GuiItem(0.90, 0.84, 0.17);
            Opacity = 0.85;
        }

        public IEnumerable<(string name, GuiItem item)> Items()
        {
            yield return ("Кнопка =", Menu);
            yield return ("Полоса прогресса", Progress);
            yield return ("Влево", Left);
            yield return ("Вправо", Right);
            yield return ("Прыжок", Jump);
        }
    }
}

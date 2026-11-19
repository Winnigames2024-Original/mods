using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GEO_DASH.UI
{
    /// <summary>Игровой цикл на Application.Idle — ровный фиксированный шаг физики.</summary>
    public class GameLoop : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct NativeMessage
        {
            public IntPtr hWnd; public uint msg; public IntPtr wParam; public IntPtr lParam;
            public uint time; public System.Drawing.Point p;
        }

        [DllImport("user32.dll")]
        static extern bool PeekMessage(out NativeMessage msg, IntPtr hWnd, uint min, uint max, uint remove);

        static bool AppIdle
        {
            get { return !PeekMessage(out _, IntPtr.Zero, 0, 0, 0); }
        }

        readonly Stopwatch _sw = new();
        double _acc, _renderAcc;
        public double FixedStep = 1.0 / 240.0;
        public int TargetFps = 60;
        public double Fps { get; private set; }
        public bool Running { get; private set; }

        public event Action<double> Step;      // физика
        public event Action Render;            // кадр

        double _fpsAcc; int _fpsFrames;

        public void Start()
        {
            if (Running) return;
            Running = true;
            _sw.Restart();
            Application.Idle += OnIdle;
        }

        public void Stop()
        {
            if (!Running) return;
            Running = false;
            Application.Idle -= OnIdle;
            _sw.Stop();
        }

        void OnIdle(object sender, EventArgs e)
        {
            while (Running && AppIdle)
            {
                double dt = _sw.Elapsed.TotalSeconds;
                _sw.Restart();
                if (dt > 0.25) dt = 0.25;

                _acc += dt;
                int guard = 0;
                while (_acc >= FixedStep && guard++ < 600)
                {
                    Step?.Invoke(FixedStep);
                    _acc -= FixedStep;
                }

                double frame = 1.0 / Math.Max(15, TargetFps);
                _renderAcc += dt;
                if (_renderAcc >= frame)
                {
                    _renderAcc = 0;
                    Render?.Invoke();
                    _fpsFrames++;
                }

                _fpsAcc += dt;
                if (_fpsAcc >= 0.5)
                {
                    Fps = _fpsFrames / _fpsAcc;
                    _fpsAcc = 0; _fpsFrames = 0;
                }

                if (_acc < FixedStep && _renderAcc < frame * 0.5)
                    Thread.Sleep(1);
            }
        }

        public void Dispose() => Stop();
    }
}

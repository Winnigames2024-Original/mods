using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GEO_DASH.UI
{
    /// <summary>
    /// Игровой цикл: ровный фиксированный шаг физики.
    /// Работает и в обычном окне, и внутри модального окна, и когда экран встроен в панель —
    /// такт даёт мультимедийный таймер, Application.Idle только добавляет плавности.
    /// </summary>
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

        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);

        static bool AppIdle => !PeekMessage(out _, IntPtr.Zero, 0, 0, 0);

        readonly Stopwatch _sw = new();
        readonly Control _owner;
        System.Threading.Timer _tick;
        bool _inPump, _periodSet;

        double _acc, _renderAcc, _fpsAcc;
        int _fpsFrames;

        public double FixedStep = 1.0 / 240.0;
        public int TargetFps = 60;
        public double Fps { get; private set; }
        public bool Running { get; private set; }

        public event Action<double> Step;      // физика
        public event Action Render;            // кадр

        /// <summary>owner — окно или панель игры: через него цикл попадает в поток интерфейса.</summary>
        public GameLoop(Control owner = null) { _owner = owner; }

        public void Start()
        {
            if (Running) return;
            Running = true;
            _sw.Restart();
            _acc = _renderAcc = _fpsAcc = 0;
            _fpsFrames = 0;

            try { timeBeginPeriod(1); _periodSet = true; } catch { }

            Application.Idle += OnIdle;
            // 2 мс — таймер стучит чаще кадра, лишние такты цикл сам пропускает
            _tick = new System.Threading.Timer(_ => Beat(), null, 2, 2);
        }

        public void Stop()
        {
            if (!Running) return;
            Running = false;
            Application.Idle -= OnIdle;
            try { _tick?.Dispose(); } catch { }
            _tick = null;
            _sw.Stop();
            if (_periodSet) { try { timeEndPeriod(1); } catch { } _periodSet = false; }
        }

        /// <summary>Такт из фонового таймера — переносим его в поток интерфейса.</summary>
        void Beat()
        {
            if (!Running) return;
            var c = _owner;
            if (c == null || c.IsDisposed || !c.IsHandleCreated) { Pump(); return; }
            try { c.BeginInvoke(new Action(Pump)); } catch { }
        }

        void OnIdle(object sender, EventArgs e)
        {
            // пока очередь сообщений пуста — крутим цикл сами, так кадры ровнее
            int guard = 0;
            while (Running && AppIdle && guard++ < 8) Pump();
        }

        void Pump()
        {
            if (!Running || _inPump) return;
            _inPump = true;
            try
            {
                double dt = _sw.Elapsed.TotalSeconds;
                if (dt <= 0) return;
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
                    _renderAcc -= frame;
                    if (_renderAcc > frame) _renderAcc = 0;   // отстали — не копим долг
                    Render?.Invoke();
                    _fpsFrames++;
                }

                _fpsAcc += dt;
                if (_fpsAcc >= 0.5)
                {
                    Fps = _fpsFrames / _fpsAcc;
                    _fpsAcc = 0; _fpsFrames = 0;
                }
            }
            catch { }
            finally { _inPump = false; }
        }

        public void Dispose() => Stop();
    }
}

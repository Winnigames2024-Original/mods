using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Персонаж: модель, анимации и отрисовка. Не контрол — просто рисовалка,
    /// которой говорят «вот холст, вот прямоугольник» и она туда рисует.
    ///
    /// Так его можно положить фоном во весь экран под интерфейсом главной, а
    /// кнопки и надписи останутся сверху: они обычные контролы и рисуются
    /// поверх фона сами.
    ///
    /// Персонаж живёт сам: мышью его не крутят, он только провожает курсор
    /// взглядом. Рендер софтверный — грань коробки это аффинное наложение
    /// куска развёртки через DrawImage по трём точкам.
    /// </summary>
    public class PlayerRenderer
    {
        // ------------------------------------------------------------- поза

        private struct Pose
        {
            public float BodyY;
            public float BodyYaw;
            public float Lean;
            public float HeadYaw;
            public float HeadPitch;
            public float HeadRoll;
            public float ArmRX, ArmLX;
            public float ArmRZ, ArmLZ;
            public float LegRX, LegLX;
            public float Cape;

            public static Pose Lerp(Pose a, Pose b, float k)
            {
                Pose p;
                p.BodyY = a.BodyY + (b.BodyY - a.BodyY) * k;
                p.BodyYaw = a.BodyYaw + (b.BodyYaw - a.BodyYaw) * k;
                p.Lean = a.Lean + (b.Lean - a.Lean) * k;
                p.HeadYaw = a.HeadYaw + (b.HeadYaw - a.HeadYaw) * k;
                p.HeadPitch = a.HeadPitch + (b.HeadPitch - a.HeadPitch) * k;
                p.HeadRoll = a.HeadRoll + (b.HeadRoll - a.HeadRoll) * k;
                p.ArmRX = a.ArmRX + (b.ArmRX - a.ArmRX) * k;
                p.ArmLX = a.ArmLX + (b.ArmLX - a.ArmLX) * k;
                p.ArmRZ = a.ArmRZ + (b.ArmRZ - a.ArmRZ) * k;
                p.ArmLZ = a.ArmLZ + (b.ArmLZ - a.ArmLZ) * k;
                p.LegRX = a.LegRX + (b.LegRX - a.LegRX) * k;
                p.LegLX = a.LegLX + (b.LegLX - a.LegLX) * k;
                p.Cape = a.Cape + (b.Cape - a.Cape) * k;
                return p;
            }
        }

        private class Act
        {
            public string Title;
            public float Seconds;
            public Func<float, Pose> Build;
        }

        private class FaceDraw
        {
            public PointF[] Points;
            public Rectangle Uv;
            public float Depth;
            public float Shade;
            public Bitmap Texture;
            public bool Eyes;
        }

        // ------------------------------------------------------------- поля

        private readonly Random _random = new Random();
        private readonly List<Act> _acts = new List<Act>();
        private readonly List<FaceDraw> _faces = new List<FaceDraw>();

        private SkinFile _skin;
        private List<Cosmetic> _worn = new List<Cosmetic>();

        private Act _act;
        private Pose _pose;
        private float _time;

        private float _blinkIn = 2.5f;
        private float _blinkLeft;

        // тычки: считаем их, и когда надоест — персонаж отмахивается
        private int _pokes;
        private float _pokeWindow;
        private float _pokeSide = 1f;
        private float _flinch;
        private float _swatLeft;
        private float _swatTime;

        // куда смотрит: цель и то, что показано сейчас
        private float _lookYaw, _lookPitch;
        private float _wantYaw, _wantPitch;
        private bool _hasTarget;
        private PointF _target;

        // где на холсте оказалась голова — чтобы считать взгляд
        private float _headX, _headY;

        private const float ViewYaw = 0.42f;
        private const float ViewPitch = 0.10f;

        public string Caption = "";
        public bool ShowOverlay = true;

        /// <summary>Название того, что персонаж делает прямо сейчас.</summary>
        public string ActTitle
        {
            get
            {
                if (_swatLeft > 0f) return "отмахивается";
                return _act == null ? "" : _act.Title;
            }
        }

        public PlayerRenderer()
        {
            BuildActs();
            _act = _acts[0];
        }

        // ------------------------------------------------------- что надето

        public void LoadFromAccount()
        {
            try
            {
                var account = AccountStore.Selected;
                if (account != null && !string.IsNullOrEmpty(account.SkinPath))
                {
                    string layered = Path.ChangeExtension(account.SkinPath, SkinDocument.Extension);

                    if (File.Exists(layered))
                    {
                        var doc = SkinDocument.LoadNare(layered);
                        if (doc != null && doc.Flat != null && doc.Flat.Image != null)
                        {
                            SetSkin(doc.Flat, account.Name);
                            ReloadCosmetics();
                            return;
                        }
                    }

                    if (File.Exists(account.SkinPath))
                    {
                        SetSkin(SkinFile.FromFile(account.SkinPath), account.Name);
                        ReloadCosmetics();
                        return;
                    }
                }

                SetSkin(SkinFile.Default(false, Settings.Current.SelectedVersion),
                        account == null ? "" : account.Name);
                ReloadCosmetics();
            }
            catch (Exception ex)
            {
                Log.Warn("Персонаж: скин не загрузился — " + ex.Message);
            }
        }

        public void SetSkin(SkinFile skin, string caption)
        {
            _skin = skin;
            Caption = caption ?? "";
        }

        public void ReloadCosmetics()
        {
            try { _worn = CosmeticStore.Worn(); }
            catch { _worn = new List<Cosmetic>(); }
        }

        /// <summary>Показать именно этот набор вещей — для примерки.</summary>
        public void ShowList(List<Cosmetic> items)
        {
            _worn = items ?? new List<Cosmetic>();
        }

        public bool Ready { get { return _skin != null && _skin.Image != null; } }

        // ----------------------------------------------------------- взгляд

        /// <summary>Куда смотреть — точка на том же холсте, где рисуется персонаж.</summary>
        public void LookAt(PointF point)
        {
            _hasTarget = true;
            _target = point;
        }

        /// <summary>Курсор ушёл — голова плавно возвращается прямо.</summary>
        public void LookAway()
        {
            _hasTarget = false;
        }

        /// <summary>
        /// Щелчок по холсту. Если попали по персонажу — он вздрагивает, а
        /// если дёргать его не переставая, отмахнётся рукой в ту сторону,
        /// откуда пристают.
        /// </summary>
        public void Poke(PointF at)
        {
            if (_scale <= 0f) return;

            float ox = _headX;
            float oy = _headY + 28f * _scale;

            // попадание считаем по фигуре целиком, а не по пикселям:
            // ловить мышью руку толщиной в четыре пикселя неинтересно
            if (at.X < ox - 9f * _scale || at.X > ox + 9f * _scale) return;
            if (at.Y < oy - 34f * _scale || at.Y > oy) return;

            _pokeSide = at.X >= ox ? 1f : -1f;
            _flinch = 1f;

            if (_pokeWindow <= 0f) _pokes = 0;
            _pokes++;
            _pokeWindow = 4f;

            if (_pokes >= 5)
            {
                _pokes = 0;
                _swatLeft = 2.2f;
                _swatTime = 0f;
            }
        }

        /// <summary>Отмахивается рукой с той стороны, откуда тыкают.</summary>
        private Pose Swat(float t)
        {
            Pose p = Zero();

            float f = (float)Math.Sin(t * 11.0);

            if (_pokeSide >= 0f)
            {
                p.ArmLX = -(1.75f + f * 0.55f);
                p.ArmLZ = -0.55f - f * 0.22f;
                p.ArmRX = -0.12f;
                p.ArmRZ = 0.06f;
            }
            else
            {
                p.ArmRX = -(1.75f + f * 0.55f);
                p.ArmRZ = 0.55f + f * 0.22f;
                p.ArmLX = -0.12f;
                p.ArmLZ = -0.06f;
            }

            p.Lean = 0.12f;                      // чуть отклоняется назад
            p.BodyYaw = -_pokeSide * 0.20f;
            p.HeadRoll = -_pokeSide * 0.10f;
            p.BodyY = Math.Abs(f) * 0.10f;
            p.Cape = 0.20f;

            return p;
        }

        // -------------------------------------------------------- анимации

        private static Pose Zero()
        {
            Pose p = new Pose();
            p.Cape = 0.12f;
            return p;
        }

        /// <summary>
        /// Только то, что человек делает стоя на месте. Ходьбы и бега нет:
        /// персонаж никуда не идёт, он ждёт, пока нажмут «Играть».
        /// </summary>
        private void BuildActs()
        {
            _acts.Add(new Act
            {
                Title = "ждёт",
                Seconds = 6f,
                Build = delegate(float t)
                {
                    Pose p = Zero();
                    p.BodyY = (float)Math.Sin(t * 1.5) * 0.10f;
                    p.ArmRX = (float)Math.Sin(t * 1.5) * 0.06f;
                    p.ArmLX = -p.ArmRX;
                    p.ArmRZ = 0.05f;
                    p.ArmLZ = -0.05f;
                    p.Cape = 0.12f + (float)Math.Sin(t * 1.5) * 0.04f;
                    return p;
                }
            });

            _acts.Add(new Act
            {
                Title = "переминается",
                Seconds = 5.5f,
                Build = delegate(float t)
                {
                    Pose p = Zero();
                    float f = (float)Math.Sin(t * 1.1);
                    p.BodyYaw = f * 0.22f;
                    p.ArmRZ = 0.06f + f * 0.05f;
                    p.ArmLZ = -0.06f + f * 0.05f;
                    p.LegRX = f * 0.12f;
                    p.LegLX = -f * 0.10f;
                    p.BodyY = Math.Abs(f) * 0.12f;
                    p.Cape = 0.14f + Math.Abs(f) * 0.08f;
                    return p;
                }
            });

            _acts.Add(new Act
            {
                Title = "здоровается",
                Seconds = 4.5f,
                Build = delegate(float t)
                {
                    Pose p = Zero();
                    p.ArmLX = -2.55f;
                    p.ArmLZ = -0.35f - (float)Math.Sin(t * 7.0) * 0.42f;
                    p.ArmRX = (float)Math.Sin(t * 1.4) * 0.06f;
                    p.ArmRZ = 0.05f;
                    p.HeadRoll = 0.08f;
                    p.BodyY = (float)Math.Sin(t * 1.4) * 0.08f;
                    return p;
                }
            });

            _acts.Add(new Act
            {
                Title = "шесть-семь",
                Seconds = 5f,
                Build = delegate(float t)
                {
                    Pose p = Zero();
                    float f = (float)Math.Sin(t * 4.2);
                    p.ArmRX = -1.45f + f * 0.42f;
                    p.ArmLX = -1.45f - f * 0.42f;
                    p.ArmRZ = 0.30f;
                    p.ArmLZ = -0.30f;
                    p.BodyY = Math.Abs(f) * 0.12f;
                    p.BodyYaw = f * 0.10f;
                    return p;
                }
            });

            _acts.Add(new Act
            {
                Title = "разминается",
                Seconds = 5f,
                Build = delegate(float t)
                {
                    Pose p = Zero();
                    float up = Math.Max(0f, (float)Math.Sin(t * 1.3));
                    p.ArmRX = -up * 2.9f;
                    p.ArmLX = -up * 2.9f;
                    p.ArmRZ = up * 0.22f;
                    p.ArmLZ = -up * 0.22f;
                    p.BodyY = up * 0.35f;
                    p.Cape = 0.12f + up * 0.25f;
                    return p;
                }
            });

            _acts.Add(new Act
            {
                Title = "кивает",
                Seconds = 4f,
                Build = delegate(float t)
                {
                    Pose p = Zero();
                    p.HeadPitch = (float)Math.Sin(t * 3.2) * 0.26f;
                    p.ArmRZ = 0.05f;
                    p.ArmLZ = -0.05f;
                    p.BodyY = (float)Math.Sin(t * 1.6) * 0.07f;
                    return p;
                }
            });

            _acts.Add(new Act
            {
                Title = "пожимает плечами",
                Seconds = 4f,
                Build = delegate(float t)
                {
                    Pose p = Zero();
                    float up = Math.Max(0f, (float)Math.Sin(t * 1.8));
                    p.ArmRZ = 0.08f + up * 0.55f;
                    p.ArmLZ = -0.08f - up * 0.55f;
                    p.ArmRX = -up * 0.35f;
                    p.ArmLX = -up * 0.35f;
                    p.BodyY = up * 0.12f;
                    return p;
                }
            });

            _acts.Add(new Act
            {
                Title = "показывает плащ",
                Seconds = 5f,
                Build = delegate(float t)
                {
                    Pose p = Zero();
                    p.BodyYaw = (float)Math.Sin(t * 0.8) * 1.5f;
                    p.ArmRZ = 0.22f;
                    p.ArmLZ = -0.22f;
                    p.Cape = 0.20f + Math.Abs((float)Math.Cos(t * 0.8)) * 0.35f;
                    return p;
                }
            });

            _acts.Add(new Act
            {
                Title = "приплясывает",
                Seconds = 5f,
                Build = delegate(float t)
                {
                    Pose p = Zero();
                    float f = (float)Math.Sin(t * 3.4);
                    p.BodyY = Math.Abs(f) * 0.42f;
                    p.BodyYaw = f * 0.28f;
                    p.HeadRoll = f * 0.16f;
                    p.ArmRX = -0.5f - f * 0.5f;
                    p.ArmLX = -0.5f + f * 0.5f;
                    p.ArmRZ = 0.35f;
                    p.ArmLZ = -0.35f;
                    p.LegRX = f * 0.18f;
                    p.LegLX = -f * 0.18f;
                    p.Cape = 0.18f + Math.Abs(f) * 0.14f;
                    return p;
                }
            });

            _acts.Add(new Act
            {
                Title = "задумался",
                Seconds = 5f,
                Build = delegate(float t)
                {
                    Pose p = Zero();
                    p.ArmLX = -2.1f;
                    p.ArmLZ = -0.55f;
                    p.HeadRoll = -0.12f;
                    p.ArmRZ = 0.05f;
                    p.BodyY = (float)Math.Sin(t * 1.2) * 0.06f;
                    return p;
                }
            });
        }

        private void NextAct()
        {
            if (_acts.Count < 2) return;

            Act next = _act;
            for (int i = 0; i < 8 && next == _act; i++)
                next = _acts[_random.Next(_acts.Count)];

            _act = next;
            _time = 0f;
        }

        // ------------------------------------------------------------- ход

        /// <summary>Шаг времени. Зовётся таймером того, кто нас рисует.</summary>
        public void Tick(float dt)
        {
            if (_pokeWindow > 0f) _pokeWindow -= dt;
            if (_flinch > 0f) _flinch -= dt * 3.2f;

            Pose want;
            float blend;

            if (_swatLeft > 0f)
            {
                _swatLeft -= dt;
                _swatTime += dt;
                want = Swat(_swatTime);
                blend = Math.Min(1f, _swatTime / 0.15f);
            }
            else
            {
                _time += dt;
                if (_time > _act.Seconds) NextAct();
                want = _act.Build(_time);
                blend = Math.Min(1f, _time / 0.25f);
                _swatTime = 0f;
            }

            // вздрагивание от тычка — поверх любой позы
            if (_flinch > 0f)
            {
                float k = Clamp(_flinch, 0f, 1f);
                want.BodyY += k * 0.28f;
                want.HeadRoll += -_pokeSide * k * 0.14f;
            }

            _pose = Pose.Lerp(_pose, want, 0.18f + 0.3f * blend);

            // взгляд за курсором считаем от того места, где оказалась голова
            if (_hasTarget)
            {
                float dx = _target.X - _headX;
                float dy = _target.Y - _headY;

                float reach = Math.Max(60f, _scale * 14f);

                // вниз по экрану — это наклон головы вниз, то есть
                // отрицательный угол: знак тут легко перепутать
                _wantYaw = Clamp(-dx / reach, -1.05f, 1.05f);
                _wantPitch = Clamp(-dy / (reach * 1.4f), -0.60f, 0.45f);
            }
            else
            {
                _wantYaw = 0f;
                _wantPitch = 0f;
            }

            _lookYaw += (_wantYaw - _lookYaw) * 0.18f;
            _lookPitch += (_wantPitch - _lookPitch) * 0.18f;

            if (_blinkLeft > 0f) _blinkLeft -= dt;
            else
            {
                _blinkIn -= dt;
                if (_blinkIn <= 0f)
                {
                    _blinkLeft = 0.12f;
                    _blinkIn = 2.2f + (float)_random.NextDouble() * 4.0f;
                    if (_random.Next(5) == 0) _blinkIn = 0.22f;
                }
            }
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        private bool Blinking { get { return _blinkLeft > 0f; } }

        // -------------------------------------------------------- отрисовка

        private float _scale;

        /// <summary>
        /// Рисует персонажа в прямоугольник. fill — занять всю высоту, как
        /// на главной; иначе вписывается скромнее, как в окошке примерки.
        /// </summary>
        public void Paint(Graphics g, Rectangle area, bool fill)
        {
            if (!Ready || area.Width <= 8 || area.Height <= 8) return;

            float scale = fill
                ? Math.Min(area.Width / 30f, area.Height / 40f)
                : Math.Min(area.Width / 24f, area.Height / 46f);

            _scale = scale;

            float ox = area.X + area.Width / 2f;
            float oy = area.Y + area.Height / 2f + 17f * scale;

            // куда уехала голова — пригодится для взгляда на следующем шаге
            _headX = ox;
            _headY = oy - 28f * scale;

            int shadowW = (int)(13f * scale);
            int shadowH = Math.Max(4, (int)(3.4f * scale));
            var shadow = new Rectangle((int)(ox - shadowW / 2f),
                                       (int)(oy - shadowH / 2f + 1), shadowW, shadowH);

            var saveSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                g.FillEllipse(brush, shadow);

            _faces.Clear();

            foreach (var part in SkinLayout.Model(_skin.Arms))
            {
                if (part.Overlay && !ShowOverlay) continue;
                Collect(part, scale, ox, oy);
            }

            foreach (var item in _worn) CollectCosmetic(item, scale, ox, oy);

            _faces.Sort(delegate(FaceDraw a, FaceDraw b) { return b.Depth.CompareTo(a.Depth); });

            var saveInterp = g.InterpolationMode;
            var savePixel = g.PixelOffsetMode;

            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;

            using (var attrs = new ImageAttributes())
            {
                attrs.SetWrapMode(WrapMode.TileFlipXY);

                foreach (var face in _faces)
                {
                    if (face.Uv.Width <= 0 || face.Uv.Height <= 0) continue;
                    var texture = face.Texture ?? _skin.Image;
                    if (texture == null) continue;

                    try
                    {
                        g.DrawImage(texture, face.Points, face.Uv, GraphicsUnit.Pixel, attrs);

                        if (face.Eyes && Blinking) CloseEyes(g, face);

                        if (face.Shade < 1f)
                        {
                            int alpha = (int)((1f - face.Shade) * 110);
                            using (var brush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                                g.FillPolygon(brush, Quad(face.Points));
                        }
                    }
                    catch { }
                }
            }

            g.InterpolationMode = saveInterp;
            g.PixelOffsetMode = savePixel;
            g.SmoothingMode = saveSmooth;
        }

        /// <summary>
        /// Моргание: поверх полосы с глазами кладём цвет лба. Работает с любым
        /// скином — где именно нарисованы глаза, знать не надо, высота у них
        /// всегда одна.
        /// </summary>
        private void CloseEyes(Graphics g, FaceDraw face)
        {
            Color tone;
            try
            {
                int x = face.Uv.X + face.Uv.Width / 2;
                int y = face.Uv.Y + Math.Max(1, face.Uv.Height / 4);
                tone = _skin.Image.GetPixel(
                    Math.Min(_skin.Image.Width - 1, x),
                    Math.Min(_skin.Image.Height - 1, y));
            }
            catch { return; }

            if (tone.A < 16) return;

            var p0 = face.Points[0];
            var p1 = face.Points[1];
            var p2 = face.Points[2];

            var lid = new[]
            {
                At(p0, p1, p2, 0.06f, 0.46f),
                At(p0, p1, p2, 0.94f, 0.46f),
                At(p0, p1, p2, 0.94f, 0.70f),
                At(p0, p1, p2, 0.06f, 0.70f)
            };

            using (var brush = new SolidBrush(tone))
                g.FillPolygon(brush, lid);
        }

        private static PointF At(PointF p0, PointF p1, PointF p2, float u, float v)
        {
            return new PointF(
                p0.X + (p1.X - p0.X) * u + (p2.X - p0.X) * v,
                p0.Y + (p1.Y - p0.Y) * u + (p2.Y - p0.Y) * v);
        }

        private static PointF[] Quad(PointF[] p)
        {
            var fourth = new PointF(p[1].X + p[2].X - p[0].X, p[1].Y + p[2].Y - p[0].Y);
            return new[] { p[0], p[1], fourth, p[2] };
        }

        // ---------------------------------------------------------- сборка

        private enum Joint { Body, Head, ArmRight, ArmLeft, LegRight, LegLeft, Cape }

        private static Joint JointOf(string title)
        {
            if (title.IndexOf("Голов", StringComparison.OrdinalIgnoreCase) >= 0 ||
                title.IndexOf("Шляп", StringComparison.OrdinalIgnoreCase) >= 0) return Joint.Head;

            bool right = title.IndexOf("Прав", StringComparison.OrdinalIgnoreCase) >= 0;

            if (title.IndexOf("рук", StringComparison.OrdinalIgnoreCase) >= 0 ||
                title.IndexOf("рукав", StringComparison.OrdinalIgnoreCase) >= 0)
                return right ? Joint.ArmRight : Joint.ArmLeft;

            if (title.IndexOf("ног", StringComparison.OrdinalIgnoreCase) >= 0 ||
                title.IndexOf("штанин", StringComparison.OrdinalIgnoreCase) >= 0)
                return right ? Joint.LegRight : Joint.LegLeft;

            return Joint.Body;
        }

        private void Collect(SkinLayout.Part part, float scale, float ox, float oy)
        {
            float w = part.W / 2f + part.Inflate;
            float h = part.H / 2f + part.Inflate;
            float d = part.D / 2f + part.Inflate;

            Joint joint = JointOf(part.Title);
            bool head = joint == Joint.Head && !part.Overlay;

            Box(part.Uv, null, joint, part.Cx, part.Cy, part.Cz, w, h, d, scale, ox, oy, head);
        }

        private void Box(Rectangle[] uv, Bitmap texture, Joint joint,
                         float cx, float cy, float cz, float w, float h, float d,
                         float scale, float ox, float oy, bool markEyes)
        {
            AddFace(uv[SkinLayout.Up], 1.00f, joint, texture, false,
                    cx - w, cy + h, cz + d, cx + w, cy + h, cz + d, cx - w, cy + h, cz - d, scale, ox, oy);

            AddFace(uv[SkinLayout.Down], 0.55f, joint, texture, false,
                    cx - w, cy - h, cz - d, cx + w, cy - h, cz - d, cx - w, cy - h, cz + d, scale, ox, oy);

            AddFace(uv[SkinLayout.Right], 0.80f, joint, texture, false,
                    cx - w, cy + h, cz + d, cx - w, cy + h, cz - d, cx - w, cy - h, cz + d, scale, ox, oy);

            AddFace(uv[SkinLayout.Front], 0.95f, joint, texture, markEyes,
                    cx - w, cy + h, cz - d, cx + w, cy + h, cz - d, cx - w, cy - h, cz - d, scale, ox, oy);

            AddFace(uv[SkinLayout.Left], 0.80f, joint, texture, false,
                    cx + w, cy + h, cz - d, cx + w, cy + h, cz + d, cx + w, cy - h, cz - d, scale, ox, oy);

            AddFace(uv[SkinLayout.Back], 0.70f, joint, texture, false,
                    cx + w, cy + h, cz + d, cx - w, cy + h, cz + d, cx + w, cy - h, cz + d, scale, ox, oy);
        }

        private void CollectCosmetic(Cosmetic item, float scale, float ox, float oy)
        {
            var texture = CosmeticStore.Texture(item);
            if (texture == null) return;

            if (item.Kind == CosmeticKind.Cape)
            {
                var uv = new Rectangle[6];
                uv[SkinLayout.Back] = new Rectangle(1, 1, 10, 16);
                uv[SkinLayout.Front] = new Rectangle(12, 1, 10, 16);
                uv[SkinLayout.Right] = new Rectangle(0, 1, 1, 16);
                uv[SkinLayout.Left] = new Rectangle(11, 1, 1, 16);
                uv[SkinLayout.Up] = new Rectangle(1, 0, 10, 1);
                uv[SkinLayout.Down] = new Rectangle(11, 0, 10, 1);

                Box(uv, texture, Joint.Cape, 0f, 16f, 3f, 5f, 8f, 0.5f, scale, ox, oy, false);
                return;
            }

            var same = Same(texture);

            if (item.Kind == CosmeticKind.Hat)
            {
                Box(same, texture, Joint.Head, 0f, 33.0f, 0f, 4.6f, 1.0f, 4.6f, scale, ox, oy, false);
                Box(same, texture, Joint.Head, 0f, 35.5f, 0f, 3.2f, 2.5f, 3.2f, scale, ox, oy, false);
                return;
            }

            Box(same, texture, Joint.Body, 0f, 19f, 4.5f, 3.5f, 4.5f, 2.5f, scale, ox, oy, false);
        }

        private static Rectangle[] Same(Bitmap texture)
        {
            var all = new Rectangle(0, 0, texture.Width, texture.Height);
            return new[] { all, all, all, all, all, all };
        }

        private void AddFace(Rectangle uv, float shade, Joint joint, Bitmap texture, bool eyes,
                             float x0, float y0, float z0,
                             float x1, float y1, float z1,
                             float x2, float y2, float z2,
                             float scale, float ox, float oy)
        {
            float d0, d1, d2;
            var p0 = Place(joint, x0, y0, z0, scale, ox, oy, out d0);
            var p1 = Place(joint, x1, y1, z1, scale, ox, oy, out d1);
            var p2 = Place(joint, x2, y2, z2, scale, ox, oy, out d2);

            float cross = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
            if (cross <= 0) return;

            _faces.Add(new FaceDraw
            {
                Points = new[] { p0, p1, p2 },
                Uv = uv,
                Depth = (d0 + d1 + d2) / 3f,
                Shade = shade,
                Texture = texture,
                Eyes = eyes
            });
        }

        private PointF Place(Joint joint, float x, float y, float z,
                             float scale, float ox, float oy, out float depth)
        {
            float armX = _skin != null && _skin.Arms == SkinArms.Slim ? 5.5f : 6f;

            switch (joint)
            {
                case Joint.Head:
                    RotZ(ref x, ref y, 0f, 24f, _pose.HeadRoll);
                    RotX(ref y, ref z, 24f, 0f, _pose.HeadPitch + _lookPitch);
                    RotY(ref x, ref z, 0f, 0f, _pose.HeadYaw + _lookYaw);
                    break;

                // Минус в позах означает «вперёд», к зрителю. Сам поворот
                // считает наоборот, поэтому знак переворачиваем здесь — один
                // раз, а не в каждой анимации.
                case Joint.ArmRight:
                    RotZ(ref x, ref y, -armX, 23f, _pose.ArmRZ);
                    RotX(ref y, ref z, 23f, 0f, -_pose.ArmRX);
                    break;

                case Joint.ArmLeft:
                    RotZ(ref x, ref y, armX, 23f, _pose.ArmLZ);
                    RotX(ref y, ref z, 23f, 0f, -_pose.ArmLX);
                    break;

                case Joint.LegRight:
                    RotX(ref y, ref z, 12f, 0f, -_pose.LegRX);
                    break;

                case Joint.LegLeft:
                    RotX(ref y, ref z, 12f, 0f, -_pose.LegLX);
                    break;

                case Joint.Cape:
                    RotX(ref y, ref z, 24f, 2.5f, -_pose.Cape);
                    break;
            }

            RotX(ref y, ref z, 12f, 0f, _pose.Lean);

            // телом тоже слегка доворачивается за курсором — без этого голова
            // будто отдельно живёт
            RotY(ref x, ref z, 0f, 0f, _pose.BodyYaw + _lookYaw * 0.22f);
            y += _pose.BodyY;

            float cosY = (float)Math.Cos(ViewYaw), sinY = (float)Math.Sin(ViewYaw);
            float rx = x * cosY + z * sinY;
            float rz = -x * sinY + z * cosY;

            float cosP = (float)Math.Cos(ViewPitch), sinP = (float)Math.Sin(ViewPitch);
            float ry = y * cosP - rz * sinP;
            depth = y * sinP + rz * cosP;

            return new PointF(ox + rx * scale, oy - ry * scale);
        }

        private static void RotX(ref float y, ref float z, float py, float pz, float angle)
        {
            if (angle == 0f) return;
            float dy = y - py, dz = z - pz;
            float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
            y = py + dy * c - dz * s;
            z = pz + dy * s + dz * c;
        }

        private static void RotY(ref float x, ref float z, float px, float pz, float angle)
        {
            if (angle == 0f) return;
            float dx = x - px, dz = z - pz;
            float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
            x = px + dx * c + dz * s;
            z = pz - dx * s + dz * c;
        }

        private static void RotZ(ref float x, ref float y, float px, float py, float angle)
        {
            if (angle == 0f) return;
            float dx = x - px, dy = y - py;
            float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
            x = px + dx * c - dy * s;
            y = py + dx * s + dy * c;
        }
    }
}

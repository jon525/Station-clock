// Station Clock screen saver for Windows
// A European railway station clock on a black screen. The red second hand sweeps round in
// 58.5 seconds and waits at 12; on the full minute the minute hand jumps and it sets off again.
//
// Build:     run build.bat (uses the C# compiler that ships with Windows / .NET Framework 4)
// Install:   right-click StationClock.scr -> Install
// Settings:  right-click StationClock.scr -> Configure
//            Stored in the registry under HKEY_CURRENT_USER\Software\StationClock

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Station Clock")]
[assembly: System.Reflection.AssemblyProduct("Station Clock")]

namespace StationClock
{
    static class Config
    {
        public static int Size = 78;            // dial diameter as a percentage of the screen's shorter side
        public static bool Drift = true;        // wander slowly to avoid burn-in
        public static bool AllMonitors = true;  // false: main monitor only, others black
        const string RegPath = @"Software\StationClock";

        public static void Load()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegPath))
                {
                    if (k == null) return;
                    int v;
                    object o = k.GetValue("Size");
                    if (o != null && int.TryParse(o.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                        Size = Math.Max(20, Math.Min(100, v));
                    o = k.GetValue("Drift");
                    if (o != null) Drift = o.ToString() != "0";
                    o = k.GetValue("AllMonitors");
                    if (o != null) AllMonitors = o.ToString() != "0";
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RegPath))
                {
                    k.SetValue("Size", Size.ToString(CultureInfo.InvariantCulture));
                    k.SetValue("Drift", Drift ? "1" : "0");
                    k.SetValue("AllMonitors", AllMonitors ? "1" : "0");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save settings:\n" + ex.Message, "Station Clock");
            }
        }
    }

    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")]
        static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        public static readonly Stopwatch Clock = Stopwatch.StartNew();

        [STAThread]
        static void Main(string[] args)
        {
            Config.Load();

            // Windows starts a screen saver with /s, asks for settings with /c and a preview with /p.
            string mode = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "/c";
            if (mode.Length > 2) mode = mode.Substring(0, 2);
            if (mode == "/p") return;

            if (mode != "/s")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SettingsForm());
                return;
            }

            // Per-monitor DPI awareness so each screen's bounds are its real pixels.
            bool dpiSet = false;
            try { dpiSet = SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
            if (!dpiSet) try { SetProcessDPIAware(); } catch { }

            List<Form> forms = new List<Form>();
            foreach (Screen sc in Screen.AllScreens)
                forms.Add(new ClockForm(sc.Bounds, Config.AllMonitors || sc.Primary));

            ApplicationContext ctx = new ApplicationContext();
            bool closing = false;
            foreach (Form f in forms)
            {
                f.FormClosed += delegate
                {
                    if (closing) return;
                    closing = true;
                    foreach (Form o in forms) if (!o.IsDisposed) o.Close();
                    ctx.ExitThread();
                };
                f.Show();
            }
            Application.Run(ctx);
        }
    }

    // One full-screen window per monitor.
    class ClockForm : Form
    {
        const double SWEEP = 58.5;   // seconds the red hand takes to go round; it then waits at 12
        static readonly PointF[] HourPts = { new PointF(-4.9f, 24f), new PointF(4.9f, 24f), new PointF(4.1f, -60f), new PointF(-4.1f, -60f) };
        static readonly PointF[] MinutePts = { new PointF(-4f, 24f), new PointF(4f, 24f), new PointF(2.9f, -88f), new PointF(-2.9f, -88f) };

        Bitmap dial;                 // rim, face and minute marks, drawn once
        float k;                     // pixels per clock unit (the rim has a radius of 101 units)
        int half;                    // half the dial bitmap's side
        float driftX, driftY;
        Rectangle lastRect;
        Timer timer;
        Point firstMouse;
        bool haveMouse;
        SolidBrush handBrush = new SolidBrush(Color.FromArgb(0x16, 0x13, 0x0F));
        SolidBrush secBrush = new SolidBrush(Color.FromArgb(0x8F, 0x1A, 0x1C));
        SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(95, 30, 20, 4));

        public ClockForm(Rectangle b, bool showClock)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = b;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            BackColor = Color.Black;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            if (!showClock) return;

            int shorter = Math.Min(b.Width, b.Height);
            float radius = Config.Size / 100f * shorter / 2f;
            k = radius / 101f;
            half = (int)Math.Ceiling(radius) + 2;
            if (Config.Drift)
            {
                driftX = Math.Max(0f, Math.Min(shorter * 0.06f, b.Width / 2f - half));
                driftY = Math.Max(0f, Math.Min(shorter * 0.06f, b.Height / 2f - half));
            }

            dial = new Bitmap(half * 2, half * 2, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(dial))
            {
                g.Clear(Color.Black);
                DrawDial(g, half, half);
            }

            timer = new Timer();
            timer.Interval = 33;
            timer.Tick += delegate
            {
                Rectangle r = DialRect();
                Invalidate(lastRect.IsEmpty ? r : Rectangle.Union(lastRect, r));
                lastRect = r;
            };
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Cursor.Hide();
            if (timer != null) timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (timer != null) timer.Stop();
            base.OnFormClosed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e) { Close(); }
        protected override void OnMouseDown(MouseEventArgs e) { Close(); }
        protected override void OnMouseWheel(MouseEventArgs e) { Close(); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!haveMouse) { firstMouse = e.Location; haveMouse = true; return; }
            if (Math.Abs(e.X - firstMouse.X) > 8 || Math.Abs(e.Y - firstMouse.Y) > 8) Close();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        // Where the dial sits right now: the centre of the screen plus a very slow drift.
        Rectangle DialRect()
        {
            double t = Program.Clock.Elapsed.TotalSeconds;
            int cx = ClientSize.Width / 2 + (int)Math.Round(Math.Sin(t / 97.0) * driftX);
            int cy = ClientSize.Height / 2 + (int)Math.Round(Math.Cos(t / 131.0) * driftY);
            return new Rectangle(cx - half, cy - half, half * 2, half * 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.Black);
            if (dial == null) return;

            Rectangle r = DialRect();
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(dial, r);

            // Swiss railway clock movement.
            DateTime now = DateTime.Now;
            double s = now.Second + now.Millisecond / 1000.0;
            double kick = Math.Exp(-s * 11.0) * Math.Cos(s * 26.0);   // the minute hand's little bounce
            float sec = (float)(Math.Min(s / SWEEP, 1.0) * 360.0);
            float min = (float)(now.Minute * 6 - 6 * kick);
            float hr = (float)((now.Hour % 12) * 30 + now.Minute * 0.5 - 0.5 * kick);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            float ox = r.X + half, oy = r.Y + half;

            Place(g, ox, oy, hr, true); g.FillPolygon(shadowBrush, HourPts);
            Place(g, ox, oy, min, true); g.FillPolygon(shadowBrush, MinutePts);
            Place(g, ox, oy, hr, false); g.FillPolygon(handBrush, HourPts);
            Place(g, ox, oy, min, false); g.FillPolygon(handBrush, MinutePts);

            Place(g, ox, oy, sec, true);
            g.FillRectangle(shadowBrush, -0.8f, -52.6f, 1.6f, 82.6f);
            g.FillEllipse(shadowBrush, -7.6f, -67.6f, 15.2f, 15.2f);
            Place(g, ox, oy, sec, false);
            g.FillRectangle(secBrush, -0.8f, -58f, 1.6f, 88f);
            g.FillEllipse(secBrush, -7.6f, -67.6f, 15.2f, 15.2f);
            g.FillEllipse(secBrush, -2.2f, -2.2f, 4.4f, 4.4f);
            g.ResetTransform();
        }

        void Place(Graphics g, float ox, float oy, float angle, bool shadow)
        {
            g.ResetTransform();
            g.TranslateTransform(ox, oy);
            g.ScaleTransform(k, k);
            if (shadow) g.TranslateTransform(0.8f, 1.4f);
            g.RotateTransform(angle);
        }

        static Color Rgb(int rgb)
        {
            return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        }

        // Rim, face and the 60 minute marks.
        void DrawDial(Graphics g, float ox, float oy)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.TranslateTransform(ox, oy);
            g.ScaleTransform(k, k);

            using (LinearGradientBrush rim = new LinearGradientBrush(new PointF(-102f, -102f), new PointF(102f, 102f), Rgb(0x6B6252), Rgb(0x57503F)))
            {
                ColorBlend rb = new ColorBlend(3);
                rb.Colors = new Color[] { Rgb(0x6B6252), Rgb(0x3A342B), Rgb(0x57503F) };
                rb.Positions = new float[] { 0f, 0.5f, 1f };
                rim.InterpolationColors = rb;
                g.FillEllipse(rim, -101f, -101f, 202f, 202f);
            }
            using (SolidBrush b = new SolidBrush(Rgb(0x2B261F)))
                g.FillEllipse(b, -97.5f, -97.5f, 195f, 195f);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(-96f, -96f, 192f, 192f);
                using (PathGradientBrush face = new PathGradientBrush(p))
                {
                    face.CenterPoint = new PointF(0f, -6f);
                    ColorBlend fb = new ColorBlend(4);   // from the edge (0) to the centre (1)
                    fb.Colors = new Color[] { Rgb(0xC5AC75), Rgb(0xD3BD88), Rgb(0xE9DCB6), Rgb(0xF3EAD0) };
                    fb.Positions = new float[] { 0f, 0.06f, 0.39f, 1f };
                    face.InterpolationColors = fb;
                    g.FillEllipse(face, -96f, -96f, 192f, 192f);
                }
            }
            using (SolidBrush b = new SolidBrush(Rgb(0x141210)))
            {
                for (int i = 0; i < 60; i++)
                {
                    bool hourMark = i % 5 == 0;
                    float tw = hourMark ? 6.4f : 2.2f, len = hourMark ? 23f : 6.5f;
                    GraphicsState st = g.Save();
                    g.RotateTransform(i * 6f);
                    g.FillRectangle(b, -tw / 2f, -90f, tw, len);
                    g.Restore(st);
                }
            }
            g.ResetTransform();
        }
    }

    class SettingsForm : Form
    {
        public SettingsForm()
        {
            Text = "Station Clock settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(420, 190);

            Label name = new Label();
            name.Text = "Clock size";
            name.Location = new Point(14, 22);
            name.AutoSize = true;
            Controls.Add(name);

            Label val = new Label();
            val.Location = new Point(360, 22);
            val.Size = new Size(50, 20);
            val.Text = Config.Size + "%";
            Controls.Add(val);

            TrackBar size = new TrackBar();
            size.AutoSize = false;
            size.TickStyle = TickStyle.None;
            size.Location = new Point(90, 16);
            size.Size = new Size(265, 30);
            size.Minimum = 20;
            size.Maximum = 100;
            size.Value = Config.Size;
            size.ValueChanged += delegate { val.Text = size.Value + "%"; };
            Controls.Add(size);

            CheckBox drift = new CheckBox();
            drift.Text = "Drift slowly around the screen (prevents burn-in)";
            drift.Location = new Point(16, 62);
            drift.AutoSize = true;
            drift.Checked = Config.Drift;
            Controls.Add(drift);

            CheckBox all = new CheckBox();
            all.Text = "Show on every monitor (otherwise the main one only)";
            all.Location = new Point(16, 92);
            all.AutoSize = true;
            all.Checked = Config.AllMonitors;
            Controls.Add(all);

            Button ok = new Button();
            ok.Text = "OK";
            ok.Location = new Point(214, 146);
            ok.Size = new Size(92, 28);
            ok.Click += delegate
            {
                Config.Size = size.Value;
                Config.Drift = drift.Checked;
                Config.AllMonitors = all.Checked;
                Config.Save();
                Close();
            };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.Location = new Point(314, 146);
            cancel.Size = new Size(92, 28);
            cancel.Click += delegate { Close(); };
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}

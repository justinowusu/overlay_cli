using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Overlay
{
    public class OverlayForm : Form
    {
        private Rectangle highlightRect;
        private Timer fadeTimer;
        private float opacity = 0.0f;
        private const float targetOpacity = 0.2f;
        private const float animationDuration = 0.3f;
        private const float displayDuration = 2.8f;
        private DateTime animationStartTime;
        private bool isFadingIn = true;
        private Screen targetScreen;

        public OverlayForm(Rectangle highlightRect, Screen targetScreen)
        {
            this.targetScreen = targetScreen;
            this.highlightRect = ConvertToScreenCoordinates(highlightRect, targetScreen);
            
            // Configure form properties for overlay
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = targetScreen.Bounds.Location;
            this.Size = targetScreen.Bounds.Size;
            
            // Start animation
            StartFadeInAnimation();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80000; // WS_EX_LAYERED
                cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT
                return cp;
            }
        }

        private Rectangle ConvertToScreenCoordinates(Rectangle rect, Screen screen)
        {
            // The input rectangle is in global screen coordinates
            // We need to convert it to coordinates relative to the target screen
            return new Rectangle(
                rect.X - screen.Bounds.X,
                rect.Y - screen.Bounds.Y,
                rect.Width,
                rect.Height
            );
        }

        private void UpdateLayeredWindow()
        {
            // Create a bitmap for the entire screen
            Bitmap bitmap = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb);
            
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // Clear with transparent
                g.Clear(Color.Transparent);
                
                if (opacity > 0)
                {
                    // Draw the filled rectangle with transparency
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb((int)(opacity * 255), 0, 122, 255)))
                    {
                        g.FillRectangle(brush, highlightRect);
                    }

                    // Draw the border with higher opacity (capped at 255)
                    int borderAlpha = Math.Min(255, (int)(opacity * 4 * 255));
                    using (Pen pen = new Pen(Color.FromArgb(borderAlpha, 0, 122, 255), 2))
                    {
                        g.DrawRectangle(pen, highlightRect);
                    }
                }
            }

            // Get device contexts
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                // Get handle to the bitmap
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);

                // Set up blend function
                BLENDFUNCTION blend = new BLENDFUNCTION();
                blend.BlendOp = AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = AC_SRC_ALPHA;

                // Update the window
                SIZE size = new SIZE(bitmap.Width, bitmap.Height);
                POINT pointSource = new POINT(0, 0);
                POINT topPos = new POINT(this.Left, this.Top);

                UpdateLayeredWindow(this.Handle, screenDc, ref topPos, ref size, memDc, ref pointSource, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                // Clean up
                ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    SelectObject(memDc, oldBitmap);
                    DeleteObject(hBitmap);
                }
                DeleteDC(memDc);
                bitmap.Dispose();
            }
        }

        private void StartFadeInAnimation()
        {
            animationStartTime = DateTime.Now;
            isFadingIn = true;

            fadeTimer = new Timer();
            fadeTimer.Interval = 10; // 10ms updates
            fadeTimer.Tick += FadeInTick;
            fadeTimer.Start();
        }

        private void FadeInTick(object sender, EventArgs e)
        {
            double elapsed = (DateTime.Now - animationStartTime).TotalSeconds;
            
            if (elapsed >= animationDuration)
            {
                opacity = targetOpacity;
                fadeTimer.Stop();
                fadeTimer.Dispose();
                
                // Update the display
                UpdateLayeredWindow();
                
                // Schedule fade out
                Timer displayTimer = new Timer();
                displayTimer.Interval = (int)(displayDuration * 1000);
                displayTimer.Tick += (s, args) =>
                {
                    displayTimer.Stop();
                    displayTimer.Dispose();
                    StartFadeOutAnimation();
                };
                displayTimer.Start();
            }
            else
            {
                opacity = (float)(elapsed / animationDuration * targetOpacity);
                UpdateLayeredWindow();
            }
        }

        private void StartFadeOutAnimation()
        {
            animationStartTime = DateTime.Now;
            isFadingIn = false;

            fadeTimer = new Timer();
            fadeTimer.Interval = 10; // 10ms updates
            fadeTimer.Tick += FadeOutTick;
            fadeTimer.Start();
        }

        private void FadeOutTick(object sender, EventArgs e)
        {
            double elapsed = (DateTime.Now - animationStartTime).TotalSeconds;
            
            if (elapsed >= animationDuration)
            {
                opacity = 0;
                fadeTimer.Stop();
                fadeTimer.Dispose();
                
                // Update the display one last time
                UpdateLayeredWindow();
                
                // Close the form and exit application
                this.Close();
                Application.Exit();
            }
            else
            {
                opacity = targetOpacity * (float)(1 - elapsed / animationDuration);
                UpdateLayeredWindow();
            }
        }

        #region P/Invoke declarations

        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;
        private const int ULW_ALPHA = 0x02;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int Width;
            public int Height;

            public SIZE(int width, int height)
            {
                this.Width = width;
                this.Height = height;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        #endregion
    }
}
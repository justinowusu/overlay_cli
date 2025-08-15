using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Overlay
{
    public class MessagePopupForm : Form
    {
        private string message;
        private Rectangle targetRect;
        private Screen targetScreen;
        private Timer fadeTimer;
        private float opacity = 0.0f;
        private const float targetOpacity = 0.98f;
        private const float animationDuration = 0.25f;
        private const float displayDuration = 3.1f;
        private DateTime animationStartTime;
        private bool isFadingIn = true;
        private const int popupHeight = 50;
        private const int minWidth = 100;
        private const int horizontalPadding = 40; // 20px padding on each side

        public MessagePopupForm(string message, Rectangle targetRect, Screen targetScreen)
        {
            this.message = message;
            this.targetRect = targetRect;
            this.targetScreen = targetScreen;

            // Calculate popup width based on actual text size
            int popupWidth = CalculatePopupWidth(message);

            // Configure form properties
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.AutoScaleMode = AutoScaleMode.None;
            
            // Calculate position
            Point position = CalculatePopupPosition(targetRect, targetScreen, popupWidth, popupHeight);
            this.Location = position;
            this.Size = new Size(popupWidth, popupHeight);
            
            // Start animation with a small delay
            Timer delayTimer = new Timer();
            delayTimer.Interval = 50; // 0.05 seconds delay
            delayTimer.Tick += (s, e) =>
            {
                delayTimer.Stop();
                delayTimer.Dispose();
                StartFadeInAnimation();
            };
            delayTimer.Start();
        }

        private int CalculatePopupWidth(string message)
        {
            // Create a temporary bitmap to measure text
            using (Bitmap tempBitmap = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(tempBitmap))
            using (Font font = new Font("Segoe UI", 15f, FontStyle.Regular))
            {
                // Measure the actual text width
                SizeF textSize = g.MeasureString(message, font);
                int calculatedWidth = (int)Math.Ceiling(textSize.Width) + horizontalPadding;
                
                // Ensure minimum width and add some extra buffer
                return Math.Max(calculatedWidth + 20, minWidth);
            }
        }

        private Point CalculatePopupPosition(Rectangle rect, Screen screen, int popupWidth, int popupHeight)
        {
            // Calculate center X position
            int popupX = rect.X + (rect.Width - popupWidth) / 2;
            
            // Try to position above the rectangle with 8px gap
            int popupY = rect.Y - popupHeight - 8;
            
            // Convert to screen-relative coordinates
            popupX -= screen.Bounds.X;
            popupY -= screen.Bounds.Y;
            
            // Check if popup extends beyond screen bounds
            Rectangle screenBounds = new Rectangle(0, 0, screen.Bounds.Width, screen.Bounds.Height);
            
            // Check horizontal bounds
            if (popupX + popupWidth > screenBounds.Right - 10)
            {
                popupX = screenBounds.Right - popupWidth - 10;
            }
            if (popupX < 10)
            {
                popupX = 10;
            }
            
            // Check vertical bounds - if no room above, place below
            if (popupY < 10)
            {
                // Place below the rectangle instead
                popupY = (rect.Y - screen.Bounds.Y) + rect.Height + 8;
            }
            
            // Add screen offset back to get absolute position
            return new Point(popupX + screen.Bounds.X, popupY + screen.Bounds.Y);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80000; // WS_EX_LAYERED
                cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        private void UpdateLayeredWindow()
        {
            Bitmap bitmap = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb);
            
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                
                if (opacity > 0)
                {
                    // Enable high quality rendering
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    
                    // Create rounded rectangle path
                    using (GraphicsPath path = CreateRoundedRectangle(new Rectangle(0, 0, this.Width, this.Height), 16))
                    {
                        // Create gradient brush
                        using (LinearGradientBrush gradientBrush = new LinearGradientBrush(
                            new Point(0, this.Height),
                            new Point(this.Width, 0),
                            Color.FromArgb((int)(opacity * 255), 38, 77, 191),  // Darker Blue
                            Color.FromArgb((int)(opacity * 255), 89, 64, 179))) // Darker Purple
                        {
                            g.FillPath(gradientBrush, path);
                        }
                    }
                    
                    // Draw the text with shadow
                    using (Font font = new Font("Segoe UI", 15f, FontStyle.Regular))
                    using (StringFormat format = new StringFormat())
                    {
                        format.Alignment = StringAlignment.Center;
                        format.LineAlignment = StringAlignment.Center;
                        format.Trimming = StringTrimming.EllipsisCharacter;
                        
                        Rectangle textRect = new Rectangle(10, 0, this.Width - 20, this.Height);
                        
                        // Draw shadow
                        using (Brush shadowBrush = new SolidBrush(Color.FromArgb((int)(opacity * 76), 0, 0, 0))) // 30% black
                        {
                            Rectangle shadowRect = new Rectangle(textRect.X, textRect.Y + 1, textRect.Width, textRect.Height);
                            g.DrawString(message, font, shadowBrush, shadowRect, format);
                        }
                        
                        // Draw text
                        using (Brush textBrush = new SolidBrush(Color.FromArgb((int)(opacity * 255), 255, 255, 255)))
                        {
                            g.DrawString(message, font, textBrush, textRect, format);
                        }
                    }
                }
            }

            // Update the layered window
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);

                BLENDFUNCTION blend = new BLENDFUNCTION();
                blend.BlendOp = AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = AC_SRC_ALPHA;

                SIZE size = new SIZE(bitmap.Width, bitmap.Height);
                POINT pointSource = new POINT(0, 0);
                POINT topPos = new POINT(this.Left, this.Top);

                UpdateLayeredWindow(this.Handle, screenDc, ref topPos, ref size, memDc, ref pointSource, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
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

        private GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            
            return path;
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
            fadeTimer.Interval = 10;
            fadeTimer.Tick += FadeOutTick;
            fadeTimer.Start();
        }

        private void FadeOutTick(object sender, EventArgs e)
        {
            double elapsed = (DateTime.Now - animationStartTime).TotalSeconds;
            
            if (elapsed >= 0.3) // 0.3 second fade out
            {
                opacity = 0;
                fadeTimer.Stop();
                fadeTimer.Dispose();
                
                UpdateLayeredWindow();
                this.Close();
            }
            else
            {
                opacity = targetOpacity * (float)(1 - elapsed / 0.3);
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
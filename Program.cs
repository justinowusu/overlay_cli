using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Overlay
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Parse command line arguments
            if (args.Length != 4)
            {
                Console.WriteLine("Usage: Overlay.exe <x> <y> <width> <height>");
                Environment.Exit(1);
            }

            int x = 0, y = 0, width = 0, height = 0;
            if (!int.TryParse(args[0], out x) ||
                !int.TryParse(args[1], out y) ||
                !int.TryParse(args[2], out width) ||
                !int.TryParse(args[3], out height))
            {
                Console.WriteLine("All parameters must be integers");
                Environment.Exit(1);
            }

            // Create the highlight rectangle
            Rectangle highlightRect = new Rectangle(x, y, width, height);

            // Find the screen that contains the rectangle
            Screen targetScreen = FindScreenContainingRect(highlightRect);

            if (targetScreen == null)
            {
                Console.WriteLine("No screen found for the given rectangle!");
                Environment.Exit(1);
            }

            // Enable visual styles for better rendering
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Small delay to ensure smooth startup (similar to Swift version)
            Thread.Sleep(150);

            // Create and show the overlay form
            OverlayForm overlayForm = new OverlayForm(highlightRect, targetScreen);
            overlayForm.Show();

            // Run the application
            Application.Run(overlayForm);
        }

        static Screen FindScreenContainingRect(Rectangle rect)
        {
            Point centerPoint = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

            // Check if any screen contains the center point
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.Bounds.Contains(centerPoint))
                {
                    return screen;
                }
            }

            // If no screen contains the center, check if any screen intersects with the rect
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.Bounds.IntersectsWith(rect))
                {
                    return screen;
                }
            }

            // Fallback to primary screen
            return Screen.PrimaryScreen;
        }
    }
}

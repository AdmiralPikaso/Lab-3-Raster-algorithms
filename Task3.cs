using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Imaging;

namespace RasterAlgorithms
{
    public static class Task3
    {
        public static int Draw(Bitmap image, Point p1, Color c1, Point p2, Color c2, Point p3, Color c3)
        {
            int minX = Math.Max(0, Math.Min(p1.X, Math.Min(p2.X, p3.X)));
            int maxX = Math.Min(image.Width - 1, Math.Max(p1.X, Math.Max(p2.X, p3.X)));
            int minY = Math.Max(0, Math.Min(p1.Y, Math.Min(p2.Y, p3.Y)));
            int maxY = Math.Min(image.Height - 1, Math.Max(p1.Y, Math.Max(p2.Y, p3.Y)));

            float denominator = (p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y);
            if (Math.Abs(denominator) < 1e-6f) return 0;

            int painted = 0;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float alpha = ((p2.Y - p3.Y) * (x - p3.X) + (p3.X - p2.X) * (y - p3.Y)) / denominator;
                    float beta = ((p3.Y - p1.Y) * (x - p3.X) + (p1.X - p3.X) * (y - p3.Y)) / denominator;
                    float gamma = 1f - alpha - beta;

                    if (alpha < 0 || beta < 0 || gamma < 0) continue;

                    int r = (int)(alpha * c1.R + beta * c2.R + gamma * c3.R);
                    int g = (int)(alpha * c1.G + beta * c2.G + gamma * c3.G);
                    int b = (int)(alpha * c1.B + beta * c2.B + gamma * c3.B);

                    image.SetPixel(x, y, Color.FromArgb(
                        Math.Clamp(r, 0, 255),
                        Math.Clamp(g, 0, 255),
                        Math.Clamp(b, 0, 255)));
                    painted++;
                }
            }
            return painted;
        }
    }
}

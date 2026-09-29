using System.Drawing;
using RasterAlgorithms;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

using Bitmap image = new(12, 12);
using (Graphics g = Graphics.FromImage(image)) g.Clear(Color.White);
using Bitmap tileForOutside = new(1, 1);
tileForOutside.SetPixel(0, 0, Color.Red);
Check(RasterOperations.FillPattern(image, new Point(0, 0), tileForOutside) == 0,
    "Unbounded area was filled");
Check(image.GetPixel(0, 0).ToArgb() == Color.White.ToArgb(),
    "Rejected fill changed the canvas");
for (int x = 1; x <= 10; x++)
{
    image.SetPixel(x, 1, Color.Black);
    image.SetPixel(x, 10, Color.Black);
}
for (int y = 1; y <= 10; y++)
{
    image.SetPixel(1, y, Color.Black);
    image.SetPixel(10, y, Color.Black);
}
for (int x = 4; x <= 7; x++)
{
    image.SetPixel(x, 4, Color.Black);
    image.SetPixel(x, 7, Color.Black);
}
for (int y = 4; y <= 7; y++)
{
    image.SetPixel(4, y, Color.Black);
    image.SetPixel(7, y, Color.Black);
}

int filled = RasterOperations.Fill(image, new Point(2, 2), (_, _) => Color.Blue);
Check(filled == 48, $"Expected 48 filled pixels, got {filled}");
Check(image.GetPixel(5, 5).ToArgb() == Color.White.ToArgb(), "Hole was filled");
Check(image.GetPixel(1, 1).ToArgb() == Color.Black.ToArgb(), "Boundary was changed");
Check(RasterOperations.FillPattern(image, new Point(0, 0), tileForOutside) == 0,
    "Click outside the contour filled the background");

using Bitmap tile = new(2, 2);
tile.SetPixel(0, 0, Color.White);
tile.SetPixel(1, 0, Color.Red);
tile.SetPixel(0, 1, Color.Green);
tile.SetPixel(1, 1, Color.Yellow);
int patterned = RasterOperations.FillPattern(image, new Point(5, 5), tile);
Check(patterned == 4, $"Expected 4 pixels in hole, got {patterned}");
Check(image.GetPixel(5, 5).ToArgb() == Color.Yellow.ToArgb(), "Pattern phase is incorrect");
Check(image.GetPixel(6, 6).ToArgb() == Color.White.ToArgb(), "White tile pixel was not copied");

List<Point> outline = RasterOperations.TraceBoundary(image, new Point(1, 5));
Check(outline.Count == 36, $"Expected 36 outline points, got {outline.Count}");
Check(outline.Distinct().Count() == 36, "Outline repeats pixels");
Check(outline.All(p => image.GetPixel(p.X, p.Y).ToArgb() == Color.Black.ToArgb()),
    "Outline left the selected color");
for (int i = 0; i < outline.Count; i++)
{
    Point a = outline[i];
    Point b = outline[(i + 1) % outline.Count];
    Check(Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1,
        "Outline points are not adjacent");
}

using Bitmap shortLine = new(4, 4);
using (Graphics g = Graphics.FromImage(shortLine)) g.Clear(Color.White);
shortLine.SetPixel(1, 1, Color.Black);
shortLine.SetPixel(2, 1, Color.Black);
List<Point> shortOutline = RasterOperations.TraceBoundary(shortLine, new Point(2, 1));
Check(shortOutline.Count == 2 && shortOutline.Distinct().Count() == 2,
    $"Two-pixel component should yield two points; got {shortOutline.Count}");

Console.WriteLine("PASS: fill with hole, cyclic pattern, ordered closed outline");

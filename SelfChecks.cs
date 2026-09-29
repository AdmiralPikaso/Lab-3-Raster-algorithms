using System.Drawing;

namespace RasterAlgorithms;

internal static class SelfChecks
{
public static void Run()
{
static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

using Bitmap image = new(12, 12);
using (Graphics g = Graphics.FromImage(image)) g.Clear(Color.White);
using Bitmap tileForOutside = new(1, 1);
tileForOutside.SetPixel(0, 0, Color.Red);
Check(Task1.FillPattern(image, new Point(0, 0), tileForOutside) == 0,
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

int filled = Task1.Fill(image, new Point(2, 2), (_, _) => Color.Blue);
Check(filled == 48, $"Expected 48 filled pixels, got {filled}");
Check(image.GetPixel(5, 5).ToArgb() == Color.White.ToArgb(), "Hole was filled");
Check(image.GetPixel(1, 1).ToArgb() == Color.Black.ToArgb(), "Boundary was changed");
Check(Task1.FillPattern(image, new Point(0, 0), tileForOutside) == 0,
    "Click outside the contour filled the background");

using Bitmap tile = new(2, 2);
tile.SetPixel(0, 0, Color.White);
tile.SetPixel(1, 0, Color.Red);
tile.SetPixel(0, 1, Color.Green);
tile.SetPixel(1, 1, Color.Yellow);
int patterned = Task1.FillPattern(image, new Point(5, 5), tile);
Check(patterned == 4, $"Expected 4 pixels in hole, got {patterned}");
Check(image.GetPixel(5, 5).ToArgb() == Color.Yellow.ToArgb(), "Pattern phase is incorrect");
Check(image.GetPixel(6, 6).ToArgb() == Color.White.ToArgb(), "White tile pixel was not copied");

using Bitmap smallArea = new(7, 7);
using (Graphics g = Graphics.FromImage(smallArea)) g.Clear(Color.White);
for (int coordinate = 0; coordinate < 7; coordinate++)
{
    smallArea.SetPixel(coordinate, 0, Color.Black);
    smallArea.SetPixel(coordinate, 6, Color.Black);
    smallArea.SetPixel(0, coordinate, Color.Black);
    smallArea.SetPixel(6, coordinate, Color.Black);
}
using Bitmap largePattern = new(20, 20);
largePattern.SetPixel(2, 2, Color.Magenta);
largePattern.SetPixel(4, 4, Color.Cyan);
Check(Task1.FillPattern(smallArea, new Point(3, 3), largePattern) == 25,
    "Large pattern did not fill the small area");
Check(smallArea.GetPixel(2, 2).ToArgb() == Color.Magenta.ToArgb()
    && smallArea.GetPixel(4, 4).ToArgb() == Color.Cyan.ToArgb(),
    "Large pattern was scaled or shifted");

List<Point> outline = Task1.TraceBoundary(image, new Point(1, 5));
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
List<Point> shortOutline = Task1.TraceBoundary(shortLine, new Point(2, 1));
Check(shortOutline.Count == 2 && shortOutline.Distinct().Count() == 2,
    $"Two-pixel component should yield two points; got {shortOutline.Count}");

// Задание 2: целочисленный Брезенхем.

static IEnumerable<Point> Canonical(IEnumerable<Point> points) =>
    points.OrderBy(p => p.Y).ThenBy(p => p.X);

static void CheckBresenham(Point from, Point to)
{
    List<Point> points = Task2.BresenhamPoints(from, to);
    int expected = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y)) + 1;
    Check(points.Count == expected, $"{from}->{to}: expected {expected} points, got {points.Count}");
    Check(points[0] == from && points[^1] == to, $"{from}->{to}: endpoint lost");
    Check(points.Distinct().Count() == points.Count, $"{from}->{to}: repeated pixels");
    for (int i = 1; i < points.Count; i++)
        Check(Math.Abs(points[i].X - points[i - 1].X) <= 1 && Math.Abs(points[i].Y - points[i - 1].Y) <= 1,
            $"{from}->{to}: step {i} is not adjacent");

    int nx = to.X - from.X, ny = to.Y - from.Y;
    double length = Math.Sqrt((double)nx * nx + (double)ny * ny);
    if (length == 0) return;
    foreach (Point p in points)
    {
        double distance = Math.Abs(ny * (p.X - from.X) - nx * (p.Y - from.Y)) / length;
        Check(distance <= 0.5 + 1e-9, $"{from}->{to}: deviation {distance:F3} at {p}");
    }
}

CheckBresenham(new Point(0, 0), new Point(0, 0));
CheckBresenham(new Point(2, 5), new Point(2, 5));
CheckBresenham(new Point(0, 3), new Point(11, 3));
CheckBresenham(new Point(4, 0), new Point(4, 9));
CheckBresenham(new Point(0, 0), new Point(9, 9));
CheckBresenham(new Point(9, 9), new Point(0, 0));
CheckBresenham(new Point(3, 27), new Point(40, 6));
CheckBresenham(new Point(40, 6), new Point(3, 27));
CheckBresenham(new Point(1, 29), new Point(2, 5));
CheckBresenham(new Point(-40, 12), new Point(17, -33));

Check(Canonical(Task2.BresenhamPoints(new Point(3, 27), new Point(40, 6)))
    .SequenceEqual(Canonical(Task2.BresenhamPoints(new Point(40, 6), new Point(3, 27)))),
    "Bresenham depends on the point order");
Check(Canonical(Task2.BresenhamPoints(new Point(3, 27), new Point(40, 6)))
    .SequenceEqual(Canonical(Task2.BresenhamPoints(new Point(27, 3), new Point(6, 40))
        .Select(p => new Point(p.Y, p.X)))),
    "Bresenham depends on the coordinate order");

using Bitmap bresenhamCanvas = new(20, 20);
using (Graphics g = Graphics.FromImage(bresenhamCanvas)) g.Clear(Color.White);
int bresenhamDrawn = Task2.DrawBresenham(bresenhamCanvas, new Point(2, 2), new Point(17, 5), Color.Black);
Check(bresenhamDrawn == 16, $"Expected 16 Bresenham pixels, got {bresenhamDrawn}");
Check(Task2.BresenhamPoints(new Point(2, 2), new Point(17, 5))
    .All(p => bresenhamCanvas.GetPixel(p.X, p.Y).ToArgb() == Color.Black.ToArgb()),
    "Bresenham missed a pixel of its own set");

// Задание 2: алгоритм Ву.

static Bitmap WhiteCanvas(int width, int height)
{
    Bitmap bitmap = new(width, height);
    using (Graphics g = Graphics.FromImage(bitmap)) g.Clear(Color.White);
    return bitmap;
}

static long Ink(Bitmap bitmap)
{
    long ink = 0;
    for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
            ink += 255 - bitmap.GetPixel(x, y).R;
    return ink;
}

using Bitmap wu = WhiteCanvas(32, 32);
int wuDrawn = Task2.DrawWu(wu, new Point(2, 3), new Point(29, 24), Color.Black);
// 28 позиций по старшей оси; в четырёх из них дробная часть равна нулю и
// закрашивается один пиксель, в остальных — два.
Check(wuDrawn == 52, $"Expected 52 Wu pixels, got {wuDrawn}");
Check(wu.GetPixel(2, 3).ToArgb() == Color.Black.ToArgb(), "Wu start is not solid");
Check(wu.GetPixel(29, 24).ToArgb() == Color.Black.ToArgb(), "Wu end is not solid");
// Суммарное покрытие равно длине отрезка: на каждой позиции по старшей оси
// два пикселя получают прозрачности, в сумме дающие 255.
Check(Ink(wu) == 28L * 255, $"Wu covers {Ink(wu)} instead of {28L * 255}");
Check(wu.GetPixel(3, 3).R > 0 && wu.GetPixel(3, 4).R > 0,
    "Wu did not spread coverage over the neighbouring row");
Check(wu.GetPixel(2, 4).R == 255, "Wu painted a pixel with zero coverage");

using Bitmap wuSteep = WhiteCanvas(16, 40);
Task2.DrawWu(wuSteep, new Point(10, 1), new Point(12, 38), Color.Black);
Check(Ink(wuSteep) == 38L * 255, $"Steep Wu covers {Ink(wuSteep)} instead of {38L * 255}");
for (int y = 0; y < 40; y++)
    Check(wuSteep.GetPixel(9, y).R == 255 && wuSteep.GetPixel(14, y).R == 255,
        $"Steep Wu touched column beyond the gap at y={y}");

using Bitmap wuVertical = WhiteCanvas(8, 8);
Check(Task2.DrawWu(wuVertical, new Point(3, 1), new Point(3, 6), Color.Black) == 6,
    "Wu vertical line is not one pixel wide");
Check(Ink(wuVertical) == 6L * 255, $"Wu vertical line covers {Ink(wuVertical)}");
for (int y = 0; y < 8; y++)
    for (int x = 0; x < 8; x++)
        Check(wuVertical.GetPixel(x, y).R == (x == 3 && y is >= 1 and <= 6 ? 0 : 255),
            "Wu vertical line touched a neighbour column");

using Bitmap wuSingle = WhiteCanvas(8, 8);
Check(Task2.DrawWu(wuSingle, new Point(4, 4), new Point(4, 4), Color.Black) == 1,
    "Wu degenerate segment should paint one pixel");
Check(wuSingle.GetPixel(4, 4).ToArgb() == Color.Black.ToArgb(), "Wu degenerate segment missed its pixel");

// Отрезок обрезается по холсту, а за границу не выходит.
using Bitmap clipped = WhiteCanvas(8, 8);
Check(Task2.DrawWu(clipped, new Point(4, 0), new Point(4, 7), Color.Black) == 8, "Wu lost clipped pixels");
Check(Task2.DrawBresenham(clipped, new Point(0, 2), new Point(7, 2), Color.Black) == 8,
    "Bresenham lost clipped pixels");
Check(Task2.DrawBresenham(clipped, new Point(-20, -20), new Point(40, 40), Color.Black) == 8,
    "Bresenham wrote outside the canvas");

using Bitmap mixed = WhiteCanvas(16, 16);
Task2.DrawWu(mixed, new Point(1, 1), new Point(1, 1), Color.Black);
Check(Task2.DrawLine(mixed, new Point(1, 1), new Point(14, 14), Color.Black, LineAlgorithm.Wu)
    == Task2.DrawWu(mixed, new Point(1, 1), new Point(14, 14), Color.Black),
    "DrawLine does not dispatch to Wu");

// Направление не должно влиять на результат: покрытие отрезка, заданного с
// другого конца, совпадает с прямой растеризацией.
static void CheckWuDirection(Point from, Point to)
{
    using Bitmap forward = WhiteCanvas(24, 24);
    using Bitmap backward = WhiteCanvas(24, 24);
    int a = Task2.DrawWu(forward, from, to, Color.Black);
    int b = Task2.DrawWu(backward, to, from, Color.Black);
    int major = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y)) + 1;
    Check(a == b, $"{from}->{to}: {a} pixels forward, {b} backward");
    Check(Ink(forward) == Ink(backward), $"{from}->{to}: coverage depends on direction");
    Check(Ink(forward) == major * 255L,
        $"{from}->{to}: coverage {Ink(forward)} instead of {major * 255L}");
    Check(forward.GetPixel(from.X, from.Y).ToArgb() == Color.Black.ToArgb()
        && forward.GetPixel(to.X, to.Y).ToArgb() == Color.Black.ToArgb(),
        $"{from}->{to}: endpoints are not solid");
}

CheckWuDirection(new Point(4, 3), new Point(20, 15));
CheckWuDirection(new Point(20, 15), new Point(4, 3));
CheckWuDirection(new Point(20, 3), new Point(4, 15));
CheckWuDirection(new Point(4, 15), new Point(20, 3));
CheckWuDirection(new Point(3, 3), new Point(3, 20));
CheckWuDirection(new Point(3, 20), new Point(3, 3));
CheckWuDirection(new Point(3, 20), new Point(5, 3));
CheckWuDirection(new Point(5, 3), new Point(3, 20));
CheckWuDirection(new Point(12, 12), new Point(12, 12));

// Растеризация лежит в кадре и совпадает с Брезенхемом на общих пикселях.
using Bitmap compared = WhiteCanvas(24, 24);
Task2.DrawBresenham(compared, new Point(3, 18), new Point(20, 4), Color.Black);
using Bitmap comparedWu = WhiteCanvas(24, 24);
Task2.DrawWu(comparedWu, new Point(3, 18), new Point(20, 4), Color.Black);
Check(Task2.BresenhamPoints(new Point(3, 18), new Point(20, 4))
    .All(p => comparedWu.GetPixel(p.X, p.Y).R < 128),
    "Wu did not cover the whole Bresenham skeleton");

// Три вершины сохраняют заданные цвета, а внутренняя точка получает их смесь.
using Bitmap triangle = WhiteCanvas(10, 10);
int trianglePixels = Task3.FillTriangle(triangle,
    new Point(1, 1), Color.Red,
    new Point(7, 1), Color.Lime,
    new Point(1, 7), Color.Blue);
Check(trianglePixels > 20, "Triangle has too few pixels");
Check(triangle.GetPixel(1, 1).ToArgb() == Color.Red.ToArgb(), "First vertex color is wrong");
Check(triangle.GetPixel(7, 1).ToArgb() == Color.Lime.ToArgb(), "Second vertex color is wrong");
Check(triangle.GetPixel(1, 7).ToArgb() == Color.Blue.ToArgb(), "Third vertex color is wrong");
Check(triangle.GetPixel(4, 4).ToArgb() == Color.FromArgb(0, 128, 128).ToArgb(),
    "Triangle color interpolation is wrong");
Check(triangle.GetPixel(8, 8).ToArgb() == Color.White.ToArgb(),
    "Triangle painted outside its bounds");
using Bitmap reversedTriangle = WhiteCanvas(10, 10);
Task3.FillTriangle(reversedTriangle,
    new Point(1, 7), Color.Blue,
    new Point(7, 1), Color.Lime,
    new Point(1, 1), Color.Red);
for (int y = 0; y < 10; y++)
    for (int x = 0; x < 10; x++)
        Check(triangle.GetPixel(x, y).ToArgb() == reversedTriangle.GetPixel(x, y).ToArgb(),
            "Triangle rasterization depends on vertex order");
Check(Task3.FillTriangle(triangle,
    new Point(1, 1), Color.Red,
    new Point(2, 2), Color.Green,
    new Point(3, 3), Color.Blue) == 0,
    "Degenerate triangle should not be painted");

using MainForm form = new();

Console.WriteLine("PASS: form startup, fill with hole, cyclic and large patterns, ordered closed outline, " +
                  "Bresenham, Wu, gradient triangle");
}
}

using System.Drawing;
using System.Drawing.Drawing2D;

namespace RasterAlgorithms;

/// <summary>Способ растеризации отрезка.</summary>
public enum LineAlgorithm
{
    Bresenham,
    Wu
}

/// <summary>
/// Рисование отрезка целочисленным алгоритмом Брезенхема и алгоритмом Ву.
/// Пиксели Брезенхема закрашиваются точным цветом, Ву смешивает цвет с фоном,
/// поэтому отрезок получается сглаженным и не имеет ступенек.
/// </summary>
public static class Task2
{
    /// <summary>
    /// Целочисленный алгоритм Брезенхема. Накапливается ошибка смещения, по её
    /// знаку выбирается шаг по старшей оси, поэтому точки отрезка всегда
    /// восьмисвязны и отклоняются от идеальной прямой не больше чем на половину
    /// пикселя. Деление и вещественные числа не используются.
    /// </summary>
    public static List<Point> BresenhamPoints(Point from, Point to)
    {
        List<Point> points = [];
        int x = from.X;
        int y = from.Y;
        int dx = Math.Abs(to.X - from.X);
        int dy = -Math.Abs(to.Y - from.Y);
        int sx = from.X < to.X ? 1 : -1;
        int sy = from.Y < to.Y ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            points.Add(new Point(x, y));
            if (x == to.X && y == to.Y) break;
            // Удвоение ошибки позволяет проверить оба условия по одному значению:
            // иначе первое изменение исказило бы второе.
            int twice = 2 * error;
            if (twice >= dy) { error += dy; x += sx; }
            if (twice <= dx) { error += dx; y += sy; }
        }
        return points;
    }

    /// <summary>
    /// Рисует отрезок алгоритмом Брезенхема точным цветом. Возвращает число
    /// пикселей, попавших внутрь холста.
    /// </summary>
    public static int DrawBresenham(Bitmap image, Point from, Point to, Color color)
    {
        ArgumentNullException.ThrowIfNull(image);
        int drawn = 0;
        foreach (Point point in BresenhamPoints(from, to))
        {
            if (!Inside(image, point.X, point.Y)) continue;
            image.SetPixel(point.X, point.Y, color);
            drawn++;
        }
        return drawn;
    }

    /// <summary>
    /// Рисует отрезок алгоритмом Ву: шаг делается по старшей оси, а в каждой
    /// позиции закрашиваются два соседних пикселя с взаимно дополнительной
    /// прозрачностью, пропорциональной дробной части ординаты. Возвращает число
    /// пикселей, получивших ненулевое покрытие.
    /// </summary>
    public static int DrawWu(Bitmap image, Point from, Point to, Color color)
    {
        ArgumentNullException.ThrowIfNull(image);
        int drawn = 0;
        WuCore(from, to, (point, alpha) =>
        {
            if (alpha <= 0 || !Inside(image, point.X, point.Y)) return;
            Blend(image, point.X, point.Y, color, alpha);
            drawn++;
        });
        return drawn;
    }

    /// <summary>Рисует отрезок выбранным алгоритмом.</summary>
    public static int DrawLine(Bitmap image, Point from, Point to, Color color, LineAlgorithm algorithm) =>
        algorithm == LineAlgorithm.Wu
            ? DrawWu(image, from, to, color)
            : DrawBresenham(image, from, to, color);

    /// <summary>
    /// Общий шаг алгоритма Ву без привязки к холсту. Пары «пиксель — прозрачность»
    /// передаются в plot, что позволяет проверять результат без рисования.
    /// Координаты приводятся к виду, где шаг идёт по x, а затем возвращаются.
    /// </summary>
    private static void WuCore(Point from, Point to, Action<Point, int> plot)
    {
        bool steep = Math.Abs(to.Y - from.Y) > Math.Abs(to.X - from.X);
        int x0 = from.X, y0 = from.Y, x1 = to.X, y1 = to.Y;
        if (steep) { (x0, y0) = (y0, x0); (x1, y1) = (y1, x1); }
        // Обмен концов идёт одной операцией: при двух отдельных присваиваниях
        // второе прочитало бы уже переставленные значения.
        if (x0 > x1) { (x0, y0, x1, y1) = (x1, y1, x0, y0); }

        int dx = x1 - x0;
        int step = Math.Abs(y1 - y0);
        int yDirection = y1 >= y0 ? 1 : -1;
        int y = y0;
        // Числитель дробной части ординаты: y + rest/dx — точное положение
        // отрезка. Это эквивалент решения p = 2 * rest - dx из оригинальной
        // формулы Ву, но без отрицательных промежуточных значений.
        int rest = 0;

        for (int x = x0; x <= x1; x++)
        {
            int cover = dx == 0 ? 0 : rest * 255 / dx;
            if (cover == 0) plot(Transform(steep, x, y), 255);
            else
            {
                plot(Transform(steep, x, y), 255 - cover);
                plot(Transform(steep, x, y + yDirection), cover);
            }
            rest += step;
            if (rest >= dx) { rest -= dx; y += yDirection; }
        }
    }

    private static Point Transform(bool steep, int x, int y) =>
        steep ? new Point(y, x) : new Point(x, y);

    /// <summary>Наложение цвета на пиксель с весом alpha из диапазона 0…255.</summary>
    private static void Blend(Bitmap image, int x, int y, Color color, int alpha)
    {
        if (alpha >= 255)
        {
            image.SetPixel(x, y, color);
            return;
        }
        Color background = image.GetPixel(x, y);
        image.SetPixel(x, y, Color.FromArgb(
            (color.R * alpha + background.R * (255 - alpha)) / 255,
            (color.G * alpha + background.G * (255 - alpha)) / 255,
            (color.B * alpha + background.B * (255 - alpha)) / 255));
    }

    private static bool Inside(Bitmap image, int x, int y) =>
        x >= 0 && y >= 0 && x < image.Width && y < image.Height;
}

public sealed partial class MainForm
{
    private bool segmentAnchored;
    private bool segmentDragging;
    private Point segmentStart;
    private Point segmentEnd;

    private LineAlgorithm SegmentAlgorithm => CurrentMode == Mode.SegmentWu
        ? LineAlgorithm.Wu : LineAlgorithm.Bresenham;

    private void HandleTask2MouseDown(Point point)
    {
        if (!segmentAnchored)
        {
            segmentStart = point;
            segmentEnd = point;
            segmentAnchored = true;
            segmentDragging = false;
            status.Text = $"Начало отрезка: ({point.X}; {point.Y}). " +
                          "Щёлкните конец или протяните мышью.";
        }
        else
        {
            segmentEnd = point;
            CommitSegment();
        }
        canvas.Invalidate();
    }

    private void HandleTask2MouseMove(MouseEventArgs e)
    {
        if (!segmentAnchored) return;
        segmentEnd = new Point(Math.Clamp(e.X, 0, image.Width - 1),
                               Math.Clamp(e.Y, 0, image.Height - 1));
        if (e.Button == MouseButtons.Left && segmentEnd != segmentStart)
            segmentDragging = true;
        status.Text = $"Отрезок ({segmentStart.X}; {segmentStart.Y}) — " +
                      $"({segmentEnd.X}; {segmentEnd.Y}).";
        canvas.Invalidate();
    }

    private void HandleTask2MouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !segmentAnchored || !segmentDragging) return;
        segmentEnd = new Point(Math.Clamp(e.X, 0, image.Width - 1),
                               Math.Clamp(e.Y, 0, image.Height - 1));
        CommitSegment();
    }

    private void CommitSegment()
    {
        LineAlgorithm algorithm = SegmentAlgorithm;
        int drawn = Task2.DrawLine(image, segmentStart, segmentEnd, boundaryColor, algorithm);
        string name = algorithm == LineAlgorithm.Wu ? "Ву" : "Брезенхема";
        status.Text = $"Алгоритм {name}: ({segmentStart.X}; {segmentStart.Y}) — " +
                      $"({segmentEnd.X}; {segmentEnd.Y}); закрашено {drawn} пикселов.";
        ClearTask2Preview();
        canvas.Invalidate();
    }

    private void ClearTask2Preview()
    {
        segmentAnchored = false;
        segmentDragging = false;
    }

    private void PaintTask2Overlay(Graphics graphics)
    {
        if (!segmentAnchored) return;
        using Pen preview = new(Color.Gray, 1) { DashStyle = DashStyle.Dot };
        graphics.DrawLine(preview, segmentStart, segmentEnd);
        using Brush start = new SolidBrush(Color.Orange);
        graphics.FillEllipse(start, segmentStart.X - 3, segmentStart.Y - 3, 7, 7);
    }
}

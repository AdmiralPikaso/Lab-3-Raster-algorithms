using System.Drawing;
using System.Drawing.Drawing2D;

namespace RasterAlgorithms;

/// <summary>Задание 3: растеризация треугольника и смешивание цветов вершин.</summary>
public static class Task3
{
    /// <summary>
    /// Перебирает пикселы ограничивающего прямоугольника. Ориентированные
    /// площади определяют принадлежность треугольнику и барицентрические веса
    /// для интерполяции красного, зелёного и синего каналов.
    /// </summary>
    public static int FillTriangle(Bitmap image,
        Point first, Color firstColor,
        Point second, Color secondColor,
        Point third, Color thirdColor)
    {
        ArgumentNullException.ThrowIfNull(image);
        long area = Edge(first, second, third);
        if (area == 0) return 0;

        int minX = Math.Max(0, Math.Min(first.X, Math.Min(second.X, third.X)));
        int maxX = Math.Min(image.Width - 1, Math.Max(first.X, Math.Max(second.X, third.X)));
        int minY = Math.Max(0, Math.Min(first.Y, Math.Min(second.Y, third.Y)));
        int maxY = Math.Min(image.Height - 1, Math.Max(first.Y, Math.Max(second.Y, third.Y)));
        int painted = 0;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Point pixel = new(x, y);
                long a = Edge(second, third, pixel);
                long b = Edge(third, first, pixel);
                long c = Edge(first, second, pixel);
                if (area > 0 ? a < 0 || b < 0 || c < 0 : a > 0 || b > 0 || c > 0)
                    continue;

                double w0 = (double)a / area;
                double w1 = (double)b / area;
                double w2 = (double)c / area;
                image.SetPixel(x, y, Color.FromArgb(
                    Channel(w0 * firstColor.R + w1 * secondColor.R + w2 * thirdColor.R),
                    Channel(w0 * firstColor.G + w1 * secondColor.G + w2 * thirdColor.G),
                    Channel(w0 * firstColor.B + w1 * secondColor.B + w2 * thirdColor.B)));
                painted++;
            }
        }

        return painted;
    }

    private static long Edge(Point a, Point b, Point p) =>
        ((long)b.X - a.X) * (p.Y - a.Y) - ((long)b.Y - a.Y) * (p.X - a.X);

    private static int Channel(double value) => Math.Clamp((int)Math.Round(value), 0, 255);
}

public sealed partial class MainForm
{
    private readonly Button[] triangleColorButtons = [new(), new(), new()];
    private readonly Color[] triangleColors = [Color.Red, Color.LimeGreen, Color.Blue];
    private readonly List<Point> triangleVertices = [];

    private void ConfigureTask3Toolbar(FlowLayoutPanel toolbar)
    {
        toolbar.Controls.Add(new Label
        {
            Text = "Цвета вершин:", AutoSize = true,
            Margin = new Padding(3, 8, 3, 0)
        });
        for (int index = 0; index < triangleColorButtons.Length; index++)
        {
            int vertexIndex = index;
            Button button = triangleColorButtons[index];
            button.Text = TriangleButtonText(index);
            button.Width = 145;
            button.Height = 27;
            button.Click += (_, _) =>
            {
                using ColorDialog dialog = new() { Color = triangleColors[vertexIndex], FullOpen = true };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                triangleColors[vertexIndex] = dialog.Color;
                button.Text = TriangleButtonText(vertexIndex);
                status.Text = $"Цвет вершины {vertexIndex + 1}: {ColorHex(dialog.Color)}.";
                canvas.Invalidate();
            };
            toolbar.Controls.Add(button);
        }
    }

    private string TriangleButtonText(int index) =>
        $"Вершина {index + 1} {ColorHex(triangleColors[index])}";

    private void HandleTask3MouseDown(Point point)
    {
        if (triangleColors.Distinct().Count() != 3)
        {
            status.Text = "Для треугольника выберите три разных цвета вершин.";
            return;
        }

        triangleVertices.Add(point);
        if (triangleVertices.Count == 3)
        {
            int count = Task3.FillTriangle(image,
                triangleVertices[0], triangleColors[0],
                triangleVertices[1], triangleColors[1],
                triangleVertices[2], triangleColors[2]);
            triangleVertices.Clear();
            status.Text = count == 0
                ? "Вершины лежат на одной прямой. Выберите три другие точки."
                : $"Градиентный треугольник построен: {count} пикселов.";
        }
        else
        {
            status.Text = $"Вершина {triangleVertices.Count} выбрана. " +
                          $"Щёлкните вершину {triangleVertices.Count + 1}.";
        }
        canvas.Invalidate();
    }

    private void ClearTask3Preview() => triangleVertices.Clear();

    private void PaintTask3Overlay(Graphics graphics)
    {
        if (triangleVertices.Count == 0) return;
        for (int index = 0; index < triangleVertices.Count; index++)
        {
            Point vertex = triangleVertices[index];
            using Brush marker = new SolidBrush(triangleColors[index]);
            graphics.FillEllipse(marker, vertex.X - 4, vertex.Y - 4, 9, 9);
            if (index > 0)
            {
                using Pen guide = new(Color.Gray, 1) { DashStyle = DashStyle.Dot };
                graphics.DrawLine(guide, triangleVertices[index - 1], vertex);
            }
        }
    }
}

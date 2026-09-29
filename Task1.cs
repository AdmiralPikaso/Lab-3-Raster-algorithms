using System.Drawing;
using System.Drawing.Imaging;

namespace RasterAlgorithms;

/// <summary>Алгоритмы работают с точными цветами пикселов, без сглаживания.</summary>
public static class Task1
{
    private static readonly Point[] Neighbors =
    [
        new(-1, 0), new(-1, -1), new(0, -1), new(1, -1),
        new(1, 0), new(1, 1), new(0, 1), new(-1, 1)
    ];

    /// <summary>
    /// Рекурсивная заливка сериями. Цвет начального пиксела задаёт область
    /// четырёхсвязности; нарисованная граница другого цвета остаётся нетронутой.
    /// Если область достигает края холста, заливка не выполняется.
    /// </summary>
    public static int Fill(Bitmap image, Point seed, Func<int, int, Color> colorAt)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(colorAt);
        if (!Inside(image, seed.X, seed.Y)) return 0;

        int target = image.GetPixel(seed.X, seed.Y).ToArgb();
        bool[] visited = new bool[checked(image.Width * image.Height)];
        Stack<Point> postponed = new();
        List<(int Left, int Right, int Y)> series = [];
        int count = 0;
        bool touchesEdge = false;

        bool Eligible(int x, int y) => Inside(image, x, y)
            && !visited[y * image.Width + x]
            && image.GetPixel(x, y).ToArgb() == target;

        void FillSeries(int x, int y, int depth)
        {
            if (touchesEdge || !Eligible(x, y)) return;
            if (depth >= 256)
            {
                postponed.Push(new Point(x, y));
                return;
            }

            int left = x;
            int right = x;
            while (left > 0 && Eligible(left - 1, y)) left--;
            while (right < image.Width - 1 && Eligible(right + 1, y)) right++;

            if (left == 0 || right == image.Width - 1 || y == 0 || y == image.Height - 1)
            {
                touchesEdge = true;
                return;
            }

            for (int column = left; column <= right; column++)
                visited[y * image.Width + column] = true;
            series.Add((left, right, y));
            count += right - left + 1;

            // На соседних строках рекурсивно запускается одна серия для каждого
            // ещё не обработанного участка. Посещённость учитывается отдельно
            // от цвета: рисунок может содержать исходный цвет области.
            for (int neighborY = y - 1; neighborY <= y + 1; neighborY += 2)
            {
                if (neighborY < 0 || neighborY >= image.Height) continue;
                for (int column = left; column <= right; column++)
                    if (Eligible(column, neighborY)) FillSeries(column, neighborY, depth + 1);
            }
        }

        FillSeries(seed.X, seed.Y, 0);
        // Ограничение глубины защищает стек на сложных узких областях.
        while (postponed.Count > 0 && !touchesEdge)
        {
            Point next = postponed.Pop();
            FillSeries(next.X, next.Y, 0);
        }
        if (touchesEdge) return 0;

        // Изображение изменяется только после проверки замкнутости области.
        foreach (var (left, right, y) in series)
            for (int x = left; x <= right; x++)
                image.SetPixel(x, y, colorAt(x, y));
        return count;
    }

    /// <summary>
    /// Пиксели рисунка берутся без масштабирования. Размер меньше холста
    /// приводит к повторению; большой рисунок используется в натуральном размере.
    /// </summary>
    public static int FillPattern(Bitmap image, Point seed, Bitmap pattern) =>
        Fill(image, seed, (x, y) => pattern.GetPixel(x % pattern.Width, y % pattern.Height));

    /// <summary>
    /// Находит восьмисвязный компонент цвета выбранной точки и обходит его
    /// внешний контур методом Мура, возвращая пиксели в порядке обхода.
    /// </summary>
    public static List<Point> TraceBoundary(Bitmap image, Point selected)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (!Inside(image, selected.X, selected.Y)) return [];

        int color = image.GetPixel(selected.X, selected.Y).ToArgb();
        bool[] component = new bool[checked(image.Width * image.Height)];
        Queue<Point> queue = new();
        queue.Enqueue(selected);
        component[selected.Y * image.Width + selected.X] = true;
        Point start = selected;
        int componentSize = 0;

        while (queue.Count > 0)
        {
            Point current = queue.Dequeue();
            componentSize++;
            if (current.Y < start.Y || current.Y == start.Y && current.X < start.X)
                start = current;

            foreach (Point offset in Neighbors)
            {
                int x = current.X + offset.X;
                int y = current.Y + offset.Y;
                if (!Inside(image, x, y)) continue;
                int index = y * image.Width + x;
                if (component[index] || image.GetPixel(x, y).ToArgb() != color) continue;
                component[index] = true;
                queue.Enqueue(new Point(x, y));
            }
        }

        // У одиночного пиксела и двухпиксельной линии нет замкнутого
        // внешнего контура, поэтому возвращаем их без обхода по кругу.
        if (componentSize <= 2)
        {
            if (componentSize == 1) return [start];
            foreach (Point offset in Neighbors)
            {
                Point other = new(start.X + offset.X, start.Y + offset.Y);
                if (Inside(image, other.X, other.Y)
                    && component[other.Y * image.Width + other.X])
                    return [start, other];
            }
        }

        List<Point> contour = [start];
        Point position = start;
        Point backtrack = new(start.X - 1, start.Y);
        Point initialBacktrack = backtrack;
        HashSet<(Point Position, Point Backtrack)> states = [(position, backtrack)];
        int stepLimit = checked(image.Width * image.Height * 8);

        for (int step = 0; step < stepLimit; step++)
        {
            int backIndex = Array.FindIndex(Neighbors, n =>
                position.X + n.X == backtrack.X && position.Y + n.Y == backtrack.Y);
            if (backIndex < 0) break;

            bool found = false;
            for (int turn = 1; turn <= 8; turn++)
            {
                int index = (backIndex + turn) % 8;
                Point next = new(position.X + Neighbors[index].X,
                                       position.Y + Neighbors[index].Y);
                if (!Inside(image, next.X, next.Y)
                    || !component[next.Y * image.Width + next.X]) continue;

                int preceding = (index + 7) % 8;
                backtrack = new Point(position.X + Neighbors[preceding].X,
                                      position.Y + Neighbors[preceding].Y);
                position = next;
                found = true;
                break;
            }

            if (!found || position == start && backtrack == initialBacktrack
                || !states.Add((position, backtrack))) break;
            contour.Add(position);
        }
        return contour;
    }

    private static bool Inside(Bitmap image, int x, int y) =>
        x >= 0 && y >= 0 && x < image.Width && y < image.Height;
}

public sealed partial class MainForm
{
    private readonly Button chooseFillColor = new();
    private readonly Button chooseBoundaryColor = new();
    private Bitmap? pattern;
    private Color fillColor = Color.CornflowerBlue;
    private Color boundaryColor = Color.Black;
    private List<Point> contour = [];
    private bool drawing;
    private Point previous;

    private void ConfigureTask1Toolbar(FlowLayoutPanel toolbar)
    {
        chooseBoundaryColor.Text = $"Граница {ColorHex(boundaryColor)}";
        chooseBoundaryColor.Width = 150;
        chooseBoundaryColor.Click += (_, _) =>
        {
            using ColorDialog dialog = new() { Color = boundaryColor, FullOpen = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            boundaryColor = dialog.Color;
            chooseBoundaryColor.Text = $"Граница {ColorHex(boundaryColor)}";
            status.Text = $"Цвет линии: {ColorHex(boundaryColor)}.";
        };
        toolbar.Controls.Add(chooseBoundaryColor);

        chooseFillColor.Text = $"Заливка {ColorHex(fillColor)}";
        chooseFillColor.Width = 150;
        chooseFillColor.Click += (_, _) =>
        {
            using ColorDialog dialog = new() { Color = fillColor, FullOpen = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            fillColor = dialog.Color;
            chooseFillColor.Text = $"Заливка {ColorHex(fillColor)}";
            status.Text = $"Цвет заливки: {ColorHex(fillColor)}.";
        };
        toolbar.Controls.Add(chooseFillColor);
        toolbar.Controls.Add(Button("Загрузить рисунок", LoadPattern, 145));
        toolbar.Controls.Add(Button("Открыть изображение", LoadImage, 160));
        toolbar.Controls.Add(Button("Сохранить границу", SaveContour, 150));
    }

    private void HandleTask1MouseDown(Point point)
    {
        if (CurrentMode == Mode.Draw)
        {
            contour.Clear();
            drawing = true;
            previous = point;
            DrawBoundarySegment(point, point);
            return;
        }

        drawing = false;
        if (CurrentMode == Mode.Trace)
        {
            contour = Task1.TraceBoundary(image, point);
            status.Text = $"Обойдено точек границы: {contour.Count}. " +
                          "Красная линия — обход, зелёная точка — начало.";
            canvas.Invalidate();
            return;
        }

        contour.Clear();
        if (CurrentMode == Mode.FillPattern && pattern is null)
        {
            status.Text = "Сначала загрузите графический файл рисунка.";
            return;
        }

        int count = CurrentMode == Mode.FillColor
            ? Task1.Fill(image, point, (_, _) => fillColor)
            : Task1.FillPattern(image, point, pattern!);
        status.Text = count == 0
            ? "Область доходит до края холста. Замкните контур и щёлкните внутри него."
            : $"Залито пикселов: {count}.";
        canvas.Invalidate();
    }

    private void HandleTask1MouseMove(MouseEventArgs e)
    {
        if (!drawing || e.Button != MouseButtons.Left) return;
        Point current = new(Math.Clamp(e.X, 0, image.Width - 1),
                            Math.Clamp(e.Y, 0, image.Height - 1));
        DrawBoundarySegment(previous, current);
        previous = current;
    }

    private void StopTask1Drawing() => drawing = false;

    private void ClearTask1Overlay()
    {
        drawing = false;
        contour.Clear();
    }

    private void DrawBoundarySegment(Point from, Point to)
    {
        int dx = Math.Abs(to.X - from.X);
        int dy = -Math.Abs(to.Y - from.Y);
        int sx = from.X < to.X ? 1 : -1;
        int sy = from.Y < to.Y ? 1 : -1;
        int error = dx + dy;
        int x = from.X;
        int y = from.Y;
        while (true)
        {
            for (int py = y - 1; py <= y + 1; py++)
                for (int px = x - 1; px <= x + 1; px++)
                    if (Inside(new Point(px, py))) image.SetPixel(px, py, boundaryColor);
            if (x == to.X && y == to.Y) break;
            int twice = 2 * error;
            if (twice >= dy) { error += dy; x += sx; }
            if (twice <= dx) { error += dx; y += sy; }
        }
        canvas.Invalidate();
    }

    private void PaintTask1Overlay(Graphics graphics)
    {
        if (contour.Count == 0) return;
        if (contour.Count > 1)
        {
            using Pen line = new(Color.Red, 2);
            graphics.DrawLines(line, contour.ToArray());
        }
        Point first = contour[0];
        using Brush marker = new SolidBrush(Color.LimeGreen);
        graphics.FillEllipse(marker, first.X - 3, first.Y - 3, 7, 7);
    }

    private void LoadPattern(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Выберите рисунок для заливки",
            Filter = "Изображения|*.png;*.bmp;*.jpg;*.jpeg;*.gif|Все файлы|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            Bitmap loaded = ReadBitmap(dialog.FileName);
            pattern?.Dispose();
            pattern = loaded;
            modeSelect.SelectedIndex = (int)Mode.FillPattern;
            status.Text = $"Рисунок {pattern.Width}×{pattern.Height} загружен. Щёлкните внутри контура.";
        }
        catch (Exception exception) when (exception is ArgumentException or OutOfMemoryException or IOException)
        {
            MessageBox.Show(this, exception.Message, "Не удалось открыть рисунок");
        }
    }

    private void LoadImage(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Выберите изображение с границей",
            Filter = "Изображения|*.png;*.bmp;*.jpg;*.jpeg;*.gif|Все файлы|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            ReplaceImage(ReadBitmap(dialog.FileName));
            modeSelect.SelectedIndex = (int)Mode.Trace;
            status.Text = "Изображение открыто. Щёлкните по пикселю нужной границы.";
        }
        catch (Exception exception) when (exception is ArgumentException or OutOfMemoryException or IOException)
        {
            MessageBox.Show(this, exception.Message, "Не удалось открыть изображение");
        }
    }

    private static Bitmap ReadBitmap(string path)
    {
        using Image source = Image.FromFile(path);
        Bitmap copy = new(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(copy);
        graphics.DrawImageUnscaled(source, 0, 0);
        return copy;
    }

    private void SaveContour(object? sender, EventArgs e)
    {
        if (contour.Count == 0)
        {
            status.Text = "Сначала выполните обход границы.";
            return;
        }
        using SaveFileDialog dialog = new()
        {
            Title = "Сохранить точки границы по порядку",
            Filter = "Текстовый файл|*.txt",
            FileName = "boundary.txt"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllLines(dialog.FileName, contour.Select((p, index) =>
                $"{index + 1}\t{p.X}\t{p.Y}"));
            status.Text = $"Сохранено {contour.Count} точек в {dialog.FileName}";
        }
        catch (IOException exception)
        {
            MessageBox.Show(this, exception.Message, "Не удалось сохранить границу");
        }
    }
}

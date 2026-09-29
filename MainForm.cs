using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace RasterAlgorithms;

public sealed class MainForm : Form
{
    private enum Mode { Draw, FillColor, FillPattern, Trace, SegmentBresenham, SegmentWu, GradientTriangle }

    private readonly PictureBox canvas = new();
    private readonly ComboBox modeSelect = new();
    private readonly Label status = new();
    private readonly Button chooseFillColor = new();
    private readonly Button chooseBoundaryColor = new();
    private readonly NumericUpDown canvasWidth = new();
    private readonly NumericUpDown canvasHeight = new();
    private Bitmap image = new(900, 600, PixelFormat.Format32bppArgb);
    private Bitmap? pattern;
    private Color fillColor = Color.CornflowerBlue;
    private Color boundaryColor = Color.Black;
    private List<Point> contour = [];
    private bool drawing;
    private Point previous;
    private bool segmentAnchored;
    private Point segmentStart;
    private Point segmentEnd;
    private readonly List<Point> triangleVertices = [];

    public MainForm()
    {
        Text = "Лабораторная №3 — растровые алгоритмы";
        ClientSize = new Size(1120, 760);
        MinimumSize = new Size(650, 450);
        StartPosition = FormStartPosition.CenterScreen;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(8),
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        Controls.Add(toolbar);

        toolbar.Controls.Add(new Label
        {
            Text = "Режим:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(3, 8, 3, 0)
        });
        modeSelect.DropDownStyle = ComboBoxStyle.DropDownList;
        modeSelect.Width = 190;
        modeSelect.Items.AddRange([
            "Рисовать границу", "Заливка цветом", "Заливка рисунком", "Обход границы",
            "Отрезок Брезенхемом", "Отрезок Ву", "Градиентный треугольник"
        ]);
        modeSelect.SelectedIndex = 0;
        modeSelect.SelectedIndexChanged += (_, _) =>
        {
            contour.Clear();
            segmentAnchored = false;
            triangleVertices.Clear();
            canvas.Invalidate();
            UpdateHint();
        };
        toolbar.Controls.Add(modeSelect);

        chooseBoundaryColor.Text = ColorButtonText("Граница", boundaryColor);
        chooseBoundaryColor.Width = 150;
        chooseBoundaryColor.Click += (_, _) =>
        {
            using ColorDialog dialog = new() { Color = boundaryColor, FullOpen = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            boundaryColor = dialog.Color;
            chooseBoundaryColor.Text = ColorButtonText("Граница", boundaryColor);
            status.Text = $"Цвет границы: {ColorHex(boundaryColor)}. Рисуйте левой кнопкой мыши.";
        };
        toolbar.Controls.Add(chooseBoundaryColor);

        chooseFillColor.Text = ColorButtonText("Заливка", fillColor);
        chooseFillColor.Width = 150;
        chooseFillColor.Click += (_, _) =>
        {
            using ColorDialog dialog = new() { Color = fillColor, FullOpen = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            fillColor = dialog.Color;
            chooseFillColor.Text = ColorButtonText("Заливка", fillColor);
            status.Text = $"Цвет заливки: {ColorHex(fillColor)}. Щёлкните внутри области.";
        };
        toolbar.Controls.Add(chooseFillColor);
        toolbar.Controls.Add(Button("Загрузить рисунок", LoadPattern, 145));
        toolbar.Controls.Add(Button("Открыть изображение", LoadImage, 160));
        toolbar.Controls.Add(Button("Новый холст", ResetCanvas, 105));
        toolbar.Controls.Add(Button("Сохранить границу", SaveContour, 150));
        toolbar.Controls.Add(new Label
        {
            Text = "Холст:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(3, 8, 3, 0)
        });
        ConfigureSizeInput(canvasWidth, image.Width);
        ConfigureSizeInput(canvasHeight, image.Height);
        toolbar.Controls.Add(canvasWidth);
        toolbar.Controls.Add(new Label
        {
            Text = "×", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 8, 0, 0)
        });
        toolbar.Controls.Add(canvasHeight);
        toolbar.Controls.Add(Button("Изменить размер", ResizeCanvas, 140));

        status.Dock = DockStyle.Bottom;
        status.Height = 42;
        status.Padding = new Padding(10, 5, 10, 5);
        status.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(status);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.DimGray };
        Controls.Add(scroll);
        scroll.BringToFront();
        toolbar.BringToFront();
        status.BringToFront();

        using (Graphics graphics = Graphics.FromImage(image)) graphics.Clear(Color.White);
        canvas.Image = image;
        canvas.SizeMode = PictureBoxSizeMode.Normal;
        canvas.Size = image.Size;
        canvas.Cursor = Cursors.Cross;
        canvas.MouseDown += CanvasMouseDown;
        canvas.MouseMove += CanvasMouseMove;
        canvas.MouseUp += CanvasMouseUp;
        canvas.Paint += CanvasPaint;
        scroll.Controls.Add(canvas);
        UpdateHint();
    }

    private Mode CurrentMode => (Mode)modeSelect.SelectedIndex;

    private static string ColorHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static string ColorButtonText(string name, Color color) => $"{name} {ColorHex(color)}";

    private static void ConfigureSizeInput(NumericUpDown input, int value)
    {
        input.Minimum = 1;
        input.Maximum = Math.Max(4096, value);
        input.Value = value;
        input.Width = 72;
        input.TextAlign = HorizontalAlignment.Right;
        input.ThousandsSeparator = true;
    }

    private static Button Button(string title, EventHandler handler, int width)
    {
        var button = new Button { Text = title, Width = width, Height = 27 };
        button.Click += handler;
        return button;
    }

    private void CanvasMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !Inside(e.Location)) return;
        if (CurrentMode == Mode.GradientTriangle)
        {
            triangleVertices.Add(e.Location);

            if (triangleVertices.Count == 3)
            {
                // Три вершины разного цвета — фиксированные, чтобы результат был наглядным.
                Point p1 = triangleVertices[0];
                Point p2 = triangleVertices[1];
                Point p3 = triangleVertices[2];

                int painted = Task3.Draw(
                    image,
                    p1, Color.Red,
                    p2, Color.Lime,
                    p3, Color.Blue);

                status.Text = $"Градиентный треугольник: ({p1.X};{p1.Y}) ({p2.X};{p2.Y}) ({p3.X};{p3.Y}), " +
                              $"закрашено {painted} пикселей.";
                triangleVertices.Clear();
                canvas.Invalidate();
                return;
            }

            status.Text = $"Вершина {triangleVertices.Count} из 3: ({e.Location.X}; {e.Location.Y}). " +
                          "Щёлкните следующую вершину.";
            canvas.Invalidate();
            return;
        }
        if (CurrentMode == Mode.Draw)
        {
            contour.Clear();
            drawing = true;
            previous = e.Location;
            DrawSegment(previous, previous);
            return;
        }

        drawing = false;
        if (SegmentMode is LineAlgorithm algorithm)
        {
            // Отрезок задаётся двумя нажатиями, поэтому предпросмотр идёт по
            // событию Paint и не смешивается с уже нарисованными пикселями.
            segmentAnchored = !segmentAnchored;
            if (segmentAnchored) segmentStart = e.Location;
            else
            {
                segmentEnd = e.Location;
                CommitSegment(algorithm);
            }
            if (segmentAnchored)
                status.Text = $"Начало отрезка: ({segmentStart.X}; {segmentStart.Y}). " +
                              "Протяните до конца или щёлкните второй раз.";
            canvas.Invalidate();
            return;
        }

        if (CurrentMode == Mode.Trace)
        {
            contour = RasterOperations.TraceBoundary(image, e.Location);
            status.Text = $"Обойдено точек границы: {contour.Count}. " +
                          "Красная линия показывает порядок обхода; зелёная точка — начало.";
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
            ? RasterOperations.Fill(image, e.Location, (_, _) => fillColor)
            : RasterOperations.FillPattern(image, e.Location, pattern!);
        if (count == 0)
        {
            status.Text = "Область доходит до края холста. Замкните контур и щёлкните внутри него.";
            return;
        }
        status.Text = $"Залито пикселов: {count}. " +
            (CurrentMode == Mode.FillPattern
                ? $"Рисунок {pattern!.Width}×{pattern.Height} без масштабирования; при необходимости повторяется."
                : "Щёлкните другую область для следующей заливки.");
        canvas.Invalidate();
    }

    private void CanvasMouseMove(object? sender, MouseEventArgs e)
    {
        if (SegmentMode is not null)
        {
            if (!segmentAnchored || e.Button != MouseButtons.Left) return;
            segmentEnd = new Point(Math.Clamp(e.X, 0, image.Width - 1),
                                   Math.Clamp(e.Y, 0, image.Height - 1));
            status.Text = $"Отрезок ({segmentStart.X}; {segmentStart.Y}) — " +
                          $"({segmentEnd.X}; {segmentEnd.Y}), длина {SegmentLength()} пикселей.";
            canvas.Invalidate();
            return;
        }

        if (!drawing || CurrentMode != Mode.Draw || e.Button != MouseButtons.Left) return;
        Point current = new(Math.Clamp(e.X, 0, image.Width - 1),
                            Math.Clamp(e.Y, 0, image.Height - 1));
        DrawSegment(previous, current);
        previous = current;
    }

    // Отрезок можно задать двумя щелчками либо протяжкой: в обоих случаях
    // алгоритм выполняется ровно один раз.
    private void CanvasMouseUp(object? sender, MouseEventArgs e)
    {
        drawing = false;
        if (e.Button != MouseButtons.Left || SegmentMode is not LineAlgorithm algorithm
            || !segmentAnchored) return;
        segmentEnd = new Point(Math.Clamp(e.X, 0, image.Width - 1),
                               Math.Clamp(e.Y, 0, image.Height - 1));
        segmentAnchored = false;
        CommitSegment(algorithm);
    }

    private LineAlgorithm? SegmentMode => CurrentMode switch
    {
        Mode.SegmentBresenham => LineAlgorithm.Bresenham,
        Mode.SegmentWu => LineAlgorithm.Wu,
        _ => null
    };

    private int SegmentLength() => Math.Max(Math.Abs(segmentEnd.X - segmentStart.X),
                                           Math.Abs(segmentEnd.Y - segmentStart.Y)) + 1;

    private void CommitSegment(LineAlgorithm algorithm)
    {
        contour.Clear();
        int drawn = Task2.DrawLine(image, segmentStart, segmentEnd, boundaryColor, algorithm);
        string name = algorithm == LineAlgorithm.Wu ? "Ву" : "Брезенхема";
        string detail = algorithm == LineAlgorithm.Wu
            ? "с дизерингом по краям"
            : "точным цветом, по одному пикселю на позицию";
        status.Text = $"Алгоритм {name}: ({segmentStart.X}; {segmentStart.Y}) — " +
                      $"({segmentEnd.X}; {segmentEnd.Y}), длина {SegmentLength()}; закрашено {drawn} пикселей {detail}.";
        canvas.Invalidate();
    }

    private void DrawSegment(Point from, Point to)
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
            // Толщина в три пиксела помогает провести замкнутую линию мышью.
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

    private void CanvasPaint(object? sender, PaintEventArgs e)
    {
        if (contour.Count == 0 && !segmentAnchored && triangleVertices.Count == 0) return;
        using Pen pen = new(Color.Red, 2);
        if (contour.Count > 1) e.Graphics.DrawLines(pen, contour.ToArray());

        using Brush brush = new SolidBrush(Color.LimeGreen);
        if (contour.Count > 0)
        {
            Point first = contour[0];
            e.Graphics.FillEllipse(brush, first.X - 3, first.Y - 3, 7, 7);
        }
        if (segmentAnchored)
        {
            // Предпросмотр рисуется поверх холста и не участвует в заливке:
            // выбранный алгоритм отработает один раз, при втором щелчке.
            using Pen preview = new(Color.Gray, 1) { DashStyle = DashStyle.Dot };
            e.Graphics.DrawLine(preview, segmentStart, segmentEnd);
            using Brush start = new SolidBrush(Color.Orange);
            e.Graphics.FillEllipse(start, segmentStart.X - 3, segmentStart.Y - 3, 7, 7);
        }

        if (triangleVertices.Count > 0)
        {
            using Brush v = new SolidBrush(Color.Orange);
            foreach (Point pt in triangleVertices)
                e.Graphics.FillEllipse(v, pt.X - 4, pt.Y - 4, 8, 8);

            if (triangleVertices.Count >= 2)
            {
                using Pen preview = new(Color.Gray, 1) { DashStyle = DashStyle.Dot };
                e.Graphics.DrawLine(preview, triangleVertices[0], triangleVertices[1]);
            }
        }
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

    private void ResetCanvas(object? sender, EventArgs e)
    {
        Bitmap fresh = new(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(fresh)) graphics.Clear(Color.White);
        ReplaceImage(fresh);
        modeSelect.SelectedIndex = (int)Mode.Draw;
        UpdateHint();
    }

    private void ResizeCanvas(object? sender, EventArgs e)
    {
        int width = Decimal.ToInt32(canvasWidth.Value);
        int height = Decimal.ToInt32(canvasHeight.Value);
        if (width == image.Width && height == image.Height)
        {
            status.Text = $"Размер холста уже {width}×{height} пикселов.";
            return;
        }

        try
        {
            Bitmap resized = new(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(resized))
            {
                graphics.Clear(Color.White);
                graphics.DrawImageUnscaled(image, 0, 0);
            }
            ReplaceImage(resized);
            status.Text = $"Размер холста: {width}×{height}. Содержимое сохранено без масштабирования; " +
                          "при уменьшении правый и нижний края обрезаются.";
        }
        catch (OutOfMemoryException)
        {
            status.Text = "Недостаточно памяти для холста такого размера.";
        }
    }

    private void ReplaceImage(Bitmap replacement)
    {
        Bitmap old = image;
        drawing = false;
        image = replacement;
        canvas.Image = image;
        canvas.Size = image.Size;
        canvasWidth.Maximum = Math.Max(4096, image.Width);
        canvasHeight.Maximum = Math.Max(4096, image.Height);
        canvasWidth.Value = image.Width;
        canvasHeight.Value = image.Height;
        contour.Clear();
        segmentAnchored = false;
        old.Dispose();
        canvas.Invalidate();
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

    private void UpdateHint()
    {
        status.Text = CurrentMode switch
        {
            Mode.Draw => "Левой кнопкой нарисуйте замкнутые контуры. Внутренние контуры образуют отверстия.",
            Mode.FillColor => "Щёлкните внутри области для рекурсивной заливки выбранным цветом.",
            Mode.FillPattern => "Загрузите рисунок и щёлкните внутри области для заливки.",
            Mode.SegmentBresenham => "Щёлкните начало и конец отрезка: он будет построен целочисленным алгоритмом Брезенхема, без сглаживания.",
            Mode.SegmentWu => "Щёлкните начало и конец отрезка: он будет построен алгоритмом Ву со сглаживанием краёв.",
            Mode.GradientTriangle => "Щёлкните три вершины треугольника — каждая вершина получит свой цвет (R/G/B), заливка выполнится барицентрическим градиентом.",
            _ => "Щёлкните по пикселю границы на изображении для её обхода."
        };
    }

    private bool Inside(Point point) => point.X >= 0 && point.Y >= 0
        && point.X < image.Width && point.Y < image.Height;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            canvas.Image = null;
            image.Dispose();
            pattern?.Dispose();
        }
        base.Dispose(disposing);
    }
}

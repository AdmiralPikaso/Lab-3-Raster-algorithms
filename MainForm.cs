using System.Drawing.Imaging;

namespace RasterAlgorithms;

/// <summary>Общий холст и переключение между тремя заданиями лабораторной.</summary>
public sealed partial class MainForm : Form
{
    private enum Mode { Draw, FillColor, FillPattern, Trace, SegmentBresenham, SegmentWu, Triangle }

    private readonly PictureBox canvas = new();
    private readonly ComboBox modeSelect = new();
    private readonly Label status = new();
    private readonly NumericUpDown canvasWidth = new();
    private readonly NumericUpDown canvasHeight = new();
    private Bitmap image = new(900, 600, PixelFormat.Format32bppArgb);

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
            Text = "Режим:", AutoSize = true,
            Margin = new Padding(3, 8, 3, 0)
        });
        modeSelect.DropDownStyle = ComboBoxStyle.DropDownList;
        modeSelect.Width = 210;
        modeSelect.Items.AddRange([
            "Рисовать границу", "Заливка цветом", "Заливка рисунком", "Обход границы",
            "Отрезок Брезенхемом", "Отрезок Ву", "Градиентный треугольник"
        ]);
        modeSelect.SelectedIndex = 0;
        modeSelect.SelectedIndexChanged += (_, _) =>
        {
            ClearTask1Overlay();
            ClearTask2Preview();
            ClearTask3Preview();
            canvas.Invalidate();
            UpdateHint();
        };
        toolbar.Controls.Add(modeSelect);

        ConfigureTask1Toolbar(toolbar);
        ConfigureTask3Toolbar(toolbar);
        toolbar.Controls.Add(Button("Новый холст", ResetCanvas, 105));
        toolbar.Controls.Add(new Label
        {
            Text = "Холст:", AutoSize = true,
            Margin = new Padding(3, 8, 3, 0)
        });
        ConfigureSizeInput(canvasWidth, image.Width);
        ConfigureSizeInput(canvasHeight, image.Height);
        toolbar.Controls.Add(canvasWidth);
        toolbar.Controls.Add(new Label
        {
            Text = "×", AutoSize = true,
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

    private static Button Button(string title, EventHandler handler, int width)
    {
        var button = new Button { Text = title, Width = width, Height = 27 };
        button.Click += handler;
        return button;
    }

    private static string ColorHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static void ConfigureSizeInput(NumericUpDown input, int value)
    {
        input.Minimum = 1;
        input.Maximum = Math.Max(4096, value);
        input.Value = value;
        input.Width = 72;
        input.TextAlign = HorizontalAlignment.Right;
        input.ThousandsSeparator = true;
    }

    private void CanvasMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !Inside(e.Location)) return;
        switch (CurrentMode)
        {
            case Mode.Draw:
            case Mode.FillColor:
            case Mode.FillPattern:
            case Mode.Trace:
                HandleTask1MouseDown(e.Location);
                break;
            case Mode.SegmentBresenham:
            case Mode.SegmentWu:
                HandleTask2MouseDown(e.Location);
                break;
            case Mode.Triangle:
                HandleTask3MouseDown(e.Location);
                break;
        }
    }

    private void CanvasMouseMove(object? sender, MouseEventArgs e)
    {
        if (CurrentMode is Mode.SegmentBresenham or Mode.SegmentWu)
            HandleTask2MouseMove(e);
        else if (CurrentMode == Mode.Draw)
            HandleTask1MouseMove(e);
    }

    private void CanvasMouseUp(object? sender, MouseEventArgs e)
    {
        StopTask1Drawing();
        if (CurrentMode is Mode.SegmentBresenham or Mode.SegmentWu)
            HandleTask2MouseUp(e);
    }

    private void CanvasPaint(object? sender, PaintEventArgs e)
    {
        PaintTask1Overlay(e.Graphics);
        PaintTask2Overlay(e.Graphics);
        PaintTask3Overlay(e.Graphics);
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
            status.Text = $"Размер холста: {width}×{height}. Изображение не масштабируется; " +
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
        image = replacement;
        canvas.Image = image;
        canvas.Size = image.Size;
        canvasWidth.Maximum = Math.Max(4096, image.Width);
        canvasHeight.Maximum = Math.Max(4096, image.Height);
        canvasWidth.Value = image.Width;
        canvasHeight.Value = image.Height;
        ClearTask1Overlay();
        ClearTask2Preview();
        ClearTask3Preview();
        old.Dispose();
        canvas.Invalidate();
    }

    private void UpdateHint()
    {
        status.Text = CurrentMode switch
        {
            Mode.Draw => "Левой кнопкой нарисуйте замкнутые контуры. Внутренние контуры образуют отверстия.",
            Mode.FillColor => "Щёлкните внутри замкнутой области для заливки выбранным цветом.",
            Mode.FillPattern => "Загрузите рисунок и щёлкните внутри замкнутой области.",
            Mode.Trace => "Щёлкните по пикселю границы на изображении для её обхода.",
            Mode.SegmentBresenham => "Задайте начало и конец отрезка щелчками или протяните мышью: алгоритм Брезенхема.",
            Mode.SegmentWu => "Задайте начало и конец отрезка щелчками или протяните мышью: алгоритм Ву.",
            _ => "Выберите три разных цвета и щёлкните три вершины треугольника."
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

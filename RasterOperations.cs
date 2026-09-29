using System.Drawing;

namespace RasterAlgorithms;

/// <summary>Алгоритмы работают с точными цветами пикселов, без сглаживания.</summary>
public static class RasterOperations
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

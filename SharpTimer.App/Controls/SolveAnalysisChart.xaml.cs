using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SharpTimer.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.Foundation;
using Windows.UI;

namespace SharpTimer.App.Controls;

public sealed partial class SolveAnalysisChart : UserControl
{
    private static readonly SolidColorBrush GridLineBrush = new(Color.FromArgb(45, 128, 128, 128));
    private Solve[] _solves = [];

    public SolveAnalysisChart()
    {
        InitializeComponent();
        Loaded += (_, _) => RenderCharts();
        ActualThemeChanged += (_, _) => RenderCharts();
    }

    public void SetText(
        string trendTitle,
        string emptyText)
    {
        TrendTitleText.Text = trendTitle;
        TrendEmptyText.Text = emptyText;
    }

    public void SetSolves(IEnumerable<Solve> solves, int decimalPlaces)
    {
        _solves = solves
            .OrderBy(solve => solve.CreatedAt)
            .ToArray();
        RenderCharts();
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderCharts();
    }

    private void RenderCharts()
    {
        RenderTrend();
    }

    private void RenderTrend()
    {
        TrendCanvas.Children.Clear();
        var values = _solves
            .Select((solve, index) => new ChartPoint(index, solve.Duration.TotalMilliseconds))
            .ToArray();

        TrendEmptyText.Visibility = values.Length < 2 ? Visibility.Visible : Visibility.Collapsed;
        if (values.Length < 2)
        {
            return;
        }

        var bounds = GetPlotBounds(TrendCanvas);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        DrawHorizontalGrid(TrendCanvas, bounds, 3);

        var min = values.Min(point => point.Value!.Value);
        var max = values.Max(point => point.Value!.Value);
        if (Math.Abs(max - min) < 1)
        {
            max = min + 1;
        }

        // 添加 Y 轴时间刻度（使用合理的整数秒）
        DrawTimeAxisLabels(TrendCanvas, bounds, min, max);

        var path = new Path
        {
            Stroke = GetBrush("AccentFillColorDefaultBrush"),
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round,
            Data = BuildLineGeometry(values, bounds, min, max)
        };
        TrendCanvas.Children.Add(path);

        foreach (var point in values)
        {
            var position = ProjectTrendPoint(point, bounds, min, max, values.Length);
            AddDot(TrendCanvas, position, GetBrush("AccentFillColorDefaultBrush"), 5);
        }
    }

    private static PathGeometry BuildLineGeometry(IReadOnlyList<ChartPoint> values, Rect bounds, double min, double max)
    {
        var figure = new PathFigure
        {
            StartPoint = ProjectTrendPoint(values[0], bounds, min, max, values.Count),
            IsClosed = false,
            IsFilled = false
        };

        for (var index = 1; index < values.Count; index++)
        {
            figure.Segments.Add(new LineSegment
            {
                Point = ProjectTrendPoint(values[index], bounds, min, max, values.Count)
            });
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static Point ProjectTrendPoint(ChartPoint point, Rect bounds, double min, double max, int count)
    {
        var xRatio = count <= 1 ? 0 : point.Index / (double)(count - 1);
        var yRatio = (point.Value!.Value - min) / (max - min);
        return new Point(bounds.Left + bounds.Width * xRatio, bounds.Bottom - bounds.Height * yRatio);
    }

    private static void DrawTimeAxisLabels(Canvas canvas, Rect bounds, double minMs, double maxMs)
    {
        var minSec = minMs / 1000.0;
        var maxSec = maxMs / 1000.0;
        var range = maxSec - minSec;

        // 计算合适的刻度间隔（优先整数、整十、整百等）
        double step;
        if (range <= 5) step = 1;
        else if (range <= 10) step = 2;
        else if (range <= 20) step = 5;
        else if (range <= 50) step = 10;
        else if (range <= 100) step = 20;
        else if (range <= 200) step = 50;
        else step = 100;

        // 计算起始刻度（向下取整到 step 的倍数）
        var startSec = Math.Floor(minSec / step) * step;

        var brush = GetBrush("TextFillColorTertiaryBrush");
        for (var sec = startSec; sec <= maxSec + step * 0.01; sec += step)
        {
            if (sec < minSec - step * 0.01) continue;

            var ratio = (sec - minSec) / (maxSec - minSec);
            var y = bounds.Bottom - bounds.Height * ratio;

            if (y < bounds.Top - 1 || y > bounds.Bottom + 1) continue;

            var label = new TextBlock
            {
                Text = sec.ToString("F0", CultureInfo.CurrentCulture),
                FontSize = 10,
                Foreground = brush
            };
            Canvas.SetLeft(label, bounds.Left);
            Canvas.SetTop(label, y - 6);
            canvas.Children.Add(label);
        }
    }

    private static void DrawHorizontalGrid(Canvas canvas, Rect bounds, int lines)
    {
        for (var index = 0; index <= lines; index++)
        {
            var y = bounds.Top + bounds.Height * index / lines;
            var line = new Line
            {
                X1 = bounds.Left,
                X2 = bounds.Right,
                Y1 = y,
                Y2 = y,
                Stroke = GridLineBrush,
                StrokeThickness = 1
            };
            canvas.Children.Add(line);
        }
    }

    private static void AddDot(Canvas canvas, Point point, Brush brush, double size)
    {
        var ellipse = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = brush
        };
        Canvas.SetLeft(ellipse, point.X - size / 2);
        Canvas.SetTop(ellipse, point.Y - size / 2);
        canvas.Children.Add(ellipse);
    }

    private static Brush GetBrush(string key)
    {
        return Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush
            ? brush
            : new SolidColorBrush(Colors.Gray);
    }

    private static Rect GetPlotBounds(Canvas canvas)
    {
        const double horizontalPadding = 8;
        const double verticalPadding = 8;
        var width = Math.Max(0, canvas.ActualWidth - horizontalPadding * 2);
        var height = Math.Max(0, canvas.ActualHeight - verticalPadding * 2);
        return new Rect(horizontalPadding, verticalPadding, width, height);
    }

    private sealed record ChartPoint(int Index, double? Value);
}

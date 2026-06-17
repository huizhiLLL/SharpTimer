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
    private int _decimalPlaces = 2;

    public SolveAnalysisChart()
    {
        InitializeComponent();
        Loaded += (_, _) => RenderCharts();
        ActualThemeChanged += (_, _) => RenderCharts();
    }

    public void SetText(string trendTitle, string distributionTitle, string emptyText)
    {
        TrendTitleText.Text = trendTitle;
        DistributionTitleText.Text = distributionTitle;
        TrendEmptyText.Text = emptyText;
        DistributionEmptyText.Text = emptyText;
    }

    public void SetSolves(IEnumerable<Solve> solves, int decimalPlaces)
    {
        _solves = solves
            .OrderBy(solve => solve.CreatedAt)
            .ToArray();
        _decimalPlaces = decimalPlaces;
        RenderCharts();
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderCharts();
    }

    private void RenderCharts()
    {
        RenderTrend();
        RenderDistribution();
    }

    private void RenderTrend()
    {
        TrendCanvas.Children.Clear();
        var values = _solves
            .Select((solve, index) => new ChartPoint(index, solve.EffectiveDuration?.TotalMilliseconds))
            .Where(point => point.Value is not null)
            .Select(point => new ChartPoint(point.Index, point.Value!.Value))
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

        // 添加 Y 轴标注
        DrawYAxisLabels(TrendCanvas, bounds, min, max, _decimalPlaces);

        // 添加 X 轴标注
        DrawXAxisLabels(TrendCanvas, bounds, values.Length);

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

    private void RenderDistribution()
    {
        DistributionCanvas.Children.Clear();
        var values = _solves
            .Select(solve => solve.EffectiveDuration?.TotalMilliseconds)
            .OfType<double>()
            .ToArray();

        DistributionEmptyText.Visibility = values.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (values.Length == 0)
        {
            return;
        }

        var bounds = GetPlotBounds(DistributionCanvas);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        DrawHorizontalGrid(DistributionCanvas, bounds, 2);

        var buckets = BuildBuckets(values, Math.Min(6, Math.Max(3, values.Length)));
        var maxCount = Math.Max(1, buckets.Max(bucket => bucket.Count));
        var gap = 8d;
        var barWidth = Math.Max(4, (bounds.Width - gap * (buckets.Length - 1)) / buckets.Length);

        // 添加 Y 轴标注（数量）
        DrawCountLabels(DistributionCanvas, bounds, maxCount);

        // 添加 X 轴标注（时间区间）
        DrawBucketLabels(DistributionCanvas, bounds, buckets, barWidth, gap, _decimalPlaces);

        for (var index = 0; index < buckets.Length; index++)
        {
            var bucket = buckets[index];
            var height = bounds.Height * bucket.Count / maxCount;
            var left = bounds.Left + index * (barWidth + gap);
            var top = bounds.Bottom - height;

            var bar = new Rectangle
            {
                Width = barWidth,
                Height = height,
                RadiusX = 4,
                RadiusY = 4,
                Fill = GetBrush("AccentFillColorDefaultBrush"),
                Opacity = 0.82
            };
            Canvas.SetLeft(bar, left);
            Canvas.SetTop(bar, top);
            DistributionCanvas.Children.Add(bar);

            var label = new TextBlock
            {
                Text = bucket.Count.ToString(CultureInfo.CurrentCulture),
                FontSize = 11,
                Foreground = GetBrush("TextFillColorSecondaryBrush")
            };
            Canvas.SetLeft(label, left + Math.Max(0, (barWidth - 12) / 2));
            Canvas.SetTop(label, Math.Max(bounds.Top, top - 18));
            DistributionCanvas.Children.Add(label);
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

    private static Bucket[] BuildBuckets(IReadOnlyList<double> values, int bucketCount)
    {
        var min = values.Min();
        var max = values.Max();
        if (Math.Abs(max - min) < 1)
        {
            return [new Bucket(min, max, values.Count)];
        }

        var step = (max - min) / bucketCount;
        var buckets = Enumerable.Range(0, bucketCount)
            .Select(index => new Bucket(min + step * index, min + step * (index + 1), 0))
            .ToArray();

        foreach (var value in values)
        {
            var index = Math.Min(bucketCount - 1, (int)((value - min) / step));
            buckets[index] = buckets[index] with { Count = buckets[index].Count + 1 };
        }

        return buckets;
    }

    private static Rect GetPlotBounds(Canvas canvas)
    {
        const double horizontalPadding = 8;
        const double verticalPadding = 8;
        var width = Math.Max(0, canvas.ActualWidth - horizontalPadding * 2);
        var height = Math.Max(0, canvas.ActualHeight - verticalPadding * 2);
        return new Rect(horizontalPadding, verticalPadding, width, height);
    }

    private static void DrawYAxisLabels(Canvas canvas, Rect bounds, double min, double max, int decimalPlaces)
    {
        var lines = 3;
        var brush = GetBrush("TextFillColorTertiaryBrush");
        for (var index = 0; index <= lines; index++)
        {
            var value = max - (max - min) * index / lines;
            var y = bounds.Top + bounds.Height * index / lines;
            var label = new TextBlock
            {
                Text = FormatTime(value, decimalPlaces),
                FontSize = 10,
                Foreground = brush
            };
            Canvas.SetLeft(label, bounds.Left);
            Canvas.SetTop(label, y - 6);
            canvas.Children.Add(label);
        }
    }

    private static void DrawXAxisLabels(Canvas canvas, Rect bounds, int count)
    {
        var brush = GetBrush("TextFillColorTertiaryBrush");
        var step = Math.Max(1, count / 5);
        for (var index = 0; index < count; index += step)
        {
            var x = bounds.Left + bounds.Width * index / Math.Max(1, count - 1);
            var label = new TextBlock
            {
                Text = (index + 1).ToString(CultureInfo.CurrentCulture),
                FontSize = 10,
                Foreground = brush
            };
            Canvas.SetLeft(label, x - 6);
            Canvas.SetTop(label, bounds.Bottom + 2);
            canvas.Children.Add(label);
        }
    }

    private static void DrawCountLabels(Canvas canvas, Rect bounds, int maxCount)
    {
        var lines = 2;
        var brush = GetBrush("TextFillColorTertiaryBrush");
        for (var index = 0; index <= lines; index++)
        {
            var value = maxCount - maxCount * index / lines;
            var y = bounds.Top + bounds.Height * index / lines;
            var label = new TextBlock
            {
                Text = value.ToString(CultureInfo.CurrentCulture),
                FontSize = 10,
                Foreground = brush
            };
            Canvas.SetLeft(label, bounds.Left);
            Canvas.SetTop(label, y - 6);
            canvas.Children.Add(label);
        }
    }

    private static void DrawBucketLabels(Canvas canvas, Rect bounds, Bucket[] buckets, double barWidth, double gap, int decimalPlaces)
    {
        var brush = GetBrush("TextFillColorTertiaryBrush");
        for (var index = 0; index < buckets.Length; index++)
        {
            var bucket = buckets[index];
            var left = bounds.Left + index * (barWidth + gap);
            var centerX = left + barWidth / 2;
            var timeLabel = FormatTime((bucket.From + bucket.To) / 2, decimalPlaces);
            var label = new TextBlock
            {
                Text = timeLabel,
                FontSize = 10,
                Foreground = brush
            };
            Canvas.SetLeft(label, centerX - 15);
            Canvas.SetTop(label, bounds.Bottom + 2);
            canvas.Children.Add(label);
        }
    }

    private static string FormatTime(double milliseconds, int decimalPlaces)
    {
        var seconds = milliseconds / 1000.0;
        return seconds.ToString($"F{decimalPlaces}", CultureInfo.CurrentCulture);
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

    private sealed record ChartPoint(int Index, double? Value);

    private sealed record Bucket(double From, double To, int Count);
}

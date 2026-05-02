using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;

namespace SharpTimer.App.Rendering;

internal static class SmartCubePreviewRenderer
{
    private const double FaceDistance = 1.5;
    private const double CellHalf = 0.54;
    private const double StickerHalf = 0.42;
    private const double YawDegrees = -38;
    private const double PitchDegrees = 27;
    private const double CameraDistance = 11.5;
    private const double MinVisibleNormalZ = 0.05;
    private static readonly Facelet[] Facelets = BuildFacelets();

    public static void Render(Canvas canvas, string? facelets)
    {
        canvas.Children.Clear();

        var state = string.IsNullOrWhiteSpace(facelets) || facelets.Length < 54
            ? null
            : facelets[..54];
        var batch = BuildRenderBatch(state);
        if (batch.Tiles.Count == 0 || !batch.Bounds.IsValid)
        {
            return;
        }

        var width = GetCanvasLength(canvas.ActualWidth, canvas.Width, 180);
        var height = GetCanvasLength(canvas.ActualHeight, canvas.Height, 180);
        var cubeWidth = Math.Max(1, batch.Bounds.Width);
        var cubeHeight = Math.Max(1, batch.Bounds.Height);
        var shadowExtra = cubeHeight * 0.18;
        var scale = Math.Min(width * 0.92 / cubeWidth, height * 0.88 / (cubeHeight + shadowExtra));
        var offsetX = (width - cubeWidth * scale) / 2 - batch.Bounds.Left * scale;
        var offsetY = (height - (cubeHeight + shadowExtra) * scale) / 2 - batch.Bounds.Top * scale;
        var strokeWidth = Math.Max(1.2, scale * 0.045);

        DrawShadow(canvas, batch.Bounds, scale, offsetX, offsetY, cubeWidth, cubeHeight);
        foreach (var tile in batch.Tiles)
        {
            DrawPolygon(canvas, tile.BasePoints, scale, offsetX, offsetY, tile.BaseColor, null, 0);
            DrawPolygon(canvas, tile.StickerPoints, scale, offsetX, offsetY, tile.StickerColor, Color.FromArgb(0xee, 8, 8, 8), strokeWidth);
        }
    }

    private static RenderBatch BuildRenderBatch(string? state)
    {
        var tiles = new List<RenderTile>(54);
        var bounds = new Bounds();
        for (var i = 0; i < Facelets.Length; i++)
        {
            var transform = BuildTransform(Facelets[i]);
            if (transform.Normal.Z <= MinVisibleNormalZ)
            {
                continue;
            }

            var basePoints = ProjectQuad(transform.Center, transform.U, transform.V, CellHalf);
            var stickerPoints = ProjectQuad(transform.Center, transform.U, transform.V, StickerHalf);
            bounds.Update(basePoints);
            bounds.Update(stickerPoints);

            tiles.Add(new RenderTile(
                basePoints,
                stickerPoints,
                transform.Center.Z,
                ShadeBase(transform.Normal),
                ShadeSticker(FaceColor(state, i), transform.Normal)));
        }

        return new RenderBatch(tiles.OrderBy(tile => tile.Depth).ToArray(), bounds);
    }

    private static void DrawShadow(
        Canvas canvas,
        Bounds bounds,
        double scale,
        double offsetX,
        double offsetY,
        double cubeWidth,
        double cubeHeight)
    {
        var centerX = bounds.CenterX * scale + offsetX;
        var bottom = bounds.Bottom * scale + offsetY;
        var shadowWidth = cubeWidth * scale * 0.54;
        var shadowHeight = cubeHeight * scale * 0.12;
        var shadow = new Ellipse
        {
            Width = shadowWidth,
            Height = shadowHeight,
            Fill = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0))
        };
        Canvas.SetLeft(shadow, centerX - shadowWidth / 2);
        Canvas.SetTop(shadow, bottom - shadowHeight * 0.45);
        canvas.Children.Add(shadow);
    }

    private static void DrawPolygon(
        Canvas canvas,
        IReadOnlyList<Point> points,
        double scale,
        double offsetX,
        double offsetY,
        Color fill,
        Color? stroke,
        double strokeWidth)
    {
        var polygon = new Polygon
        {
            Fill = new SolidColorBrush(fill)
        };

        foreach (var point in points)
        {
            polygon.Points.Add(new Point(point.X * scale + offsetX, point.Y * scale + offsetY));
        }

        if (stroke is not null && strokeWidth > 0)
        {
            polygon.Stroke = new SolidColorBrush(stroke.Value);
            polygon.StrokeThickness = strokeWidth;
        }

        canvas.Children.Add(polygon);
    }

    private static Transform BuildTransform(Facelet facelet)
    {
        var center = ApplyViewRotation(facelet.Center);
        var normal = ApplyViewRotation(facelet.Normal).Normalize();
        var u = ApplyViewRotation(facelet.U).Normalize();
        var v = ApplyViewRotation(facelet.V).Normalize();
        return new Transform(center, normal, u, v);
    }

    private static Point[] ProjectQuad(Vec3 center, Vec3 u, Vec3 v, double halfSize)
    {
        return
        [
            Project(center.Add(u.Scale(-halfSize)).Add(v.Scale(-halfSize))),
            Project(center.Add(u.Scale(halfSize)).Add(v.Scale(-halfSize))),
            Project(center.Add(u.Scale(halfSize)).Add(v.Scale(halfSize))),
            Project(center.Add(u.Scale(-halfSize)).Add(v.Scale(halfSize)))
        ];
    }

    private static Point Project(Vec3 point)
    {
        var perspective = CameraDistance / (CameraDistance - point.Z);
        return new Point(point.X * perspective, -point.Y * perspective);
    }

    private static Vec3 ApplyViewRotation(Vec3 point)
    {
        var rotated = RotateAroundAxis(point, axis: 1, YawDegrees);
        return RotateAroundAxis(rotated, axis: 0, PitchDegrees);
    }

    private static Vec3 RotateAroundAxis(Vec3 point, int axis, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180;
        var sin = Math.Sin(radians);
        var cos = Math.Cos(radians);
        return axis switch
        {
            0 => new Vec3(point.X, point.Y * cos - point.Z * sin, point.Y * sin + point.Z * cos),
            1 => new Vec3(point.X * cos + point.Z * sin, point.Y, -point.X * sin + point.Z * cos),
            _ => new Vec3(point.X * cos - point.Y * sin, point.X * sin + point.Y * cos, point.Z)
        };
    }

    private static Color ShadeSticker(Color color, Vec3 normal)
    {
        var light = Clamp(0.76 + normal.Z * 0.16 + Math.Max(0, normal.Y) * 0.08, 0.62, 1.08);
        var shaded = MultiplyColor(color, light);
        return normal.Y > 0.5
            ? Blend(shaded, Microsoft.UI.Colors.White, 0.06)
            : shaded;
    }

    private static Color ShadeBase(Vec3 normal)
    {
        var light = Clamp(0.35 + normal.Z * 0.08 + Math.Max(0, normal.Y) * 0.04, 0.24, 0.52);
        return MultiplyColor(Color.FromArgb(255, 16, 16, 16), light);
    }

    private static Color FaceColor(string? state, int index)
    {
        if (state is null)
        {
            return Color.FromArgb(255, 140, 140, 140);
        }

        return state[index] switch
        {
            'U' => Color.FromArgb(255, 251, 251, 251),
            'R' => Color.FromArgb(255, 239, 68, 68),
            'F' => Color.FromArgb(255, 63, 155, 70),
            'D' => Color.FromArgb(255, 245, 209, 66),
            'L' => Color.FromArgb(255, 242, 139, 36),
            'B' => Color.FromArgb(255, 45, 103, 207),
            _ => Color.FromArgb(255, 140, 140, 140)
        };
    }

    private static Color MultiplyColor(Color color, double factor)
    {
        return Color.FromArgb(
            color.A,
            ClampToByte(color.R * factor),
            ClampToByte(color.G * factor),
            ClampToByte(color.B * factor));
    }

    private static Color Blend(Color color, Color overlay, double amount)
    {
        var inverse = 1 - amount;
        return Color.FromArgb(
            ClampToByte(color.A * inverse + overlay.A * amount),
            ClampToByte(color.R * inverse + overlay.R * amount),
            ClampToByte(color.G * inverse + overlay.G * amount),
            ClampToByte(color.B * inverse + overlay.B * amount));
    }

    private static byte ClampToByte(double value)
    {
        return (byte)Math.Round(Clamp(value, 0, 255));
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    private static double GetCanvasLength(double actual, double requested, double fallback)
    {
        if (!double.IsNaN(actual) && actual > 0)
        {
            return actual;
        }

        return !double.IsNaN(requested) && requested > 0
            ? requested
            : fallback;
    }

    private static Facelet[] BuildFacelets()
    {
        var facelets = new List<Facelet>(54);
        AddFace(facelets, new Vec3(0, FaceDistance, 0), new Vec3(0, 1, 0), new Vec3(1, 0, 0), new Vec3(0, 0, 1));
        AddFace(facelets, new Vec3(FaceDistance, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 0, -1), new Vec3(0, -1, 0));
        AddFace(facelets, new Vec3(0, 0, FaceDistance), new Vec3(0, 0, 1), new Vec3(1, 0, 0), new Vec3(0, -1, 0));
        AddFace(facelets, new Vec3(0, -FaceDistance, 0), new Vec3(0, -1, 0), new Vec3(1, 0, 0), new Vec3(0, 0, -1));
        AddFace(facelets, new Vec3(-FaceDistance, 0, 0), new Vec3(-1, 0, 0), new Vec3(0, 0, 1), new Vec3(0, -1, 0));
        AddFace(facelets, new Vec3(0, 0, -FaceDistance), new Vec3(0, 0, -1), new Vec3(-1, 0, 0), new Vec3(0, -1, 0));
        return facelets.ToArray();
    }

    private static void AddFace(List<Facelet> facelets, Vec3 faceCenter, Vec3 normal, Vec3 u, Vec3 v)
    {
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var uOffset = column - 1;
                var vOffset = row - 1;
                var center = faceCenter.Add(u.Scale(uOffset)).Add(v.Scale(vOffset));
                facelets.Add(new Facelet(center, normal, u, v));
            }
        }
    }

    private sealed record Facelet(Vec3 Center, Vec3 Normal, Vec3 U, Vec3 V);

    private sealed record Transform(Vec3 Center, Vec3 Normal, Vec3 U, Vec3 V);

    private sealed record RenderTile(
        IReadOnlyList<Point> BasePoints,
        IReadOnlyList<Point> StickerPoints,
        double Depth,
        Color BaseColor,
        Color StickerColor);

    private sealed record RenderBatch(IReadOnlyList<RenderTile> Tiles, Bounds Bounds);

    private sealed class Bounds
    {
        public double Left { get; private set; } = double.PositiveInfinity;
        public double Top { get; private set; } = double.PositiveInfinity;
        public double Right { get; private set; } = double.NegativeInfinity;
        public double Bottom { get; private set; } = double.NegativeInfinity;
        public bool IsValid => !double.IsInfinity(Left) && !double.IsInfinity(Top)
            && !double.IsInfinity(Right) && !double.IsInfinity(Bottom);
        public double Width => Right - Left;
        public double Height => Bottom - Top;
        public double CenterX => (Left + Right) / 2;

        public void Update(IEnumerable<Point> points)
        {
            foreach (var point in points)
            {
                Left = Math.Min(Left, point.X);
                Top = Math.Min(Top, point.Y);
                Right = Math.Max(Right, point.X);
                Bottom = Math.Max(Bottom, point.Y);
            }
        }
    }

    private readonly record struct Vec3(double X, double Y, double Z)
    {
        public Vec3 Add(Vec3 other)
        {
            return new Vec3(X + other.X, Y + other.Y, Z + other.Z);
        }

        public Vec3 Scale(double scale)
        {
            return new Vec3(X * scale, Y * scale, Z * scale);
        }

        public Vec3 Normalize()
        {
            var length = Math.Sqrt(X * X + Y * Y + Z * Z);
            return length <= 0
                ? this
                : new Vec3(X / length, Y / length, Z / length);
        }
    }
}

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;

namespace SharpTimer.App.Rendering;

internal static class SmartCubePreviewRenderer
{
    private const double FaceDistance = 1.5;
    private const double CellHalf = 0.505;
    private const double StickerHalf = 0.455;
    public const double DefaultYawDegrees = -38;
    public const double DefaultPitchDegrees = 27;
    private const double CameraDistance = 11.5;
    private const double MinVisibleNormalZ = 0.05;
    private const double StableProjectionWidth = 5.25;
    private const double StableProjectionHeight = 5.25;
    private const double BaseCornerRadius = 0.045;
    private const double StickerCornerRadiusSmall = 0.105;
    private const double StickerCornerRadiusLarge = 0.345;
    private static readonly Facelet[] Facelets = BuildFacelets();

    public static void Render(
        CanvasDrawingSession session,
        double width,
        double height,
        SmartCubePreviewRenderRequest request)
    {
        session.Clear(Color.FromArgb(0, 0, 0, 0));

        var state = string.IsNullOrWhiteSpace(request.Facelets) || request.Facelets.Length < 54
            ? null
            : request.Facelets[..54];
        var animation = SmartCubeMoveAnimation.IsValid(request.Animation)
            ? request.Animation
            : null;
        var batch = BuildRenderBatch(
            state,
            request.YawDegrees,
            request.PitchDegrees,
            request.Orientation,
            animation);
        if (batch.Tiles.Count == 0 || !batch.Bounds.IsValid)
        {
            return;
        }

        var renderWidth = GetCanvasLength(width, 180);
        var renderHeight = GetCanvasLength(height, 180);
        var scale = Math.Min(
            renderWidth * 0.92 / StableProjectionWidth,
            renderHeight * 0.88 / StableProjectionHeight);
        var offsetX = renderWidth / 2;
        var offsetY = renderHeight / 2;
        var strokeWidth = Math.Max(1.2, scale * 0.045);

        foreach (var tile in batch.Tiles)
        {
            DrawRoundedQuad(
                session,
                tile.BasePoints,
                scale,
                offsetX,
                offsetY,
                tile.BaseColor,
                null,
                0,
                BaseCornerRadius * scale);
            DrawRoundedQuad(
                session,
                tile.StickerPoints,
                scale,
                offsetX,
                offsetY,
                tile.StickerColor,
                Color.FromArgb(0xee, 8, 8, 8),
                strokeWidth,
                tile.StickerCornerRadii.Select(radius => radius * scale).ToArray());
        }
    }

    private static RenderBatch BuildRenderBatch(
        string? state,
        double yawDegrees,
        double pitchDegrees,
        SmartCubePreviewOrientation? orientation,
        SmartCubeMoveAnimation? animation)
    {
        animation = SmartCubeMoveAnimation.IsValid(animation) ? animation : null;
        var animatedState = animation?.FromFacelets ?? state;
        var tiles = new List<RenderTile>(54);
        var bounds = new Bounds();
        for (var i = 0; i < Facelets.Length; i++)
        {
            var facelet = ApplyMoveAnimation(Facelets[i], animation);
            var transform = BuildTransform(facelet, yawDegrees, pitchDegrees, orientation);
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
                ShadeSticker(FaceColor(animatedState, i), transform.Normal),
                facelet.StickerCornerRadii));
        }

        return new RenderBatch(tiles.OrderBy(tile => tile.Depth).ToArray(), bounds);
    }

    private static void DrawRoundedQuad(
        CanvasDrawingSession session,
        IReadOnlyList<Point> points,
        double scale,
        double offsetX,
        double offsetY,
        Color fill,
        Color? stroke,
        double strokeWidth,
        double cornerRadius)
    {
        DrawRoundedQuad(
            session,
            points,
            scale,
            offsetX,
            offsetY,
            fill,
            stroke,
            strokeWidth,
            Enumerable.Repeat(cornerRadius, points.Count).ToArray());
    }

    private static void DrawRoundedQuad(
        CanvasDrawingSession session,
        IReadOnlyList<Point> points,
        double scale,
        double offsetX,
        double offsetY,
        Color fill,
        Color? stroke,
        double strokeWidth,
        IReadOnlyList<double> cornerRadii)
    {
        var scaledPoints = points
            .Select(point => new Point(point.X * scale + offsetX, point.Y * scale + offsetY))
            .ToArray();
        using var geometry = BuildRoundedGeometry(session, scaledPoints, cornerRadii);
        session.FillGeometry(geometry, fill);

        if (stroke is not null && strokeWidth > 0)
        {
            session.DrawGeometry(geometry, stroke.Value, (float)strokeWidth);
        }
    }

    private static CanvasGeometry BuildRoundedGeometry(
        CanvasDrawingSession session,
        IReadOnlyList<Point> points,
        IReadOnlyList<double> cornerRadii)
    {
        if (points.Count < 3)
        {
            return CanvasGeometry.CreatePolygon(session, Array.Empty<System.Numerics.Vector2>());
        }

        var starts = new Point[points.Count];
        var ends = new Point[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var previous = points[(i - 1 + points.Count) % points.Count];
            var next = points[(i + 1) % points.Count];
            var previousLength = Distance(point, previous);
            var nextLength = Distance(point, next);
            var cornerRadius = i < cornerRadii.Count
                ? cornerRadii[i]
                : 0;
            var radius = Math.Min(cornerRadius, Math.Min(previousLength, nextLength) * 0.38);

            starts[i] = MoveToward(point, previous, radius);
            ends[i] = MoveToward(point, next, radius);
        }

        using var builder = new CanvasPathBuilder(session);
        builder.BeginFigure(ToVector(ends[0]));
        for (var i = 1; i < points.Count; i++)
        {
            builder.AddLine(ToVector(starts[i]));
            builder.AddQuadraticBezier(ToVector(points[i]), ToVector(ends[i]));
        }

        builder.AddLine(ToVector(starts[0]));
        builder.AddQuadraticBezier(ToVector(points[0]), ToVector(ends[0]));
        builder.EndFigure(CanvasFigureLoop.Closed);
        return CanvasGeometry.CreatePath(builder);
    }

    private static System.Numerics.Vector2 ToVector(Point point)
    {
        return new System.Numerics.Vector2((float)point.X, (float)point.Y);
    }

    private static double Distance(Point first, Point second)
    {
        var deltaX = second.X - first.X;
        var deltaY = second.Y - first.Y;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    private static Point MoveToward(Point from, Point to, double distance)
    {
        var length = Distance(from, to);
        if (length <= 0)
        {
            return from;
        }

        var ratio = distance / length;
        return new Point(
            from.X + (to.X - from.X) * ratio,
            from.Y + (to.Y - from.Y) * ratio);
    }

    private static Transform BuildTransform(
        Facelet facelet,
        double yawDegrees,
        double pitchDegrees,
        SmartCubePreviewOrientation? orientation)
    {
        var center = ApplyOrientation(facelet.Center, orientation);
        var normal = ApplyOrientation(facelet.Normal, orientation);
        var u = ApplyOrientation(facelet.U, orientation);
        var v = ApplyOrientation(facelet.V, orientation);
        center = ApplyViewRotation(center, yawDegrees, pitchDegrees);
        normal = ApplyViewRotation(normal, yawDegrees, pitchDegrees).Normalize();
        u = ApplyViewRotation(u, yawDegrees, pitchDegrees).Normalize();
        v = ApplyViewRotation(v, yawDegrees, pitchDegrees).Normalize();
        return new Transform(center, normal, u, v);
    }

    private static Vec3 ApplyOrientation(Vec3 point, SmartCubePreviewOrientation? orientation)
    {
        return orientation?.Rotate(point) ?? point;
    }

    private static Facelet ApplyMoveAnimation(Facelet facelet, SmartCubeMoveAnimation? animation)
    {
        if (animation is null || !IsInAnimatedLayer(facelet.Center, animation.Face))
        {
            return facelet;
        }

        var angle = GetMoveAnimationAngle(animation.Face, animation.Power) * Clamp(animation.Progress, 0, 1);
        var axis = animation.Face switch
        {
            'R' or 'L' => 0,
            'U' or 'D' => 1,
            _ => 2
        };

        return new Facelet(
            RotateAroundAxis(facelet.Center, axis, angle),
            RotateAroundAxis(facelet.Normal, axis, angle),
            RotateAroundAxis(facelet.U, axis, angle),
            RotateAroundAxis(facelet.V, axis, angle),
            facelet.StickerCornerRadii);
    }

    private static bool IsInAnimatedLayer(Vec3 center, char face)
    {
        return face switch
        {
            'U' => center.Y > 0.5,
            'D' => center.Y < -0.5,
            'R' => center.X > 0.5,
            'L' => center.X < -0.5,
            'F' => center.Z > 0.5,
            'B' => center.Z < -0.5,
            _ => false
        };
    }

    private static double GetMoveAnimationAngle(char face, int power)
    {
        var quarterTurn = face switch
        {
            'U' => -90,
            'D' => 90,
            'R' => -90,
            'L' => 90,
            'F' => -90,
            'B' => 90,
            _ => 0
        };

        return quarterTurn * power;
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

    private static Vec3 ApplyViewRotation(Vec3 point, double yawDegrees, double pitchDegrees)
    {
        var rotated = RotateAroundAxis(point, axis: 1, yawDegrees);
        return RotateAroundAxis(rotated, axis: 0, pitchDegrees);
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

    private static double GetCanvasLength(double actual, double fallback)
    {
        return actual > 0
            ? actual
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
                facelets.Add(new Facelet(center, normal, u, v, CreateStickerCornerRadii(row, column)));
            }
        }
    }

    private static double[] CreateStickerCornerRadii(int row, int column)
    {
        var radii = new double[4];
        if (row == 1 && column == 1)
        {
            FillRadii(radii, StickerCornerRadiusLarge);
        }
        else if (row == 1)
        {
            var innerXSign = column == 0 ? 1 : -1;
            SetRadiiOnInnerSide(radii, innerXSign, 0, StickerCornerRadiusLarge);
        }
        else if (column == 1)
        {
            var innerYSign = row == 0 ? 1 : -1;
            SetRadiiOnInnerSide(radii, 0, innerYSign, StickerCornerRadiusLarge);
        }
        else
        {
            var innerXSign = column == 0 ? 1 : -1;
            var innerYSign = row == 0 ? 1 : -1;
            SetRadiiOnInnerSide(radii, innerXSign, innerYSign, StickerCornerRadiusSmall);
        }

        return radii;
    }

    private static void FillRadii(double[] radii, double radius)
    {
        for (var i = 0; i < radii.Length; i++)
        {
            radii[i] = radius;
        }
    }

    private static void SetRadiiOnInnerSide(double[] radii, int innerXSign, int innerYSign, double radius)
    {
        for (var i = 0; i < radii.Length; i++)
        {
            var cornerXSign = i is 1 or 2 ? 1 : -1;
            var cornerYSign = i >= 2 ? 1 : -1;
            if ((innerXSign == 0 || cornerXSign == innerXSign)
                && (innerYSign == 0 || cornerYSign == innerYSign))
            {
                radii[i] = radius;
            }
        }
    }

    private sealed record Facelet(
        Vec3 Center,
        Vec3 Normal,
        Vec3 U,
        Vec3 V,
        IReadOnlyList<double> StickerCornerRadii);

    private sealed record Transform(Vec3 Center, Vec3 Normal, Vec3 U, Vec3 V);

    private sealed record RenderTile(
        IReadOnlyList<Point> BasePoints,
        IReadOnlyList<Point> StickerPoints,
        double Depth,
        Color BaseColor,
        Color StickerColor,
        IReadOnlyList<double> StickerCornerRadii);

    private sealed record RenderBatch(IReadOnlyList<RenderTile> Tiles, Bounds Bounds);

    private sealed class Bounds
    {
        public double Left { get; private set; } = double.PositiveInfinity;
        public double Top { get; private set; } = double.PositiveInfinity;
        public double Right { get; private set; } = double.NegativeInfinity;
        public double Bottom { get; private set; } = double.NegativeInfinity;
        public bool IsValid => !double.IsInfinity(Left) && !double.IsInfinity(Top)
            && !double.IsInfinity(Right) && !double.IsInfinity(Bottom);

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

    internal readonly record struct Vec3(double X, double Y, double Z)
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

internal sealed record SmartCubePreviewRenderRequest(
    string? Facelets,
    double YawDegrees,
    double PitchDegrees,
    SmartCubePreviewOrientation? Orientation,
    SmartCubeMoveAnimation? Animation,
    bool UseLightweightShapes);

internal sealed record SmartCubeMoveAnimation(string FromFacelets, string Move, double Progress)
{
    public char Face => Move[0];

    public int Power => Move.Length == 1
        ? 1
        : Move[1] == '2'
            ? 2
            : -1;

    public static bool IsValid(SmartCubeMoveAnimation? animation)
    {
        return animation is not null
            && animation.FromFacelets.Length == 54
            && animation.Move.Length is 1 or 2
            && "URFDLB".Contains(animation.Move[0], StringComparison.Ordinal)
            && (animation.Move.Length == 1 || animation.Move[1] is '2' or '\'');
    }
}

internal sealed record SmartCubePreviewOrientation(double X, double Y, double Z, double W)
{
    public static SmartCubePreviewOrientation? Create(double x, double y, double z, double w)
    {
        var length = Math.Sqrt(x * x + y * y + z * z + w * w);
        if (length <= 0)
        {
            return null;
        }

        return new SmartCubePreviewOrientation(x / length, y / length, z / length, w / length);
    }

    public SmartCubePreviewRenderer.Vec3 Rotate(SmartCubePreviewRenderer.Vec3 point)
    {
        var source = new SmartCubePreviewRenderer.Vec3(point.X, -point.Z, point.Y);
        var rotated = RotateSource(source);
        return new SmartCubePreviewRenderer.Vec3(rotated.X, rotated.Z, -rotated.Y);
    }

    public SmartCubePreviewOrientation Inverse()
    {
        return new SmartCubePreviewOrientation(-X, -Y, -Z, W);
    }

    public SmartCubePreviewOrientation Multiply(SmartCubePreviewOrientation other)
    {
        return Create(
            W * other.X + X * other.W + Y * other.Z - Z * other.Y,
            W * other.Y - X * other.Z + Y * other.W + Z * other.X,
            W * other.Z + X * other.Y - Y * other.X + Z * other.W,
            W * other.W - X * other.X - Y * other.Y - Z * other.Z)
            ?? new SmartCubePreviewOrientation(0, 0, 0, 1);
    }

    public SmartCubePreviewOrientation SlerpToward(SmartCubePreviewOrientation target, double amount)
    {
        var dot = X * target.X + Y * target.Y + Z * target.Z + W * target.W;
        var targetX = target.X;
        var targetY = target.Y;
        var targetZ = target.Z;
        var targetW = target.W;
        if (dot < 0)
        {
            dot = -dot;
            targetX = -targetX;
            targetY = -targetY;
            targetZ = -targetZ;
            targetW = -targetW;
        }

        var clamped = Math.Max(0, Math.Min(1, amount));
        if (dot > 0.9995)
        {
            return Create(
                X + (targetX - X) * clamped,
                Y + (targetY - Y) * clamped,
                Z + (targetZ - Z) * clamped,
                W + (targetW - W) * clamped)
                ?? target;
        }

        dot = Math.Max(-1, Math.Min(1, dot));
        var theta0 = Math.Acos(dot);
        var theta = theta0 * clamped;
        var sinTheta = Math.Sin(theta);
        var sinTheta0 = Math.Sin(theta0);
        if (sinTheta0 <= 0)
        {
            return target;
        }

        var scale0 = Math.Cos(theta) - dot * sinTheta / sinTheta0;
        var scale1 = sinTheta / sinTheta0;
        return Create(
            X * scale0 + targetX * scale1,
            Y * scale0 + targetY * scale1,
            Z * scale0 + targetZ * scale1,
            W * scale0 + targetW * scale1)
            ?? target;
    }

    public bool IsCloseTo(SmartCubePreviewOrientation target, double maxAngleDegrees)
    {
        var dot = Math.Abs(X * target.X + Y * target.Y + Z * target.Z + W * target.W);
        var maxHalfAngleRadians = Math.Max(0, maxAngleDegrees) * Math.PI / 360;
        return dot >= Math.Cos(maxHalfAngleRadians);
    }

    private SmartCubePreviewRenderer.Vec3 RotateSource(SmartCubePreviewRenderer.Vec3 point)
    {
        var qx = X;
        var qy = Y;
        var qz = Z;
        var qw = W;
        var tx = 2 * (qy * point.Z - qz * point.Y);
        var ty = 2 * (qz * point.X - qx * point.Z);
        var tz = 2 * (qx * point.Y - qy * point.X);
        return new SmartCubePreviewRenderer.Vec3(
            point.X + qw * tx + qy * tz - qz * ty,
            point.Y + qw * ty + qz * tx - qx * tz,
            point.Z + qw * tz + qx * ty - qy * tx);
    }
}

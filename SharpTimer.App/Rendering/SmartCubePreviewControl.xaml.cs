using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SharpTimer.Core.SmartCubes;
using System;
using System.Diagnostics;

namespace SharpTimer.App.Rendering;

public sealed partial class SmartCubePreviewControl : UserControl
{
    private const double MinPitch = -68;
    private const double MaxPitch = 68;
    private const double DragThreshold = 4;
    private const double DragSensitivity = 0.45;
    private const double AnimationMilliseconds = 110;
    private const double GyroDefaultYawDegrees = 0;
    private const double GyroDefaultPitchDegrees = 18;
    private const double InitialOrientationFrameBlend = 0.22;
    private const double OrientationInterpolationTimeConstantMilliseconds = 42;
    private const double MinOrientationFrameBlend = 0.08;
    private const double MaxOrientationFrameBlend = 0.55;

    private bool _isPointerDown;
    private bool _didDrag;
    private bool _isFrameRendering;
    private bool _hasPendingFrameRender;
    private bool _pendingFrameRenderLightweight;
    private bool _pendingFrameRenderRounded;
    private string? _facelets;
    private string? _animationFrom;
    private string? _animationTo;
    private string? _animationMove;
    private SmartCubePreviewOrientation? _orientation;
    private SmartCubePreviewOrientation? _targetOrientation;
    private SmartCubePreviewOrientation? _rawOrientation;
    private SmartCubePreviewOrientation? _orientationCalibration;
    private bool _hasOrientationCalibration;
    private DateTimeOffset _animationStartedAt;
    private double _yaw = SmartCubePreviewRenderer.DefaultYawDegrees;
    private double _pitch = SmartCubePreviewRenderer.DefaultPitchDegrees;
    private double _startX;
    private double _startY;
    private double _lastX;
    private double _lastY;
    private long _lastRenderingTimestamp;

    public SmartCubePreviewControl()
    {
        InitializeComponent();
        SizeChanged += SmartCubePreviewControl_SizeChanged;
        Unloaded += SmartCubePreviewControl_Unloaded;
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? InteractionCompleted;

    public void SetFacelets(string? facelets)
    {
        if (_animationTo is not null && string.Equals(facelets, _animationTo, StringComparison.Ordinal))
        {
            _facelets = facelets;
            RequestFrameRender(useLightweightShapes: true);
            return;
        }

        StopAnimation();
        _facelets = facelets;
        RequestFrameRender(useLightweightShapes: false, preferRoundedShapes: true);
    }

    public void PlayMove(string fromFacelets, string toFacelets, string move)
    {
        if (!ThreeByThreeFacelets.IsValidState(fromFacelets)
            || !ThreeByThreeFacelets.IsValidState(toFacelets)
            || string.IsNullOrWhiteSpace(move))
        {
            SetFacelets(toFacelets);
            return;
        }

        _facelets = toFacelets;
        _animationFrom = fromFacelets;
        _animationTo = toFacelets;
        _animationMove = SmartCubeMoveNotation.Normalize(move);
        _animationStartedAt = DateTimeOffset.UtcNow;
        StartAnimation();
        RequestFrameRender(useLightweightShapes: true);
    }

    public void ResetView()
    {
        _yaw = SmartCubePreviewRenderer.DefaultYawDegrees;
        _pitch = SmartCubePreviewRenderer.DefaultPitchDegrees;
        _orientation = null;
        _targetOrientation = null;
        _rawOrientation = null;
        _orientationCalibration = null;
        _hasOrientationCalibration = false;
        RequestFrameRender(useLightweightShapes: false, preferRoundedShapes: true);
    }

    public void SetOrientation(double x, double y, double z, double w)
    {
        _rawOrientation = SmartCubePreviewOrientation.Create(x, y, z, w);
        if (_rawOrientation is not null && !_hasOrientationCalibration)
        {
            _yaw = GyroDefaultYawDegrees;
            _pitch = GyroDefaultPitchDegrees;
            _orientationCalibration = _rawOrientation.Inverse();
            _hasOrientationCalibration = true;
        }

        var nextOrientation = _rawOrientation is null
            ? null
            : _orientationCalibration?.Multiply(_rawOrientation) ?? _rawOrientation;
        if (_orientation is null || nextOrientation is null)
        {
            _orientation = nextOrientation;
            _targetOrientation = null;
            RequestFrameRender(useLightweightShapes: nextOrientation is not null);
            return;
        }

        _targetOrientation = nextOrientation;
        StartFrameRendering();
    }

    public void ResetViewAngles()
    {
        _yaw = SmartCubePreviewRenderer.DefaultYawDegrees;
        _pitch = SmartCubePreviewRenderer.DefaultPitchDegrees;
        RequestFrameRender(useLightweightShapes: false, preferRoundedShapes: true);
    }

    public void ResetOrientationToDefault()
    {
        _yaw = GyroDefaultYawDegrees;
        _pitch = GyroDefaultPitchDegrees;
        _orientationCalibration = _rawOrientation?.Inverse();
        _hasOrientationCalibration = _rawOrientation is not null;
        _orientation = _rawOrientation is null
            ? null
            : _orientationCalibration?.Multiply(_rawOrientation);
        _targetOrientation = null;
        RequestFrameRender(useLightweightShapes: _orientation is not null);
    }

    public void StopAnimation()
    {
        _animationFrom = null;
        _animationTo = null;
        _animationMove = null;
        StopFrameRenderingIfIdle();
    }

    private void StartAnimation()
    {
        StartFrameRendering();
    }

    private void StartFrameRendering()
    {
        if (_isFrameRendering)
        {
            return;
        }

        _lastRenderingTimestamp = 0;
        CompositionTarget.Rendering += CompositionTarget_Rendering;
        _isFrameRendering = true;
    }

    private void StopFrameRenderingIfIdle()
    {
        if (_isFrameRendering && !HasMoveAnimation() && !HasOrientationTransition() && !_hasPendingFrameRender)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            _isFrameRendering = false;
            _lastRenderingTimestamp = 0;
        }
    }

    private void SmartCubePreviewControl_Unloaded(object sender, RoutedEventArgs e)
    {
        StopAnimation();
        _hasPendingFrameRender = false;
        _pendingFrameRenderLightweight = false;
        _pendingFrameRenderRounded = false;
        StopFrameRenderingIfIdle();
    }

    private void SmartCubePreviewControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        PreviewCanvas.Width = ActualWidth;
        PreviewCanvas.Height = ActualHeight;
        RequestFrameRender(useLightweightShapes: HasMoveAnimation() || HasOrientationTransition());
    }

    private void CompositionTarget_Rendering(object? sender, object e)
    {
        var elapsedMilliseconds = GetFrameElapsedMilliseconds();
        var shouldRender = false;
        var renderLightweight = false;
        var pendingRounded = _pendingFrameRenderRounded;

        if (_hasPendingFrameRender)
        {
            shouldRender = true;
            renderLightweight = _pendingFrameRenderLightweight;
            _hasPendingFrameRender = false;
            _pendingFrameRenderLightweight = false;
            _pendingFrameRenderRounded = false;
        }

        if (HasMoveAnimation())
        {
            if (GetAnimationProgress() >= 1)
            {
                _facelets = _animationTo;
                _animationFrom = null;
                _animationTo = null;
                _animationMove = null;
            }
            else
            {
                renderLightweight = true;
            }

            shouldRender = true;
        }

        if (_targetOrientation is not null)
        {
            _orientation = _orientation?.SlerpToward(_targetOrientation, GetOrientationFrameBlend(elapsedMilliseconds))
                ?? _targetOrientation;
            if (_orientation.IsCloseTo(_targetOrientation))
            {
                _orientation = _targetOrientation;
                _targetOrientation = null;
            }
            else
            {
                renderLightweight = true;
            }

            shouldRender = true;
        }

        if (pendingRounded && !HasMoveAnimation() && !HasOrientationTransition())
        {
            renderLightweight = false;
        }

        if (shouldRender)
        {
            Render(renderLightweight);
        }

        StopFrameRenderingIfIdle();
    }

    private void PreviewCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(PreviewCanvas).Position;
        _isPointerDown = true;
        _didDrag = false;
        _startX = position.X;
        _startY = position.Y;
        _lastX = position.X;
        _lastY = position.Y;
        PreviewCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void PreviewCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPointerDown)
        {
            return;
        }

        var position = e.GetCurrentPoint(PreviewCanvas).Position;
        var totalX = position.X - _startX;
        var totalY = position.Y - _startY;
        if (!_didDrag && Math.Sqrt(totalX * totalX + totalY * totalY) < DragThreshold)
        {
            return;
        }

        _didDrag = true;
        var deltaX = position.X - _lastX;
        var deltaY = position.Y - _lastY;
        _lastX = position.X;
        _lastY = position.Y;
        _yaw += deltaX * DragSensitivity;
        _pitch = Math.Max(MinPitch, Math.Min(MaxPitch, _pitch + deltaY * DragSensitivity));
        RequestFrameRender(useLightweightShapes: true);
        e.Handled = true;
    }

    private void PreviewCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndPointerInteraction(e);
    }

    private void PreviewCanvas_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        EndPointerInteraction(e);
    }

    private void EndPointerInteraction(PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
        PreviewCanvas.ReleasePointerCapture(e.Pointer);
        RequestFrameRender(useLightweightShapes: false, preferRoundedShapes: true);
        InteractionCompleted?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void PreviewCanvas_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (_didDrag)
        {
            _didDrag = false;
            e.Handled = true;
            return;
        }

        OpenRequested?.Invoke(this, EventArgs.Empty);
        InteractionCompleted?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void RequestFrameRender(bool useLightweightShapes, bool preferRoundedShapes = false)
    {
        _hasPendingFrameRender = true;
        if (preferRoundedShapes)
        {
            _pendingFrameRenderRounded = true;
            _pendingFrameRenderLightweight = false;
        }
        else if (useLightweightShapes && !_pendingFrameRenderRounded)
        {
            _pendingFrameRenderLightweight = true;
        }

        StartFrameRendering();
    }

    private void Render(bool useLightweightShapes = false)
    {
        SmartCubePreviewRenderer.Render(
            PreviewCanvas,
            _animationTo ?? _facelets,
            _yaw,
            _pitch,
            _orientation,
            CreateAnimation(),
            useLightweightShapes);
    }

    private SmartCubeMoveAnimation? CreateAnimation()
    {
        if (_animationFrom is null || _animationMove is null)
        {
            return null;
        }

        return new SmartCubeMoveAnimation(
            _animationFrom,
            _animationMove,
            EaseAnimationProgress(GetAnimationProgress()));
    }

    private bool HasMoveAnimation()
    {
        return _animationFrom is not null && _animationTo is not null && _animationMove is not null;
    }

    private bool HasOrientationTransition()
    {
        return _targetOrientation is not null;
    }

    private double GetAnimationProgress()
    {
        var elapsed = (DateTimeOffset.UtcNow - _animationStartedAt).TotalMilliseconds;
        return Math.Max(0, Math.Min(1, elapsed / AnimationMilliseconds));
    }

    private static double EaseAnimationProgress(double progress)
    {
        return progress * progress * (3 - 2 * progress);
    }

    private double GetFrameElapsedMilliseconds()
    {
        var timestamp = Stopwatch.GetTimestamp();
        if (_lastRenderingTimestamp == 0)
        {
            _lastRenderingTimestamp = timestamp;
            return 0;
        }

        var elapsedMilliseconds = (timestamp - _lastRenderingTimestamp) * 1000d / Stopwatch.Frequency;
        _lastRenderingTimestamp = timestamp;
        return elapsedMilliseconds;
    }

    private static double GetOrientationFrameBlend(double elapsedMilliseconds)
    {
        if (elapsedMilliseconds <= 0)
        {
            return InitialOrientationFrameBlend;
        }

        var blend = 1 - Math.Exp(-elapsedMilliseconds / OrientationInterpolationTimeConstantMilliseconds);
        return Math.Max(MinOrientationFrameBlend, Math.Min(MaxOrientationFrameBlend, blend));
    }
}

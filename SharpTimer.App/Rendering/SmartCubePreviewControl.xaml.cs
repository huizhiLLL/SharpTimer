using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SharpTimer.Core.SmartCubes;
using System;

namespace SharpTimer.App.Rendering;

public sealed partial class SmartCubePreviewControl : UserControl
{
    private const double MinPitch = -68;
    private const double MaxPitch = 68;
    private const double DragThreshold = 4;
    private const double DragSensitivity = 0.45;
    private const double AnimationMilliseconds = 150;

    private bool _isPointerDown;
    private bool _didDrag;
    private bool _isAnimationRunning;
    private string? _facelets;
    private string? _animationFrom;
    private string? _animationTo;
    private string? _animationMove;
    private DateTimeOffset _animationStartedAt;
    private double _yaw = SmartCubePreviewRenderer.DefaultYawDegrees;
    private double _pitch = SmartCubePreviewRenderer.DefaultPitchDegrees;
    private double _startX;
    private double _startY;
    private double _lastX;
    private double _lastY;

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
            Render();
            return;
        }

        StopAnimation();
        _facelets = facelets;
        Render();
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
        Render();
    }

    public void ResetView()
    {
        _yaw = SmartCubePreviewRenderer.DefaultYawDegrees;
        _pitch = SmartCubePreviewRenderer.DefaultPitchDegrees;
        Render();
    }

    public void StopAnimation()
    {
        if (_isAnimationRunning)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            _isAnimationRunning = false;
        }

        _animationFrom = null;
        _animationTo = null;
        _animationMove = null;
    }

    private void StartAnimation()
    {
        if (_isAnimationRunning)
        {
            return;
        }

        CompositionTarget.Rendering += CompositionTarget_Rendering;
        _isAnimationRunning = true;
    }

    private void SmartCubePreviewControl_Unloaded(object sender, RoutedEventArgs e)
    {
        StopAnimation();
    }

    private void SmartCubePreviewControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        PreviewCanvas.Width = ActualWidth;
        PreviewCanvas.Height = ActualHeight;
        Render();
    }

    private void CompositionTarget_Rendering(object? sender, object e)
    {
        if (_animationFrom is null || _animationTo is null || _animationMove is null)
        {
            StopAnimation();
            Render();
            return;
        }

        if (GetAnimationProgress() >= 1)
        {
            _facelets = _animationTo;
            StopAnimation();
            Render();
            return;
        }

        Render();
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
        Render();
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

    private void Render()
    {
        SmartCubePreviewRenderer.Render(
            PreviewCanvas,
            _animationTo ?? _facelets,
            _yaw,
            _pitch,
            CreateAnimation());
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

    private double GetAnimationProgress()
    {
        var elapsed = (DateTimeOffset.UtcNow - _animationStartedAt).TotalMilliseconds;
        return Math.Max(0, Math.Min(1, elapsed / AnimationMilliseconds));
    }

    private static double EaseAnimationProgress(double progress)
    {
        return progress * progress * (3 - 2 * progress);
    }
}

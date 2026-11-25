using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace WorkflowStudio.Controls;

/// <summary>
/// A lightweight, zero-dependency WinUI 3 pane splitter control.
/// Demonstrates custom UIElement pointer capture, ProtectedCursor, and visual feedback.
/// </summary>
public sealed class PaneSplitter : Grid
{
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(PaneSplitter),
            new PropertyMetadata(Orientation.Vertical, OnOrientationChanged));

    private readonly Border _gripBar;
    private bool _isDragging;
    private Point _lastPointerPosition;

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Fires continuously while the user drags the splitter, passing the pointer delta in pixels.
    /// </summary>
    public event EventHandler<double>? DragDelta;

    public PaneSplitter()
    {
        Background = new SolidColorBrush(Colors.Transparent);

        _gripBar = new Border
        {
            CornerRadius = new CornerRadius(2),
            Opacity = 0.35
        };

        Children.Add(_gripBar);
        UpdateLayoutForOrientation();

        Loaded += (_, _) => ApplyThemeBrush(isHighlighted: false);
        ActualThemeChanged += (_, _) => ApplyThemeBrush(isHighlighted: _isDragging);
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
    }

    private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PaneSplitter splitter)
        {
            splitter.UpdateLayoutForOrientation();
        }
    }

    private void UpdateLayoutForOrientation()
    {
        if (Orientation == Orientation.Vertical)
        {
            Width = 8;
            Height = double.NaN;
            _gripBar.Width = 2;
            _gripBar.Height = 36;
            _gripBar.HorizontalAlignment = HorizontalAlignment.Center;
            _gripBar.VerticalAlignment = VerticalAlignment.Center;
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        }
        else
        {
            Height = 8;
            Width = double.NaN;
            _gripBar.Height = 2;
            _gripBar.Width = 36;
            _gripBar.HorizontalAlignment = HorizontalAlignment.Center;
            _gripBar.VerticalAlignment = VerticalAlignment.Center;
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
        }
    }

    private void ApplyThemeBrush(bool isHighlighted)
    {
        var key = isHighlighted ? "AccentFillColorDefaultBrush" : "DividerStrokeColorDefaultBrush";
        if (Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush)
        {
            _gripBar.Background = brush;
        }
        _gripBar.Opacity = isHighlighted ? 1.0 : 0.65;
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            ApplyThemeBrush(isHighlighted: true);
        }
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            ApplyThemeBrush(isHighlighted: false);
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var referenceElement = Parent as UIElement ?? this;
        _lastPointerPosition = e.GetCurrentPoint(referenceElement).Position;
        _isDragging = CapturePointer(e.Pointer);
        ApplyThemeBrush(isHighlighted: true);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var referenceElement = Parent as UIElement ?? this;
        var current = e.GetCurrentPoint(referenceElement).Position;
        var delta = Orientation == Orientation.Vertical
            ? current.X - _lastPointerPosition.X
            : current.Y - _lastPointerPosition.Y;

        _lastPointerPosition = current;

        if (Math.Abs(delta) > 0.1)
        {
            DragDelta?.Invoke(this, delta);
        }

        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleasePointerCapture(e.Pointer);
            ApplyThemeBrush(isHighlighted: false);
            e.Handled = true;
        }
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        ApplyThemeBrush(isHighlighted: false);
    }
}

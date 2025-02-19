using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Diagnostics;
using WallMod.Helpers;
using WallMod.Models;
using WallMod.ViewModels;

namespace WallMod.Views;

/**
 * Code behind for main application functionality
 * 
 * NOTE: messy code for dragging rect functionality
 */
public partial class MainWindow : Window
{
    private enum RectOperation { None, Dragging, Resizing }
    private bool _isPointerDown;
    private RectOperation _operation = RectOperation.None;

    private Avalonia.Point _dragStart;
    private double _rectStartX, _rectStartY;
    private double _rectStartWidth, _rectStartHeight;
    private double _aspectRatio = 1.0;
    private const double CornerHitSize = 16;

    private DateTime lastTapTime = DateTime.MinValue;
    private int DoubleTapThreshold = 500;
    private Wallpaper lastTapImage = new();

    public MainWindow()
    {
        InitializeComponent();

        this.SizeChanged += OnWindowSizeChanged;
    }


    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // disable the rect
        ResetRectangle();

        if (DataContext is MainWindowViewModel vm)
        {
            vm.LastSelMonitor = null;
            // manipulate monitor UI
            foreach (MonitorInfo mon in vm.MonitorList)
            {
                mon.FillColour = "Navy";
            }
            SetBackgroundButton.IsEnabled = false;
        }


    }

    private void GridSplitterDragExec(object? sender, VectorEventArgs e)
    {
        // disable the rect
        ResetRectangle();

        if (DataContext is MainWindowViewModel vm)
        {
            vm.LastSelMonitor = null;
            // manipulate monitor UI
            foreach (MonitorInfo mon in vm.MonitorList)
            {
                mon.FillColour = "Navy";
            }
            SetBackgroundButton.IsEnabled = false;
        }
    }


    public void ShowDraggableRectangle(MonitorInfo monitor)
    {
        if (DragRect == null || OverlayCanvas == null || PreviewImage == null) return;

        double realW = monitor.Bounds.Width, realH = monitor.Bounds.Height;
        if (realW <= 0 || realH <= 0) return;

        double previewW = PreviewImage.Bounds.Width, previewH = PreviewImage.Bounds.Height;
        if (previewW <= 0 || previewH <= 0) return;

        double monitorRatio = realW / realH;
        double finalWidth = previewW, finalHeight = previewH;

        if (monitorRatio > previewW / previewH)
        {
            finalHeight = previewW / monitorRatio;
        }
        else
        {
            finalWidth = previewH * monitorRatio;
        }

        DragRect.Width = finalWidth;
        DragRect.Height = finalHeight;
        _aspectRatio = finalWidth / finalHeight;

        Canvas.SetLeft(DragRect, (previewW - finalWidth) / 2);
        Canvas.SetTop(DragRect, (previewH - finalHeight) / 2);

        DragRect.IsVisible = true;
    }

    public void ResetRectangle()
    {
        if (DragRect == null) return;
        DragRect.IsVisible = false;
        DragRect.Width = DragRect.Height = 0;
        Canvas.SetLeft(DragRect, 0);
        Canvas.SetTop(DragRect, 0);
        _aspectRatio = 1.0;
    }

    // ------------------------------------------------------
    // 2) Pointer Pressed => Decide DRAG vs RESIZE
    // ------------------------------------------------------
    private void DragRect_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DragRect == null || OverlayCanvas == null) return;

        _isPointerDown = true;
        e.Pointer.Capture(DragRect);

        var pos = e.GetPosition(OverlayCanvas);

        // Save current rect coords
        _dragStart = pos;
        _rectStartX = Canvas.GetLeft(DragRect);
        _rectStartY = Canvas.GetTop(DragRect);
        _rectStartWidth = DragRect.Width;
        _rectStartHeight = DragRect.Height;

        double rectRight = _rectStartX + _rectStartWidth;
        double rectBottom = _rectStartY + _rectStartHeight;

        // If near bottom-right corner => Resize
        if (Math.Abs(pos.X - rectRight) < CornerHitSize && Math.Abs(pos.Y - rectBottom) < CornerHitSize)
        {
            _operation = RectOperation.Resizing;
        }
        else
        {
            _operation = RectOperation.Dragging;
        }
    }

    private void DragRect_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerDown || e.Pointer.Captured != DragRect) return;
        var pos = e.GetPosition(OverlayCanvas);
        double dx = pos.X - _dragStart.X, dy = pos.Y - _dragStart.Y;
        if (_operation == RectOperation.Dragging) DoDragging(dx, dy);
        if (_operation == RectOperation.Resizing) DoResizing(dx, dy);
    }

    private void DragRect_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Captured == DragRect) e.Pointer.Capture(null);
        _isPointerDown = false;
        _operation = RectOperation.None;
    }

    private void DoDragging(double dx, double dy)
    {
        double maxX = OverlayCanvas.Bounds.Width - DragRect.Width;
        double maxY = OverlayCanvas.Bounds.Height - DragRect.Height;

        double newX = Math.Clamp(_rectStartX + dx, 0, maxX);
        double newY = Math.Clamp(_rectStartY + dy, 0, maxY);

        Canvas.SetLeft(DragRect, newX);
        Canvas.SetTop(DragRect, newY);
    }


    private void DoResizing(double dx, double dy)
    {
        double newWidth = _rectStartWidth + dx;
        double newHeight = _rectStartHeight + dy;

        // 1) Enforce aspect ratio: newHeight is derived from newWidth
        newHeight = newWidth / _aspectRatio;

        // 2) Minimum size
        if (newWidth < 10) newWidth = 10;
        if (newHeight < 10) newHeight = 10;

        // 3) Clamp to canvas edges
        double maxWidth = OverlayCanvas.Bounds.Width - _rectStartX;
        double maxHeight = OverlayCanvas.Bounds.Height - _rectStartY;

        if (newWidth > maxWidth) newWidth = maxWidth;
        if (newHeight > maxHeight) newHeight = maxHeight;

        // 4) If we hit the bottom edge (vertical clamp), re-calc width so aspect ratio holds
        if (newHeight == maxHeight)
        {
            newWidth = newHeight * _aspectRatio;
            if (newWidth > maxWidth)
                newWidth = maxWidth;
        }

        // 5) If we hit the right edge (horizontal clamp), re-calc height so aspect ratio holds
        if (newWidth == maxWidth)
        {
            newHeight = newWidth / _aspectRatio;
            if (newHeight > maxHeight)
                newHeight = maxHeight;
        }

        DragRect.Width = newWidth;
        DragRect.Height = newHeight;
    }


    private async void OnImageTapped(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.DataContext is Wallpaper wallpaper)
        {
            // if user clicked a diff wallpaper than before, reset rect
            if (wallpaper != lastTapImage)
            {
                ResetRectangle();
                SetBackgroundButton.IsEnabled = false;
            }

            var viewModel = DataContext as MainWindowViewModel;
            if (viewModel != null)
            {
                if (CheckDoubleTapped(wallpaper))
                {
                    lastTapTime = DateTime.Now;
                    lastTapImage = wallpaper;
                    await viewModel.ImageDoubleTapped(wallpaper);
                }
                else
                {
                    lastTapTime = DateTime.Now;
                    lastTapImage = wallpaper;
                    await viewModel.ImageTapped(wallpaper);
                }
            }
        }
    }

    private bool CheckDoubleTapped(Wallpaper tappedWallpaper)
    {
        var currentTime = DateTime.Now;
        var elapsed = (currentTime - lastTapTime).TotalMilliseconds;

        if (elapsed < DoubleTapThreshold && lastTapImage == tappedWallpaper)
        {
            return true;
        }
        else
        {
            lastTapTime = currentTime;
            return false;
        }
    }

    private async void OnPreviewMonitorTapped(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.DataContext is MonitorInfo monitor)
        {
            var viewModel = DataContext as MainWindowViewModel;
            if (viewModel != null)
            {
                Debug.WriteLine("monitor tapped = " + monitor.MonitorIdPath);
                await viewModel.MonitorTapped(monitor);

                // size rect specific monitors real aspect ratio
                ShowDraggableRectangle(monitor);
                SetBackgroundButton.IsEnabled = true;
            }
        }
    }

    private void OnSelectAllClicked(object? sender, RoutedEventArgs e)
    {
        SetBackgroundButton.IsEnabled = true;
        ResetRectangle();
        var viewModel = DataContext as MainWindowViewModel;
        viewModel.StyleDropdownEnabled = true;
        viewModel.AllMonitorsSelected();
    }


    private void EnlargePreviewImg(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.CurrentWallpaperPreview != null)
        {
            EnlargedPreviewImage.IsVisible = true;
            MainGrid.Opacity = 0.01;
        }
    }

    private void OnEnlargedImageClick(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            EnlargedPreviewImage.IsVisible = false;
            MainGrid.Opacity = 1;
        }
    }


    private void OpenImageClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.CurrentWallpaperPreview != null)
        {
            FileExporerHelper fileExporerHelper = new FileExporerHelper();
            if (viewModel.LastSelectedWallpaper != null)
            {
                fileExporerHelper.OpenFileInExplorer(viewModel.LastSelectedWallpaper.FilePath);
            }
        }
    }


    private void OnSetWallpaperClicked(object? sender, RoutedEventArgs e)
    {
        SetBackgroundButton.IsEnabled = false; // ensure no spamming
        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel.StyleDropdownEnabled == true)
        {
            viewModel.SetWallpaper();
        }
        else
        {
            SetCroppedWallpaper();
        }
        SetBackgroundButton.IsEnabled = true;
    }

    public void SetCroppedWallpaper()
    {
        if (DragRect == null || OverlayCanvas == null || PreviewImage == null) return;

        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel?.LastSelectedWallpaper == null || viewModel.LastSelMonitor == null) return;

        var monitor = viewModel.LastSelMonitor;
        var wallpaper = viewModel.LastSelectedWallpaper;

        using var originalImage = SkiaSharp.SKBitmap.Decode(wallpaper.FilePath);
        int cropX = (int)(Canvas.GetLeft(DragRect) * (originalImage.Width / PreviewImage.Bounds.Width));
        int cropY = (int)(Canvas.GetTop(DragRect) * (originalImage.Height / PreviewImage.Bounds.Height));
        int cropWidth = (int)(DragRect.Width * (originalImage.Width / PreviewImage.Bounds.Width));
        int cropHeight = (int)(DragRect.Height * (originalImage.Height / PreviewImage.Bounds.Height));

        viewModel.SetWallpaperWithCrop(wallpaper.FilePath, monitor.MonitorIdPath, cropX, cropY, cropWidth, cropHeight);
    }
}
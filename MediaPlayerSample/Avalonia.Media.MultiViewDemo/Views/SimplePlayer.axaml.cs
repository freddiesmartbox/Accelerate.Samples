using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Media.MultiViewDemo.ViewModels;
using Avalonia.Platform.Storage;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;
using Vortice.MediaFoundation;

namespace Avalonia.Media.MultiViewDemo.Views;

public partial class SimplePlayer : UserControl
{
    private MediaPlayerPresenter[] _presenters;
    private int _currentPresenterIndex;
    private MediaPlayerPresenter _currentPresenter;
    private Size _currentSize;

    private SimpleViewModel? Vm { get; set; }

    public ObservableCollection<Bitmap> Screenshots { get; } = new();

    public SimplePlayer()
    {
        InitializeComponent();

        _presenters = [_presenter1, _presenter2, _presenter3];
        _currentPresenterIndex = 0;
        _currentPresenter = _presenter1;

        this.DataContextChanged += SimplePlayer_DataContextChanged;
        this.Loaded += SimplePlayer_Loaded;
        this.Unloaded += SimplePlayer_Unloaded;
        this.InterpolationModeCbx.SelectionChanged += InterpolationModeCbx_SelectionChanged;
    }

    private void InterpolationModeCbx_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        RenderOptions.SetBitmapInterpolationMode(this, Vm!.InterpolationMode);
    }

    private void SimplePlayer_DataContextChanged(object? sender, EventArgs e)
    {
        if (Vm is { } oldVm)
            Vm?.Player?.UnInitialize();

        Vm = DataContext as SimpleViewModel;

        if (this.IsAttachedToVisualTree())
            TryInitPlayer();
    }

    private void SimplePlayer_Loaded(object? sender, RoutedEventArgs e)
    {
        TryInitPlayer();
    }

    private void SimplePlayer_Unloaded(object? sender, RoutedEventArgs e)
    {
        Vm?.Player?.UnInitialize();
    }

    private void TryInitPlayer()
    {
        if (Vm?.Player is { } player)
        {
            Vm.InitPlayer();
            player.UpdateTargetVisual(_currentPresenter);
            player.NaturalSizeChanged += Player_NaturalSizeChanged;
        }
    }

    private void Player_NaturalSizeChanged(object? sender, NaturalSizeChangedEventArgs e)
    {
        UpdatePlayerSize(e.NewSize ?? default);
    }

    private void UpdatePlayerSize(Size size)
    {
        UpdatePlayerSize(size, _currentPresenter);
    }

    private void UpdatePlayerSize(Size size, MediaPlayerPresenter presenter)
    {
        _currentSize = size;

        var elemVisual = ElementComposition.GetElementChildVisual(presenter);
        var compositor = elemVisual?.Compositor;

        if (compositor is null || elemVisual is null)
        {
            return;
        }

        elemVisual.Size = new Vector(size.Width, size.Height);
        presenter.SetNaturalSize(size);
        presenter.InvalidateMeasure();
        presenter.InvalidateArrange();
    }

    private void Slider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        var candidate = TimeSpan.FromTicks((long)_progressSlider.Value);
        if (Vm?.Player is { } player && Math.Abs((candidate - player.Position).TotalSeconds) > 0.5)
            player.Position = candidate;
    }

    private async void LoadButton_Click(object? sender, RoutedEventArgs e)
    {
        var storageProver = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProver is null || Vm is null)
            return;

        var files = await storageProver.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false
        });

        if (files.Count != 1)
            return;
        if (files[0].Path is not { } path)
            return;

        Vm.Source = new StorageFileSource(files[0]);
    }

    private void ToggleButton_Click(object? sender, RoutedEventArgs e)
    {
        _currentPresenterIndex = (++_currentPresenterIndex) % _presenters.Length;
        _currentPresenter = _presenters[_currentPresenterIndex];

        if (Vm?.Player is { } player)
        {
            player.UpdateTargetVisual(_currentPresenter);
            UpdatePlayerSize(_currentSize);
        }
    }

    private void CyclePresenter_Click(object? sender, RoutedEventArgs e)
    {
        Vm?.Player?.UpdateTargetVisual(null);

        var owner = (Viewbox)_currentPresenter?.Parent!;
        owner.Child = _currentPresenter = new MediaPlayerPresenter();
        
        Vm?.Player?.UpdateTargetVisual(_currentPresenter);
    }

    public async void Snap_Click(object? sender, RoutedEventArgs e)
    {
        if (ElementComposition.GetElementChildVisual(_currentPresenter) is { } compositionVisual)
        {
            var bmp = await compositionVisual.Compositor.CreateCompositionVisualSnapshot(compositionVisual, 1);
            bmp.Save(@"C:\Dev\Junk\bmp.bmp");
            Screenshots.Add(bmp);
        }
    }

    public void PopOut_Click(object? sender, RoutedEventArgs e)
    {
        var presenter = _currentPresenter = new MediaPlayerPresenter();
        var vb = new Viewbox() { Child = presenter, VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch };
        var dp = new DockPanel() { VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch };
        dp.Children.Add(new TextBlock() { Text = "PopOut", [DockPanel.DockProperty] = Dock.Top, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(2), FontWeight = FontWeight.SemiBold });
        dp.Children.Add(vb);
        var w = new Window() { Content = dp, Width = 300, Height = 300 };

        presenter.AttachedToVisualTree += (s, e) =>
        {
            Vm?.Player.UpdateTargetVisual(presenter);
            UpdatePlayerSize(_currentSize, presenter);
        };

        w.Show();
    }
}

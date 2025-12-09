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

namespace Avalonia.Media.MultiViewDemo.Views;

public partial class SimplePlayer : UserControl
{
    private Layoutable[] _presenters;
    private int _currentPresenter;
    private Size _currentSize;

    private SimpleViewModel? Vm { get; set; }

    public ObservableCollection<Bitmap> Screenshots { get; } = new();

    public SimplePlayer()
    {
        InitializeComponent();

        _presenters = [_presenter1, _presenter2, _presenter3];
        _currentPresenter = 0;

        this.DataContextChanged += SimplePlayer_DataContextChanged;
        this.Loaded += SimplePlayer_Loaded;
        this.Unloaded += SimplePlayer_Unloaded;
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
            player.NaturalSizeChanged += Player_NaturalSizeChanged;
        }
    }

    private void Player_NaturalSizeChanged(object? sender, NaturalSizeChangedEventArgs e)
    {
        UpdatePlayerSize(e.NewSize ?? default);
    }

    private void UpdatePlayerSize(Size size)
    {
        Vm?.Player?.UpdateTargetVisual(_presenters[_currentPresenter]);

        _currentSize = size;
        var presenter = _presenters[_currentPresenter];

        foreach(var p in _presenters)
        {
            var e = ElementComposition.GetElementChildVisual(p);
            var c = e?.Compositor;
        }

        var elemVisual = ElementComposition.GetElementChildVisual(presenter);
        var compositor = elemVisual?.Compositor;

        if (compositor is null || elemVisual is null)
        {
            return;
        }

        elemVisual.Size = new Vector(size.Width, size.Height);
        (presenter as MediaPlayerPresenter)?.SetNaturalSize(size);
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
        _currentPresenter = (++_currentPresenter) % _presenters.Length;

        if (Vm?.Player is { } player)
        {
            player.UpdateTargetVisual(_presenters[_currentPresenter]);
            UpdatePlayerSize(_currentSize);
        }
    }

    private void CyclePresenter_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm?.Player is { } player)
            player.UpdateTargetVisual(null);
        
        var owner = (Viewbox)_presenters[_currentPresenter].Parent!;
        _presenters[_currentPresenter] = owner.Child = new MediaPlayerPresenter();
    }

    public async void SnapAsync()
    {
        if (ElementComposition.GetElementChildVisual(_presenters[_currentPresenter]) is { } compositionVisual)
        {
            var bmp = await compositionVisual.Compositor.CreateCompositionVisualSnapshot(compositionVisual, 1);
            bmp.Save(@"C:\Dev\Junk\bmp.bmp");
            Screenshots.Add(bmp);
        }
    }
}

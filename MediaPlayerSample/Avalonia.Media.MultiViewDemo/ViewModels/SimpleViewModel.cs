using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Media.MultiViewDemo.ViewModels
{
    public partial class SimpleViewModel : ViewModelBase, IDisposable
    {
        [ObservableProperty] private MediaSource? _source;
        [ObservableProperty] private TimeSpan? _duration;
        [ObservableProperty] private TimeSpan? _position;
        [ObservableProperty] private long _ticks = 1;
        [ObservableProperty] private long _currentPosition;
        [ObservableProperty] private bool _autoplay = true;
        [ObservableProperty] private bool _autopause = false;
        [ObservableProperty] private bool _isMuted = false;
        [ObservableProperty] private double _volume = 1.0;
        private bool _initialized;
        private bool _shouldAutopause = false;

        public MediaPlayer Player { get; } = new MediaPlayer();

        public SimpleViewModel()
        {
        }

        protected async override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.PropertyName == nameof(Source))
            {
                await Player.StopAsync();

                if (Source is not null)
                {
                    await Player.SetSourceAsync(Source);
                    await Player.PrepareAsync();
                }

                _shouldAutopause = Autoplay && Autopause;
                Player.IsMuted = IsMuted || _shouldAutopause;
                Player.Volume = Volume;

                await Player.PrepareAsync();
            }

            if (e.PropertyName == nameof(Autoplay))
            {
                Player.LoadedBehavior = Autoplay ? MediaPlayerLoadedBehavior.AutoPlay : MediaPlayerLoadedBehavior.Manual;
            }
        }

        partial void OnIsMutedChanged(bool value)
        {
            Player.IsMuted = value;
            Player.Volume = Volume;
        }

        partial void OnVolumeChanged(double value)
        {
            Player.Volume = Volume;
        }

        public async void InitPlayer()
        {
            if (_initialized)
                return;
            Player.LoadedBehavior = MediaPlayerLoadedBehavior.AutoPlay;
            await Player.InitializeAsync();

            Player.PropertyChanged += Player_PropertyChanged;
            Player.NaturalSizeChanged += Player_NaturalSizeChanged;

            _initialized = true;
        }

        private void Player_NaturalSizeChanged(object? sender, NaturalSizeChangedEventArgs e)
        {
            // NaturalSizeChanged implies it may not fire if the natural size doesn't change, which may be a problem;
            // in testing, however, this hasn't been observed...
            AutopauseIfRequested();
        }

        private async void AutopauseIfRequested()
        {
            if (_shouldAutopause)
            {
                _shouldAutopause = false;
                await Player.PauseAsync();

                // reset volume so it is as expected when playback starts again
                Player.IsMuted = IsMuted;
                Player.Volume = Volume;
            }
        }

        private void Player_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Player.Duration))
            {
                Duration = Player.Duration;
                Ticks = Duration?.Ticks ?? 0;
            }
            else if(e.PropertyName == nameof(Player.Position))
            {
                var position = Player.Position;
                Ticks = long.Max(Ticks, position.Ticks);
                CurrentPosition = position.Ticks;
                Position = position;
            }
        }

        public async void PlayAsync()
        {
            await Player.PlayAsync();
        }

        public async void PauseAsync()
        {
            await Player.PauseAsync();
        }

        public async void StopAsync()
        {
            await Player.StopAsync();
        }

        public void Dispose()
        {
            _initialized = false;
        }
    }
}

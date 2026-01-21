using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using TerraFX.Interop.Vulkan;

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
        [ObservableProperty] private bool _isPlaying = false;

        private bool _initialized;
        private bool _shouldAutopause = false;

        private string _myVideosDirectory = "Videos";
        private IReadOnlyList<string>? _myVideos;
        private int _myVideosIndex = -1;

        public MediaPlayer Player { get; } = new MediaPlayer();
        public TimeSpan AutopausePosition { get; set; } = TimeSpan.FromMilliseconds(100);

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
                //await AutopauseIfRequested();
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
            Player.MediaStarted += Player_MediaStarted;

            _initialized = true;
        }

        private async void Player_MediaStarted(object? sender, EventArgs e)
        {
            IsPlaying = !_shouldAutopause;
            await AutopauseIfRequested();
        }

        private async Task AutopauseIfRequested()
        {
            if (_shouldAutopause)
            {
                await PauseAsync();
                Player.Position = AutopausePosition;

                // reset volume so it is as expected when playback starts again
                Player.IsMuted = IsMuted;
                Player.Volume = Volume;
            }
        }

        private async void Player_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Player.Duration))
            {
                if (_shouldAutopause)
                {
                    // apply Position here, because it doesn't for our purposes when on the first invocation of MediaStarted
                    Player.Position = AutopausePosition;
                }

                Duration = Player.Duration;
                Ticks = Duration?.Ticks ?? 0;
            }
            else if (e.PropertyName == nameof(Player.Position))
            {
                var position = Player.Position;

                // force duration to position if we exceed it
                if (Duration < position)
                    Duration = position;
                if (Ticks < position.Ticks)
                    Ticks = position.Ticks;

                CurrentPosition = position.Ticks;
                Position = position;
            }
        }

        public async Task PlayAsync()
        {
            if (_shouldAutopause)
            {
                _shouldAutopause = false; // reset latch
                Player.Position = TimeSpan.FromMilliseconds(0); // return to start
            }

            await Player.PlayAsync();
            IsPlaying = true;
        }

        public async Task PauseAsync()
        {
            await Player.PauseAsync();
            IsPlaying = false;
        }

        public async Task StopAsync()
        {
            await Player.StopAsync();
            IsPlaying = false;
        }

        public async Task PlayPauseAsync()
        {
            if (IsPlaying)
                await PauseAsync();
            else
                await PlayAsync();
        }

        private static FrozenSet<string> _videoExtensions = [".avi", ".mp4", "mpeg", ".ogv", ".flv"];

        [MemberNotNull(nameof(_myVideos))]
        private void EnsureMyVideos()
        {
            if (_myVideos is null)
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var dir = Path.Combine(home, _myVideosDirectory);

                _myVideos = Directory.EnumerateFiles(dir, "*", System.IO.SearchOption.AllDirectories)
                    .Where(f => _videoExtensions.Contains(Path.GetExtension(f)))
                    .ToImmutableArray();
            }
        }

        public async void PlayPreviousAsync()
        {
            EnsureMyVideos();

            if (_myVideos.Count == 0)
            {
                Console.Beep();
                return;
            }

            _myVideosIndex = _myVideosIndex < 1 ? _myVideos.Count - 1 : _myVideosIndex - 1;
            Source = new UriSource(_myVideos[_myVideosIndex]);
        }

        public async void PlayNextAsync()
        {
            EnsureMyVideos();

            if (_myVideos.Count == 0)
            {
                Console.Beep();
                return;
            }

            _myVideosIndex = (_myVideosIndex + 1) % _myVideos.Count;
            Source = new UriSource(_myVideos[_myVideosIndex]);
        }

        public void Dispose()
        {
            _initialized = false;
        }
    }
}

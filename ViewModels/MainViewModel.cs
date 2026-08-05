using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HiResAudioPlayerTagMaster.Models;
using HiResAudioPlayerTagMaster.Services;

namespace HiResAudioPlayerTagMaster.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AudioPlaybackService _audioService;
        private readonly TagEditorService _tagEditorService;
        private readonly LibraryService _libraryService;

        public VisualizerRenderer Visualizer { get; } = new();

        [ObservableProperty]
        private ObservableCollection<TrackModel> _tracks = new();

        [ObservableProperty]
        private ObservableCollection<TrackModel> _filteredTracks = new();

        [ObservableProperty]
        private ObservableCollection<PlaylistModel> _playlists = new();

        [ObservableProperty]
        private ObservableCollection<AudioDeviceModel> _outputDevices = new();

        [ObservableProperty]
        private AudioDeviceModel? _selectedOutputDevice;

        [ObservableProperty]
        private TrackModel? _currentTrack;

        [ObservableProperty]
        private TrackModel? _selectedTrack;

        [ObservableProperty]
        private TagMetadataModel? _editingTag;

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private bool _isTagDrawerOpen;

        [ObservableProperty]
        private bool _isEqualizerOpen;

        [ObservableProperty]
        private float _volume = 0.85f;

        [ObservableProperty]
        private double _currentPositionSeconds;

        [ObservableProperty]
        private double _totalDurationSeconds = 180;

        [ObservableProperty]
        private string _positionFormatted = "00:00";

        [ObservableProperty]
        private string _durationFormatted = "03:00";

        [ObservableProperty]
        private string _searchFilter = string.Empty;

        [ObservableProperty]
        private VisualizerMode _selectedVisualizerMode = VisualizerMode.SpectrumBars;

        [ObservableProperty]
        private bool _isReplayGainEnabled = true;

        [ObservableProperty]
        private List<EqualizerPreset> _equalizerPresets = EqualizerPreset.GetDefaultPresets();

        [ObservableProperty]
        private EqualizerPreset? _selectedEqualizerPreset;

        // 10 EQ Band Gains (-12dB to +12dB)
        [ObservableProperty] private float _eqBand0;
        [ObservableProperty] private float _eqBand1;
        [ObservableProperty] private float _eqBand2;
        [ObservableProperty] private float _eqBand3;
        [ObservableProperty] private float _eqBand4;
        [ObservableProperty] private float _eqBand5;
        [ObservableProperty] private float _eqBand6;
        [ObservableProperty] private float _eqBand7;
        [ObservableProperty] private float _eqBand8;
        [ObservableProperty] private float _eqBand9;

        public MainViewModel()
        {
            _audioService = new AudioPlaybackService();
            _tagEditorService = new TagEditorService();
            _libraryService = new LibraryService();

            _audioService.FftDataAvailable += (s, bins) => Visualizer.UpdateFftBins(bins);
            _audioService.WaveformDataAvailable += (s, wave) => Visualizer.UpdateWaveform(wave);
            _audioService.TrackEnded += (s, e) => PlayNextTrack();

            SelectedEqualizerPreset = EqualizerPresets.FirstOrDefault();

            LoadOutputDevices();
            LoadSyntheticDemoTracks();
        }

        private void LoadOutputDevices()
        {
            var devs = _audioService.GetAvailableDevices();
            OutputDevices = new ObservableCollection<AudioDeviceModel>(devs);
            SelectedOutputDevice = OutputDevices.FirstOrDefault();
        }

        partial void OnSelectedOutputDeviceChanged(AudioDeviceModel? value)
        {
            if (value != null)
            {
                _audioService.SetOutputDevice(value);
            }
        }

        private void LoadSyntheticDemoTracks()
        {
            var demoTracks = _libraryService.GetSyntheticDemoTracks();
            Tracks = new ObservableCollection<TrackModel>(demoTracks);
            ApplySearchFilter();
            CurrentTrack = Tracks.FirstOrDefault();
            if (CurrentTrack != null)
            {
                EditingTag = _tagEditorService.ReadTags(CurrentTrack.FilePath);
            }
        }

        partial void OnSearchFilterChanged(string value)
        {
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchFilter))
            {
                FilteredTracks = new ObservableCollection<TrackModel>(Tracks);
            }
            else
            {
                var q = SearchFilter.ToLowerInvariant();
                var filtered = Tracks.Where(t =>
                    t.Title.ToLowerInvariant().Contains(q) ||
                    t.Artist.ToLowerInvariant().Contains(q) ||
                    t.Album.ToLowerInvariant().Contains(q) ||
                    t.Genre.ToLowerInvariant().Contains(q) ||
                    t.CodecFormat.ToLowerInvariant().Contains(q)).ToList();
                FilteredTracks = new ObservableCollection<TrackModel>(filtered);
            }
        }

        partial void OnSelectedTrackChanged(TrackModel? value)
        {
            if (value != null)
            {
                EditingTag = _tagEditorService.ReadTags(value.FilePath);
            }
        }

        [RelayCommand]
        public void PlayPause()
        {
            if (_audioService.IsPlaying)
            {
                _audioService.Pause();
                IsPlaying = false;
            }
            else
            {
                if (CurrentTrack != null && !CurrentTrack.FilePath.StartsWith("DEMO://"))
                {
                    _audioService.PlayFile(CurrentTrack.FilePath, TimeSpan.FromSeconds(CurrentPositionSeconds));
                }
                else
                {
                    _audioService.StartDemoPlayback();
                }
                IsPlaying = true;
            }
        }

        [RelayCommand]
        public void PlayTrack(TrackModel track)
        {
            if (track == null) return;
            CurrentTrack = track;
            CurrentTrack.IsPlaying = true;

            if (track.FilePath.StartsWith("DEMO://"))
            {
                _audioService.StartDemoPlayback();
            }
            else
            {
                _audioService.PlayFile(track.FilePath);
            }

            IsPlaying = true;
            EditingTag = _tagEditorService.ReadTags(track.FilePath);

            // Preload next track in queue for gapless playback
            int idx = Tracks.IndexOf(track);
            if (idx >= 0 && idx + 1 < Tracks.Count)
            {
                var nextTrack = Tracks[idx + 1];
                if (!nextTrack.FilePath.StartsWith("DEMO://"))
                {
                    _audioService.PreloadNextFile(nextTrack.FilePath);
                }
            }
        }

        [RelayCommand]
        public void PlayNextTrack()
        {
            if (Tracks.Count == 0) return;
            int idx = CurrentTrack != null ? Tracks.IndexOf(CurrentTrack) : -1;
            int nextIdx = (idx + 1) % Tracks.Count;
            PlayTrack(Tracks[nextIdx]);
        }

        [RelayCommand]
        public void PlayPreviousTrack()
        {
            if (Tracks.Count == 0) return;
            int idx = CurrentTrack != null ? Tracks.IndexOf(CurrentTrack) : 0;
            int prevIdx = (idx - 1 + Tracks.Count) % Tracks.Count;
            PlayTrack(Tracks[prevIdx]);
        }

        partial void OnVolumeChanged(float value)
        {
            _audioService.SetVolume(value);
        }

        partial void OnCurrentPositionSecondsChanged(double value)
        {
            PositionFormatted = TimeSpan.FromSeconds(value).ToString(@"mm\:ss");
        }

        [RelayCommand]
        public void SeekToPosition(double seconds)
        {
            CurrentPositionSeconds = seconds;
            _audioService.Seek(TimeSpan.FromSeconds(seconds));
        }

        partial void OnSelectedVisualizerModeChanged(VisualizerMode value)
        {
            Visualizer.Mode = value;
        }

        [RelayCommand]
        public async Task AddFolderAsync()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Music Library Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                var scanned = await _libraryService.ScanFolderAsync(dialog.FolderName);
                foreach (var t in scanned)
                {
                    Tracks.Add(t);
                }
                ApplySearchFilter();
            }
        }

        [RelayCommand]
        public async Task AddFilesAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Hi-Res Audio Files",
                Filter = "Audio Files (*.flac;*.wav;*.alac;*.aiff;*.mp3;*.m4a;*.ogg)|*.flac;*.wav;*.alac;*.aiff;*.mp3;*.m4a;*.ogg|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    var track = _libraryService.CreateTrackModelFromFile(file);
                    Tracks.Add(track);
                }
                ApplySearchFilter();
            }
        }

        [RelayCommand]
        public void OpenTagDrawer(TrackModel? track)
        {
            var target = track ?? SelectedTrack ?? CurrentTrack;
            if (target != null)
            {
                EditingTag = _tagEditorService.ReadTags(target.FilePath);
                IsTagDrawerOpen = true;
            }
        }

        [RelayCommand]
        public void SaveTagMetadata()
        {
            if (EditingTag != null && !string.IsNullOrEmpty(EditingTag.FilePath))
            {
                bool ok = _tagEditorService.WriteTags(EditingTag, EditingTag.RawCoverArtBytes);
                if (ok)
                {
                    MessageBox.Show("Metadata tags updated successfully!", "Tag Master Pro", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Refresh track info in list
                    var existing = Tracks.FirstOrDefault(t => t.FilePath == EditingTag.FilePath);
                    if (existing != null)
                    {
                        var updated = _libraryService.CreateTrackModelFromFile(existing.FilePath);
                        int idx = Tracks.IndexOf(existing);
                        Tracks[idx] = updated;
                        ApplySearchFilter();
                    }
                }
                else
                {
                    MessageBox.Show("Failed to save metadata tags. Ensure file is not read-only.", "Tag Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        public void SelectCoverArtImage()
        {
            if (EditingTag == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Cover Art Image",
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(dialog.FileName);
                    EditingTag.RawCoverArtBytes = bytes;
                    EditingTag.CoverArtPreview = TagEditorService.LoadBitmapImageFromBytes(bytes);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not load image: {ex.Message}", "Image Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        public void ToggleEqualizer()
        {
            IsEqualizerOpen = !IsEqualizerOpen;
        }

        partial void OnSelectedEqualizerPresetChanged(EqualizerPreset? value)
        {
            if (value != null && value.Bands.Length >= 10)
            {
                EqBand0 = value.Bands[0];
                EqBand1 = value.Bands[1];
                EqBand2 = value.Bands[2];
                EqBand3 = value.Bands[3];
                EqBand4 = value.Bands[4];
                EqBand5 = value.Bands[5];
                EqBand6 = value.Bands[6];
                EqBand7 = value.Bands[7];
                EqBand8 = value.Bands[8];
                EqBand9 = value.Bands[9];
                ApplyEqualizerSettings();
            }
        }

        private void ApplyEqualizerSettings()
        {
            float[] gains = new float[] { EqBand0, EqBand1, EqBand2, EqBand3, EqBand4, EqBand5, EqBand6, EqBand7, EqBand8, EqBand9 };
            _audioService.SetEqualizerPreset(gains);
        }

        partial void OnEqBand0Changed(float value) => ApplyEqualizerSettings();
        partial void OnEqBand1Changed(float value) => ApplyEqualizerSettings();
        partial void OnEqBand2Changed(float value) => ApplyEqualizerSettings();
        partial void OnEqBand3Changed(float value) => ApplyEqualizerSettings();
        partial void OnEqBand4Changed(float value) => ApplyEqualizerSettings();
        partial void OnEqBand5Changed(float value) => ApplyEqualizerSettings();
        partial void OnEqBand6Changed(float value) => ApplyEqualizerSettings();
        partial void OnEqBand7Changed(float value) => ApplyEqualizerSettings();
        partial void OnEqBand8Changed(float value) => ApplyEqualizerSettings();
        partial void OnEqBand9Changed(float value) => ApplyEqualizerSettings();

        [RelayCommand]
        public async Task ExportM3U8Async()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Playlist M3U8",
                Filter = "M3U8 Playlist (*.m3u8)|*.m3u8",
                FileName = "HiRes_Playlist.m3u8"
            };

            if (dialog.ShowDialog() == true)
            {
                await _libraryService.SavePlaylistM3U8Async(dialog.FileName, Tracks);
                MessageBox.Show("M3U8 Playlist exported successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        public async Task ImportM3U8Async()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import M3U8 Playlist",
                Filter = "M3U8 Playlist (*.m3u8;*.m3u)|*.m3u8;*.m3u"
            };

            if (dialog.ShowDialog() == true)
            {
                var loaded = await _libraryService.LoadPlaylistM3U8Async(dialog.FileName);
                foreach (var t in loaded)
                {
                    Tracks.Add(t);
                }
                ApplySearchFilter();
            }
        }
    }
}

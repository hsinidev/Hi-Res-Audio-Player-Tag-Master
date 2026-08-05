using System;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HiResAudioPlayerTagMaster.Models
{
    public partial class TagMetadataModel : ObservableObject
    {
        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _artist = string.Empty;

        [ObservableProperty]
        private string _album = string.Empty;

        [ObservableProperty]
        private string _albumArtist = string.Empty;

        [ObservableProperty]
        private uint _year;

        [ObservableProperty]
        private uint _trackNumber;

        [ObservableProperty]
        private uint _discNumber;

        [ObservableProperty]
        private string _genre = string.Empty;

        [ObservableProperty]
        private string _composer = string.Empty;

        [ObservableProperty]
        private string _lyrics = string.Empty;

        [ObservableProperty]
        private uint _bpm;

        [ObservableProperty]
        private double _replayGainTrackGain;

        [ObservableProperty]
        private byte[]? _rawCoverArtBytes;

        [ObservableProperty]
        private BitmapImage? _coverArtPreview;

        [ObservableProperty]
        private string _audioInfoSummary = string.Empty;

        [ObservableProperty]
        private bool _isModified;
    }
}

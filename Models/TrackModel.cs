using System;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HiResAudioPlayerTagMaster.Models
{
    public partial class TrackModel : ObservableObject
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
        private uint _year;

        [ObservableProperty]
        private uint _trackNumber;

        [ObservableProperty]
        private string _genre = string.Empty;

        [ObservableProperty]
        private TimeSpan _duration;

        [ObservableProperty]
        private int _bitrateKbps;

        [ObservableProperty]
        private int _sampleRateHz;

        [ObservableProperty]
        private int _bitsPerSample;

        [ObservableProperty]
        private int _channels;

        [ObservableProperty]
        private string _codecFormat = string.Empty; // e.g. "FLAC 24-bit / 192kHz", "WAV 16-bit", "MP3 320kbps"

        [ObservableProperty]
        private double _replayGainDb;

        [ObservableProperty]
        private BitmapImage? _coverArt;

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private bool _isHiRes;

        public string FileName => Path.GetFileName(FilePath);
        public string DurationFormatted => Duration.ToString(@"mm\:ss");

        public string HiResBadgeText
        {
            get
            {
                if (IsHiRes) return "HI-RES LOSSLESS";
                if (CodecFormat.Contains("FLAC", StringComparison.OrdinalIgnoreCase) ||
                    CodecFormat.Contains("WAV", StringComparison.OrdinalIgnoreCase) ||
                    CodecFormat.Contains("ALAC", StringComparison.OrdinalIgnoreCase) ||
                    CodecFormat.Contains("AIFF", StringComparison.OrdinalIgnoreCase))
                    return "LOSSLESS";
                return "COMPRESSED";
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HiResAudioPlayerTagMaster.Models;

namespace HiResAudioPlayerTagMaster.Services
{
    public class LibraryService
    {
        private readonly TagEditorService _tagEditor = new();
        private readonly string[] _supportedExtensions = new[]
        {
            ".flac", ".wav", ".alac", ".aiff", ".mp3", ".m4a", ".aac", ".ogg", ".wma"
        };

        public async Task<List<TrackModel>> ScanFolderAsync(string folderPath, IProgress<int>? progress = null)
        {
            var tracks = new List<TrackModel>();

            if (!Directory.Exists(folderPath)) return tracks;

            var files = await Task.Run(() =>
            {
                return Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();
            });

            int total = files.Count;
            int current = 0;

            foreach (var file in files)
            {
                var track = await Task.Run(() => CreateTrackModelFromFile(file));
                if (track != null)
                {
                    tracks.Add(track);
                }

                current++;
                progress?.Report((int)((double)current / total * 100));
            }

            return tracks;
        }

        public TrackModel CreateTrackModelFromFile(string filePath)
        {
            var tagData = _tagEditor.ReadTags(filePath);
            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            bool isHiRes = false;
            int bitrate = 0;
            int sampleRate = 44100;
            int bitsPerSample = 16;
            int channels = 2;
            TimeSpan duration = TimeSpan.FromMinutes(3);

            try
            {
                using var tagFile = TagLib.File.Create(filePath);
                var p = tagFile.Properties;
                if (p != null)
                {
                    bitrate = p.AudioBitrate;
                    sampleRate = p.AudioSampleRate;
                    bitsPerSample = p.BitsPerSample > 0 ? p.BitsPerSample : (ext == ".flac" || ext == ".wav" ? 24 : 16);
                    channels = p.AudioChannels;
                    duration = p.Duration;

                    if (sampleRate >= 88200 || bitsPerSample >= 24)
                    {
                        isHiRes = true;
                    }
                }
            }
            catch
            {
            }

            string codecFormat = $"{ext.TrimStart('.').ToUpper()} {bitsPerSample}-bit / {sampleRate / 1000.0:F1}kHz";
            if (bitrate > 0) codecFormat += $" ({bitrate} kbps)";

            return new TrackModel
            {
                FilePath = filePath,
                Title = string.IsNullOrWhiteSpace(tagData.Title) ? Path.GetFileNameWithoutExtension(filePath) : tagData.Title,
                Artist = string.IsNullOrWhiteSpace(tagData.Artist) ? "Unknown Artist" : tagData.Artist,
                Album = string.IsNullOrWhiteSpace(tagData.Album) ? "Unknown Album" : tagData.Album,
                Year = tagData.Year,
                TrackNumber = tagData.TrackNumber,
                Genre = tagData.Genre,
                Duration = duration,
                BitrateKbps = bitrate,
                SampleRateHz = sampleRate,
                BitsPerSample = bitsPerSample,
                Channels = channels,
                CodecFormat = codecFormat,
                ReplayGainDb = tagData.ReplayGainTrackGain,
                CoverArt = tagData.CoverArtPreview,
                IsHiRes = isHiRes
            };
        }

        public async Task SavePlaylistM3U8Async(string m3u8Path, IEnumerable<TrackModel> tracks)
        {
            using var writer = new StreamWriter(m3u8Path, false, System.Text.Encoding.UTF8);
            await writer.WriteLineAsync("#EXTM3U");

            foreach (var track in tracks)
            {
                await writer.WriteLineAsync($"#EXTINF:{(int)track.Duration.TotalSeconds},{track.Artist} - {track.Title}");
                await writer.WriteLineAsync(track.FilePath);
            }
        }

        public async Task<List<TrackModel>> LoadPlaylistM3U8Async(string m3u8Path)
        {
            var tracks = new List<TrackModel>();
            if (!File.Exists(m3u8Path)) return tracks;

            var lines = await File.ReadAllLinesAsync(m3u8Path);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;

                if (File.Exists(trimmed))
                {
                    var track = CreateTrackModelFromFile(trimmed);
                    if (track != null) tracks.Add(track);
                }
            }

            return tracks;
        }

        public List<TrackModel> GetSyntheticDemoTracks()
        {
            return new List<TrackModel>
            {
                new TrackModel
                {
                    FilePath = "DEMO://audiophile_amber_suite.flac",
                    Title = "Audiophile Amber Harmonic Sweep",
                    Artist = "Antigravity Sound Lab",
                    Album = "Hi-Res Reference Master Vol. 1",
                    Year = 2026,
                    TrackNumber = 1,
                    Genre = "Acoustic / Reference",
                    Duration = TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(15)),
                    BitrateKbps = 9216,
                    SampleRateHz = 192000,
                    BitsPerSample = 24,
                    Channels = 2,
                    CodecFormat = "FLAC 24-bit / 192.0kHz (9216 kbps)",
                    IsHiRes = true,
                    ReplayGainDb = -1.2
                },
                new TrackModel
                {
                    FilePath = "DEMO://obsidian_bass_test.wav",
                    Title = "Onyx Obsidian Low Frequency Resonance",
                    Artist = "Antigravity Sound Lab",
                    Album = "Hi-Res Reference Master Vol. 1",
                    Year = 2026,
                    TrackNumber = 2,
                    Genre = "Audiophile Test",
                    Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(45)),
                    BitrateKbps = 4608,
                    SampleRateHz = 96000,
                    BitsPerSample = 24,
                    Channels = 2,
                    CodecFormat = "WAV 24-bit / 96.0kHz (4608 kbps)",
                    IsHiRes = true,
                    ReplayGainDb = 0.0
                },
                new TrackModel
                {
                    FilePath = "DEMO://classical_starlight_concerto.flac",
                    Title = "Starlight Violin Concerto in D Minor",
                    Artist = "Fluent Philharmonic Orchestra",
                    Album = "Classical Gems Hi-Res",
                    Year = 2025,
                    TrackNumber = 3,
                    Genre = "Classical",
                    Duration = TimeSpan.FromMinutes(6).Add(TimeSpan.FromSeconds(20)),
                    BitrateKbps = 8448,
                    SampleRateHz = 176400,
                    BitsPerSample = 24,
                    Channels = 2,
                    CodecFormat = "FLAC 24-bit / 176.4kHz (8448 kbps)",
                    IsHiRes = true,
                    ReplayGainDb = -2.5
                }
            };
        }
    }
}

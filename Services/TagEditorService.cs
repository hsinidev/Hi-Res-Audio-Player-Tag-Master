using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using TagLib;
using HiResAudioPlayerTagMaster.Models;

namespace HiResAudioPlayerTagMaster.Services
{
    public class TagEditorService
    {
        public TagMetadataModel ReadTags(string filePath)
        {
            var model = new TagMetadataModel { FilePath = filePath };

            if (!System.IO.File.Exists(filePath)) return model;

            try
            {
                using var tagFile = TagLib.File.Create(filePath);

                var tag = tagFile.Tag;
                var properties = tagFile.Properties;

                model.Title = tag.Title ?? Path.GetFileNameWithoutExtension(filePath);
                model.Artist = tag.FirstPerformer ?? tag.FirstAlbumArtist ?? "Unknown Artist";
                model.Album = tag.Album ?? "Unknown Album";
                model.AlbumArtist = tag.FirstAlbumArtist ?? string.Empty;
                model.Year = tag.Year;
                model.TrackNumber = tag.Track;
                model.DiscNumber = tag.Disc;
                model.Genre = tag.FirstGenre ?? "Unknown Genre";
                model.Composer = tag.FirstComposer ?? string.Empty;
                model.Lyrics = tag.Lyrics ?? string.Empty;
                model.Bpm = tag.BeatsPerMinute;

                // ReplayGain tags
                if (!double.IsNaN(tag.ReplayGainTrackGain))
                {
                    model.ReplayGainTrackGain = tag.ReplayGainTrackGain;
                }

                // Audio Format Info Summary
                model.AudioInfoSummary = $"{properties.Description} | {properties.AudioSampleRate} Hz | {properties.BitsPerSample} bit | {properties.AudioBitrate} kbps | {properties.AudioChannels} Ch";

                // Cover Art
                if (tag.Pictures != null && tag.Pictures.Length > 0)
                {
                    var pic = tag.Pictures[0];
                    model.RawCoverArtBytes = pic.Data.Data;
                    model.CoverArtPreview = LoadBitmapImageFromBytes(pic.Data.Data);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TagLib error reading {filePath}: {ex.Message}");
                model.Title = Path.GetFileNameWithoutExtension(filePath);
            }

            return model;
        }

        public bool WriteTags(TagMetadataModel model, byte[]? newCoverArtBytes = null)
        {
            if (!System.IO.File.Exists(model.FilePath)) return false;

            try
            {
                using var tagFile = TagLib.File.Create(model.FilePath);
                var tag = tagFile.Tag;

                tag.Title = model.Title;
                tag.Performers = new[] { model.Artist };
                tag.Album = model.Album;
                tag.AlbumArtists = string.IsNullOrWhiteSpace(model.AlbumArtist) ? new[] { model.Artist } : new[] { model.AlbumArtist };
                tag.Year = model.Year;
                tag.Track = model.TrackNumber;
                tag.Disc = model.DiscNumber;
                tag.Genres = new[] { model.Genre };
                tag.Composers = string.IsNullOrWhiteSpace(model.Composer) ? Array.Empty<string>() : new[] { model.Composer };
                tag.Lyrics = model.Lyrics;
                tag.BeatsPerMinute = model.Bpm;

                if (Math.Abs(model.ReplayGainTrackGain) > 0.001)
                {
                    tag.ReplayGainTrackGain = model.ReplayGainTrackGain;
                }

                // Update Cover Art if provided
                if (newCoverArtBytes != null)
                {
                    var pic = new TagLib.Picture(new TagLib.ByteVector(newCoverArtBytes))
                    {
                        Type = PictureType.FrontCover,
                        MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg,
                        Description = "Cover Art"
                    };
                    tag.Pictures = new IPicture[] { pic };
                    model.RawCoverArtBytes = newCoverArtBytes;
                    model.CoverArtPreview = LoadBitmapImageFromBytes(newCoverArtBytes);
                }

                tagFile.Save();
                model.IsModified = false;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TagLib error writing {model.FilePath}: {ex.Message}");
                return false;
            }
        }

        public static BitmapImage? LoadBitmapImageFromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;

            try
            {
                using var ms = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}

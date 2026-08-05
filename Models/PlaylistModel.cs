using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HiResAudioPlayerTagMaster.Models
{
    public partial class PlaylistModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = "Default Playlist";

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private ObservableCollection<TrackModel> _tracks = new();

        public int TrackCount => Tracks.Count;
    }
}

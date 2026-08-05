namespace HiResAudioPlayerTagMaster.Models
{
    public enum AudioDriverType
    {
        WasapiShared,
        WasapiExclusive,
        WaveOut,
        DirectSound
    }

    public class AudioDeviceModel
    {
        public string DeviceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AudioDriverType DriverType { get; set; }
        public bool IsExclusiveSupported { get; set; }

        public override string ToString()
        {
            return $"[{DriverType}] {Name}";
        }
    }
}

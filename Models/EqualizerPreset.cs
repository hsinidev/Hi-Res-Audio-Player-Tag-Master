using System.Collections.Generic;

namespace HiResAudioPlayerTagMaster.Models
{
    public class EqualizerPreset
    {
        public string Name { get; set; } = "Flat";
        // 10 bands: 31Hz, 62Hz, 125Hz, 250Hz, 500Hz, 1kHz, 2kHz, 4kHz, 8kHz, 16kHz (values in dB: -12.0 to +12.0)
        public float[] Bands { get; set; } = new float[10];

        public static List<EqualizerPreset> GetDefaultPresets()
        {
            return new List<EqualizerPreset>
            {
                new EqualizerPreset { Name = "Audiophile Flat", Bands = new float[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } },
                new EqualizerPreset { Name = "Bass Boost", Bands = new float[] { 6.0f, 5.0f, 4.0f, 2.5f, 1.0f, 0, 0, 0, 0, 0 } },
                new EqualizerPreset { Name = "Vocal Enhancer", Bands = new float[] { -2.0f, -1.0f, 0, 2.0f, 4.0f, 4.5f, 3.5f, 1.5f, 0, -1.0f } },
                new EqualizerPreset { Name = "Acoustic / Classical", Bands = new float[] { 3.0f, 2.0f, 1.0f, 0, 1.5f, 2.0f, 3.0f, 3.5f, 2.5f, 1.5f } },
                new EqualizerPreset { Name = "Rock & Electronic", Bands = new float[] { 5.0f, 4.0f, 2.0f, -1.0f, -1.5f, 0, 2.5f, 4.0f, 5.0f, 4.5f } },
                new EqualizerPreset { Name = "Jazz Crisp", Bands = new float[] { 3.0f, 2.0f, 0, 1.5f, 1.5f, 2.0f, 2.5f, 3.0f, 4.0f, 3.5f } }
            };
        }
    }
}

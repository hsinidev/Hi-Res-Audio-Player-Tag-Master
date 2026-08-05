using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using HiResAudioPlayerTagMaster.Models;

namespace HiResAudioPlayerTagMaster.Services
{
    public class AudioPlaybackService : IDisposable
    {
        private IWavePlayer? _wavePlayer;
        private AudioFileReader? _activeAudioReader;
        private AudioFileReader? _nextAudioReader; // Pre-loaded for gapless transition
        private EqualizerSampleProvider? _equalizerProvider;
        private FftSampleProvider? _fftProvider;
        private SyntheticDemoSampleProvider? _demoProvider;

        private AudioDriverType _currentDriverType = AudioDriverType.WasapiShared;
        private MMDevice? _selectedMMDevice;
        private float _volume = 1.0f;
        private double _replayGainDb = 0.0;

        public event EventHandler<float[]>? FftDataAvailable;
        public event EventHandler<float[]>? WaveformDataAvailable;
        public event EventHandler? TrackEnded;
        public event EventHandler<TimeSpan>? PositionChanged;

        public bool IsPlaying { get; private set; }
        public bool IsDemoPlaying { get; private set; }
        public TimeSpan CurrentPosition => _activeAudioReader?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan TotalDuration => _activeAudioReader?.TotalTime ?? TimeSpan.FromMinutes(3);

        public AudioPlaybackService()
        {
        }

        public List<AudioDeviceModel> GetAvailableDevices()
        {
            var devices = new List<AudioDeviceModel>();

            try
            {
                var enumerator = new MMDeviceEnumerator();
                var collection = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                foreach (var dev in collection)
                {
                    devices.Add(new AudioDeviceModel
                    {
                        DeviceId = dev.ID,
                        Name = dev.FriendlyName,
                        DriverType = AudioDriverType.WasapiShared,
                        IsExclusiveSupported = true
                    });

                    devices.Add(new AudioDeviceModel
                    {
                        DeviceId = dev.ID,
                        Name = $"{dev.FriendlyName} (WASAPI Exclusive Bit-Perfect)",
                        DriverType = AudioDriverType.WasapiExclusive,
                        IsExclusiveSupported = true
                    });
                }
            }
            catch
            {
                // Fallback to WaveOut default
            }

            if (devices.Count == 0)
            {
                devices.Add(new AudioDeviceModel
                {
                    DeviceId = "default",
                    Name = "Default System Audio Output",
                    DriverType = AudioDriverType.WasapiShared,
                    IsExclusiveSupported = false
                });
            }

            return devices;
        }

        public void SetOutputDevice(AudioDeviceModel deviceModel)
        {
            _currentDriverType = deviceModel.DriverType;
            try
            {
                var enumerator = new MMDeviceEnumerator();
                _selectedMMDevice = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                    .FirstOrDefault(d => d.ID == deviceModel.DeviceId);
            }
            catch
            {
                _selectedMMDevice = null;
            }

            if (IsPlaying)
            {
                // Restart playback engine with new output driver
                var pos = CurrentPosition;
                var currentPath = _activeAudioReader?.FileName;
                if (!string.IsNullOrEmpty(currentPath))
                {
                    PlayFile(currentPath, pos);
                }
            }
        }

        public void PlayFile(string filePath, TimeSpan? startPosition = null)
        {
            Stop();

            if (!File.Exists(filePath)) return;

            try
            {
                _activeAudioReader = new AudioFileReader(filePath);

                if (startPosition.HasValue && startPosition.Value < _activeAudioReader.TotalTime)
                {
                    _activeAudioReader.CurrentTime = startPosition.Value;
                }

                // Wrap with Equalizer & FFT providers
                _equalizerProvider = new EqualizerSampleProvider(_activeAudioReader);
                _fftProvider = new FftSampleProvider(_equalizerProvider);
                _fftProvider.FftCalculated += (s, fftBins) => FftDataAvailable?.Invoke(this, fftBins);
                _fftProvider.WaveformDataAvailable += (s, waveform) => WaveformDataAvailable?.Invoke(this, waveform);

                InitializePlayer(_fftProvider.ToWaveProvider16());

                _wavePlayer?.Play();
                IsPlaying = true;
                IsDemoPlaying = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing file: {ex.Message}");
                // Fallback to Demo mode if playback fails
                StartDemoPlayback();
            }
        }

        public void PreloadNextFile(string filePath)
        {
            try
            {
                _nextAudioReader?.Dispose();
                if (File.Exists(filePath))
                {
                    _nextAudioReader = new AudioFileReader(filePath);
                }
            }
            catch
            {
                _nextAudioReader = null;
            }
        }

        public void StartDemoPlayback()
        {
            Stop();

            _demoProvider = new SyntheticDemoSampleProvider(44100, 2);
            _equalizerProvider = new EqualizerSampleProvider(_demoProvider);
            _fftProvider = new FftSampleProvider(_equalizerProvider);
            _fftProvider.FftCalculated += (s, fftBins) => FftDataAvailable?.Invoke(this, fftBins);
            _fftProvider.WaveformDataAvailable += (s, waveform) => WaveformDataAvailable?.Invoke(this, waveform);

            InitializePlayer(_fftProvider.ToWaveProvider16());

            _wavePlayer?.Play();
            IsPlaying = true;
            IsDemoPlaying = true;
        }

        private void InitializePlayer(IWaveProvider waveProvider)
        {
            _wavePlayer?.Dispose();

            if (_currentDriverType == AudioDriverType.WasapiExclusive && _selectedMMDevice != null)
            {
                _wavePlayer = new WasapiOut(_selectedMMDevice, AudioClientShareMode.Exclusive, true, 50);
            }
            else if (_currentDriverType == AudioDriverType.WasapiShared && _selectedMMDevice != null)
            {
                _wavePlayer = new WasapiOut(_selectedMMDevice, AudioClientShareMode.Shared, true, 100);
            }
            else
            {
                _wavePlayer = new WaveOutEvent { DesiredLatency = 100 };
            }

            _wavePlayer.Volume = _volume;
            _wavePlayer.PlaybackStopped += OnPlaybackStopped;
            _wavePlayer.Init(waveProvider);
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (IsPlaying)
            {
                if (_nextAudioReader != null)
                {
                    // Gapless transition to preloaded track
                    var nextReader = _nextAudioReader;
                    _nextAudioReader = null;
                    _activeAudioReader?.Dispose();
                    _activeAudioReader = nextReader;

                    _equalizerProvider = new EqualizerSampleProvider(_activeAudioReader);
                    _fftProvider = new FftSampleProvider(_equalizerProvider);
                    _fftProvider.FftCalculated += (s, fftBins) => FftDataAvailable?.Invoke(this, fftBins);
                    _fftProvider.WaveformDataAvailable += (s, waveform) => WaveformDataAvailable?.Invoke(this, waveform);

                    InitializePlayer(_fftProvider.ToWaveProvider16());
                    _wavePlayer?.Play();
                    return;
                }

                IsPlaying = false;
                TrackEnded?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Pause()
        {
            if (IsPlaying && _wavePlayer != null)
            {
                _wavePlayer.Pause();
                IsPlaying = false;
            }
        }

        public void Resume()
        {
            if (!IsPlaying && _wavePlayer != null)
            {
                _wavePlayer.Play();
                IsPlaying = true;
            }
        }

        public void Stop()
        {
            IsPlaying = false;
            IsDemoPlaying = false;
            if (_wavePlayer != null)
            {
                _wavePlayer.PlaybackStopped -= OnPlaybackStopped;
                _wavePlayer.Stop();
                _wavePlayer.Dispose();
                _wavePlayer = null;
            }

            _activeAudioReader?.Dispose();
            _activeAudioReader = null;
            _nextAudioReader?.Dispose();
            _nextAudioReader = null;
            _demoProvider = null;
        }

        public void Seek(TimeSpan targetTime)
        {
            if (_activeAudioReader != null)
            {
                _activeAudioReader.CurrentTime = targetTime;
            }
        }

        public void SetVolume(float volume)
        {
            _volume = Math.Clamp(volume, 0.0f, 1.0f);
            if (_wavePlayer != null)
            {
                _wavePlayer.Volume = _volume;
            }
        }

        public void SetReplayGain(double replayGainDb)
        {
            _replayGainDb = replayGainDb;
            if (_equalizerProvider != null)
            {
                _equalizerProvider.ReplayGainDb = (float)replayGainDb;
            }
        }

        public void SetEqualizerBand(int bandIndex, float gainDb)
        {
            _equalizerProvider?.SetBandGain(bandIndex, gainDb);
        }

        public void SetEqualizerPreset(float[] bandGains)
        {
            if (_equalizerProvider != null && bandGains.Length >= 10)
            {
                for (int i = 0; i < 10; i++)
                {
                    _equalizerProvider.SetBandGain(i, bandGains[i]);
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    #region Equalizer DSP Sample Provider
    public class EqualizerSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly BiQuadFilter[,] _filters; // [channel, band]
        private readonly float[] _centerFrequencies = new float[] { 31f, 62f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f };
        private readonly float[] _bandGains = new float[10];

        public WaveFormat WaveFormat => _source.WaveFormat;
        public float ReplayGainDb { get; set; } = 0f;

        public EqualizerSampleProvider(ISampleProvider source)
        {
            _source = source;
            int channels = source.WaveFormat.Channels;
            int sampleRate = source.WaveFormat.SampleRate;
            _filters = new BiQuadFilter[channels, 10];

            for (int ch = 0; ch < channels; ch++)
            {
                for (int band = 0; band < 10; band++)
                {
                    _filters[ch, band] = BiQuadFilter.PeakingEQ(sampleRate, _centerFrequencies[band], 0.8f, 0f);
                }
            }
        }

        public void SetBandGain(int bandIndex, float gainDb)
        {
            if (bandIndex < 0 || bandIndex >= 10) return;
            _bandGains[bandIndex] = gainDb;

            int sampleRate = WaveFormat.SampleRate;
            int channels = WaveFormat.Channels;

            for (int ch = 0; ch < channels; ch++)
            {
                _filters[ch, bandIndex] = BiQuadFilter.PeakingEQ(sampleRate, _centerFrequencies[bandIndex], 0.8f, gainDb);
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);
            int channels = WaveFormat.Channels;

            float linearReplayGain = (float)Math.Pow(10.0, ReplayGainDb / 20.0);

            for (int i = 0; i < samplesRead; i++)
            {
                int ch = i % channels;
                float sample = buffer[offset + i];

                // Apply 10 bands of peaking EQ filters
                for (int band = 0; band < 10; band++)
                {
                    if (Math.Abs(_bandGains[band]) > 0.01f)
                    {
                        sample = _filters[ch, band].Transform(sample);
                    }
                }

                if (Math.Abs(ReplayGainDb) > 0.01f)
                {
                    sample *= linearReplayGain;
                }

                buffer[offset + i] = Math.Clamp(sample, -1.0f, 1.0f);
            }

            return samplesRead;
        }
    }
    #endregion

    #region FFT & Waveform Analyzer Sample Provider
    public class FftSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly Complex[] _fftBuffer = new Complex[1024];
        private int _fftPos = 0;

        public event EventHandler<float[]>? FftCalculated;
        public event EventHandler<float[]>? WaveformDataAvailable;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public FftSampleProvider(ISampleProvider source)
        {
            _source = source;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);

            float[] waveform = new float[Math.Min(samplesRead, 256)];
            Array.Copy(buffer, offset, waveform, 0, waveform.Length);
            WaveformDataAvailable?.Invoke(this, waveform);

            for (int i = 0; i < samplesRead; i += WaveFormat.Channels)
            {
                float sample = buffer[offset + i];
                _fftBuffer[_fftPos].X = (float)(sample * FastFourierTransform.HannWindow(_fftPos, 1024));
                _fftBuffer[_fftPos].Y = 0;
                _fftPos++;

                if (_fftPos >= 1024)
                {
                    _fftPos = 0;
                    FastFourierTransform.FFT(true, 10, _fftBuffer);

                    float[] magnitudes = new float[64];
                    for (int b = 0; b < 64; b++)
                    {
                        int index = b * (512 / 64);
                        float mag = (float)Math.Sqrt(_fftBuffer[index].X * _fftBuffer[index].X + _fftBuffer[index].Y * _fftBuffer[index].Y);
                        magnitudes[b] = Math.Clamp(mag * 3.5f, 0.0f, 1.0f);
                    }

                    FftCalculated?.Invoke(this, magnitudes);
                }
            }

            return samplesRead;
        }
    }
    #endregion

    #region Synthetic Audio Demo Generator
    public class SyntheticDemoSampleProvider : ISampleProvider
    {
        private double _phaseMain;
        private double _phaseHarmonic;
        private double _phaseBass;
        private double _beatTimer;
        private readonly int _sampleRate;
        private readonly int _channels;

        public WaveFormat WaveFormat { get; }

        public SyntheticDemoSampleProvider(int sampleRate = 44100, int channels = 2)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i += _channels)
            {
                _beatTimer += 1.0 / _sampleRate;
                double sweepFreq = 220 + 440 * Math.Sin(_beatTimer * 0.8);
                double bassFreq = 55 + (Math.Sin(_beatTimer * 4.0) > 0 ? 55 : 0);

                _phaseMain += 2.0 * Math.PI * sweepFreq / _sampleRate;
                _phaseHarmonic += 2.0 * Math.PI * (sweepFreq * 1.5) / _sampleRate;
                _phaseBass += 2.0 * Math.PI * bassFreq / _sampleRate;

                float sampleLeft = (float)(0.35 * Math.Sin(_phaseMain) + 0.15 * Math.Sin(_phaseHarmonic) + 0.3 * Math.Sin(_phaseBass));
                float sampleRight = (float)(0.35 * Math.Sin(_phaseMain * 1.005) + 0.15 * Math.Cos(_phaseHarmonic) + 0.3 * Math.Sin(_phaseBass));

                buffer[offset + i] = sampleLeft;
                if (_channels > 1) buffer[offset + i + 1] = sampleRight;
            }

            return count;
        }
    }
    #endregion
}

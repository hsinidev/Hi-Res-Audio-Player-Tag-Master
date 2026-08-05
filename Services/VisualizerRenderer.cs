using System;
using SkiaSharp;

namespace HiResAudioPlayerTagMaster.Services
{
    public enum VisualizerMode
    {
        SpectrumBars,
        Oscilloscope,
        StereoVuMeter
    }

    public class VisualizerRenderer
    {
        private float[] _lastSpectrumBins = new float[64];
        private float[] _peakDecayBins = new float[64];
        private float[] _lastWaveform = new float[256];
        private float _vuLeft = 0f;
        private float _vuRight = 0f;
        private float _vuLeftPeak = 0f;
        private float _vuRightPeak = 0f;

        public VisualizerMode Mode { get; set; } = VisualizerMode.SpectrumBars;

        public void UpdateFftBins(float[] bins)
        {
            if (bins == null || bins.Length == 0) return;

            int count = Math.Min(bins.Length, 64);
            for (int i = 0; i < count; i++)
            {
                float target = bins[i];
                _lastSpectrumBins[i] = _lastSpectrumBins[i] * 0.4f + target * 0.6f;

                if (_lastSpectrumBins[i] > _peakDecayBins[i])
                {
                    _peakDecayBins[i] = _lastSpectrumBins[i];
                }
                else
                {
                    _peakDecayBins[i] = Math.Max(0, _peakDecayBins[i] - 0.015f);
                }
            }

            // VU meter calculation
            float avgLeft = 0f;
            float avgRight = 0f;
            for (int i = 0; i < count / 2; i++) avgLeft += _lastSpectrumBins[i];
            for (int i = count / 2; i < count; i++) avgRight += _lastSpectrumBins[i];

            _vuLeft = _vuLeft * 0.7f + (avgLeft / (count / 2f)) * 0.3f;
            _vuRight = _vuRight * 0.7f + (avgRight / (count / 2f)) * 0.3f;

            _vuLeftPeak = Math.Max(_vuLeft, _vuLeftPeak - 0.01f);
            _vuRightPeak = Math.Max(_vuRight, _vuRightPeak - 0.01f);
        }

        public void UpdateWaveform(float[] waveform)
        {
            if (waveform == null || waveform.Length == 0) return;
            int count = Math.Min(waveform.Length, 256);
            Array.Copy(waveform, _lastWaveform, count);
        }

        public void Render(SKCanvas canvas, int width, int height)
        {
            canvas.Clear(SKColor.Parse("#05070A")); // Onyx Obsidian Background

            // Draw Background Grid Pattern
            using (var gridPaint = new SKPaint { Color = SKColor.Parse("#0C111C"), StrokeWidth = 1, IsAntialias = true })
            {
                for (int x = 0; x < width; x += 40)
                    canvas.DrawLine(x, 0, x, height, gridPaint);
                for (int y = 0; y < height; y += 30)
                    canvas.DrawLine(0, y, width, y, gridPaint);
            }

            switch (Mode)
            {
                case VisualizerMode.SpectrumBars:
                    RenderSpectrumBars(canvas, width, height);
                    break;

                case VisualizerMode.Oscilloscope:
                    RenderOscilloscope(canvas, width, height);
                    break;

                case VisualizerMode.StereoVuMeter:
                    RenderVuMeter(canvas, width, height);
                    break;
            }
        }

        private void RenderSpectrumBars(SKCanvas canvas, int width, int height)
        {
            int barCount = 64;
            float padding = 3f;
            float totalPadding = padding * (barCount + 1);
            float barWidth = Math.Max(2, (width - totalPadding) / barCount);
            float maxBarHeight = height - 40f;

            using var barPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
            using var capPaint = new SKPaint { Color = SKColor.Parse("#F8FAFC"), IsAntialias = true };

            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(0, height - 20),
                new SKPoint(0, 20),
                new[] { SKColor.Parse("#10B981"), SKColor.Parse("#F59E0B"), SKColor.Parse("#EF4444") },
                new[] { 0.0f, 0.7f, 1.0f },
                SKShaderTileMode.Clamp);

            barPaint.Shader = shader;

            for (int i = 0; i < barCount; i++)
            {
                float val = _lastSpectrumBins[i];
                float peak = _peakDecayBins[i];

                float h = Math.Clamp(val * maxBarHeight, 4f, maxBarHeight);
                float peakY = (height - 20f) - Math.Clamp(peak * maxBarHeight, 4f, maxBarHeight);

                float x = padding + i * (barWidth + padding);
                float y = (height - 20f) - h;

                // Draw Spectrum Bar
                var rect = new SKRect(x, y, x + barWidth, height - 20f);
                canvas.DrawRoundRect(rect, 2f, 2f, barPaint);

                // Draw Peak Hold Indicator
                canvas.DrawRect(x, peakY, barWidth, 3f, capPaint);
            }

            // Frequency Labels Line
            using var labelPaint = new SKPaint { Color = SKColor.Parse("#475569"), TextSize = 10f, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Cascadia Code") };
            canvas.DrawText("20Hz", 10, height - 5, labelPaint);
            canvas.DrawText("250Hz", width * 0.25f, height - 5, labelPaint);
            canvas.DrawText("1kHz", width * 0.5f, height - 5, labelPaint);
            canvas.DrawText("4kHz", width * 0.75f, height - 5, labelPaint);
            canvas.DrawText("20kHz", width - 40, height - 5, labelPaint);
        }

        private void RenderOscilloscope(SKCanvas canvas, int width, int height)
        {
            float centerY = height / 2f;
            using var wavePaint = new SKPaint
            {
                Color = SKColor.Parse("#F59E0B"),
                StrokeWidth = 2.5f,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            using var glowPaint = new SKPaint
            {
                Color = SKColor.Parse("#F59E0B").WithAlpha(80),
                StrokeWidth = 6f,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            using var path = new SKPath();
            float step = (float)width / (_lastWaveform.Length - 1);

            for (int i = 0; i < _lastWaveform.Length; i++)
            {
                float x = i * step;
                float y = centerY - (_lastWaveform[i] * (height * 0.4f));

                if (i == 0) path.MoveTo(x, y);
                else path.LineTo(x, y);
            }

            canvas.DrawPath(path, glowPaint);
            canvas.DrawPath(path, wavePaint);

            // Zero Line
            using var zeroLine = new SKPaint { Color = SKColor.Parse("#1E293B"), StrokeWidth = 1f, IsAntialias = true };
            canvas.DrawLine(0, centerY, width, centerY, zeroLine);
        }

        private void RenderVuMeter(SKCanvas canvas, int width, int height)
        {
            float meterHeight = 36f;
            float leftY = height * 0.35f;
            float rightY = height * 0.65f;
            float meterWidth = width - 80f;
            float startX = 60f;

            using var bgPaint = new SKPaint { Color = SKColor.Parse("#0F172A"), Style = SKPaintStyle.Fill };
            using var borderPaint = new SKPaint { Color = SKColor.Parse("#334155"), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
            using var textPaint = new SKPaint { Color = SKColor.Parse("#94A3B8"), TextSize = 12f, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Segoe UI") };

            canvas.DrawText("L CH", 15, leftY + 22, textPaint);
            canvas.DrawText("R CH", 15, rightY + 22, textPaint);

            // Channel Backgrounds
            var rectL = new SKRect(startX, leftY, startX + meterWidth, leftY + meterHeight);
            var rectR = new SKRect(startX, rightY, startX + meterWidth, rightY + meterHeight);
            canvas.DrawRoundRect(rectL, 4, 4, bgPaint);
            canvas.DrawRoundRect(rectR, 4, 4, bgPaint);

            using var fillGradient = SKShader.CreateLinearGradient(
                new SKPoint(startX, 0),
                new SKPoint(startX + meterWidth, 0),
                new[] { SKColor.Parse("#10B981"), SKColor.Parse("#F59E0B"), SKColor.Parse("#EF4444") },
                new[] { 0.0f, 0.75f, 1.0f },
                SKShaderTileMode.Clamp);

            using var activePaint = new SKPaint { Shader = fillGradient, Style = SKPaintStyle.Fill, IsAntialias = true };
            using var peakPaint = new SKPaint { Color = SKColor.Parse("#FFFFFF"), Style = SKPaintStyle.Fill, IsAntialias = true };

            // Left Fill & Peak
            float fillWLeft = Math.Clamp(_vuLeft * meterWidth, 0, meterWidth);
            float peakXLeft = startX + Math.Clamp(_vuLeftPeak * meterWidth, 0, meterWidth);
            canvas.DrawRoundRect(new SKRect(startX, leftY, startX + fillWLeft, leftY + meterHeight), 4, 4, activePaint);
            canvas.DrawRect(peakXLeft - 2, leftY, 4, meterHeight, peakPaint);

            // Right Fill & Peak
            float fillWRight = Math.Clamp(_vuRight * meterWidth, 0, meterWidth);
            float peakXRight = startX + Math.Clamp(_vuRightPeak * meterWidth, 0, meterWidth);
            canvas.DrawRoundRect(new SKRect(startX, rightY, startX + fillWRight, rightY + meterHeight), 4, 4, activePaint);
            canvas.DrawRect(peakXRight - 2, rightY, 4, meterHeight, peakPaint);

            canvas.DrawRoundRect(rectL, 4, 4, borderPaint);
            canvas.DrawRoundRect(rectR, 4, 4, borderPaint);
        }
    }
}

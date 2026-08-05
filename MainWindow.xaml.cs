using System;
using System.Windows;
using System.Windows.Threading;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using HiResAudioPlayerTagMaster.ViewModels;

namespace HiResAudioPlayerTagMaster
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly DispatcherTimer _renderTimer;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            // 60 FPS Render Timer for SkiaSharp Audio Spectrum Visualizer
            _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _renderTimer.Tick += (s, e) => SkiaCanvas.InvalidateVisual();
            _renderTimer.Start();
        }

        private void OnSkiaCanvasPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            int width = e.Info.Width;
            int height = e.Info.Height;

            _viewModel.Visualizer.Render(canvas, width, height);
        }

        protected override void OnClosed(EventArgs e)
        {
            _renderTimer.Stop();
            base.OnClosed(e);
        }
    }
}

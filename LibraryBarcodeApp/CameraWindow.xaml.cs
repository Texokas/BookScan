using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;
using ZXing.Windows.Compatibility;

namespace LibraryBarcodeApp;

public partial class CameraWindow : Window
{
    private VideoCaptureDevice? _device;
    private volatile bool _isClosing;
    private readonly BarcodeReader _reader = new() { Options = { TryHarder = true } };

    public string? ScannedBarcode { get; private set; }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    public CameraWindow()
    {
        InitializeComponent();
        Loaded  += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        if (devices.Count == 0)
        {
            StatusText.Text = "Камера не найдена. Подключите веб-камеру и повторите.";
            return;
        }

        DeviceText.Text = devices[0].Name;
        _device = new VideoCaptureDevice(devices[0].MonikerString);
        _device.NewFrame += OnNewFrame;
        _device.Start();
        StatusText.Text = "Наведите камеру на штрихкод...";
    }

    private void OnNewFrame(object sender, NewFrameEventArgs e)
    {
        if (_isClosing) return;

        var frame = (Bitmap)e.Frame.Clone();

        var result = _reader.Decode(frame);

        var source = BitmapToSource(frame);
        frame.Dispose();
        source.Freeze(); // обязательно перед передачей в UI-поток

        if (_isClosing) return;

        Dispatcher.BeginInvoke(() =>
        {
            if (!_isClosing) CameraFeed.Source = source;
        });

        if (result is not null && !_isClosing)
        {
            ScannedBarcode = result.Text;
            Dispatcher.BeginInvoke(() =>
            {
                if (!_isClosing)
                {
                    DialogResult = true;
                    Close();
                }
            });
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _isClosing = true;
        if (_device?.IsRunning == true)
        {
            _device.NewFrame -= OnNewFrame;
            _device.SignalToStop();
            _device.WaitForStop();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private static BitmapSource BitmapToSource(Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }
}

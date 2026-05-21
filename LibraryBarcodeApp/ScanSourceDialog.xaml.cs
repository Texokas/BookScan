using System.Windows;

namespace LibraryBarcodeApp;

public enum ScanSource { Camera, Image }

public partial class ScanSourceDialog : Window
{
    public ScanSource? SelectedSource { get; private set; }

    public ScanSourceDialog()
    {
        InitializeComponent();
    }

    private void Camera_Click(object sender, RoutedEventArgs e)
    {
        SelectedSource = ScanSource.Camera;
        DialogResult = true;
        Close();
    }

    private void Image_Click(object sender, RoutedEventArgs e)
    {
        SelectedSource = ScanSource.Image;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

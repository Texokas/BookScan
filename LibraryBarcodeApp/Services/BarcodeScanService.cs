using LibraryBarcodeApp.Helpers;
using Microsoft.Win32;
using System.Windows;

namespace LibraryBarcodeApp.Services;

public class BarcodeScanService
{
    public string? ScanBarcodeFromImage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        return BarcodeHelper.DecodeBarcodeFromImage(dialog.FileName);
    }

    public string? ScanBarcodeFromCamera(Window owner)
    {
        var cameraWindow = new CameraWindow { Owner = owner };
        return cameraWindow.ShowDialog() == true ? cameraWindow.ScannedBarcode : null;
    }
}

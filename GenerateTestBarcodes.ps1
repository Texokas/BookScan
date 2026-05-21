# Generates test barcode PNG files using ZXing from the already-compiled project
$zxingDll    = "$PSScriptRoot\LibraryBarcodeApp\bin\Debug\net8.0-windows\zxing.dll"
$compatDll   = "$PSScriptRoot\LibraryBarcodeApp\bin\Debug\net8.0-windows\ZXing.Windows.Compatibility.dll"
$outDir      = "$PSScriptRoot\TestBarcodes"

if (-not (Test-Path $zxingDll)) {
    Write-Error "DLL not found: $zxingDll. Build the project first (dotnet build)."
    exit 1
}

Add-Type -Path $zxingDll
Add-Type -Path $compatDll

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$barcodes = @(
    @{ Value = "978-5-389-12345-6"; Label = "Война_и_мир"         }
    @{ Value = "978-5-17-100000-1"; Label = "Преступление_и_наказание" }
    @{ Value = "978-5-04-110000-3"; Label = "Мастер_и_Маргарита"  }
    @{ Value = "978-5-699-12345-0"; Label = "Анна_Каренина"        }
    @{ Value = "978-5-389-99999-9"; Label = "Тестовая_книга"       }
)

$writer = New-Object ZXing.Windows.Compatibility.BarcodeWriter
$writer.Format = [ZXing.BarcodeFormat]::EAN_13

foreach ($item in $barcodes) {
    $writer.Options = New-Object ZXing.Common.EncodingOptions
    $writer.Options.Width  = 400
    $writer.Options.Height = 150
    $writer.Options.Margin = 10

    $bitmap = $writer.Write($item.Value)
    $path   = "$outDir\$($item.Label)_$($item.Value -replace '-','').png"
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    Write-Host "Created: $path"
}

Write-Host "`nDone. Barcode images saved to: $outDir"

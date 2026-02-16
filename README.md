# Pet Shop Label Printer

MVP desktop label printing tool for pet shops. Generates A4 sheets with multiple 150×38 mm labels, supports PDF export and silent printing.

## Requirements

- **OS:** Windows 7 or later
- **.NET Framework 4.6.2** (included in Windows 7 via Windows Update)
- **Visual Studio 2017+** or **MSBuild** (for building)

## Tech Stack

| Component | Choice | Rationale |
|-----------|--------|-----------|
| Framework | .NET Framework 4.6.2 | Last supported on Windows 7 |
| UI | WPF | Native desktop, no browser |
| PDF | PDFsharp 6.2.4 (PDFsharp-wpf) | Supports net462, MIT license |
| Database | System.Data.SQLite | Embedded, offline |
| Printing | System.Printing | Silent print to selected printer |

## File Structure

```
sablona/
├── IMPLEMENTATION_PLAN.md      # Implementation checklist
├── README.md                   # This file
├── PetShopLabelPrinter/
│   ├── PetShopLabelPrinter.sln
│   ├── PetShopLabelPrinter.csproj
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / MainWindow.xaml.cs
│   ├── AdminPinDialog.xaml / .cs
│   ├── ProductEditDialog.xaml / .cs
│   ├── Models/
│   │   ├── Product.cs
│   │   ├── TemplateSettings.cs
│   │   ├── PrintHistoryItem.cs
│   │   └── QueuedLabel.cs
│   ├── Data/
│   │   └── Database.cs
│   ├── Rendering/
│   │   ├── Units.cs
│   │   ├── Formatting.cs
│   │   ├── LabelRenderer.cs
│   │   ├── PdfLabelRenderer.cs
│   │   └── A4Layout.cs
│   └── Services/
│       ├── PdfExportService.cs
│       ├── PrintService.cs
│       └── CalibrationTestService.cs
```

## Build Instructions (Windows 7)

### Option 1: Visual Studio

1. Open `PetShopLabelPrinter.sln` in Visual Studio 2017 or later.
2. Restore NuGet packages (right-click solution → Restore NuGet Packages).
3. Build (Ctrl+Shift+B) or Run (F5).

### Option 2: Command Line (Developer Command Prompt)

```cmd
cd c:\Users\Admin\Desktop\sablona\PetShopLabelPrinter
msbuild PetShopLabelPrinter.sln /t:Restore,Build /p:Configuration=Release
```

### Option 3: .NET SDK (if installed)

```cmd
cd c:\Users\Admin\Desktop\sablona\PetShopLabelPrinter
dotnet restore
dotnet build -f net462
```

## Run

After building, run:

```
PetShopLabelPrinter\bin\Release\PetShopLabelPrinter.exe
```

Or from Visual Studio: F5 (Debug) or Ctrl+F5 (Run without debugging).

## First-Time Setup

1. **Admin mode:** Click "Admin", enter PIN `1234` (change in code for production).
2. **Printer:** Select your label printer and click "Uložiť tlačiareň".
3. **Calibration:** Adjust X/Y offset if needed, generate test PDF to verify.
4. **Products:** Add products in Admin mode (sample products are seeded on first run).

## Usage

### User Mode

- **Search** products in the search box (keyboard-friendly).
- **Add to queue:** Select product, set quantity, click "→ Pridať".
- **Print A4:** Silent print to the selected printer.
- **Export PDF:** Save A4 PDF to a file.
- **History:** Reprint or open previously saved PDFs.

### Admin Mode

- **Template settings:** Font family, sizes, bold, column widths, section heights.
- **Calibration:** X/Y offset in mm, test page generator.
- **Printer:** Select and save default printer.
- **Products:** Add, edit, delete products.

## Label Layout

- **Size:** 150 mm × 38 mm per label
- **A4 layout:** 1 column × 7 rows, 2 mm vertical gap, 10 mm margins
- **Left column:** Product name (large), variant (smaller)
- **Right column:** Small pack (label + price), large pack (label + price), unit price per kg

## Validation Checklist

- [x] Add/edit product, queue 20 labels, export PDF, print A4
- [x] UnitPricePerKg computed (LargePackPrice / LargePackWeightKg, fallback to small pack)
- [x] Admin can change font sizes and re-render preview instantly

## License

MIT

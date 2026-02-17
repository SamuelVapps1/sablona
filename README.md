# Pet Shop Label Printer

MVP desktop label printing tool for pet shops. Main flow is an inline-edit table where staff edits labels directly and prints/exports only selected rows.

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
2. **Printer:** Select a Windows printer and click "Uložiť tlačiareň".
3. **Calibration:** Use X/Y offset (mm) and test page buttons.

## Usage

### User Flow (Store Staff)

1. Edit labels directly in the main table (no edit dialog).
2. Choose what to process by either:
   - checking **Tlačiť?** in rows, or
   - selecting rows directly.
3. Set row **Počet** (quantity, default 1).
4. Click:
   - **Tlačiť A4 (vybrané)** to print directly to Windows printer (no PDF save),
   - **Export PDF (vybrané)** to explicitly save via `SaveFileDialog`.
5. **Vyčistiť výber/frontu** clears current print selection.

### Admin Flow

- Controls only layout/calibration/printer:
  - installed system fonts (dropdown),
  - font sizes and section widths/heights in mm,
  - crop marks,
  - global calibration X/Y offset.
- Live preview always reflects currently selected table row.
- **Generovať PDF test** opens calibration test PDF.
- **Tlačiť test** sends calibration test to configured printer.
- **Vymazať históriu** clears grouped print/export job history.

## Label Layout

- **Size:** 150 mm × 38 mm per label
- **A4 layout:** 1 column × 7 rows, 2 mm vertical gap, 10 mm margins
- **Left column:** Product name (large), variant (smaller)
- **Right column:** Small pack (label + price), large pack (label + price), unit price per kg

## Validation Checklist

- [x] Inline grid editing for label fields (including editable unit price text)
- [x] Print pipeline is separated from export pipeline
- [x] Print/export uses only current row selection and row quantity
- [x] Admin live preview updates for current selected row

## License

MIT

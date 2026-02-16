# Pet Shop Label Printer – File Structure

```
sablona/
├── IMPLEMENTATION_PLAN.md       # Implementation checklist
├── FILE_STRUCTURE.md            # This file
├── README.md                   # Build/run instructions
├── LICENSE
│
└── PetShopLabelPrinter/
    ├── PetShopLabelPrinter.sln
    ├── PetShopLabelPrinter.csproj
    │
    ├── App.xaml
    ├── App.xaml.cs
    ├── MainWindow.xaml
    ├── MainWindow.xaml.cs
    ├── AdminPinDialog.xaml
    ├── AdminPinDialog.xaml.cs
    ├── ProductEditDialog.xaml
    ├── ProductEditDialog.xaml.cs
    │
    ├── Models/
    │   ├── Product.cs              # Product entity, UnitPricePerKg computed
    │   ├── TemplateSettings.cs     # Fonts, sizes, layout mm, calibration
    │   ├── PrintHistoryItem.cs     # Print history record
    │   └── QueuedLabel.cs          # Product + Quantity for print queue
    │
    ├── Data/
    │   └── Database.cs             # SQLite: Products, PrintHistory, TemplateSettings, AppSettings
    │
    ├── Rendering/
    │   ├── Units.cs                # mm ↔ WPF units conversion
    │   ├── Formatting.cs           # Euro locale, decimal comma
    │   ├── LabelRenderer.cs        # WPF DrawingContext label renderer
    │   ├── PdfLabelRenderer.cs     # PdfSharp XGraphics label renderer
    │   └── A4Layout.cs             # 1×7 layout, positions, pagination
    │
    └── Services/
        ├── PdfExportService.cs     # Export queue to PDF
        ├── PrintService.cs         # Silent print via DocumentPaginator
        └── CalibrationTestService.cs  # A4 test page with ruler/grid
```

## NuGet Packages

- **PDFsharp-wpf** 6.2.4 – PDF generation (net462)
- **System.Data.SQLite.Core** 1.0.118 – SQLite
- **System.Text.Json** 6.0.10 – JSON for template settings

## References

- **System.Printing** – Printer queue, silent print

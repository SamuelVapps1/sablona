# Pet Shop Label Printer – Implementation Plan

## Tech Stack (Windows 7 Compatible)

| Component | Choice | Rationale |
|-----------|--------|------------|
| Framework | .NET Framework 4.6.2 | Last supported on Windows 7 |
| UI | WPF | Native desktop, no browser |
| PDF | PDFsharp 6.2.4 (PDFsharp-wpf) | Supports net462, MIT license |
| Database | System.Data.SQLite | Embedded, offline |
| Printing | System.Printing + PrintDialog (no ShowDialog) | Silent print to selected printer |

## Validation Checklist

- [ ] Add/edit product, queue 20 labels, export PDF, print A4 without scaling issues
- [ ] UnitPricePerKg computed and formatted correctly (LargePackPrice / LargePackWeightKg fallback)
- [ ] Admin can change font sizes and re-render preview instantly

## Implementation Checklist

### 1. Solution & Project Setup
- [x] Create .sln and .csproj targeting net462
- [x] Add NuGet: PDFsharp-wpf, System.Data.SQLite.Core
- [x] Add System.Printing reference

### 2. Data Layer
- [x] SQLite schema: Products, PrintHistory, TemplateSettings, AppSettings
- [x] Product model with all required fields
- [x] UnitPricePerKg computation logic
- [x] Repository classes for CRUD

### 3. Template Engine
- [x] TemplateSettings model (fonts, sizes, alignments, column widths, section heights)
- [x] Version field for future multi-template support
- [x] Label renderer: mm-based coordinates, WPF DrawingContext
- [x] Left column: ProductName, VariantText
- [x] Right column: Top/Middle/Bottom sections with separators

### 4. A4 Layout Engine
- [x] 1 col × 7 rows per A4 portrait
- [x] Margins: 10mm L/T, 2mm vertical gap
- [x] Pagination for >7 labels
- [x] Optional crop marks (Admin setting)
- [x] Hairline border per label

### 5. Rendering Pipeline
- [x] WPF DrawingVisual for preview
- [x] PDF export via PDFsharp (same mm logic)
- [x] Print via PrintDialog.PrintDocument (no ShowDialog)
- [x] Calibration X/Y offset applied to all outputs

### 6. Main Window UX
- [x] User mode: search, product list, print queue, Print A4, Export PDF, history
- [x] Admin mode: template settings, printer selection, calibration, test page
- [x] Simple PIN/password for Admin

### 7. Calibration
- [x] X/Y offset in mm
- [x] Test page: A4 with ruler/grid + one label outline

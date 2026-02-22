# Pet Shop Label Printer

WPF aplikácia na tlač štítkov pre pet shop s podporou viacerých formátov a Alfa+ interoperabilitou.

## CSV formát (Alfa+ import/export)

### Import z Alfa+

1. **Súbor → Import z Alfa+ (CSV)** – spustí trojkrokový sprievodcu.
2. **Krok 1:** Vyberte CSV súbor (UTF-8 alebo Windows-1250).
3. **Krok 2:** Oddeľovač (čiarka alebo bodkočiarka) a či prvý riadok je hlavička.
4. **Krok 3:** Mapovanie stĺpcov CSV na polia: Názov, Cena, SKU, EAN, Dátum spotreby.
5. Mapovanie sa uloží ako predvoľba pre ďalší import.

**Spárovanie:** Existujúce produkty sa hľadajú podľa SKU, potom podľa EAN. Zhoda → aktualizácia; inak → nový produkt.

**Čiarový kód:** Ak je BarcodeEnabled a BarcodeValue prázdne, automaticky sa použije EAN (ak existuje) alebo SKU. Formát: EAN13 pre 12/13 číslic, inak CODE128.

### Export do CSV

**Súbor → Export do CSV** – exportuje aktuálny zoznam produktov.

**Stĺpce:** Name, Price, SKU, EAN, ExpiryDate, BarcodeEnabled, BarcodeValue, BarcodeFormat

**Oddeľovač:** Predvolene bodkočiarka (`;`), konfigurovateľné v dialógu exportu.

### Príklad CSV (import)

```csv
Názov;Cena;Kód;EAN;SP
Royal Canin Medium Adult;42.90;RC-MA-17;5901234123457;2025-12-31
Purina Pro Plan;38.50;PPP-14;5901234123458;
```

Riadky s prázdnym názvom sa preskočia.

## Kalibrácia tlače podľa šablóny

Každá `Šablóna štítku` má vlastné kalibračné hodnoty:
- `Offset X/Y (mm)` v rozsahu `-5 .. +5`
- `Scale X/Y (%)` v rozsahu `90 .. 110`

Nastavenie:
1. Otvorte `Spravovať šablóny`.
2. Vyberte šablónu a kliknite `Upraviť`.
3. V sekcii `Kalibrácia tlače` nastavte offset a scale.

Test tlače:
- V hlavnom paneli kliknite `Test print (kalibrácia)`.
- Vytlačí sa test pre aktuálne vybranú šablónu s:
  - vonkajším rámom štítku,
  - vnútorným rámom podľa paddingu,
  - osou/rulerom `0–50 mm` pre X aj Y,
  - stredovým crosshair markerom,
  - textom názvu šablóny, rozmerov a aktuálnych kalibračných hodnôt.

Odporúčanie:
- Ak je výtlačok posunutý, dolaďte `Offset`.
- Ak nesedí mierka (šírka/výška), dolaďte `Scale X/Y`.

## A4 hárok: rozloženie a kalibrácia

- Rozloženie pre tlač/PDF je na A4 (`210 x 297 mm`) s nastaviteľným:
  - `SheetMarginMm` (predvolené 8 mm)
  - `GapMm` (predvolené 2 mm)
- Podporované sú zmiešané veľkosti štítkov na tej istej A4 stránke (bez rotácie).
- Umiestňovanie je deterministické: zľava doprava, zhora nadol, s prechodom na nový riadok/stranu pri nedostatku miesta.
- Bezpečnostná tolerancia `epsilon = 0.5 mm`:
  - nový riadok: `x + w > printableRight - epsilon`
  - nová strana: `y + h > printableBottom - epsilon`
  - tým sa zníži riziko orezania na hranách.

### A4 nastavenia

- V hlavnom paneli kliknite `A4 nastavenia`.
- Nastavíte:
  - Margin, Gap
  - globálnu kalibráciu hárka (`Scale X/Y`, `Offset X/Y`)
  - `Debug layout` (prekreslí printable box, obrysy umiestnených štítkov a indexy)
- Tlačidlo `Test print A4` vytlačí:
  - A4 border
  - printable area border
  - 10 mm grid
  - ruler osy X (0–200 mm) a Y (0–287 mm)
  - aktuálne hodnoty nastavení

### Poznámka k mierke v ovládači tlačiarne

- Aplikácia sa snaží tlačiť na 100 % bez fit-to-page.
- Ak ovládač tlačiarne stále škáluje výstup, vypnite v dialógu tlačiarne:
  - `Fit to page`, `Scale to fit`, `Shrink/Oversize` a podobné voľby.
- Ak je šablóna väčšia než printable area (po marginoch), tlač/export sa zastaví s jasným chybovým hlásením.

### Jednotky a poradie transformácií

- WPF: mm -> device independent units cez `Units.MmToWpfUnits(mm)`.
- PDF: mm -> points cez `XUnit.FromMillimeter(mm).Point`.
- Globálna A4 kalibrácia sa aplikuje v poradí:
  1. `scale` (okolo stredu stránky)
  2. `translate` (offset v mm)

[Files]
[Files]
Source: "bin\Release\net462\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs
[Setup]
AppName=LauraPets LabelMaker
AppVersion=1.0
DefaultDirName={pf}\LauraPetsLabelMaker
DefaultGroupName=LauraPets LabelMaker
OutputDir=installer
OutputBaseFilename=LauraPets_LabelMaker_Setup
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
DisableProgramGroupPage=yes

[Files]
Source: "bin\Release\net462\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Pet Shop Label Printer"; Filename: "{app}\PetShopLabelPrinter.exe"
Name: "{commondesktop}\Pet Shop Label Printer"; Filename: "{app}\PetShopLabelPrinter.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Vytvoriť ikonu na ploche"; GroupDescription: "Dodatočné možnosti:"
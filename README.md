# HP LaserJet P1005 Settings

A small, modern settings app for the **HP LaserJet P1005** (and similar legacy HP/Marvell-driver printers) on Windows 10/11.
It replaces the old *Printing Preferences* and *Printer Properties → Device Settings* dialogs with one clean window
that has plain-language explanations, light/dark mode and Turkish/English UI.

**Typical uses:** turn on **EconoMode**, switch **A4/Letter**, flip **status messages** off to stop the
repeating *non-HP / third-party cartridge* pop-ups, change **print density**, and more, without digging through Control Panel.

[Türkçe açıklama aşağıda / Turkish below](#türkçe)

## Ekran görüntüleri / Screenshots

![Yazdırma sekmesi / Printing tab](docs/screenshot1.png)

**Yazdırma sekmesi:** çözünürlük, EconoMode, kağıt boyutu ve yön ayarları tek ekranda.<br>
**Printing tab:** resolution, EconoMode, paper size and orientation on one screen.

![Aygıt sekmesi / Device tab](docs/screenshot2.png)

**Aygıt sekmesi:** durum iletileri, sıkışma düzeltme ve baskı yoğunluğu gibi yazıcı ayarları.<br>
**Device tab:** printer settings such as status messages, jam recovery and print density.

![İngilizce arayüz, Yazdırma sekmesi / English interface, Printing tab](docs/screenshot3.png)

**İngilizce arayüz, Yazdırma sekmesi:** sağ üstteki TR | EN seçicisiyle dil değiştirilir.<br>
**English interface, Printing tab:** switch the language with the TR | EN selector at the top right.

![İngilizce arayüz, Aygıt sekmesi / English interface, Device tab](docs/screenshot4.png)

**İngilizce arayüz, Aygıt sekmesi:** durum iletileri, sıkışma düzeltme ve baskı yoğunluğu İngilizce olarak.<br>
**English interface, Device tab:** status messages, jam recovery and print density in English.

## Features

| Tab | Setting | What it does |
|---|---|---|
| Printing | Resolution (FastRes 1200 / 600 dpi) | Print quality mode |
| Printing | EconoMode | Saves toner, lighter print |
| Printing | REt | Edge smoothing |
| Printing | Print all text as black | Turns gray/colored text black |
| Printing | Paper size, orientation, rotate 180°, mirror | Layout |
| Device | Status messages | Printer pop-ups (jam, cartridge warnings) on/off |
| Device | Jam recovery, manual duplex, Courier dark, print density 1–5 | Printer behavior |

Other settings (paper source, duplex, watermark, scaling) are still available through a button that opens HP's original window.

## Requirements

- Windows 10 or 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- An installed printer named exactly **`HP LaserJet P1005`** (the name is currently hard-coded)
- Tested with the 2013 HP driver (version 1.0.2.2680). Other drivers may store settings differently.

## Install

1. Download the latest `.exe` from the **Releases** page.
2. Run it. Changes on the *Printing* tab need no special rights.
3. The *Device* tab writes machine-wide printer settings and needs **administrator rights**.
   If the app is not elevated it shows a *Restart as administrator* button.

## How it works

The old driver stores its settings in two places, and this app reads and writes them directly:

- Per-user print defaults: `HKCU\Printers\DevModePerUser` (a binary DEVMODE block; the HP-specific flags live in its driver-private part)
- Machine-wide device settings: `HKLM\SYSTEM\CurrentControlSet\Control\Print\Printers\HP LaserJet P1005\PrinterDriverData`

The byte offsets were found by changing one option at a time in the original dialogs and comparing snapshots.

## Known limitations

- Only tested on the HP LaserJet P1005. Printer name is hard-coded.
- Paper type and paper source are not exposed yet.
- Language and theme choices apply to the current session only (the app follows Windows by default).

## Disclaimer

This project is **not affiliated with or endorsed by HP Inc.** "HP" and "LaserJet" are trademarks of their owners.
The app edits printer settings in the Windows registry. Use at your own risk; the settings it changes are the same ones the original dialogs change.

## Build from source

Open the project in Visual Studio 2022 (".NET desktop development" workload), target .NET 8, and run.
To publish a single file: *Build → Publish → Folder*, Deployment mode *Framework-dependent*, *Produce single file*.

## Contributing

Issues and pull requests are welcome. Reports for other models are especially useful:
open an issue with your printer model and driver version.

## License

[MIT](LICENSE)

---

## Türkçe

**HP LaserJet P1005** (ve benzeri eski HP sürücülü yazıcılar) için Windows 10/11 üzerinde çalışan, sade ve modern bir ayar uygulaması.
Eski *Yazdırma Tercihleri* ve *Yazıcı Özellikleri → Aygıt Ayarları* pencerelerinin yerine, açıklamalı, açık/koyu temalı ve Türkçe/İngilizce arayüzlü tek bir pencere sunar.

**Sık kullanım:** **EconoMode** açma, **A4/Letter** seçme, tekrar eden **muadil kartuş uyarılarını** durdurmak için **durum iletilerini** kapatma, **baskı yoğunluğunu** ayarlama.

### Gereksinimler

- Windows 10 veya 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Adı tam olarak **`HP LaserJet P1005`** olan yüklü bir yazıcı (ad şimdilik koda sabit yazılı)
- 2013 tarihli HP sürücüsüyle (1.0.2.2680) denenmiştir.

### Kurulum

1. **Releases** sayfasından en son `.exe` dosyasını indir.
2. Çalıştır. *Yazdırma* sekmesi özel yetki gerektirmez.
3. *Aygıt* sekmesi makine geneli ayar yazdığı için **yönetici yetkisi** ister. Yetki yoksa uygulama "Yönetici olarak yeniden başlat" düğmesi gösterir.

### Uyarı

Bu proje HP Inc. ile bağlantılı değildir. Uygulama kayıt defterindeki yazıcı ayarlarını değiştirir, kullanım sorumluluğu sana aittir. Değiştirdiği ayarlar, orijinal pencerelerin değiştirdiği ayarlarla aynıdır.

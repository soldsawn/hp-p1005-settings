using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace YaziciKesif
{
    public partial class MainWindow : Window
    {
        const string Yazici = "HP LaserJet P1005";

        // Sürücüye özel alan konumları (deneylerle bulundu)
        const int OfsBayrak = 604;   // bit: 0x02=180°, 0x04=REt, 0x08=EconoMode, 0x10=Tüm yazıları siyah
        const int OfsAyna = 605;     // bit 0x01 = ayna yazdırma
        const int OfsKalite = 912;   // 0 = FastRes 1200, 1 = 600 dpi
        const int OfsTur = 296;      // 16 bit: 1 = düz kağıt
        const byte Dondur = 0x02, Ret = 0x04, Econo = 0x08, Siyah = 0x10;

        // Standart DEVMODE alanları
        const int OfsYon = 76, OfsKagit = 78, OfsBoy = 80, OfsEn = 82;

        static readonly (string ad, int kod, int boy, int en)[] Kagitlar =
        {
            ("A4", 9, 2970, 2100),
            ("Letter", 1, 2794, 2159),
            ("Legal", 5, 3556, 2159),
            ("A5", 11, 2100, 1480),
        };


        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        bool yukleniyor;
        RadioButton[] boyutRdo;

        public MainWindow()
        {
            InitializeComponent();
            boyutRdo = new[] { rdoA4, rdoLetter, rdoLegal, rdoA5 };
            en = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "tr";
            Loaded += (s, e) =>
            {
                Tema();
                rdoTR.IsChecked = !en;
                rdoEN.IsChecked = en;
                DilUygula();
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "--aygit") >= 0)
                {
                    sekmeAygit.IsChecked = true;
                    SekmeGoster();
                }
            };
            Activated += (s, e) => { Tema(); Yukle(); };   // eski pencereden dönünce yenile
        }

        // ---------- Tema (Windows'un açık/koyu ayarını izler) ----------
        static bool AcikTema()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return Convert.ToInt32(k?.GetValue("AppsUseLightTheme") ?? 1) != 0;
            }
            catch { return true; }
        }

        // Düğmeyle yapılan seçim yalnızca bu oturum için geçerli; kaydedilmez.
        bool? oturumTercihi;
        bool acikTema = true;

        void Tema_Click(object s, RoutedEventArgs e)
        {
            oturumTercihi = !acikTema;
            Tema();
        }

        void Tema()
        {
            bool acik = acikTema = oturumTercihi ?? AcikTema();
            simgeAy.Visibility = acik ? Visibility.Visible : Visibility.Collapsed;
            simgeGunes.Visibility = acik ? Visibility.Collapsed : Visibility.Visible;
            string[] ad = { "Bg", "Card", "Text", "Muted", "Line", "Accent", "Track" };
            string[] renk = acik
                ? new[] { "#F3F5F7", "#FFFFFF", "#1B2430", "#5F6B7A", "#DDE3EA", "#0A7CC2", "#C3CCD6" }
                : new[] { "#14181D", "#1E242B", "#E8EDF2", "#98A4B3", "#2D3640", "#3BA7E8", "#3A444F" };
            for (int i = 0; i < ad.Length; i++)
                Resources[ad[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(renk[i]));

            var h = new WindowInteropHelper(this).Handle;
            if (h != IntPtr.Zero)
            {
                int koyu = acik ? 0 : 1;
                DwmSetWindowAttribute(h, 20, ref koyu, sizeof(int));   // koyu başlık çubuğu
            }
        }

        // ---------- Okuma ----------
        static byte[] Oku()
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Printers\DevModePerUser");
            return (byte[])k.GetValue(Yazici);
        }

        static int Oku16(byte[] b, int o) => BitConverter.ToInt16(b, o);

        static void Yaz16(byte[] b, int o, int v)
        {
            b[o] = (byte)v;
            b[o + 1] = (byte)(v >> 8);
        }

        static void Bayrak(byte[] d, int ofs, byte bit, bool? acik)
        {
            byte yeni = acik == true ? (byte)(d[ofs] | bit) : (byte)(d[ofs] & ~bit);
            YazBayt(d, ofs, yeni);
        }

        void Yukle()
        {
            try
            {
                var d = Oku();
                yukleniyor = true;

                rdoK0.IsChecked = d[OfsKalite] == 0;
                rdoK1.IsChecked = d[OfsKalite] != 0;
                chkEcono.IsChecked = (d[OfsBayrak] & Econo) != 0;
                chkRet.IsChecked = (d[OfsBayrak] & Ret) != 0;
                chkSiyah.IsChecked = (d[OfsBayrak] & Siyah) != 0;
                chkDondur.IsChecked = (d[OfsBayrak] & Dondur) != 0;
                chkAyna.IsChecked = (d[OfsAyna] & 0x01) != 0;

                int kagit = Oku16(d, OfsKagit);
                for (int i = 0; i < Kagitlar.Length; i++) boyutRdo[i].IsChecked = Kagitlar[i].kod == kagit;

                bool yatay = Oku16(d, OfsYon) == 2;
                rdoYatay.IsChecked = yatay;
                rdoDikey.IsChecked = !yatay;

                int tur = Oku16(d, OfsTur);
                txtTur.Text = tur == 1 ? T("Kağıt türü: düz kağıt")
                                       : T("Kağıt türü düz kağıt değil") + " (" + T("kod") + " " + tur + ").";
                btnDuz.Visibility = tur == 1 ? Visibility.Collapsed : Visibility.Visible;
                YukleAygit();
            }
            catch (Exception ex)
            {
                txtDurum.Text = T("Ayarlar okunamadı: ") + ex.Message;
            }
            finally
            {
                yukleniyor = false;
            }
        }

        // ---------- Yazma ----------
        // Windows'un SetPrinter yolu sürücü kodunu çağırıp belleği bozuyordu (çökme).
        // Bu yüzden kullanıcı ayar bloğu doğrudan kayıt defterine yazılır.
        const int OfsToplam = 588;   // 16 bit sağlama toplamı

        static void ToplamEkle(byte[] d, int fark)
        {
            int t = (d[OfsToplam] | (d[OfsToplam + 1] << 8)) + fark;
            d[OfsToplam] = (byte)t;
            d[OfsToplam + 1] = (byte)(t >> 8);
        }

        static void YazBayt(byte[] d, int ofs, byte yeni)
        {
            int fark = yeni - d[ofs];
            d[ofs] = yeni;
            ToplamEkle(d, ofs % 2 == 0 ? fark : fark << 8);
        }

        static void YazOzel16(byte[] d, int ofs, int yeni)
        {
            int eski = (ushort)BitConverter.ToInt16(d, ofs);
            Yaz16(d, ofs, yeni);
            ToplamEkle(d, yeni - eski);
        }

        void Uygula(Action<byte[]> degistir)
        {
            if (yukleniyor) return;
            try
            {
                var dm = Oku();
                degistir(dm);
                using var k = Registry.CurrentUser.OpenSubKey(@"Printers\DevModePerUser", true);
                k.SetValue(Yazici, dm, RegistryValueKind.Binary);
                txtDurum.Text = T("Kaydedildi.");
            }
            catch (Exception ex)
            {
                txtDurum.Text = T("Hata: ") + ex.Message;
            }
        }

        // ---------- Aygıt ayarları (makine geneli, yönetici yetkisi ister) ----------
        const string AygitYolu = @"SYSTEM\CurrentControlSet\Control\Print\Printers\HP LaserJet P1005\PrinterDriverData";

        [StructLayout(LayoutKind.Sequential)]
        struct PRINTER_DEFAULTS
        {
            public IntPtr pDatatype;
            public IntPtr pDevMode;
            public int DesiredAccess;
        }

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool OpenPrinter(string name, out IntPtr h, ref PRINTER_DEFAULTS def);
        [DllImport("winspool.drv", SetLastError = true)]
        static extern bool ClosePrinter(IntPtr h);
        [DllImport("winspool.drv", CharSet = CharSet.Unicode)]
        static extern int SetPrinterData(IntPtr h, string valueName, int type, byte[] data, int size);

        static bool Yonetici()
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        static int Dword(string ad, int varsayilan)
        {
            using var k = Registry.LocalMachine.OpenSubKey(AygitYolu);
            return Convert.ToInt32(k?.GetValue(ad) ?? varsayilan);
        }

        void YukleAygit()
        {
            chkDurum.IsChecked = Dword("Status", 0) != 0;
            chkSikisma.IsChecked = Dword("JamRecovery", 0) != 0;
            chkManuel.IsChecked = Dword("ManualDuplex", 0) != 0;

            bool koyu = Dword("CourierDark", 0) != 0;
            rdoC0.IsChecked = !koyu;
            rdoC1.IsChecked = koyu;

            int yog = Dword("Density", 3);
            foreach (var r in new[] { rdoY1, rdoY2, rdoY3, rdoY4, rdoY5 })
                r.IsChecked = int.Parse((string)r.Tag) == yog;

            bool yonetici = Yonetici();
            txtYonetici.Text = yonetici
                ? T("✓ Yönetici olarak çalışıyor. Aygıt ayarlarını değiştirebilirsin.")
                : T("Bu ayarları değiştirmek için yönetici yetkisi gerekir. Şimdilik yalnızca görüntüleyebilirsin.");
            btnYonetici.Visibility = yonetici ? Visibility.Collapsed : Visibility.Visible;
            kartlarAygit.IsEnabled = yonetici;
            kartlarAygit.Opacity = yonetici ? 1.0 : 0.5;
        }

        void AygitYaz(string ad, int deger)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                var def = new PRINTER_DEFAULTS { DesiredAccess = 0x000F000C };   // PRINTER_ALL_ACCESS
                if (!OpenPrinter(Yazici, out h, ref def))
                    throw new Exception(T("Yazıcı açılamadı") + " (" + T("kod") + " " + Marshal.GetLastWin32Error() + ")");
                int r = SetPrinterData(h, ad, 4, BitConverter.GetBytes(deger), 4);   // 4 = REG_DWORD
                if (r != 0) throw new Exception(T("Kaydedilemedi") + " (" + T("kod") + " " + r + ")");
                txtDurum.Text = T("Kaydedildi.");
            }
            catch (Exception ex)
            {
                txtDurum.Text = T("Hata: ") + ex.Message;
            }
            finally
            {
                if (h != IntPtr.Zero) ClosePrinter(h);
            }
        }

        void SekmeGoster()
        {
            bool aygit = sekmeAygit.IsChecked == true;
            sayfaYazdir.Visibility = aygit ? Visibility.Collapsed : Visibility.Visible;
            sayfaAygit.Visibility = aygit ? Visibility.Visible : Visibility.Collapsed;
        }

        void Sekme_Click(object s, RoutedEventArgs e) => SekmeGoster();

        void Yonetici_Click(object s, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--aygit")
                { UseShellExecute = true, Verb = "runas" });
                Application.Current.Shutdown();
            }
            catch
            {
                txtDurum.Text = T("Yönetici onayı verilmedi.");
            }
        }

        void Durum_Click(object s, RoutedEventArgs e) => AygitYaz("Status", chkDurum.IsChecked == true ? 1 : 0);
        void Sikisma_Click(object s, RoutedEventArgs e) => AygitYaz("JamRecovery", chkSikisma.IsChecked == true ? 1 : 0);
        void Manuel_Click(object s, RoutedEventArgs e) => AygitYaz("ManualDuplex", chkManuel.IsChecked == true ? 1 : 0);
        void Courier_Click(object s, RoutedEventArgs e) => AygitYaz("CourierDark", rdoC1.IsChecked == true ? 1 : 0);
        void Yogunluk_Click(object s, RoutedEventArgs e) => AygitYaz("Density", int.Parse((string)((RadioButton)s).Tag));

        // ---------- Dil (TR / EN) ----------
        // XAML'deki Türkçe metinler özgün metin sayılır; İngilizce karşılıkları aşağıdaki sözlükten gelir.
        bool en;

        static readonly Dictionary<string, string> EN = new()
        {
            ["HP LaserJet P1005 Ayarları"] = "HP LaserJet P1005 Settings",
            ["Değişiklikler anında uygulanır."] = "Changes apply instantly.",
            ["Açık / koyu mod"] = "Light / dark mode",
            ["Yazdırma"] = "Printing",
            ["Aygıt"] = "Device",
            ["Baskı kalitesi"] = "Print quality",
            ["Çözünürlük"] = "Resolution",
            ["FastRes 1200, 600 dpi donanımla 1200 dpi'a yakın kalite verir. 600 dpi daha basit ve biraz daha hızlıdır."] = "FastRes 1200 gives near-1200 dpi quality on 600 dpi hardware. 600 dpi is simpler and slightly faster.",
            ["Daha az toner, daha soluk baskı"] = "Uses less toner, prints lighter",
            ["REt (kenar netleştirme)"] = "REt (edge sharpening)",
            ["Kenarları yumuşatır, genelde açık kalmalı"] = "Smooths edges, usually best left on",
            ["Tüm yazıları siyah yazdır"] = "Print all text as black",
            ["Gri ve renkli yazıları siyaha çevirir"] = "Turns gray and colored text black",
            ["Kağıt ve yön"] = "Paper and orientation",
            ["Kağıt boyutu"] = "Paper size",
            ["Yön"] = "Orientation",
            ["Dikey"] = "Portrait",
            ["Yatay"] = "Landscape",
            ["180° döndür"] = "Rotate 180°",
            ["Sayfayı baş aşağı çevirir"] = "Flips the page upside down",
            ["Ayna yazdırma"] = "Mirror printing",
            ["Sayfayı yatay olarak ters çevirir"] = "Flips the page horizontally",
            ["Düz kağıda döndür"] = "Reset to plain paper",
            ["Kağıt kaynağı, çift taraflı baskı, filigran ve ölçekleme gibi diğer ayarlar için HP'nin eski penceresini açabilirsin."] = "For other settings such as paper source, duplex, watermark and scaling, open HP's original window.",
            ["Eski ayar penceresini aç"] = "Open original settings window",
            ["Yönetici olarak yeniden başlat"] = "Restart as administrator",
            ["Uyarılar ve hata kurtarma"] = "Alerts and error recovery",
            ["Durum iletileri"] = "Status messages",
            ["Kağıt sıkışması, kartuş uyarısı gibi yazıcı mesajlarını ekranda gösterir. Kapalıyken hiçbiri çıkmaz, gerçek bir sıkışmayı da görmezsin."] = "Shows printer messages such as paper jams and cartridge warnings on screen. When off, none appear, including real jams.",
            ["Sıkışma düzeltme"] = "Jam recovery",
            ["Sıkışmadan sonra kaybolan sayfaları yazıcı yeniden basar"] = "Reprints pages lost after a paper jam",
            ["Elle iki taraflı baskıya izin ver"] = "Allow manual duplex printing",
            ["Yazdırma pencerelerinde elle çift taraflı seçeneğini gösterir"] = "Shows the manual duplex option in print dialogs",
            ["Baskı görünümü"] = "Print appearance",
            ["Courier yazı tipi"] = "Courier font",
            ["Koyu"] = "Dark",
            ["Baskı yoğunluğu"] = "Print density",
            ["1 daha açık, 5 daha koyu baskı verir (toner yoğunluğu)."] = "1 prints lighter, 5 prints darker (toner density).",
            // Koddan gelen metinler
            ["Kağıt türü: düz kağıt"] = "Paper type: plain paper",
            ["Kağıt türü düz kağıt değil"] = "Paper type is not plain paper",
            ["kod"] = "code",
            ["Ayarlar okunamadı: "] = "Could not read settings: ",
            ["Hata: "] = "Error: ",
            ["Kaydedildi."] = "Saved.",
            ["Yönetici onayı verilmedi."] = "Administrator approval was not given.",
            ["Yazıcı açılamadı"] = "Could not open printer",
            ["Kaydedilemedi"] = "Could not save",
            ["✓ Yönetici olarak çalışıyor. Aygıt ayarlarını değiştirebilirsin."] = "✓ Running as administrator. You can change device settings.",
            ["Bu ayarları değiştirmek için yönetici yetkisi gerekir. Şimdilik yalnızca görüntüleyebilirsin."] = "Changing these settings requires administrator rights. You can only view them for now.",
        };

        string T(string tr) => en && EN.TryGetValue(tr, out var e) ? e : tr;

        static readonly HashSet<string> Dinamik = new() { "txtTur", "txtYonetici", "txtDurum" };
        readonly Dictionary<(object, string), string> asil = new();

        string Cev(object el, string alan, string simdi)
        {
            if (!asil.TryGetValue((el, alan), out var tr)) asil[(el, alan)] = tr = simdi;
            return T(tr);
        }

        void Cevir(DependencyObject o)
        {
            if (o is TextBlock tb)
            {
                if (!Dinamik.Contains(tb.Name)) tb.Text = Cev(tb, "T", tb.Text);
            }
            else if (o is ContentControl cc && cc.Content is string s)
            {
                cc.Content = Cev(cc, "C", s);
            }
            if (o is FrameworkElement fe && fe.ToolTip is string tt) fe.ToolTip = Cev(fe, "TT", tt);

            foreach (var c in LogicalTreeHelper.GetChildren(o))
                if (c is DependencyObject d) Cevir(d);
        }

        void DilUygula()
        {
            Title = Cev(this, "Title", Title);
            Cevir(this);
            txtDurum.Text = T("Değişiklikler anında uygulanır.");
            Yukle();   // koddan yazılan metinleri de yenile
        }

        void Dil_Click(object s, RoutedEventArgs e)
        {
            en = rdoEN.IsChecked == true;
            DilUygula();
        }

        // ---------- Olaylar ----------
        void Econo_Click(object s, RoutedEventArgs e) => Uygula(d => Bayrak(d, OfsBayrak, Econo, chkEcono.IsChecked));
        void Ret_Click(object s, RoutedEventArgs e) => Uygula(d => Bayrak(d, OfsBayrak, Ret, chkRet.IsChecked));
        void Siyah_Click(object s, RoutedEventArgs e) => Uygula(d => Bayrak(d, OfsBayrak, Siyah, chkSiyah.IsChecked));
        void Dondur_Click(object s, RoutedEventArgs e) => Uygula(d => Bayrak(d, OfsBayrak, Dondur, chkDondur.IsChecked));
        void Ayna_Click(object s, RoutedEventArgs e) => Uygula(d => Bayrak(d, OfsAyna, 0x01, chkAyna.IsChecked));

        void Kalite_Click(object s, RoutedEventArgs e)
        {
            byte v = (byte)(rdoK1.IsChecked == true ? 1 : 0);
            Uygula(d => YazBayt(d, OfsKalite, v));
        }

        void Boyut_Click(object s, RoutedEventArgs e)
        {
            var k = Kagitlar[int.Parse((string)((RadioButton)s).Tag)];
            Uygula(d =>
            {
                Yaz16(d, OfsKagit, k.kod);
                Yaz16(d, OfsBoy, k.boy);
                Yaz16(d, OfsEn, k.en);
            });
        }

        void Yon_Click(object s, RoutedEventArgs e)
        {
            int yon = rdoYatay.IsChecked == true ? 2 : 1;
            Uygula(d => Yaz16(d, OfsYon, yon));
        }

        void Duz_Click(object s, RoutedEventArgs e)
        {
            Uygula(d => YazOzel16(d, OfsTur, 1));
            Yukle();
        }

        void Eski_Click(object s, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("rundll32.exe",
                "printui.dll,PrintUIEntry /e /n \"" + Yazici + "\"") { UseShellExecute = true });
        }
    }
}

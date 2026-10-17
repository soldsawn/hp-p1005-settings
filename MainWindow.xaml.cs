using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using static YaziciKesif.P1005DevMode;

namespace YaziciKesif
{
    public partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        // Ayarlanan yazıcı: sürücüsü HP LaserJet P1005 olan kurulu yazıcılardan biri; hiç yoksa null.
        string? yazici;
        // Ayar bloğu bu uygulamanın tanıdığı sürücüye ait mi (P1005DevMode.Desteklenir). Değilse
        // Yazdırma sekmesi yalnızca görüntülenir: yanlış konuma yazmak ayarları bozar.
        bool destek;
        bool yukleniyor;
        readonly RadioButton[] boyutRdo;

        public MainWindow()
        {
            InitializeComponent();
            boyutRdo = new[] { rdoA4, rdoLetter, rdoLegal, rdoA5 };
            // Kaydedilmiş dil, yoksa Windows'un dili
            var dil = YaziciDeposu.Tercih("Dil");
            en = dil != null ? dil == "en" : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "tr";
            var tema = YaziciDeposu.Tercih("Tema");
            temaTercihi = tema == "acik" ? true : tema == "koyu" ? false : null;
            Loaded += (s, e) =>
            {
                Tema();
                rdoTR.IsChecked = !en;
                rdoEN.IsChecked = en;
                YaziciSec();
                DilUygula();
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "--aygit") >= 0)
                {
                    sekmeAygit.IsChecked = true;
                    SekmeGoster();
                }
            };
            Activated += (s, e) => { Tema(); Yukle(); };   // eski pencereden dönünce yenile
            // Onay kartı açıkken Esc onu kapatır, bir pencere iletisinde olduğu gibi.
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && onayKatmani.Visibility == Visibility.Visible) { OnayKapat(); e.Handled = true; }
            };
        }

        static string? Arguman(string ad)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, ad);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
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

        // Düğmeyle yapılan seçim kaydedilir; hiç seçilmediyse Windows'un ayarı izlenir.
        bool? temaTercihi;
        bool acikTema = true;

        void Tema_Click(object s, RoutedEventArgs e)
        {
            temaTercihi = !acikTema;
            YaziciDeposu.TercihYaz("Tema", temaTercihi == true ? "acik" : "koyu");
            Tema();
        }

        void Tema()
        {
            bool acik = acikTema = temaTercihi ?? AcikTema();
            simgeAy.Visibility = acik ? Visibility.Visible : Visibility.Collapsed;
            simgeGunes.Visibility = acik ? Visibility.Collapsed : Visibility.Visible;
            string[] ad = { "Bg", "Card", "Text", "Muted", "Line", "Accent", "Track", "Warn" };
            string[] renk = acik
                ? new[] { "#F3F5F7", "#FFFFFF", "#1B2430", "#5F6B7A", "#DDE3EA", "#0A7CC2", "#C3CCD6", "#B4690E" }
                : new[] { "#14181D", "#1E242B", "#E8EDF2", "#98A4B3", "#2D3640", "#3BA7E8", "#3A444F", "#E8A23B" };
            for (int i = 0; i < ad.Length; i++)
                Resources[ad[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(renk[i]));

            var h = new WindowInteropHelper(this).Handle;
            if (h != IntPtr.Zero)
            {
                int koyu = acik ? 0 : 1;
                DwmSetWindowAttribute(h, 20, ref koyu, sizeof(int));   // koyu başlık çubuğu
            }
        }

        // ---------- Yazıcı seçimi ----------
        // Windows aynı yazıcıyı ikinci kez kurunca "HP LaserJet P1005 (Kopya 1)" gibi bir ad verir:
        // sürücüsü P1005 olan her yazıcı listelenir, birden fazlaysa başlığın altında seçilir.
        void YaziciSec()
        {
            var liste = YaziciDeposu.P1005Yazicilari();
            yukleniyor = true;
            cmbYazici.ItemsSource = liste;
            cmbYazici.Visibility = liste.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            var istenen = Arguman("--yazici") ?? YaziciDeposu.Tercih("Yazici");
            yazici = liste.Count == 0 ? null : istenen != null && liste.Contains(istenen) ? istenen : liste[0];
            cmbYazici.SelectedItem = yazici;
            yukleniyor = false;
        }

        void Yazici_Changed(object s, SelectionChangedEventArgs e)
        {
            if (yukleniyor || cmbYazici.SelectedItem is not string ad) return;
            yazici = ad;
            YaziciDeposu.TercihYaz("Yazici", ad);
            Yukle();
        }

        // ---------- Okuma ----------

        static void Kilit(FrameworkElement e, bool acik)
        {
            e.IsEnabled = acik;
            e.Opacity = acik ? 1.0 : 0.5;
        }

        void Uyari(string? metin)
        {
            txtUyari.Text = metin ?? "";
            txtUyari.Visibility = metin == null ? Visibility.Collapsed : Visibility.Visible;
        }

        void Yukle()
        {
            if (yazici == null)
            {
                destek = false;
                Kilit(sayfaYazdir, false);
                Kilit(sayfaAygit, false);
                btnGeriDon.IsEnabled = false;
                Uyari(T("HP LaserJet P1005 bulunamadı. Yazıcının kurulu olduğundan emin ol."));
                return;
            }
            Kilit(sayfaAygit, true);
            string? uyari = null;
            try
            {
                yukleniyor = true;
                var d = YaziciDeposu.Oku(yazici);
                destek = Desteklenir(d, out var neden);
                if (d == null)
                    uyari = T("Bu yazıcının yazdırma ayarları henüz oluşmamış. \"Yazdırma ayar penceresini aç\" ile bir kez açıp Tamam'a bas.");
                else if (!destek)
                    uyari = T("Bu sürücü sürümü desteklenmiyor, yazdırma ayarları yalnızca görüntülenir")
                          + $" ({T("beklenen")} {BeklenenSurum}/{BeklenenBoyut}+{BeklenenEkBoyut}, {T("bulunan")} {neden}).";
                Kilit(sayfaYazdir, destek);

                // Desteklenmeyen ama yeterince uzun bir blok yine de gösterilir, yalnızca yazılmaz.
                if (d != null && d.Length > OfsKalite)
                {
                    rdoK0.IsChecked = d[OfsKalite] == 0;
                    rdoK1.IsChecked = d[OfsKalite] != 0;
                    chkEcono.IsChecked = BitAcik(d, OfsBayrak, Econo);
                    chkRet.IsChecked = BitAcik(d, OfsBayrak, Ret);
                    chkSiyah.IsChecked = BitAcik(d, OfsBayrak, Siyah);
                    chkDondur.IsChecked = BitAcik(d, OfsBayrak, Dondur);
                    chkAyna.IsChecked = BitAcik(d, OfsAyna, Ayna);

                    int kagit = Oku16(d, OfsKagit);
                    for (int i = 0; i < Kagitlar.Length; i++) boyutRdo[i].IsChecked = Kagitlar[i].kod == kagit;

                    bool yatay = Oku16(d, OfsYon) == 2;
                    rdoYatay.IsChecked = yatay;
                    rdoDikey.IsChecked = !yatay;

                    int tur = Oku16(d, OfsTur);
                    txtTur.Text = tur == 1 ? T("Kağıt türü: düz kağıt")
                                           : T("Kağıt türü düz kağıt değil") + " (" + T("kod") + " " + tur + ").";
                    btnDuz.Visibility = tur == 1 ? Visibility.Collapsed : Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                txtDurum.Text = T("Ayarlar okunamadı: ") + ex.Message;
            }
            finally
            {
                yukleniyor = false;
            }
            try { YukleAygit(); }
            catch (Exception ex) { txtDurum.Text = T("Ayarlar okunamadı: ") + ex.Message; }
            btnGeriDon.IsEnabled = YaziciDeposu.YedekVar(yazici);
            Uyari(uyari);
        }

        // ---------- Yazma ----------

        void Kaydedildi()
        {
            txtDurum.Text = T("Kaydedildi.");
            btnGeriDon.IsEnabled = true;
        }

        void Uygula(Action<byte[]> degistir)
        {
            if (yukleniyor || yazici == null || !destek) return;
            try
            {
                var dm = YaziciDeposu.Oku(yazici) ?? throw new Exception(T("Yazdırma ayarları bulunamadı."));
                // Pencere açıkken sürücü değişmiş olabilir: her yazmadan önce yeniden bakılır.
                if (!Desteklenir(dm, out _)) throw new Exception(T("Sürücü sürümü desteklenmiyor, yazılmadı."));
                YaziciDeposu.YedekAlEgerYoksa(yazici);
                degistir(dm);
                YaziciDeposu.Yaz(yazici, dm);
                Kaydedildi();
            }
            catch (Exception ex)
            {
                txtDurum.Text = T("Hata: ") + ex.Message;
            }
        }

        // ---------- Aygıt ayarları (makine geneli, yönetici yetkisi ister) ----------

        void YukleAygit()
        {
            if (yazici == null) return;
            chkDurum.IsChecked = YaziciDeposu.Dword(yazici, "Status", 0) != 0;
            chkSikisma.IsChecked = YaziciDeposu.Dword(yazici, "JamRecovery", 0) != 0;
            chkManuel.IsChecked = YaziciDeposu.Dword(yazici, "ManualDuplex", 0) != 0;

            bool koyu = YaziciDeposu.Dword(yazici, "CourierDark", 0) != 0;
            rdoC0.IsChecked = !koyu;
            rdoC1.IsChecked = koyu;

            int yog = YaziciDeposu.Dword(yazici, "Density", 3);
            foreach (var r in new[] { rdoY1, rdoY2, rdoY3, rdoY4, rdoY5 })
                r.IsChecked = int.Parse((string)r.Tag) == yog;

            bool yonetici = YaziciDeposu.Yonetici();
            txtYonetici.Text = yonetici
                ? T("✓ Yönetici olarak çalışıyor. Aygıt ayarlarını değiştirebilirsin.")
                : T("Bu ayarları değiştirmek için yönetici yetkisi gerekir. Şimdilik yalnızca görüntüleyebilirsin.");
            btnYonetici.Visibility = yonetici ? Visibility.Collapsed : Visibility.Visible;
            Kilit(kartlarAygit, yonetici);
        }

        /// <summary>Bir aygıt ayarını yazar; başarısızsa nedenini fırlatır.</summary>
        void AygitYazVeyaHata(string ad, int deger)
        {
            int r = YaziciDeposu.AygitYaz(yazici!, ad, deger);
            if (r < 0) throw new Exception(T("Yazıcı açılamadı") + " (" + T("kod") + " " + -r + ")");
            if (r != 0) throw new Exception(T("Kaydedilemedi") + " (" + T("kod") + " " + r + ")");
        }

        void AygitYaz(string ad, int deger)
        {
            if (yazici == null) return;
            try
            {
                YaziciDeposu.YedekAlEgerYoksa(yazici);
                AygitYazVeyaHata(ad, deger);
                Kaydedildi();
            }
            catch (Exception ex)
            {
                txtDurum.Text = T("Hata: ") + ex.Message;
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
                // Seçili yazıcı yönetici olarak açılan pencerede de seçili kalsın.
                var arg = "--aygit" + (yazici != null ? " --yazici \"" + yazici + "\"" : "");
                Process.Start(new ProcessStartInfo(Environment.ProcessPath!, arg)
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

        // ---------- İlk ayarlara geri dönme ----------
        // Yedek, bu uygulama bir yazıcıya ilk kez yazmadan hemen önce alınır (YaziciDeposu).
        // Yazdırma ayarları her zaman geri yüklenir; aygıt ayarları yalnızca yönetici olarak.
        // Önce pencerenin içindeki onay kartı açılır (onayKatmani); geri yükleme "Geri dön" ile olur.
        void GeriDon_Click(object s, RoutedEventArgs e)
        {
            if (yazici == null) return;
            var y = YaziciDeposu.YedekOku(yazici);
            if (y == null) return;
            txtOnayMetin.Text = T("Bu uygulamayla yaptığın tüm değişiklikler geri alınır, yazıcı eski ayarlarına döner.");
            txtOnayTarih.Text = T("Eski ayarların kaydedildiği tarih: ") +y.Tarih.ToString("g", en ? CultureInfo.GetCultureInfo("en-US") : CultureInfo.GetCultureInfo("tr-TR"));
            onayKatmani.Visibility = Visibility.Visible;
            btnOnayEvet.Focus();
        }

        void OnayKapat() => onayKatmani.Visibility = Visibility.Collapsed;

        void OnayHayir_Click(object s, RoutedEventArgs e) => OnayKapat();

        void OnayEvet_Click(object s, RoutedEventArgs e)
        {
            OnayKapat();
            if (yazici == null) return;
            var y = YaziciDeposu.YedekOku(yazici);
            if (y == null) return;
            try
            {
                if (y.DevMode != null) YaziciDeposu.Yaz(yazici, Convert.FromBase64String(y.DevMode));
                else YaziciDeposu.Sil(yazici);

                bool aygitFarkli = false;
                foreach (var (ad, varsayilan) in YaziciDeposu.AygitAyarlari)
                {
                    if (!y.Aygit.TryGetValue(ad, out var v) || YaziciDeposu.Dword(yazici, ad, varsayilan) == v) continue;
                    if (YaziciDeposu.Yonetici()) AygitYazVeyaHata(ad, v);
                    else aygitFarkli = true;
                }
                Yukle();
                txtDurum.Text = T("İlk ayarlara dönüldü.") + (aygitFarkli ? " " + T("Aygıt ayarları için yönetici olarak yeniden başlatıp tekrar dene.") : "");
            }
            catch (Exception ex)
            {
                txtDurum.Text = T("Hata: ") + ex.Message;
            }
        }

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
            ["Yazdırma ayar penceresini aç"] = "Open print settings window",
            ["Özellik ayar penceresini aç"] = "Open properties window",
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
            ["İlk ayarlara geri dön"] = "Restore original settings",
            ["Bu uygulamayla yaptığın değişiklikleri geri alır"] = "Undoes the changes you made with this app",
            ["Vazgeç"] = "Cancel",
            ["Geri dön"] = "Restore",
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
            ["HP LaserJet P1005 bulunamadı. Yazıcının kurulu olduğundan emin ol."] = "No HP LaserJet P1005 printer found. Make sure the printer is installed.",
            ["Bu yazıcının yazdırma ayarları henüz oluşmamış. \"Yazdırma ayar penceresini aç\" ile bir kez açıp Tamam'a bas."] = "This printer has no print settings yet. Open them once with \"Open print settings window\" and press OK.",
            ["Bu sürücü sürümü desteklenmiyor, yazdırma ayarları yalnızca görüntülenir"] = "This driver version is not supported, print settings are shown read-only",
            ["beklenen"] = "expected",
            ["bulunan"] = "found",
            ["Yazdırma ayarları bulunamadı."] = "Print settings not found.",
            ["Sürücü sürümü desteklenmiyor, yazılmadı."] = "Driver version not supported, nothing was written.",
            ["Bu uygulamayla yaptığın tüm değişiklikler geri alınır, yazıcı eski ayarlarına döner."] = "All changes you made with this app are undone and the printer goes back to its earlier settings.",
            ["Eski ayarların kaydedildiği tarih: "] = "Earlier settings saved on: ",
            ["İlk ayarlara dönüldü."] = "Original settings restored.",
            ["Aygıt ayarları için yönetici olarak yeniden başlatıp tekrar dene."] = "For the device settings, restart as administrator and try again.",
        };

        string T(string tr) => en && EN.TryGetValue(tr, out var e) ? e : tr;

        static readonly HashSet<string> Dinamik = new() { "txtTur", "txtYonetici", "txtDurum", "txtUyari", "txtOnayMetin", "txtOnayTarih" };
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
            YaziciDeposu.TercihYaz("Dil", en ? "en" : "tr");
            DilUygula();
        }

        // ---------- Olaylar ----------
        void Econo_Click(object s, RoutedEventArgs e) => Uygula(d => Bayrak(d, OfsBayrak, Econo, chkEcono.IsChecked == true));
        void Ret_Click(object s, RoutedEventArgs e) => Uygula(d => Bayrak(d, OfsBayrak, Ret, chkRet.IsChecked == true));
        void Siyah_Click(object s, RoutedEventArgs e) => Uygula(d => Bayrak(d, OfsBayrak, Siyah, chkSiyah.IsChecked == true));
        void Dondur_Click(object s, RoutedEventArgs e) => Uygula(d => Bayrak(d, OfsBayrak, Dondur, chkDondur.IsChecked == true));
        void Ayna_Click(object s, RoutedEventArgs e) => Uygula(d => Bayrak(d, OfsAyna, Ayna, chkAyna.IsChecked == true));

        void Kalite_Click(object s, RoutedEventArgs e)
        {
            byte v = (byte)(rdoK1.IsChecked == true ? 1 : 0);
            Uygula(d => YazBayt(d, OfsKalite, v));
        }

        void Boyut_Click(object s, RoutedEventArgs e)
        {
            int sira = int.Parse((string)((RadioButton)s).Tag);
            Uygula(d => KagitYaz(d, sira));
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

        // HP'nin eski pencereleri, seçili yazıcı için
        void HpPenceresi(string anahtar)
        {
            if (yazici == null) return;
            Process.Start(new ProcessStartInfo("rundll32.exe",
                "printui.dll,PrintUIEntry " + anahtar + " /n \"" + yazici + "\"")
            { UseShellExecute = true });
        }

        void Eski_Click(object s, RoutedEventArgs e) => HpPenceresi("/e");
        void Ozellik_Click(object s, RoutedEventArgs e) => HpPenceresi("/p");
    }
}

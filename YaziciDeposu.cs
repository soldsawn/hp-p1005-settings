using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;

namespace YaziciKesif
{
    /// <summary>
    /// Kayıt defteri ve yazdırma biriktiricisiyle konuşan her şey: P1005 yazıcılarını bulmak,
    /// ayar bloğunu okuyup yazmak, aygıt ayarları, ilk yazmadan önceki yedek ve uygulama tercihleri.
    /// </summary>
    public static class YaziciDeposu
    {
        public const string Surucu = "HP LaserJet P1005";
        const string YazicilarYolu = @"SYSTEM\CurrentControlSet\Control\Print\Printers";
        const string KullaniciYolu = @"Printers\DevModePerUser";
        const string TercihYolu = @"Software\YaziciKesif";

        // Aygıt sekmesinin ayarları ve kayıt defterinde yokken geçerli değerleri
        public static readonly (string ad, int varsayilan)[] AygitAyarlari =
        {
            ("Status", 0), ("JamRecovery", 0), ("ManualDuplex", 0), ("CourierDark", 0), ("Density", 3),
        };

        // ---------- Yazıcılar ----------

        /// <summary>Sürücüsü HP LaserJet P1005 olan kurulu yazıcılar ("… (Kopya 1)" gibi adlar dahil).</summary>
        public static List<string> P1005Yazicilari()
        {
            var bulunan = new List<string>();
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(YazicilarYolu);
                foreach (var ad in k?.GetSubKeyNames() ?? Array.Empty<string>())
                {
                    using var y = k!.OpenSubKey(ad);
                    if (string.Equals(y?.GetValue("Printer Driver") as string, Surucu, StringComparison.OrdinalIgnoreCase))
                        bulunan.Add(ad);
                }
            }
            catch { /* okunamayan kayıt: yazıcı yok sayılır */ }
            bulunan.Sort(StringComparer.CurrentCultureIgnoreCase);
            return bulunan;
        }

        // ---------- Yazdırma ayarları (kullanıcıya özel) ----------

        /// <summary>Yazıcının kullanıcı ayar bloğu; hiç ayarlanmamışsa null.</summary>
        public static byte[]? Oku(string yazici)
        {
            using var k = Registry.CurrentUser.OpenSubKey(KullaniciYolu);
            return k?.GetValue(yazici) as byte[];
        }

        // Windows'un SetPrinter yolu sürücü kodunu çağırıp belleği bozuyordu (çökme).
        // Bu yüzden kullanıcı ayar bloğu doğrudan kayıt defterine yazılır.
        public static void Yaz(string yazici, byte[] d)
        {
            using var k = Registry.CurrentUser.CreateSubKey(KullaniciYolu, true);
            k.SetValue(yazici, d, RegistryValueKind.Binary);
        }

        /// <summary>Kullanıcı ayarını kaldırır: yazıcı yeniden sürücünün varsayılanlarıyla başlar.</summary>
        public static void Sil(string yazici)
        {
            using var k = Registry.CurrentUser.OpenSubKey(KullaniciYolu, true);
            k?.DeleteValue(yazici, false);
        }

        // ---------- Aygıt ayarları (makine geneli, yönetici yetkisi ister) ----------

        static string AygitYolu(string yazici) => $@"{YazicilarYolu}\{yazici}\PrinterDriverData";

        public static int Dword(string yazici, string ad, int varsayilan)
        {
            using var k = Registry.LocalMachine.OpenSubKey(AygitYolu(yazici));
            return Convert.ToInt32(k?.GetValue(ad) ?? varsayilan);
        }

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

        /// <summary>Hata kodu döner: 0 başarı; açılamazsa -(Win32 hata kodu).</summary>
        public static int AygitYaz(string yazici, string ad, int deger)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                var def = new PRINTER_DEFAULTS { DesiredAccess = 0x000F000C };   // PRINTER_ALL_ACCESS
                if (!OpenPrinter(yazici, out h, ref def)) return -Marshal.GetLastWin32Error();
                return SetPrinterData(h, ad, 4, BitConverter.GetBytes(deger), 4);   // 4 = REG_DWORD
            }
            finally
            {
                if (h != IntPtr.Zero) ClosePrinter(h);
            }
        }

        public static bool Yonetici()
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        // ---------- İlk yazmadan önceki yedek ----------
        // Bir yazıcıya bu uygulamayla ilk kez yazılmadan hemen önce alınır ve bir daha üzerine
        // yazılmaz: "İlk ayarlara geri dön" hep uygulamadan önceki hale döner.

        public sealed class Yedek
        {
            public string Yazici { get; set; } = "";
            public DateTime Tarih { get; set; }
            public string? DevMode { get; set; }                 // base64; yazıcı hiç ayarlanmamışsa null
            public Dictionary<string, int> Aygit { get; set; } = new();
        }

        static string YedekKlasoru => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YaziciKesif");

        static string YedekDosyasi(string yazici)
        {
            var ad = string.Concat(yazici.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            return Path.Combine(YedekKlasoru, $"yedek-{ad}.json");
        }

        public static bool YedekVar(string yazici) => File.Exists(YedekDosyasi(yazici));

        public static void YedekAlEgerYoksa(string yazici)
        {
            var dosya = YedekDosyasi(yazici);
            if (File.Exists(dosya)) return;
            var y = new Yedek { Yazici = yazici, Tarih = DateTime.Now };
            var d = Oku(yazici);
            if (d != null) y.DevMode = Convert.ToBase64String(d);
            foreach (var (ad, varsayilan) in AygitAyarlari) y.Aygit[ad] = Dword(yazici, ad, varsayilan);
            Directory.CreateDirectory(YedekKlasoru);
            File.WriteAllText(dosya, JsonSerializer.Serialize(y, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static Yedek? YedekOku(string yazici)
        {
            var dosya = YedekDosyasi(yazici);
            return File.Exists(dosya) ? JsonSerializer.Deserialize<Yedek>(File.ReadAllText(dosya)) : null;
        }

        // ---------- Uygulama tercihleri ----------

        public static string? Tercih(string ad)
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(TercihYolu);
                return k?.GetValue(ad) as string;
            }
            catch { return null; }
        }

        public static void TercihYaz(string ad, string deger)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(TercihYolu, true);
                k.SetValue(ad, deger, RegistryValueKind.String);
            }
            catch { /* tercih kaydedilemezse oturum boyunca geçerli kalır */ }
        }
    }
}

namespace YaziciKesif
{
    /// <summary>
    /// HP LaserJet P1005 sürücüsünün kullanıcı ayar bloğu (DEVMODE + sürücüye özel kısım).
    /// Yalnızca baytlarla çalışır, kayıt defterine dokunmaz; bu yüzden test edilebilir.
    /// Konumlar, orijinal pencerelerde bir seçeneği değiştirip blokları karşılaştırarak bulundu.
    /// </summary>
    public static class P1005DevMode
    {
        // Sürücüye özel alan konumları (deneylerle bulundu)
        public const int OfsBayrak = 604;   // bit: 0x02=180°, 0x04=REt, 0x08=EconoMode, 0x10=Tüm yazıları siyah
        public const int OfsAyna = 605;     // bit 0x01 = ayna yazdırma
        public const int OfsKalite = 912;   // 0 = FastRes 1200, 1 = 600 dpi
        public const int OfsTur = 296;      // 16 bit: 1 = düz kağıt
        public const int OfsToplam = 588;   // 16 bit sağlama toplamı (sürücüye özel kısmın)
        public const byte Dondur = 0x02, Ret = 0x04, Econo = 0x08, Siyah = 0x10, Ayna = 0x01;

        // Standart DEVMODE alanları
        public const int OfsSurucuSurumu = 66, OfsBoyut = 68, OfsEkBoyut = 70;
        public const int OfsYon = 76, OfsKagit = 78, OfsBoy = 80, OfsEn = 82;

        // Konumların doğrulandığı sürücü: HP'nin 2013 sürücüsü (1.0.2.2680). Başka bir sürümde
        // aynı konumlar başka şeyler olabilir; o zaman yazmak ayarları bozar.
        public const int BeklenenSurum = 3, BeklenenBoyut = 220, BeklenenEkBoyut = 821;

        public static readonly (string ad, int kod, int boy, int en)[] Kagitlar =
        {
            ("A4", 9, 2970, 2100),
            ("Letter", 1, 2794, 2159),
            ("Legal", 5, 3556, 2159),
            ("A5", 11, 2100, 1480),
        };

        public static int Oku16(byte[] b, int o) => BitConverter.ToInt16(b, o);

        public static void Yaz16(byte[] b, int o, int v)
        {
            b[o] = (byte)v;
            b[o + 1] = (byte)(v >> 8);
        }

        /// <summary>
        /// Blok bu uygulamanın tanıdığı sürücüye mi ait? Değilse <paramref name="neden"/> nedenini söyler.
        /// Uzunluk, başlıktaki boyutlar ve sürücü sürümü birlikte kontrol edilir.
        /// </summary>
        public static bool Desteklenir(byte[]? d, out string neden)
        {
            neden = "";
            if (d == null || d.Length < OfsKalite + 1)
            {
                neden = $"{d?.Length ?? 0} bayt";
                return false;
            }
            int surum = (ushort)Oku16(d, OfsSurucuSurumu);
            int boyut = (ushort)Oku16(d, OfsBoyut);
            int ek = (ushort)Oku16(d, OfsEkBoyut);
            if (surum != BeklenenSurum || boyut != BeklenenBoyut || ek != BeklenenEkBoyut || d.Length != boyut + ek)
            {
                neden = $"{surum}/{boyut}+{ek}, {d.Length} bayt";
                return false;
            }
            return true;
        }

        // ---------- Sürücüye özel kısma yazma: sağlama toplamı birlikte güncellenir ----------

        static void ToplamEkle(byte[] d, int fark)
        {
            int t = (d[OfsToplam] | (d[OfsToplam + 1] << 8)) + fark;
            d[OfsToplam] = (byte)t;
            d[OfsToplam + 1] = (byte)(t >> 8);
        }

        /// <summary>Bir baytı yazar; toplam 16 bitlik kelimeler üzerinden olduğu için tek konumdaki bayt üst yarıya düşer.</summary>
        public static void YazBayt(byte[] d, int ofs, byte yeni)
        {
            int fark = yeni - d[ofs];
            d[ofs] = yeni;
            ToplamEkle(d, ofs % 2 == 0 ? fark : fark << 8);
        }

        public static void YazOzel16(byte[] d, int ofs, int yeni)
        {
            int eski = (ushort)BitConverter.ToInt16(d, ofs);
            Yaz16(d, ofs, yeni);
            ToplamEkle(d, yeni - eski);
        }

        public static bool BitAcik(byte[] d, int ofs, byte bit) => (d[ofs] & bit) != 0;

        public static void Bayrak(byte[] d, int ofs, byte bit, bool acik)
        {
            byte yeni = acik ? (byte)(d[ofs] | bit) : (byte)(d[ofs] & ~bit);
            YazBayt(d, ofs, yeni);
        }

        /// <summary>Kağıt boyutu standart DEVMODE alanlarındadır; sağlama toplamı bu kısmı kapsamaz.</summary>
        public static void KagitYaz(byte[] d, int sira)
        {
            var k = Kagitlar[sira];
            Yaz16(d, OfsKagit, k.kod);
            Yaz16(d, OfsBoy, k.boy);
            Yaz16(d, OfsEn, k.en);
        }

        /// <summary>Sağlama toplamı alanındaki değer (16 bit).</summary>
        public static int ToplamOku(byte[] d) => d[OfsToplam] | (d[OfsToplam + 1] << 8);
    }
}

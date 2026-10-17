using YaziciKesif;
using static YaziciKesif.P1005DevMode;

namespace YaziciKesif.Tests;

// The byte logic behind every write the app makes. The sample is a real DEVMODE block of an
// HP LaserJet P1005 with the 2013 driver (TestData/p1005-devmode.bin). The driver's checksum
// algorithm itself is not known; the app keeps it right by adding each change's difference,
// and these tests hold that bookkeeping to what it does on the real block.
public class P1005DevModeTests
{
    static byte[] Ornek() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", "p1005-devmode.bin"));

    [Fact]
    public void Gercek_blok_desteklenir()
    {
        Assert.True(Desteklenir(Ornek(), out var neden), neden);
    }

    [Fact]
    public void Kisa_ya_da_bos_blok_reddedilir()
    {
        Assert.False(Desteklenir(null, out _));
        Assert.False(Desteklenir(Ornek()[..600], out var neden));
        Assert.Contains("600", neden);
    }

    [Theory]
    [InlineData(OfsSurucuSurumu, 4)]     // another driver version
    [InlineData(OfsEkBoyut, 820)]        // a driver-private part of another size
    public void Baska_surucunun_blogu_reddedilir(int ofs, int deger)
    {
        var d = Ornek();
        Yaz16(d, ofs, deger);
        Assert.False(Desteklenir(d, out _));
    }

    [Fact]
    public void Uzunlugu_basligina_uymayan_blok_reddedilir()
    {
        var d = Ornek().Concat(new byte[2]).ToArray();
        Assert.False(Desteklenir(d, out var neden));
        Assert.Contains("1043", neden);
    }

    [Theory]
    [InlineData(OfsBayrak, Econo)]
    [InlineData(OfsBayrak, Ret)]
    [InlineData(OfsBayrak, Siyah)]
    [InlineData(OfsBayrak, Dondur)]
    [InlineData(OfsAyna, Ayna)]
    public void Bayrak_acip_kapatmak_blogu_aynen_geri_getirir(int ofs, byte bit)
    {
        var asil = Ornek();
        var d = Ornek();
        bool basta = BitAcik(d, ofs, bit);
        Bayrak(d, ofs, bit, !basta);
        Assert.Equal(!basta, BitAcik(d, ofs, bit));
        Assert.NotEqual(asil, d);                        // the checksum moved with it
        Bayrak(d, ofs, bit, basta);
        Assert.Equal(asil, d);                           // every byte back, checksum included
    }

    [Fact]
    public void Cift_konumdaki_bayt_toplami_farki_kadar_tek_konumdaki_256_kati_kadar_degistirir()
    {
        var d = Ornek();
        int toplam = ToplamOku(d);

        YazBayt(d, OfsBayrak, (byte)(d[OfsBayrak] + 3));            // 604, even
        Assert.Equal((toplam + 3) & 0xFFFF, ToplamOku(d));

        toplam = ToplamOku(d);
        YazBayt(d, OfsAyna, (byte)(d[OfsAyna] + 1));                // 605, odd: the word's high byte
        Assert.Equal((toplam + 0x100) & 0xFFFF, ToplamOku(d));
    }

    [Fact]
    public void Kagit_turu_16_bitlik_yazilir_ve_toplama_islenir()
    {
        var d = Ornek();
        int toplam = ToplamOku(d), eski = (ushort)Oku16(d, OfsTur);
        YazOzel16(d, OfsTur, 7);
        Assert.Equal(7, Oku16(d, OfsTur));
        Assert.Equal((toplam + 7 - eski) & 0xFFFF, ToplamOku(d));
        YazOzel16(d, OfsTur, eski);
        Assert.Equal(Ornek(), d);
    }

    [Fact]
    public void Kagit_boyutu_standart_alanlara_yazilir_toplama_dokunmaz()
    {
        var d = Ornek();
        int toplam = ToplamOku(d);
        KagitYaz(d, 3);                                  // A5
        Assert.Equal(11, Oku16(d, OfsKagit));
        Assert.Equal(2100, Oku16(d, OfsBoy));
        Assert.Equal(1480, Oku16(d, OfsEn));
        Assert.Equal(toplam, ToplamOku(d));
    }
}

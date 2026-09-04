namespace YaziciTakip.Configuration;

/// <summary>
/// appsettings.json -> "Hakedis" bölümü. Hakediş belgesinin başlığında görünecek
/// firma bilgileri. Boş bırakılırsa ilgili satır belgede hiç gösterilmez.
/// </summary>
public class HakedisOptions
{
    public const string SectionName = "Hakedis";

    /// <summary>Belgeyi düzenleyen (gönderen) firma adı.</summary>
    public string FromCompany { get; set; } = string.Empty;

    /// <summary>Belgenin gönderileceği marka servis/bayi firması adı.</summary>
    public string ToCompany { get; set; } = string.Empty;
}

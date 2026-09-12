namespace TasmanianDevil;

/// <summary>
/// Well-known country-pack codes for <see cref="PiiOptions.Countries"/>, by ISO 3166-1 alpha-2.
/// These are the opt-in national-identifier packs; the generic recognizers and the US pack are always
/// on and need no code. Plain <c>string</c> constants (not an enum) so the set stays open as packs are
/// added. <c>gb</c> is accepted as an alias for <see cref="Uk"/>.
/// </summary>
public static class PiiCountries
{
    /// <summary>
    /// United States (SSN, ITIN, ABA routing, bank account, driver license, passport, NPI, MBI, DEA).
    /// Always enabled - listing it in <see cref="PiiOptions.Countries"/> is a harmless no-op.
    /// </summary>
    public const string Us = "us";

    /// <summary>United Kingdom (NINO, NHS, postcode, passport, driving licence, vehicle registration).</summary>
    public const string Uk = "uk";

    /// <summary>Germany (ID card, tax ID, passport, PLZ, social security, VAT ID, Führerschein, Kfz, tax number, Handelsregister).</summary>
    public const string De = "de";

    /// <summary>India (Aadhaar, PAN, GSTIN, passport, voter ID, vehicle registration).</summary>
    public const string In = "in";

    /// <summary>Italy (fiscal code, VAT code, driver's license, identity card, passport).</summary>
    public const string It = "it";

    /// <summary>Spain (NIF/DNI, NIE, passport).</summary>
    public const string Es = "es";

    /// <summary>The Netherlands (BSN, postcode, passport).</summary>
    public const string Nl = "nl";
}

namespace TasmanianDevil.Azure;

/// <summary>
/// Default mapping from Azure AI Language PII entity categories to TasmanianDevil's canonical entity
/// types. Override via <see cref="AzurePiiOptions.CategoryMap"/> for a different vocabulary; categories
/// not present in the map pass through unchanged.
/// </summary>
public static class AzurePiiCategoryMap
{
    /// <summary>The built-in Azure category -> canonical entity type mapping.</summary>
    public static readonly IReadOnlyDictionary<string, string> Default = new Dictionary<string, string>
    {
        ["Person"] = PiiEntities.Person,
        ["Address"] = PiiEntities.Address,
        ["PhoneNumber"] = PiiEntities.PhoneNumber,
        ["Email"] = PiiEntities.EmailAddress,
        ["Organization"] = PiiEntities.Organization,
        ["DateTime"] = PiiEntities.DateTime,
        ["CreditCardNumber"] = PiiEntities.CreditCard,
        ["USSocialSecurityNumber"] = PiiEntities.UsSsn,
        ["IPAddress"] = PiiEntities.IpAddress,
        ["IBAN"] = PiiEntities.IbanCode,
        ["URL"] = PiiEntities.Url,
    };
}

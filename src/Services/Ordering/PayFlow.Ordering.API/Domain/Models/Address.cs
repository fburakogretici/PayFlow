namespace PayFlow.Ordering.API.Domain.Models;

/// <summary>
/// DDD Value Object: Kimliği (ID) olmayan, değer eşitliğiyle tanımlanan nesne.
/// Guard Clause ile domain kurallarını constructor seviyesinde uygular.
/// </summary>
public record Address
{
    public string Street { get; init; }
    public string City { get; init; }
    public string Country { get; init; }
    public string ZipCode { get; init; }

    // EF Core için parametresiz constructor (private)
    private Address() { Street = string.Empty; City = string.Empty; Country = string.Empty; ZipCode = string.Empty; }

    public Address(string street, string city, string country, string zipCode)
    {
        // Guard Clauses – Domain invariant koruması
        Street  = Guard(street,  nameof(Street),  "Street cannot be empty.");
        City    = Guard(city,    nameof(City),    "City cannot be empty.");
        Country = Guard(country, nameof(Country), "Country cannot be empty.");
        ZipCode = Guard(zipCode, nameof(ZipCode), "ZipCode cannot be empty.");
    }

    private static string Guard(string value, string fieldName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(message, fieldName);
        return value.Trim();
    }
}

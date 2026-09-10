namespace PayFlow.Ordering.API.Domain.Models;

// DDD Value Object: Kimliği (ID) olmayan, değer eşitliğiyle tanımlanan nesne
public record Address(string Street, string City, string Country, string ZipCode);

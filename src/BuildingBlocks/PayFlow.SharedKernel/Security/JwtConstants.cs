namespace PayFlow.SharedKernel.Security;

public static class JwtConstants
{
    public const string SecretKey = "PayFlow_Super_Secret_Enterprise_Fintech_Key_2026_@999!";
    public const string Issuer = "PayFlow.Identity";
    public const string Audience = "PayFlow.Microservices";
    public const int ExpirationMinutes = 120;
}

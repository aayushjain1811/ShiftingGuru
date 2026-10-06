namespace ShiftingGuru.Services.Auth;

/// <summary>
/// NEW (mobile app): the Identity role for app customers, next to the
/// existing Admin and Vendor roles. Created the first time a customer
/// registers, so AdminSeeder doesn't need to change.
/// </summary>
public static class CustomerRoles
{
    public const string Customer = "Customer";
}
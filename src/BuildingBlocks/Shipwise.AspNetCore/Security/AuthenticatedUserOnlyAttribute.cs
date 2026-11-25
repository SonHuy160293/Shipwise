namespace Shipwise.AspNetCore.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AuthenticatedUserOnlyAttribute : Attribute
{
}

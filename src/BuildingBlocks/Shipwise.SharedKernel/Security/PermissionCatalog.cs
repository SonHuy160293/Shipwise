namespace Shipwise.SharedKernel.Security;

public static class PermissionCatalog
{
    public static string ToValue(string function, ActionType action)
        => $"{function}.{action}";
}

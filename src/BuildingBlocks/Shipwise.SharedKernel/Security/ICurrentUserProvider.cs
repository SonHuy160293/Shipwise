namespace Shipwise.SharedKernel.Security;

public interface ICurrentUserProvider
{
    CurrentUser? GetCurrentUser();
}

using Shipwise.SharedKernel.Abstractions.Message;

namespace Shipwise.Identity.Application.Features.Auth.Queries.GetAccount;

public sealed record GetAccountQuery(Guid UserId) : IQuery<AccountResponse>;

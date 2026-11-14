using Asp.Versioning;
using Shipwise.AspNetCore.Security;
using Shipwise.AspNetCore.Controllers;
using Shipwise.SharedKernel.Abstractions.Shared;
using Shipwise.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Shipwise.AspNetCore.Controllers;

[ApiController]
[ApiVersion(1.0)]
[ServiceFilter(typeof(IntegrationApiKeyFilter))]
[Route("api/v{version:apiVersion}/integrations/[controller]")]
[Produces("application/json")]
public abstract class IntegrationApiController : ControllerBase
{
    protected IntegrationApiController(ISender sender)
    {
        Sender = sender;
    }

    protected ISender Sender { get; }

    protected ActionResult HandleFailure(Result result)
    {
        if (result.Error == Error.None)
        {
            return BadRequest();
        }

        return result.Error.Code switch
        {
            var code when code.EndsWith(".NotFound", StringComparison.Ordinal) => NotFound(result.Error),
            var code when code.Contains("Unauthorized", StringComparison.Ordinal) => Unauthorized(result.Error),
            var code when code.Contains("Forbidden", StringComparison.Ordinal) => StatusCode(StatusCodes.Status403Forbidden, result.Error),
            var code when code.Contains("Conflict", StringComparison.Ordinal) => Conflict(result.Error),
            _ => BadRequest(result.Error),
        };
    }
}

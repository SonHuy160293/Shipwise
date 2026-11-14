using Asp.Versioning;
using Shipwise.SharedKernel.Abstractions.Shared;
using Shipwise.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Shipwise.AspNetCore.Controllers;

[ApiController]
[ApiVersion(1.0)]
//[Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public abstract class ApiController : ControllerBase
{
    protected ApiController(ISender sender)
    {
        Sender = sender;
    }

    protected ISender Sender { get; }

    protected ActionResult HandleFailure(Result result)
        => result.Error == Error.None
            ? BadRequest()
            : BadRequest(result.Error);
}

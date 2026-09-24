using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace ECommerce.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/[controller]")]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class BaseApiController : ControllerBase
{
}

using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

public sealed class HealthController : BaseApiController
{
    [HttpGet]
    public IActionResult Get() => Ok(new { Status = "Healthy" });
}

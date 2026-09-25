using Distributor.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Distributor.Api.Controllers;

[ApiController, Route("api/offline"), Authorize]
public sealed class OfflineController(OfflineData data) : ControllerBase
{
    [HttpGet("snapshot")]
    public async Task<object> Snapshot() => await data.Snapshot(User);
}

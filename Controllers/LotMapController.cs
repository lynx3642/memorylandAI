using Microsoft.AspNetCore.Mvc;
using SmartCem.Models;
using SmartCem.Services;

namespace SmartCem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LotMapController(ILotMapService svc) : ControllerBase
{
    // GET /api/lotmap/overview – toàn bộ zones + lots (cho bản đồ chính)
    [HttpGet("overview")]
    public async Task<IActionResult> Overview()
        => Ok(await svc.GetOverviewAsync());

    // GET /api/lotmap/zones
    [HttpGet("zones")]
    public async Task<IActionResult> GetZones()
        => Ok(await svc.GetAllZoneMapsAsync());

    // GET /api/lotmap/zones/A
    [HttpGet("zones/{code}")]
    public async Task<IActionResult> GetZone(string code)
    {
        var z = await svc.GetZoneMapAsync(code.ToUpper());
        return z is null ? NotFound() : Ok(z);
    }

    // PUT /api/lotmap/zones  (upsert zone map config)
    [HttpPut("zones")]
    public async Task<IActionResult> UpsertZone([FromBody] ZoneMap zm)
        => Ok(await svc.UpsertZoneMapAsync(zm));

    // GET /api/lotmap/lots?zone=A
    [HttpGet("lots")]
    public async Task<IActionResult> GetLots([FromQuery] string? zone)
    {
        if (!string.IsNullOrWhiteSpace(zone))
            return Ok(await svc.GetLotsByZoneAsync(zone.ToUpper()));

        var overview = await svc.GetOverviewAsync();
        return Ok(overview.Lots);
    }

    // GET /api/lotmap/lots/42
    [HttpGet("lots/{id:int}")]
    public async Task<IActionResult> GetLot(int id)
    {
        var l = await svc.GetLotAsync(id);
        return l is null ? NotFound() : Ok(l);
    }

    // GET /api/lotmap/lots/by-code/A-01-01
    [HttpGet("lots/by-code/{code}")]
    public async Task<IActionResult> GetLotByCode(string code)
    {
        var l = await svc.GetLotByCodeAsync(code.ToUpper());
        return l is null ? NotFound() : Ok(l);
    }

    // POST /api/lotmap/lots
    [HttpPost("lots")]
    public async Task<IActionResult> CreateLot([FromBody] CreateLotRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var l = await svc.CreateLotAsync(req);
        return CreatedAtAction(nameof(GetLot), new { id = l.Id }, l);
    }

    // PATCH /api/lotmap/lots/42/position  (drag & drop)
    [HttpPatch("lots/{id:int}/position")]
    public async Task<IActionResult> UpdatePosition(int id, [FromBody] UpdateLotPositionRequest req)
    {
        var l = await svc.UpdateLotPositionAsync(id, req);
        return l is null ? NotFound() : Ok(l);
    }

    // PATCH /api/lotmap/lots/42/assign  (gán/hủy mộ phần)
    [HttpPatch("lots/{id:int}/assign")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignGraveRequest req)
    {
        var l = await svc.AssignGraveAsync(id, req);
        return l is null ? NotFound() : Ok(l);
    }

    // DELETE /api/lotmap/lots/42/unassign
    [HttpDelete("lots/{id:int}/unassign")]
    public async Task<IActionResult> Unassign(int id)
    {
        var l = await svc.UnassignGraveAsync(id);
        return l is null ? NotFound() : Ok(l);
    }

    // DELETE /api/lotmap/lots/42
    [HttpDelete("lots/{id:int}")]
    public async Task<IActionResult> DeleteLot(int id)
    {
        var ok = await svc.DeleteLotAsync(id);
        return ok ? NoContent() : NotFound();
    }

    // POST /api/lotmap/generate-grid  (tự tạo lưới lô theo zone)
    [HttpPost("generate-grid")]
    public async Task<IActionResult> GenerateGrid([FromBody] GenerateGridRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var lots = await svc.GenerateGridAsync(req);
        return Ok(new { message = $"Đã tạo {lots.Count()} lô trong khu {req.ZoneCode}", lots });
    }
}

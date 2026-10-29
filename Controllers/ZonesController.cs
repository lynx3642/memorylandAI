using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCem.Data;
using SmartCem.Models;

namespace SmartCem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ZonesController(SmartCemDb db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var zones = await db.Zones.OrderBy(z => z.Code).ToListAsync();
        var lots = await db.Lots.ToListAsync();

        var result = zones.Select(z => {
            var zLots = lots.Where(l => l.ZoneCode == z.Code).ToList();
            var occ = zLots.Count(l => l.Status == LotStatus.Occupied);
            var emp = zLots.Count(l => l.Status == LotStatus.Empty);
            var res = zLots.Count(l => l.Status == LotStatus.Reserved);
            var total = zLots.Count > 0 ? zLots.Count : z.TotalLots;
            return new {
                z.Id,
                z.Code,
                Name = z.Name ?? $"Khu {z.Code}",
                z.Description,
                TotalLots = total,
                OccupiedLots = occ,
                EmptyLots = emp,
                ReservedLots = res
            };
        });

        return Ok(result);
    }
}


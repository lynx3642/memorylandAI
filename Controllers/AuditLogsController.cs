using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCem.Data;

namespace SmartCem.Controllers;

[ApiController]
[Route("api/audit-logs")]
public class AuditLogsController(SmartCemDb db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? action,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var q = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
            q = q.Where(l => l.Action == action);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(l =>
                l.Content.ToLower().Contains(s) ||
                (l.PerformedBy != null && l.PerformedBy.ToLower().Contains(s)));
        }

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(l => l.At)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new {
                l.Id,
                l.GraveId,
                l.Action,
                l.Content,
                l.PerformedBy,
                l.At
            })
            .ToListAsync();

        return Ok(new {
            items,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }
}


using Microsoft.AspNetCore.Mvc;
using SmartCem.Data;
using SmartCem.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace SmartCem.Controllers;

/// <summary>
/// SSE endpoint – client subscribes và nhận update mỗi khi có import mới
/// hoặc có thay đổi trạng thái lô (occupied / empty)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RealtimeController(SmartCemDb db) : ControllerBase
{
    // Broadcast hub (in-memory, single-server) ─────────────────────────
    private static readonly List<(string Id, TaskCompletionSource<string> Tcs)> _clients = [];
    private static readonly Lock _lock = new();

    /// <summary>
    /// Gọi method này từ ImportService sau mỗi import để push update
    /// </summary>
    public static void Broadcast(string eventType, object payload)
    {
        var json = JsonSerializer.Serialize(new { type = eventType, data = payload, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

        List<(string, TaskCompletionSource<string>)> snapshot;
        lock (_lock) { snapshot = [.._clients]; }

        foreach (var (_, tcs) in snapshot)
            tcs.TrySetResult(json);
    }

    // GET /api/realtime/stream  ──────────────────────────────────────
    [HttpGet("stream")]
    public async Task Stream(CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");
        Response.Headers.Append("Access-Control-Allow-Origin", "*");

        var clientId = Guid.NewGuid().ToString("N");

        // Send initial connection event
        await WriteEvent("connected", new { clientId, message = "SmartCem Realtime stream connected" });

        while (!ct.IsCancellationRequested)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_lock) { _clients.Add((clientId, tcs)); }

            try
            {
                // Wait for event OR heartbeat every 15s
                var winner = await Task.WhenAny(tcs.Task, Task.Delay(15_000, ct));

                if (winner == tcs.Task && tcs.Task.IsCompletedSuccessfully)
                {
                    await Response.WriteAsync($"data: {tcs.Task.Result}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                }
                else
                {
                    // Heartbeat
                    await Response.WriteAsync($": heartbeat\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) { break; }
            finally
            {
                lock (_lock) { _clients.RemoveAll(c => c.Id == clientId); }
            }
        }
    }

    // GET /api/realtime/zone-stats  ──────────────────────────────────
    /// <summary>Snapshot thống kê khu vực cho polling fallback</summary>
    [HttpGet("zone-stats")]
    public async Task<IActionResult> GetZoneStats()
    {
        var zones = await db.ZoneMaps.ToListAsync();
        var lots  = await db.Lots.GroupBy(l => l.ZoneCode)
            .Select(g => new {
                Zone     = g.Key,
                Total    = g.Count(),
                Occupied = g.Count(l => l.Status == LotStatus.Occupied),
                Reserved = g.Count(l => l.Status == LotStatus.Reserved),
                Empty    = g.Count(l => l.Status == LotStatus.Empty)
            }).ToListAsync();

        var result = zones.Select(z => {
            var s = lots.FirstOrDefault(l => l.Zone == z.Code);
            int occ = s?.Occupied ?? 0, res = s?.Reserved ?? 0, emp = s?.Empty ?? 0, total = occ + res + emp;
            return new {
                code     = z.Code,
                label    = z.Label,
                color    = z.Color,
                total,
                occupied = occ,
                reserved = res,
                empty    = emp,
                pctFull  = total > 0 ? Math.Round((double)occ / total * 100, 1) : 0.0
            };
        });

        return Ok(new {
            stats     = result,
            updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            totalLots = lots.Sum(l => l.Total),
            totalOccupied = lots.Sum(l => l.Occupied)
        });
    }

    private async Task WriteEvent(string eventName, object data)
    {
        var json = JsonSerializer.Serialize(data);
        await Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n");
        await Response.Body.FlushAsync();
    }
}

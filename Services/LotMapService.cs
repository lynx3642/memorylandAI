using Microsoft.EntityFrameworkCore;
using SmartCem.Data;
using SmartCem.Models;

namespace SmartCem.Services;

public interface ILotMapService
{
    Task<LotMapOverviewDto> GetOverviewAsync();
    Task<IEnumerable<LotDto>> GetLotsByZoneAsync(string zone);
    Task<LotDto?> GetLotAsync(int id);
    Task<LotDto?> GetLotByCodeAsync(string code);
    Task<LotDto> CreateLotAsync(CreateLotRequest req);
    Task<LotDto?> UpdateLotPositionAsync(int id, UpdateLotPositionRequest req);
    Task<LotDto?> AssignGraveAsync(int id, AssignGraveRequest req);
    Task<LotDto?> UnassignGraveAsync(int id);
    Task<bool> DeleteLotAsync(int id);
    Task<IEnumerable<LotDto>> GenerateGridAsync(GenerateGridRequest req);
    Task<ZoneMapDto?> GetZoneMapAsync(string code);
    Task<IEnumerable<ZoneMapDto>> GetAllZoneMapsAsync();
    Task<ZoneMapDto> UpsertZoneMapAsync(ZoneMap zm);
}

public class LotMapService(SmartCemDb db) : ILotMapService
{
    private static LotDto ToDto(Lot l) => new(
        l.Id, l.LotCode, l.ZoneCode,
        l.X, l.Y, l.W, l.H, l.Rotation,
        l.Status.ToString(), l.GraveId,
        l.Grave?.FullName,
        l.Grave != null ? $"{l.Grave.BornYear ?? 0}–{l.Grave.DiedYear ?? 0}" : null,
        l.Notes, l.Row, l.Col, l.UpdatedAt
    );

    private static ZoneMapDto ToZoneDto(ZoneMap z, int occ, int res, int empty) => new(
        z.Id, z.Code, z.Label, z.X, z.Y, z.W, z.H, z.Color, z.Rows, z.Cols,
        occ + res + empty, occ, empty, res
    );

    public async Task<LotMapOverviewDto> GetOverviewAsync()
    {
        var zoneMaps = await db.ZoneMaps.ToListAsync();
        var lots = await db.Lots.Include(l => l.Grave).ToListAsync();

        var zoneDtos = zoneMaps.Select(z => {
            var zl = lots.Where(l => l.ZoneCode == z.Code).ToList();
            return ToZoneDto(z,
                zl.Count(l => l.Status == LotStatus.Occupied),
                zl.Count(l => l.Status == LotStatus.Reserved),
                zl.Count(l => l.Status == LotStatus.Empty));
        });

        return new LotMapOverviewDto(zoneDtos, lots.Select(ToDto));
    }

    public async Task<IEnumerable<LotDto>> GetLotsByZoneAsync(string zone)
    {
        var lots = await db.Lots.Include(l => l.Grave)
            .Where(l => l.ZoneCode == zone).ToListAsync();
        return lots.Select(ToDto);
    }

    public async Task<LotDto?> GetLotAsync(int id)
    {
        var l = await db.Lots.Include(l => l.Grave).FirstOrDefaultAsync(l => l.Id == id);
        return l is null ? null : ToDto(l);
    }

    public async Task<LotDto?> GetLotByCodeAsync(string code)
    {
        var l = await db.Lots.Include(l => l.Grave).FirstOrDefaultAsync(l => l.LotCode == code);
        return l is null ? null : ToDto(l);
    }

    public async Task<LotDto> CreateLotAsync(CreateLotRequest req)
    {
        Enum.TryParse<LotStatus>(req.Status, true, out var st);
        var lot = new Lot {
            LotCode = req.LotCode.Trim().ToUpper(),
            ZoneCode = req.ZoneCode.Trim().ToUpper(),
            X = req.X, Y = req.Y, W = req.W, H = req.H,
            Status = req.GraveId.HasValue ? LotStatus.Occupied : st,
            GraveId = req.GraveId, Notes = req.Notes,
            Row = req.Row, Col = req.Col
        };
        db.Lots.Add(lot);
        await db.SaveChangesAsync();
        return ToDto(lot);
    }

    public async Task<LotDto?> UpdateLotPositionAsync(int id, UpdateLotPositionRequest req)
    {
        var lot = await db.Lots.FindAsync(id);
        if (lot is null) return null;
        lot.X = req.X; lot.Y = req.Y; lot.W = req.W; lot.H = req.H;
        lot.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToDto(lot);
    }

    public async Task<LotDto?> AssignGraveAsync(int id, AssignGraveRequest req)
    {
        var lot = await db.Lots.Include(l => l.Grave).FirstOrDefaultAsync(l => l.Id == id);
        if (lot is null) return null;

        Enum.TryParse<LotStatus>(req.Status, true, out var st);
        lot.GraveId = req.GraveId;
        lot.Status = req.GraveId.HasValue ? LotStatus.Occupied : st;
        lot.UpdatedAt = DateTime.UtcNow;

        // Reload & sync grave
        if (req.GraveId.HasValue)
        {
            var g = await db.Graves.FindAsync(req.GraveId.Value);
            if (g != null)
            {
                g.LotCode = lot.LotCode;
                g.Zone = lot.ZoneCode;
                g.UpdatedAt = DateTime.UtcNow;
            }
            lot.Grave = g;
        }
        else
        {
            lot.Grave = null;
        }

        db.AuditLogs.Add(new AuditLog {
            GraveId = req.GraveId,
            Action = req.GraveId.HasValue ? "assign" : "unassign",
            Content = req.GraveId.HasValue
                ? $"Gán mộ #{req.GraveId} ({lot.Grave?.FullName}) vào lô {lot.LotCode}"
                : $"Gỡ mộ khỏi lô {lot.LotCode}",
            PerformedBy = "Admin"
        });
        await db.SaveChangesAsync();
        return ToDto(lot);
    }

    public async Task<LotDto?> UnassignGraveAsync(int id)
        => await AssignGraveAsync(id, new AssignGraveRequest(null, "Empty"));

    public async Task<bool> DeleteLotAsync(int id)
    {
        var lot = await db.Lots.FindAsync(id);
        if (lot is null) return false;
        db.Lots.Remove(lot);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<LotDto>> GenerateGridAsync(GenerateGridRequest req)
    {
        // Remove existing lots in zone
        var existing = db.Lots.Where(l => l.ZoneCode == req.ZoneCode && l.GraveId == null);
        db.Lots.RemoveRange(existing);
        await db.SaveChangesAsync();

        double gapX = req.GapPct, gapY = req.GapPct;
        double cellW = Math.Round((req.TotalW - gapX * (req.Cols + 1)) / req.Cols, 3);
        double cellH = Math.Round((req.TotalH - gapY * (req.Rows + 1)) / req.Rows, 3);

        var lots = new List<Lot>();
        for (int r = 0; r < req.Rows; r++)
        {
            for (int c = 0; c < req.Cols; c++)
            {
                var code = $"{req.ZoneCode}-{r+1:D2}-{c+1:D2}";
                if (await db.Lots.AnyAsync(l => l.LotCode == code)) continue;

                lots.Add(new Lot {
                    LotCode = code, ZoneCode = req.ZoneCode,
                    X = Math.Round(req.StartX + gapX + c * (cellW + gapX), 3),
                    Y = Math.Round(req.StartY + gapY + r * (cellH + gapY), 3),
                    W = cellW, H = cellH,
                    Status = LotStatus.Empty, Row = r+1, Col = c+1
                });
            }
        }
        db.Lots.AddRange(lots);
        await db.SaveChangesAsync();
        return lots.Select(ToDto);
    }

    public async Task<ZoneMapDto?> GetZoneMapAsync(string code)
    {
        var z = await db.ZoneMaps.FirstOrDefaultAsync(z => z.Code == code);
        if (z is null) return null;
        var lots = await db.Lots.Where(l => l.ZoneCode == code).ToListAsync();
        return ToZoneDto(z,
            lots.Count(l => l.Status == LotStatus.Occupied),
            lots.Count(l => l.Status == LotStatus.Reserved),
            lots.Count(l => l.Status == LotStatus.Empty));
    }

    public async Task<IEnumerable<ZoneMapDto>> GetAllZoneMapsAsync()
    {
        var zones = await db.ZoneMaps.ToListAsync();
        var lots = await db.Lots.GroupBy(l => l.ZoneCode)
            .Select(g => new { Zone = g.Key, Occupied = g.Count(l => l.Status == LotStatus.Occupied),
                Reserved = g.Count(l => l.Status == LotStatus.Reserved),
                Empty = g.Count(l => l.Status == LotStatus.Empty) })
            .ToListAsync();

        return zones.Select(z => {
            var s = lots.FirstOrDefault(l => l.Zone == z.Code);
            return ToZoneDto(z, s?.Occupied ?? 0, s?.Reserved ?? 0, s?.Empty ?? 0);
        });
    }

    public async Task<ZoneMapDto> UpsertZoneMapAsync(ZoneMap zm)
    {
        var existing = await db.ZoneMaps.FirstOrDefaultAsync(z => z.Code == zm.Code);
        int occ = 0, res = 0, empty = 0;

        if (existing is null)
        {
            db.ZoneMaps.Add(zm);
        }
        else
        {
            existing.Label = zm.Label; existing.X = zm.X; existing.Y = zm.Y;
            existing.W = zm.W; existing.H = zm.H; existing.Color = zm.Color;
            existing.Rows = zm.Rows; existing.Cols = zm.Cols;
            zm = existing;
        }

        await db.SaveChangesAsync();

        var lots = await db.Lots.Where(l => l.ZoneCode == zm.Code).ToListAsync();
        occ = lots.Count(l => l.Status == LotStatus.Occupied);
        res = lots.Count(l => l.Status == LotStatus.Reserved);
        empty = lots.Count(l => l.Status == LotStatus.Empty);

        return ToZoneDto(zm, occ, res, empty);
    }
}

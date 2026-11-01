using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SmartCem.Data;
using SmartCem.Models;
using System.Drawing;

namespace SmartCem.Services;

public interface IGraveService
{
    Task<(IEnumerable<GraveDto> Items, int Total)> SearchAsync(
        string? q, string? zone, string? status, string? contractType,
        bool? anniversarySoon, bool? expiringSoon, int page, int pageSize);
    Task<GraveDto?> GetByIdAsync(int id);
    Task<GraveDto> CreateAsync(CreateGraveRequest req, string? performedBy);
    Task<GraveDto?> UpdateAsync(int id, CreateGraveRequest req, string? performedBy);
    Task<bool> DeleteAsync(int id, string? performedBy);
    Task<DashboardDto> GetDashboardAsync();
    Task<IEnumerable<AnniversaryReminderDto>> GetUpcomingAnniversariesAsync(int days = 30);
    Task<IEnumerable<ContractExpirationDto>> GetExpiringContractsAsync(int days = 60);
    Task<byte[]> ExportGravesExcelAsync();
    Task SetPhotoAsync(int id, string relativePath);
    Task SetQrAsync(int id, string relativePath);
}

public class GraveService(SmartCemDb db) : IGraveService
{
    // Ngày giỗ tính theo âm lịch (LunarDeathDate "dd/mm Âm lịch"), không theo ngày mất dương lịch
    private static int? DaysToAnniversary(string? lunarDeathDate)
    {
        var dm = Lunar.ParseDayMonth(lunarDeathDate);
        if (dm is null) return null;
        var next = Lunar.NextSolarOf(dm.Value.d, dm.Value.m, DateTime.Now);
        return next is null ? null : (next.Value.Date - DateTime.Now.Date).Days;
    }

    private static bool CheckAnniversarySoon(string? lunarDeathDate, int windowDays = 30)
        => DaysToAnniversary(lunarDeathDate) is int d && d <= windowDays;

    private static bool CheckContractExpiringSoon(DateTime? contractEndDate, int windowDays = 60)
    {
        if (!contractEndDate.HasValue) return false;
        var diff = (contractEndDate.Value.Date - DateTime.UtcNow.Date).TotalDays;
        return diff <= windowDays;
    }

    private static GraveDto ToDto(Grave g) => new(
        g.Id, g.FullName, g.Alias,
        g.BornYear, g.DiedYear, g.DiedDate, g.LunarDeathDate,
        g.Zone, g.LotCode, g.GraveType, g.AreaM2,
        g.Hometown, g.ContactName, g.ContactRelation, g.ContactPhone, g.ContactAddress,
        g.ContractType, g.ContractStartDate, g.ContractEndDate,
        g.Notes, g.Status.ToString(),
        g.PhotoPath, g.QrImagePath,
        g.CreatedAt,
        CheckAnniversarySoon(g.LunarDeathDate),
        CheckContractExpiringSoon(g.ContractEndDate)
    );

    public async Task<(IEnumerable<GraveDto> Items, int Total)> SearchAsync(
        string? q, string? zone, string? status, string? contractType,
        bool? anniversarySoon, bool? expiringSoon, int page, int pageSize)
    {
        var query = db.Graves.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLower();
            query = query.Where(g =>
                g.FullName.ToLower().Contains(search) ||
                g.LotCode.ToLower().Contains(search) ||
                (g.Alias != null && g.Alias.ToLower().Contains(search)) ||
                (g.Hometown != null && g.Hometown.ToLower().Contains(search)) ||
                (g.ContactName != null && g.ContactName.ToLower().Contains(search)) ||
                (g.ContactPhone != null && g.ContactPhone.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(zone))
            query = query.Where(g => g.Zone == zone);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<GraveStatus>(status, true, out var st))
            query = query.Where(g => g.Status == st);

        if (!string.IsNullOrWhiteSpace(contractType))
            query = query.Where(g => g.ContractType == contractType);

        var allItems = await query
            .OrderByDescending(g => g.UpdatedAt)
            .ToListAsync();

        IEnumerable<Grave> filtered = allItems;
        if (anniversarySoon == true)
            filtered = filtered.Where(g => CheckAnniversarySoon(g.LunarDeathDate));

        if (expiringSoon == true)
            filtered = filtered.Where(g => CheckContractExpiringSoon(g.ContractEndDate));

        var list = filtered.ToList();
        var total = list.Count;
        var paged = list.Skip((page - 1) * pageSize).Take(pageSize);

        return (paged.Select(ToDto), total);
    }

    public async Task<GraveDto?> GetByIdAsync(int id)
    {
        var g = await db.Graves.FindAsync(id);
        return g is null ? null : ToDto(g);
    }

    public async Task<GraveDto> CreateAsync(CreateGraveRequest req, string? performedBy)
    {
        Enum.TryParse<GraveStatus>(req.Status, true, out var status);
        var g = new Grave
        {
            FullName = req.FullName.Trim(),
            Alias = req.Alias?.Trim(),
            BornYear = req.BornYear,
            DiedYear = req.DiedYear,
            DiedDate = req.DiedDate,
            LunarDeathDate = req.LunarDeathDate?.Trim(),
            Zone = req.Zone.Trim().ToUpper(),
            LotCode = req.LotCode.Trim().ToUpper(),
            GraveType = req.GraveType,
            AreaM2 = req.AreaM2,
            Hometown = req.Hometown?.Trim(),
            ContactName = req.ContactName?.Trim(),
            ContactRelation = req.ContactRelation?.Trim(),
            ContactPhone = req.ContactPhone?.Trim(),
            ContactAddress = req.ContactAddress?.Trim(),
            ContractType = req.ContractType,
            ContractStartDate = req.ContractStartDate,
            ContractEndDate = req.ContractEndDate,
            Notes = req.Notes,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Graves.Add(g);
        await db.SaveChangesAsync();

        // Đồng bộ với bản đồ Lô (Lot Map)
        var lot = await db.Lots.FirstOrDefaultAsync(l => l.LotCode == g.LotCode);
        if (lot != null)
        {
            lot.GraveId = g.Id;
            lot.Status = LotStatus.Occupied;
            lot.UpdatedAt = DateTime.UtcNow;
        }

        db.AuditLogs.Add(new AuditLog
        {
            GraveId = g.Id, Action = "add",
            Content = $"Thêm hồ sơ: {g.FullName} ({g.LotCode})",
            PerformedBy = performedBy ?? "Admin"
        });
        await db.SaveChangesAsync();
        return ToDto(g);
    }

    public async Task<GraveDto?> UpdateAsync(int id, CreateGraveRequest req, string? performedBy)
    {
        var g = await db.Graves.FindAsync(id);
        if (g is null) return null;

        var oldLotCode = g.LotCode;
        var newLotCode = req.LotCode.Trim().ToUpper();

        Enum.TryParse<GraveStatus>(req.Status, true, out var status);
        g.FullName = req.FullName.Trim();
        g.Alias = req.Alias?.Trim();
        g.BornYear = req.BornYear;
        g.DiedYear = req.DiedYear;
        g.DiedDate = req.DiedDate;
        g.LunarDeathDate = req.LunarDeathDate?.Trim();
        g.Zone = req.Zone.Trim().ToUpper();
        g.LotCode = newLotCode;
        g.GraveType = req.GraveType;
        g.AreaM2 = req.AreaM2;
        g.Hometown = req.Hometown?.Trim();
        g.ContactName = req.ContactName?.Trim();
        g.ContactRelation = req.ContactRelation?.Trim();
        g.ContactPhone = req.ContactPhone?.Trim();
        g.ContactAddress = req.ContactAddress?.Trim();
        g.ContractType = req.ContractType;
        g.ContractStartDate = req.ContractStartDate;
        g.ContractEndDate = req.ContractEndDate;
        g.Notes = req.Notes;
        g.Status = status;
        g.UpdatedAt = DateTime.UtcNow;

        // Đồng bộ Lot nếu đổi vị trí lô
        if (oldLotCode != newLotCode)
        {
            var oldLot = await db.Lots.FirstOrDefaultAsync(l => l.LotCode == oldLotCode && l.GraveId == g.Id);
            if (oldLot != null)
            {
                oldLot.GraveId = null;
                oldLot.Status = LotStatus.Empty;
                oldLot.UpdatedAt = DateTime.UtcNow;
            }

            var newLot = await db.Lots.FirstOrDefaultAsync(l => l.LotCode == newLotCode);
            if (newLot != null)
            {
                newLot.GraveId = g.Id;
                newLot.Status = LotStatus.Occupied;
                newLot.UpdatedAt = DateTime.UtcNow;
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            GraveId = g.Id, Action = "edit",
            Content = $"Cập nhật hồ sơ: {g.FullName} ({g.LotCode})",
            PerformedBy = performedBy ?? "Admin"
        });
        await db.SaveChangesAsync();
        return ToDto(g);
    }

    public async Task<bool> DeleteAsync(int id, string? performedBy)
    {
        var g = await db.Graves.FindAsync(id);
        if (g is null) return false;

        // Giải phóng ô đất trên bản đồ
        var lot = await db.Lots.FirstOrDefaultAsync(l => l.LotCode == g.LotCode || l.GraveId == g.Id);
        if (lot != null)
        {
            lot.GraveId = null;
            lot.Status = LotStatus.Empty;
            lot.UpdatedAt = DateTime.UtcNow;
        }

        db.AuditLogs.Add(new AuditLog
        {
            Action = "delete", Content = $"Xóa hồ sơ: {g.FullName} ({g.LotCode})",
            PerformedBy = performedBy ?? "Admin"
        });
        db.Graves.Remove(g);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task SetPhotoAsync(int id, string relativePath)
    {
        var g = await db.Graves.FindAsync(id);
        if (g is null) return;
        g.PhotoPath = relativePath;
        g.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task SetQrAsync(int id, string relativePath)
    {
        var g = await db.Graves.FindAsync(id);
        if (g is null) return;
        g.QrImagePath = relativePath;
        g.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<AnniversaryReminderDto>> GetUpcomingAnniversariesAsync(int days = 30)
    {
        var graves = await db.Graves.Where(g => g.LunarDeathDate != null && g.Status != GraveStatus.Archived).ToListAsync();
        var list = new List<AnniversaryReminderDto>();

        foreach (var g in graves)
        {
            if (DaysToAnniversary(g.LunarDeathDate) is not int diff || diff < 0 || diff > days) continue;
            list.Add(new AnniversaryReminderDto(
                g.Id, g.FullName, g.Zone, g.LotCode,
                g.DiedDate, g.LunarDeathDate, diff,
                g.ContactName, g.ContactPhone
            ));
        }

        return list.OrderBy(x => x.DaysUntil);
    }

    public async Task<IEnumerable<ContractExpirationDto>> GetExpiringContractsAsync(int days = 60)
    {
        var graves = await db.Graves.Where(g => g.ContractEndDate != null).ToListAsync();
        var today = DateTime.UtcNow.Date;
        var list = new List<ContractExpirationDto>();

        foreach (var g in graves)
        {
            if (!g.ContractEndDate.HasValue) continue;
            var diff = (int)(g.ContractEndDate.Value.Date - today).TotalDays;
            if (diff <= days)
            {
                list.Add(new ContractExpirationDto(
                    g.Id, g.FullName, g.Zone, g.LotCode,
                    g.ContractType, g.ContractEndDate, diff,
                    g.ContactName, g.ContactPhone,
                    diff < 0
                ));
            }
        }

        return list.OrderBy(x => x.DaysRemaining);
    }

    public async Task<DashboardDto> GetDashboardAsync()
    {
        var totalGraves = await db.Graves.CountAsync();
        var zones = await db.Zones.ToListAsync();
        var lots = await db.Lots.ToListAsync();

        var occupiedLots = lots.Count(l => l.Status == LotStatus.Occupied);
        var emptyLots = lots.Count(l => l.Status == LotStatus.Empty);

        // Đọc trực tiếp từ ServiceOrdersFull
        var pendingOrders = await db.ServiceOrdersFull.CountAsync(o => o.Status == ServiceOrderFullStatus.Pending);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var doneMonth = await db.ServiceOrdersFull.CountAsync(o =>
            o.Status == ServiceOrderFullStatus.Done && o.CompletedAt >= monthStart);

        var monthRevenue = await db.ServiceOrdersFull
            .Where(o => o.IsPaid && o.PaidAt >= monthStart)
            .SumAsync(o => (decimal?)o.Price - (o.Discount ?? 0)) ?? 0;

        var totalRevenue = await db.ServiceOrdersFull
            .Where(o => o.IsPaid)
            .SumAsync(o => (decimal?)o.Price - (o.Discount ?? 0)) ?? 0;

        var zoneSummaries = zones.Select(z =>
        {
            var zLots = lots.Where(l => l.ZoneCode == z.Code).ToList();
            var occ = zLots.Count(l => l.Status == LotStatus.Occupied);
            var emp = zLots.Count(l => l.Status == LotStatus.Empty);
            var tot = zLots.Count > 0 ? zLots.Count : z.TotalLots;
            return new ZoneSummaryDto(z.Code, z.Name ?? $"Khu {z.Code}", tot, occ, emp);
        });

        var recentLogs = await db.AuditLogs
            .OrderByDescending(l => l.At).Take(10)
            .Select(l => new RecentActivityDto(l.Action, l.Content, l.PerformedBy, l.At))
            .ToListAsync();

        var annivList = await GetUpcomingAnniversariesAsync(30);
        var expireList = await GetExpiringContractsAsync(60);

        return new DashboardDto(
            totalGraves, zones.Count,
            occupiedLots, emptyLots,
            pendingOrders, doneMonth,
            monthRevenue, totalRevenue,
            annivList.Count(),
            expireList.Count(),
            zoneSummaries, recentLogs,
            annivList.Take(5),
            expireList.Take(5)
        );
    }

    public async Task<byte[]> ExportGravesExcelAsync()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("DanhSachMoPhan");

        // Header style
        var headers = new[] {
            "STT", "Họ và tên", "Tên gọi/Biệt hiệu", "Năm sinh", "Năm mất",
            "Ngày mất DL", "Ngày giỗ ÂL", "Khu vực", "Mã lô", "Kiểu mộ",
            "Diện tích (m²)", "Quê quán", "Thân nhân", "Mối quan hệ", "Điện thoại",
            "Địa chỉ", "Trạng thái", "Ghi chú"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cells[1, i + 1].Value = headers[i];
            ws.Cells[1, i + 1].Style.Font.Bold = true;
            ws.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(201, 168, 76));
            ws.Cells[1, i + 1].Style.Font.Color.SetColor(Color.White);
            ws.Cells[1, i + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        var graves = await db.Graves.OrderBy(g => g.Zone).ThenBy(g => g.LotCode).ToListAsync();
        int row = 2;
        foreach (var g in graves)
        {
            ws.Cells[row, 1].Value = row - 1;
            ws.Cells[row, 2].Value = g.FullName;
            ws.Cells[row, 3].Value = g.Alias ?? "";
            ws.Cells[row, 4].Value = g.BornYear?.ToString() ?? "";
            ws.Cells[row, 5].Value = g.DiedYear?.ToString() ?? "";
            ws.Cells[row, 6].Value = g.DiedDate?.ToString("dd/MM/yyyy") ?? "";
            ws.Cells[row, 7].Value = g.LunarDeathDate ?? "";
            ws.Cells[row, 8].Value = g.Zone;
            ws.Cells[row, 9].Value = g.LotCode;
            ws.Cells[row, 10].Value = g.GraveType;
            ws.Cells[row, 11].Value = g.AreaM2;
            ws.Cells[row, 12].Value = g.Hometown ?? "";
            ws.Cells[row, 13].Value = g.ContactName ?? "";
            ws.Cells[row, 14].Value = g.ContactRelation ?? "";
            ws.Cells[row, 15].Value = g.ContactPhone ?? "";
            ws.Cells[row, 16].Value = g.ContactAddress ?? "";
            ws.Cells[row, 17].Value = g.Status.ToString();
            ws.Cells[row, 18].Value = g.Notes ?? "";
            row++;
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        return await package.GetAsByteArrayAsync();
    }
}

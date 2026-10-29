using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SmartCem.Data;
using SmartCem.Models;
using System.Drawing;

namespace SmartCem.Services;

public interface IServiceService
{
    // Packages
    Task<IEnumerable<ServicePackageDto>> GetPackagesAsync(bool activeOnly = false);
    Task<ServicePackageDto?> GetPackageAsync(int id);
    Task<ServicePackageDto> CreatePackageAsync(ServicePackage pkg);
    Task<ServicePackageDto?> UpdatePackageAsync(int id, ServicePackage pkg);
    Task<bool> DeletePackageAsync(int id);

    // Orders
    Task<(IEnumerable<ServiceOrderDto> Items, int Total)> GetOrdersAsync(
        string? status, int? graveId, int? packageId, string? search, int page, int pageSize);
    Task<ServiceOrderDto?> GetOrderAsync(int id);
    Task<ServiceOrderDto> CreateOrderAsync(CreateOrderRequest req);
    Task<ServiceOrderDto?> UpdateOrderStatusAsync(int id, UpdateOrderStatusRequest req);
    Task<ServiceOrderDto?> MarkPaidAsync(int id, MarkPaidRequest req);
    Task<ServiceOrderDto?> SetPhotoAsync(int id, string path);
    Task<bool> CancelOrderAsync(int id, string? reason);
    Task<ServiceReceiptDto?> GetReceiptAsync(int id);
    Task<byte[]> ExportOrdersExcelAsync();

    // Stats
    Task<ServiceStatsDto> GetStatsAsync();
}

public class ServiceService(SmartCemDb db) : IServiceService
{
    private static ServicePackageDto PkgDto(ServicePackage p, int orderCount) => new(
        p.Id, p.Code, p.Name, p.Description, p.Category, p.Price, p.Unit,
        p.IsActive, p.IsRecurring, p.RecurringDays, p.IconClass, p.Color, orderCount
    );

    private static ServiceOrderDto OrdDto(ServiceOrderFull o) => new(
        o.Id, o.OrderCode,
        o.GraveId, o.Grave?.FullName ?? "—", o.Grave?.LotCode ?? "—",
        o.PackageId, o.Package?.Name,
        o.ServiceName,
        o.Price, o.Discount, o.FinalPrice,
        o.Status.ToString(), o.AssignedTo,
        o.ScheduledAt, o.OrderedAt,
        o.StartedAt, o.CompletedAt,
        o.Notes, o.CompletionNote,
        o.PhotoAfterPath, o.IsPaid,
        o.PaymentMethod, o.PaidAt,
        o.UpdatedAt
    );

    // ─── Packages ─────────────────────────────────────────────────────
    public async Task<IEnumerable<ServicePackageDto>> GetPackagesAsync(bool activeOnly = false)
    {
        var q = db.ServicePackages.AsQueryable();
        if (activeOnly) q = q.Where(p => p.IsActive);

        var pkgs = await q.OrderBy(p => p.Category).ThenBy(p => p.Code).ToListAsync();
        var counts = await db.ServiceOrdersFull
            .GroupBy(o => o.PackageId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id ?? 0, x => x.Count);

        return pkgs.Select(p => PkgDto(p, counts.GetValueOrDefault(p.Id, 0)));
    }

    public async Task<ServicePackageDto?> GetPackageAsync(int id)
    {
        var p = await db.ServicePackages.FindAsync(id);
        if (p is null) return null;
        var cnt = await db.ServiceOrdersFull.CountAsync(o => o.PackageId == id);
        return PkgDto(p, cnt);
    }

    public async Task<ServicePackageDto> CreatePackageAsync(ServicePackage pkg)
    {
        if (string.IsNullOrWhiteSpace(pkg.Code))
        {
            var maxId = await db.ServicePackages.MaxAsync(p => (int?)p.Id) ?? 0;
            pkg.Code = $"SVC-{(maxId + 1):D2}";
        }
        db.ServicePackages.Add(pkg);
        await db.SaveChangesAsync();
        return PkgDto(pkg, 0);
    }

    public async Task<ServicePackageDto?> UpdatePackageAsync(int id, ServicePackage updated)
    {
        var pkg = await db.ServicePackages.FindAsync(id);
        if (pkg is null) return null;

        pkg.Name = updated.Name;
        pkg.Code = string.IsNullOrWhiteSpace(updated.Code) ? pkg.Code : updated.Code;
        pkg.Description = updated.Description;
        pkg.Category = updated.Category;
        pkg.Price = updated.Price;
        pkg.Unit = updated.Unit;
        pkg.IsActive = updated.IsActive;
        pkg.IsRecurring = updated.IsRecurring;
        pkg.RecurringDays = updated.RecurringDays;
        pkg.IconClass = updated.IconClass;
        pkg.Color = updated.Color;

        await db.SaveChangesAsync();
        var cnt = await db.ServiceOrdersFull.CountAsync(o => o.PackageId == id);
        return PkgDto(pkg, cnt);
    }

    public async Task<bool> DeletePackageAsync(int id)
    {
        var pkg = await db.ServicePackages.FindAsync(id);
        if (pkg is null) return false;
        pkg.IsActive = false; // soft delete
        await db.SaveChangesAsync();
        return true;
    }

    // ─── Orders ───────────────────────────────────────────────────────
    public async Task<(IEnumerable<ServiceOrderDto> Items, int Total)> GetOrdersAsync(
        string? status, int? graveId, int? packageId, string? search, int page, int pageSize)
    {
        var q = db.ServiceOrdersFull
            .Include(o => o.Grave)
            .Include(o => o.Package)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ServiceOrderFullStatus>(status, true, out var st))
            q = q.Where(o => o.Status == st);
        if (graveId.HasValue)   q = q.Where(o => o.GraveId == graveId.Value);
        if (packageId.HasValue) q = q.Where(o => o.PackageId == packageId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(o =>
                o.OrderCode.ToLower().Contains(s) ||
                o.ServiceName.ToLower().Contains(s) ||
                (o.Grave != null && (o.Grave.FullName.ToLower().Contains(s) || o.Grave.LotCode.ToLower().Contains(s))) ||
                (o.AssignedTo != null && o.AssignedTo.ToLower().Contains(s)));
        }

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(o => o.OrderedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return (items.Select(OrdDto), total);
    }

    public async Task<ServiceOrderDto?> GetOrderAsync(int id)
    {
        var o = await db.ServiceOrdersFull
            .Include(o => o.Grave).Include(o => o.Package)
            .FirstOrDefaultAsync(o => o.Id == id);
        return o is null ? null : OrdDto(o);
    }

    public async Task<ServiceOrderDto> CreateOrderAsync(CreateOrderRequest req)
    {
        var lastId = await db.ServiceOrdersFull.MaxAsync(o => (int?)o.Id) ?? 0;
        var code = $"SVO-{DateTime.Now:yyyy}-{(lastId + 1):D5}";

        string serviceName = req.ServiceName;
        decimal price = req.Price;

        if (req.PackageId.HasValue)
        {
            var pkg = await db.ServicePackages.FindAsync(req.PackageId.Value);
            if (pkg != null)
            {
                serviceName = pkg.Name;
                if (price <= 0) price = pkg.Price;
            }
        }

        var order = new ServiceOrderFull
        {
            OrderCode = code,
            GraveId = req.GraveId,
            PackageId = req.PackageId,
            ServiceName = serviceName,
            Price = price,
            Discount = req.Discount,
            AssignedTo = req.AssignedTo,
            ScheduledAt = req.ScheduledAt,
            Notes = req.Notes,
            Status = ServiceOrderFullStatus.Pending,
            OrderedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.ServiceOrdersFull.Add(order);

        db.AuditLogs.Add(new AuditLog {
            GraveId = req.GraveId, Action = "service",
            Content = $"Tạo đơn dịch vụ {code}: {serviceName}",
            PerformedBy = "Admin"
        });
        await db.SaveChangesAsync();

        var full = await db.ServiceOrdersFull.Include(o => o.Grave).Include(o => o.Package)
            .FirstAsync(o => o.Id == order.Id);
        return OrdDto(full);
    }

    public async Task<ServiceOrderDto?> UpdateOrderStatusAsync(int id, UpdateOrderStatusRequest req)
    {
        var o = await db.ServiceOrdersFull.Include(o => o.Grave).Include(o => o.Package)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (o is null) return null;

        if (!Enum.TryParse<ServiceOrderFullStatus>(req.Status, true, out var newStatus))
            return null;

        var old = o.Status;
        o.Status = newStatus;
        o.UpdatedAt = DateTime.UtcNow;

        if (req.AssignedTo != null) o.AssignedTo = req.AssignedTo;
        if (req.ScheduledAt.HasValue) o.ScheduledAt = req.ScheduledAt;

        if (newStatus == ServiceOrderFullStatus.InProgress && old != ServiceOrderFullStatus.InProgress)
            o.StartedAt = DateTime.UtcNow;

        if (newStatus == ServiceOrderFullStatus.Done)
        {
            o.CompletedAt = DateTime.UtcNow;
            o.CompletionNote = req.CompletionNote;
        }

        db.AuditLogs.Add(new AuditLog {
            GraveId = o.GraveId, Action = "service",
            Content = $"Cập nhật đơn {o.OrderCode}: {old} → {newStatus}",
            PerformedBy = "Admin"
        });
        await db.SaveChangesAsync();
        return OrdDto(o);
    }

    public async Task<ServiceOrderDto?> MarkPaidAsync(int id, MarkPaidRequest req)
    {
        var o = await db.ServiceOrdersFull.Include(o => o.Grave).Include(o => o.Package)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (o is null) return null;

        o.IsPaid = true;
        o.PaymentMethod = req.PaymentMethod;
        o.PaidAt = DateTime.UtcNow;
        o.UpdatedAt = DateTime.UtcNow;

        db.AuditLogs.Add(new AuditLog {
            GraveId = o.GraveId, Action = "payment",
            Content = $"Thanh toán đơn {o.OrderCode}: {o.FinalPrice:N0} VND ({req.PaymentMethod})",
            PerformedBy = "Admin"
        });
        await db.SaveChangesAsync();
        return OrdDto(o);
    }

    public async Task<ServiceOrderDto?> SetPhotoAsync(int id, string path)
    {
        var o = await db.ServiceOrdersFull.Include(o => o.Grave).Include(o => o.Package)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (o is null) return null;
        o.PhotoAfterPath = path;
        o.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return OrdDto(o);
    }

    public async Task<bool> CancelOrderAsync(int id, string? reason)
    {
        var o = await db.ServiceOrdersFull.FindAsync(id);
        if (o is null) return false;
        o.Status = ServiceOrderFullStatus.Cancelled;
        o.Notes = string.IsNullOrEmpty(reason) ? o.Notes : $"{o.Notes}\n[Hủy] {reason}";
        o.UpdatedAt = DateTime.UtcNow;

        db.AuditLogs.Add(new AuditLog {
            GraveId = o.GraveId, Action = "service",
            Content = $"Hủy đơn dịch vụ {o.OrderCode}: {reason}",
            PerformedBy = "Admin"
        });
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<ServiceReceiptDto?> GetReceiptAsync(int id)
    {
        var o = await db.ServiceOrdersFull
            .Include(o => o.Grave)
            .Include(o => o.Package)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (o is null) return null;

        return new ServiceReceiptDto(
            o.OrderCode,
            o.Id,
            o.Grave?.FullName ?? "—",
            o.Grave?.LotCode ?? "—",
            o.Grave?.Zone ?? "—",
            o.Grave?.ContactName,
            o.Grave?.ContactPhone,
            o.Grave?.ContactAddress,
            o.ServiceName,
            o.Package?.Name,
            o.Price,
            o.Discount,
            o.FinalPrice,
            o.IsPaid,
            o.PaymentMethod,
            o.PaidAt,
            o.OrderedAt,
            o.CompletedAt,
            o.Notes,
            o.AssignedTo
        );
    }

    public async Task<byte[]> ExportOrdersExcelAsync()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("DonDichVu");

        var headers = new[] {
            "STT", "Mã đơn", "Hồ sơ mộ", "Mã lô", "Khu", "Dịch vụ",
            "Đơn giá", "Giảm giá", "Thành tiền", "Trạng thái", "Thanh toán",
            "Hình thức", "Ngày thanh toán", "Nhân viên thực hiện", "Ngày hẹn", "Ngày hoàn thành", "Ghi chú"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cells[1, i + 1].Value = headers[i];
            ws.Cells[1, i + 1].Style.Font.Bold = true;
            ws.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(46, 125, 107));
            ws.Cells[1, i + 1].Style.Font.Color.SetColor(Color.White);
            ws.Cells[1, i + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        var orders = await db.ServiceOrdersFull
            .Include(o => o.Grave)
            .OrderByDescending(o => o.OrderedAt)
            .ToListAsync();

        int row = 2;
        foreach (var o in orders)
        {
            ws.Cells[row, 1].Value = row - 1;
            ws.Cells[row, 2].Value = o.OrderCode;
            ws.Cells[row, 3].Value = o.Grave?.FullName ?? "—";
            ws.Cells[row, 4].Value = o.Grave?.LotCode ?? "—";
            ws.Cells[row, 5].Value = o.Grave?.Zone ?? "—";
            ws.Cells[row, 6].Value = o.ServiceName;
            ws.Cells[row, 7].Value = o.Price;
            ws.Cells[row, 8].Value = o.Discount ?? 0;
            ws.Cells[row, 9].Value = o.FinalPrice;
            ws.Cells[row, 10].Value = o.Status.ToString();
            ws.Cells[row, 11].Value = o.IsPaid ? "Đã thanh toán" : "Chưa thanh toán";
            ws.Cells[row, 12].Value = o.PaymentMethod ?? "";
            ws.Cells[row, 13].Value = o.PaidAt?.ToString("dd/MM/yyyy HH:mm") ?? "";
            ws.Cells[row, 14].Value = o.AssignedTo ?? "";
            ws.Cells[row, 15].Value = o.ScheduledAt?.ToString("dd/MM/yyyy") ?? "";
            ws.Cells[row, 16].Value = o.CompletedAt?.ToString("dd/MM/yyyy HH:mm") ?? "";
            ws.Cells[row, 17].Value = o.Notes ?? "";
            row++;
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        return await package.GetAsByteArrayAsync();
    }

    // ─── Stats ────────────────────────────────────────────────────────
    public async Task<ServiceStatsDto> GetStatsAsync()
    {
        var all = await db.ServiceOrdersFull.ToListAsync();
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var total     = all.Count;
        var pending   = all.Count(o => o.Status == ServiceOrderFullStatus.Pending);
        var inProg    = all.Count(o => o.Status == ServiceOrderFullStatus.InProgress);
        var done      = all.Count(o => o.Status == ServiceOrderFullStatus.Done);
        var cancelled = all.Count(o => o.Status == ServiceOrderFullStatus.Cancelled);

        var totalRev = all.Where(o => o.IsPaid).Sum(o => o.FinalPrice);
        var monthRev = all.Where(o => o.IsPaid && o.PaidAt >= monthStart).Sum(o => o.FinalPrice);

        // By category
        var pkgMap = await db.ServicePackages.ToDictionaryAsync(p => p.Id, p => p);
        var catLabels = new Dictionary<string, string> {
            ["cleaning"] = "Vệ sinh", ["flower"] = "Hoa & lễ",
            ["maintenance"] = "Bảo trì", ["ceremony"] = "Cúng giỗ", ["other"] = "Khác"
        };
        var byCategory = all
            .Where(o => o.PackageId.HasValue && pkgMap.ContainsKey(o.PackageId.Value))
            .GroupBy(o => pkgMap[o.PackageId!.Value].Category)
            .Select(g => new CategoryStat(
                g.Key, catLabels.GetValueOrDefault(g.Key, g.Key),
                g.Count(), g.Where(o => o.IsPaid).Sum(o => o.FinalPrice)))
            .ToList();

        // Monthly revenue last 6 months
        var monthly = Enumerable.Range(0, 6)
            .Select(i => now.AddMonths(-i))
            .Select(m => {
                var ms = new DateTime(m.Year, m.Month, 1);
                var me = ms.AddMonths(1);
                var orders = all.Where(o => o.IsPaid && o.PaidAt >= ms && o.PaidAt < me);
                return new MonthlyRevenue(m.ToString("MM/yyyy"), orders.Sum(o => o.FinalPrice), orders.Count());
            })
            .Reverse()
            .ToList();

        return new ServiceStatsDto(total, pending, inProg, done, cancelled,
            totalRev, monthRev, byCategory, monthly);
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartCem.Models;

// ─── Gói dịch vụ (danh mục) ─────────────────────────────────────────
public class ServicePackage
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";         // "Vệ sinh định kỳ tháng"

    [MaxLength(20)]
    public string Code { get; set; } = "";         // SVC-01

    [MaxLength(1000)]
    public string? Description { get; set; }

    public string Category { get; set; } = "other"; // cleaning | flower | maintenance | ceremony | other

    public decimal Price { get; set; }
    public string? Unit { get; set; }              // "lần" | "tháng" | "năm"

    public bool IsActive { get; set; } = true;
    public bool IsRecurring { get; set; } = false;
    public int? RecurringDays { get; set; }        // lặp mỗi N ngày

    public string? IconClass { get; set; }         // font-awesome class
    public string? Color { get; set; }             // hex

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ServiceOrderFull> Orders { get; set; } = [];
}

// ─── Đơn dịch vụ ───────────────────────────────────────────────────
public class ServiceOrderFull
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string OrderCode { get; set; } = "";    // SVO-2024-00001

    public int GraveId { get; set; }
    public Grave? Grave { get; set; }

    public int? PackageId { get; set; }
    public ServicePackage? Package { get; set; }

    [MaxLength(200)]
    public string ServiceName { get; set; } = "";  // fallback nếu không có package

    public decimal Price { get; set; }
    public decimal? Discount { get; set; }

    [NotMapped]
    public decimal FinalPrice => Price - (Discount ?? 0);

    public ServiceOrderFullStatus Status { get; set; } = ServiceOrderFullStatus.Pending;

    public string? AssignedTo { get; set; }        // tên nhân viên
    public DateTime? ScheduledAt { get; set; }     // ngày hẹn thực hiện
    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? CompletionNote { get; set; }    // ghi chú khi hoàn thành

    public string? PhotoAfterPath { get; set; }    // ảnh sau khi thực hiện

    public bool IsPaid { get; set; } = false;
    public string? PaymentMethod { get; set; }     // cash | bank | qr
    public DateTime? PaidAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ServiceOrderFullStatus
{
    Pending,       // Chờ xác nhận
    Confirmed,     // Đã xác nhận, chờ thực hiện
    InProgress,    // Đang thực hiện
    Done,          // Hoàn thành
    Cancelled      // Đã hủy
}

// ─── DTOs ──────────────────────────────────────────────────────────
public record ServicePackageDto(
    int Id, string Code, string Name, string? Description,
    string Category, decimal Price, string? Unit,
    bool IsActive, bool IsRecurring, int? RecurringDays,
    string? IconClass, string? Color, int TotalOrders
);

public record ServiceOrderDto(
    int Id, string OrderCode,
    int GraveId, string GraveName, string GraveLot,
    int? PackageId, string? PackageName,
    string ServiceName,
    decimal Price, decimal? Discount, decimal FinalPrice,
    string Status, string? AssignedTo,
    DateTime? ScheduledAt, DateTime OrderedAt,
    DateTime? StartedAt, DateTime? CompletedAt,
    string? Notes, string? CompletionNote,
    string? PhotoAfterPath, bool IsPaid,
    string? PaymentMethod, DateTime? PaidAt,
    DateTime UpdatedAt
);

public record CreateOrderRequest(
    [Required] int GraveId,
    int? PackageId,
    [Required] string ServiceName,
    decimal Price,
    decimal? Discount,
    string? AssignedTo,
    DateTime? ScheduledAt,
    string? Notes
);

public record UpdateOrderStatusRequest(
    [Required] string Status,
    string? CompletionNote,
    string? AssignedTo,
    DateTime? ScheduledAt
);

public record MarkPaidRequest(string PaymentMethod = "cash");

public record ServiceStatsDto(
    int TotalOrders, int PendingOrders, int InProgressOrders,
    int DoneOrders, int CancelledOrders,
    decimal TotalRevenue, decimal MonthRevenue,
    IEnumerable<CategoryStat> ByCategory,
    IEnumerable<MonthlyRevenue> MonthlyRevenues
);

public record CategoryStat(string Category, string Label, int Count, decimal Revenue);
public record MonthlyRevenue(string Month, decimal Revenue, int Orders);

public record ServiceReceiptDto(
    string OrderCode,
    int OrderId,
    string GraveName,
    string GraveLot,
    string Zone,
    string? ContactName,
    string? ContactPhone,
    string? ContactAddress,
    string ServiceName,
    string? PackageName,
    decimal Price,
    decimal? Discount,
    decimal FinalPrice,
    bool IsPaid,
    string? PaymentMethod,
    DateTime? PaidAt,
    DateTime OrderedAt,
    DateTime? CompletedAt,
    string? Notes,
    string? AssignedTo
);

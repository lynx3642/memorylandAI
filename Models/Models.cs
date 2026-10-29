using System.ComponentModel.DataAnnotations;

namespace SmartCem.Models;

// ─── Grave / Hồ sơ mộ phần ─────────────────────────────────────────
public class Grave
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string FullName { get; set; } = "";

    [MaxLength(100)]
    public string? Alias { get; set; }

    public int? BornYear { get; set; }
    public int? DiedYear { get; set; }

    // Ngày mất chi tiết (Dương lịch)
    public DateTime? DiedDate { get; set; }

    // Ngày giỗ Âm lịch (ví dụ: "15/07 Âm lịch")
    [MaxLength(50)]
    public string? LunarDeathDate { get; set; }

    [MaxLength(10)]
    public string Zone { get; set; } = "A";          // Khu A–E

    [MaxLength(20)]
    public string LotCode { get; set; } = "";         // A-01-05

    // Kiểu mộ (Mộ đơn, Mộ đôi, Mộ gia tộc, Cải táng)
    [MaxLength(50)]
    public string GraveType { get; set; } = "Mộ đơn";

    // Diện tích (m2)
    public double AreaM2 { get; set; } = 3.5;

    [MaxLength(200)]
    public string? Hometown { get; set; }

    // Thân nhân / Người liên hệ
    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(50)]
    public string? ContactRelation { get; set; }      // Con trưởng, Vợ, Chồng, Cháu,...

    [MaxLength(20)]
    public string? ContactPhone { get; set; }

    [MaxLength(300)]
    public string? ContactAddress { get; set; }

    // Hợp đồng & Thời hạn quản lý
    [MaxLength(50)]
    public string ContractType { get; set; } = "Vĩnh viễn"; // Vĩnh viễn | Thuê 20 năm | Thuê 30 năm | Thuê 50 năm
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public GraveStatus Status { get; set; } = GraveStatus.Active;

    public string? PhotoPath { get; set; }            // relative path under wwwroot/uploads
    public string? QrImagePath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ServiceOrderFull> ServiceOrders { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}

public enum GraveStatus { Pending, Active, Archived }

// ─── Zone / Khu vực ────────────────────────────────────────────────
public class Zone
{
    public int Id { get; set; }

    [Required, MaxLength(10)]
    public string Code { get; set; } = "";            // A, B, C …

    [MaxLength(200)]
    public string? Name { get; set; }

    public int TotalLots { get; set; }
    public string? Description { get; set; }
}

// ─── Audit Log ─────────────────────────────────────────────────────
public class AuditLog
{
    public int Id { get; set; }

    public int? GraveId { get; set; }
    public Grave? Grave { get; set; }

    [MaxLength(50)]
    public string Action { get; set; } = "";         // add | edit | delete | import | qr | service | payment

    [MaxLength(500)]
    public string Content { get; set; } = "";

    [MaxLength(100)]
    public string? PerformedBy { get; set; }

    public DateTime At { get; set; } = DateTime.UtcNow;
}

// ─── Import Job ─────────────────────────────────────────────────────
public class ImportJob
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public ImportFileType FileType { get; set; }
    public int TotalRows { get; set; }
    public int Imported { get; set; }
    public int Failed { get; set; }
    public ImportJobStatus Status { get; set; } = ImportJobStatus.Processing;
    public string? ErrorLog { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
}

public enum ImportFileType { Excel, Csv, Json }
public enum ImportJobStatus { Processing, Done, Failed }

// ─── DTOs ──────────────────────────────────────────────────────────
public record GraveDto(
    int Id, string FullName, string? Alias,
    int? BornYear, int? DiedYear, DateTime? DiedDate, string? LunarDeathDate,
    string Zone, string LotCode, string GraveType, double AreaM2,
    string? Hometown, string? ContactName, string? ContactRelation, string? ContactPhone, string? ContactAddress,
    string ContractType, DateTime? ContractStartDate, DateTime? ContractEndDate,
    string? Notes, string Status,
    string? PhotoPath, string? QrImagePath,
    DateTime CreatedAt,
    bool IsAnniversarySoon = false,
    bool IsContractExpiringSoon = false
);

public record CreateGraveRequest(
    [Required] string FullName,
    string? Alias,
    int? BornYear, int? DiedYear, DateTime? DiedDate, string? LunarDeathDate,
    [Required] string Zone,
    [Required] string LotCode,
    string GraveType = "Mộ đơn",
    double AreaM2 = 3.5,
    string? Hometown = null,
    string? ContactName = null,
    string? ContactRelation = null,
    string? ContactPhone = null,
    string? ContactAddress = null,
    string ContractType = "Vĩnh viễn",
    DateTime? ContractStartDate = null,
    DateTime? ContractEndDate = null,
    string? Notes = null,
    string Status = "Active"
);

public record AnniversaryReminderDto(
    int GraveId, string FullName, string Zone, string LotCode,
    DateTime? DiedDate, string? LunarDeathDate, int DaysUntil,
    string? ContactName, string? ContactPhone
);

public record ContractExpirationDto(
    int GraveId, string FullName, string Zone, string LotCode,
    string ContractType, DateTime? ContractEndDate, int DaysRemaining,
    string? ContactName, string? ContactPhone, bool IsExpired
);

public record ImportResultDto(
    int JobId, string FileName,
    int Total, int Imported, int Failed,
    string Status, string? ErrorLog
);

public record DashboardDto(
    int TotalGraves, int TotalZones,
    int OccupiedLots, int EmptyLots,
    int PendingOrders, int DoneOrdersMonth,
    decimal MonthRevenue, decimal TotalRevenue,
    int UpcomingAnniversariesCount,
    int ExpiringContractsCount,
    IEnumerable<ZoneSummaryDto> ZoneSummaries,
    IEnumerable<RecentActivityDto> RecentActivity,
    IEnumerable<AnniversaryReminderDto> UpcomingAnniversaries,
    IEnumerable<ContractExpirationDto> ExpiringContracts
);

public record ZoneSummaryDto(string Zone, string Name, int Total, int Occupied, int Empty);
public record RecentActivityDto(string Action, string Content, string? By, DateTime At);

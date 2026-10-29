using System.ComponentModel.DataAnnotations;

namespace SmartCem.Models;

// ─── Lot / Ô đất trên bản đồ ────────────────────────────────────────
public class Lot
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string LotCode { get; set; } = "";     // A-01-05

    [Required, MaxLength(10)]
    public string ZoneCode { get; set; } = "";    // A

    // Vị trí trên canvas bản đồ (pixel %)
    public double X { get; set; }   // % từ trái
    public double Y { get; set; }   // % từ trên
    public double W { get; set; }   // % chiều rộng
    public double H { get; set; }   // % chiều cao

    public double Rotation { get; set; } = 0;  // độ xoay (future)

    public LotStatus Status { get; set; } = LotStatus.Empty;

    // FK về Grave (nullable – khi ô còn trống)
    public int? GraveId { get; set; }
    public Grave? Grave { get; set; }

    [MaxLength(200)]
    public string? Notes { get; set; }

    public int? Row { get; set; }
    public int? Col { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum LotStatus { Empty, Occupied, Reserved, Unavailable }

// ─── ZoneMap / Cấu hình khu vực bản đồ ─────────────────────────────
public class ZoneMap
{
    public int Id { get; set; }

    [Required, MaxLength(10)]
    public string Code { get; set; } = "";

    [MaxLength(100)]
    public string? Label { get; set; }

    // Vị trí vùng trên tổng bản đồ (%)
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }

    public string Color { get; set; } = "#2E7D6B"; // màu viền khu

    // Grid config
    public int Rows { get; set; } = 5;
    public int Cols { get; set; } = 10;
}

// ─── DTOs ─────────────────────────────────────────────────────────────
public record LotDto(
    int Id, string LotCode, string ZoneCode,
    double X, double Y, double W, double H, double Rotation,
    string Status, int? GraveId,
    string? GraveName, string? GraveYears,
    string? Notes, int? Row, int? Col,
    DateTime UpdatedAt
);

public record ZoneMapDto(
    int Id, string Code, string? Label,
    double X, double Y, double W, double H,
    string Color, int Rows, int Cols,
    int TotalLots, int OccupiedLots, int EmptyLots, int ReservedLots
);

public record CreateLotRequest(
    [Required] string LotCode,
    [Required] string ZoneCode,
    double X, double Y, double W, double H,
    string Status = "Empty",
    int? GraveId = null,
    string? Notes = null,
    int? Row = null,
    int? Col = null
);

public record UpdateLotPositionRequest(double X, double Y, double W, double H);
public record AssignGraveRequest(int? GraveId, string Status = "Occupied");

public record GenerateGridRequest(
    [Required] string ZoneCode,
    int Rows, int Cols,
    double StartX, double StartY,
    double TotalW, double TotalH,
    double GapPct = 0.5  // khoảng cách giữa các ô tính theo % tổng
);

public record LotMapOverviewDto(
    IEnumerable<ZoneMapDto> Zones,
    IEnumerable<LotDto> Lots
);

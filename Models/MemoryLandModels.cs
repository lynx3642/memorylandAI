using System.ComponentModel.DataAnnotations;

namespace SmartCem.Models;

// ─── Đối tác địa phương (Partner App) ───────────────────────────────
public class Partner
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(20)]  public string Phone { get; set; } = "";
    [MaxLength(100)] public string Area { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double Rating { get; set; } = 4.5;
    public int CompletedJobs { get; set; }
    public bool Available { get; set; } = true;
    [MaxLength(10)] public string Color { get; set; } = "#2e7d6b";
}

// ─── Công việc chăm sóc từ xa (chu trình 5 bước) ─────────────────────
public enum CareJobStatus { Dispatched, Accepted, CheckedIn, BeforeDone, Verified, Rejected, Unassigned }

public class CareJob
{
    public int Id { get; set; }
    [MaxLength(30)] public string JobCode { get; set; } = "";
    public int GraveId { get; set; }
    public int? OrderId { get; set; }
    public int? PartnerId { get; set; }
    public int? PreviousPartnerId { get; set; }
    public CareJobStatus Status { get; set; } = CareJobStatus.Dispatched;
    public double EtaMinutes { get; set; }
    public double DispatchScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CheckinAt { get; set; }
    public double? CheckinDistanceM { get; set; }
    public DateTime? BeforeAt { get; set; }
    public bool BeforeInApp { get; set; }
    public DateTime? AfterAt { get; set; }
    [MaxLength(80)] public string? BeforeHash { get; set; }
    [MaxLength(80)] public string? AfterHash { get; set; }
    public double? BeforeWeed { get; set; }
    public double? AfterWeed { get; set; }
    public string? BeforeThumb { get; set; }
    public string? AfterThumb { get; set; }
    public double? VerifyScore { get; set; }
    public string? VerifyJson { get; set; }
    public DateTime? ReportSentAt { get; set; }
    public bool Refunded { get; set; }
    public string? Note { get; set; }
}

// ─── Dự án quy hoạch B2G (6 bước) ─────────────────────────────────────
public class PlanningProject
{
    public int Id { get; set; }
    [MaxLength(200)] public string Cemetery { get; set; } = "";
    [MaxLength(100)] public string Commune { get; set; } = "";
    [MaxLength(100)] public string Province { get; set; } = "";
    public double AreaHa { get; set; }
    public decimal Fee { get; set; }
    public int Step { get; set; } = 1;          // 1..6 ; 6 = hoàn tất bàn giao
    public int SlotsGained { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
}

public record BookRequest(int GraveId, int PackageId, DateTime? ScheduledAt, string? Note, string PaymentMethod = "qr");
public record CheckinRequest(double Lat, double Lng, string? Qr);
public record PhotoRequest(string Kind, string Hash, double Weed, DateTime CapturedAt, bool InApp, double Lat, double Lng, string? Thumb);
public record AssistantRequest(string Message, int? GraveId);
public record PlannerRequest(double WidthM = 120, double HeightM = 80, int Seed = 7);
public record NewProjectRequest(string Cemetery, string Commune, string Province, double AreaHa, decimal Fee);

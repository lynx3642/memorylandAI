using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartCem.Models;

namespace SmartCem.Data;

/// <summary>Khởi tạo bảng/dữ liệu cho MemoryLand AI (idempotent – chạy được trên smartcem.db cũ).</summary>
public static class MemoryLandSeeder
{
    public static void Seed(SmartCemDb db)
    {
        EnsureNewTables(db);

        if (!db.Partners.Any())
        {
            db.Partners.AddRange(
                new Partner { Name = "Nguyễn Văn B", Phone = "0877xxxxxx", Area = "Xã Tản Lĩnh – Ba Vì", Lat = 21.0312, Lng = 105.8521, Rating = 4.9, CompletedJobs = 214, Color = "#2e7d6b" },
                new Partner { Name = "Trần Thị C", Phone = "0877xxxxxx", Area = "Xã Vật Lại – Ba Vì", Lat = 21.0268, Lng = 105.8463, Rating = 4.8, CompletedJobs = 187, Color = "#c9a84c" },
                new Partner { Name = "Lê Văn D", Phone = "0877xxxxxx", Area = "TT. Tây Đằng – Ba Vì", Lat = 21.0405, Lng = 105.8602, Rating = 4.6, CompletedJobs = 96, Color = "#2563a8" },
                new Partner { Name = "Phạm Thị E", Phone = "0877xxxxxx", Area = "Xã Minh Quang – Ba Vì", Lat = 21.0191, Lng = 105.8388, Rating = 4.7, CompletedJobs = 132, Color = "#8b3a52" },
                new Partner { Name = "Đỗ Văn G", Phone = "0877xxxxxx", Area = "Xã Cẩm Lĩnh – Ba Vì", Lat = 21.0522, Lng = 105.8730, Rating = 4.3, CompletedJobs = 41, Color = "#5b3fa8" },
                new Partner { Name = "Vũ Thị H", Phone = "0877xxxxxx", Area = "Xã Phú Cường – Ba Vì", Lat = 21.0334, Lng = 105.8710, Rating = 4.5, CompletedJobs = 73, Color = "#d1743c" },
                new Partner { Name = "Hoàng Văn K", Phone = "0877xxxxxx", Area = "Xã Thái Hòa – Ba Vì", Lat = 21.0145, Lng = 105.8575, Rating = 4.4, CompletedJobs = 58, Color = "#3c8dbc" },
                new Partner { Name = "Bùi Thị L", Phone = "0877xxxxxx", Area = "Xã Đồng Thái – Ba Vì", Lat = 21.0460, Lng = 105.8420, Rating = 4.8, CompletedJobs = 160, Color = "#7a9a2e" });
            db.SaveChanges();
        }

        // Gói dịch vụ theo kế hoạch kinh doanh của dự án
        void Pkg(string code, string name, string cat, decimal price, string unit, bool rec, int? days, string icon, string color, string desc)
        {
            if (db.ServicePackages.Any(p => p.Code == code)) return;
            db.ServicePackages.Add(new ServicePackage { Code = code, Name = name, Category = cat, Price = price, Unit = unit, IsRecurring = rec, RecurringDays = days, IconClass = icon, Color = color, Description = desc });
        }
        Pkg("ML-01", "Gói lẻ – Dọn mộ & thắp hương", "cleaning", 200000, "lần", false, null, "fa-broom", "#2e7d6b",
            "Dọn cỏ, lau bia, dâng hương hoa theo nghi thức. Ảnh trước/sau, nghiệm thu AI Verify. (150.000 – 250.000 đ/lần)");
        Pkg("ML-02", "Gói Lễ Tết", "ceremony", 550000, "dịp", false, null, "fa-gift", "#c9a84c",
            "Tảo mộ + mâm lễ chuẩn nghi thức cho Tết, Thanh minh, Rằm tháng Bảy, giỗ. (450.000 – 650.000 đ/dịp)");
        Pkg("ML-03", "Gói Hiếu Đạo trọn năm", "maintenance", 2400000, "năm", true, 365, "fa-heart", "#8b3a52",
            "Chăm sóc định kỳ + 4 dịp lễ trong năm + nhắc lịch âm tự động + báo cáo hình ảnh. (1.800.000 – 3.600.000 đ/năm)");
        db.SaveChanges();

        if (!db.PlanningProjects.Any())
        {
            var now = DateTime.UtcNow;
            db.PlanningProjects.AddRange(
                new PlanningProject { Cemetery = "Nghĩa trang Đồng Mả", Commune = "Xã Tản Lĩnh", Province = "Hà Nội", AreaHa = 1.2, Fee = 28_000_000, Step = 6, SlotsGained = 410, StartedAt = now.AddDays(-120), FinishedAt = now.AddDays(-80) },
                new PlanningProject { Cemetery = "Nghĩa trang Cồn Sậy", Commune = "Xã Vật Lại", Province = "Hà Nội", AreaHa = 0.8, Fee = 20_000_000, Step = 4, SlotsGained = 265, StartedAt = now.AddDays(-35) },
                new PlanningProject { Cemetery = "Nghĩa trang Bãi Dâu", Commune = "Xã Yên Phú", Province = "Hưng Yên", AreaHa = 2.0, Fee = 40_000_000, Step = 3, SlotsGained = 690, StartedAt = now.AddDays(-20) },
                new PlanningProject { Cemetery = "Nghĩa trang Cánh Đồng Sau", Commune = "Xã Đại Đồng", Province = "Bắc Ninh", AreaHa = 0.6, Fee = 15_000_000, Step = 2, SlotsGained = 190, StartedAt = now.AddDays(-9) },
                new PlanningProject { Cemetery = "Nghĩa trang Gò Dài", Commune = "Xã Hải Hậu", Province = "Nam Định", AreaHa = 1.5, Fee = 32_000_000, Step = 1, SlotsGained = 0, StartedAt = now.AddDays(-2) });
            db.SaveChanges();
        }

        if (!db.CareJobs.Any())
        {
            // Một ca đã nghiệm thu thành công làm dữ liệu mẫu
            var g = db.Graves.OrderBy(x => x.Id).FirstOrDefault();
            var p = db.Partners.OrderBy(x => x.Id).FirstOrDefault();
            if (g is not null && p is not null)
            {
                var t = DateTime.UtcNow.AddHours(-20);
                db.CareJobs.Add(new CareJob
                {
                    JobCode = "CJ-2026-00001", GraveId = g.Id, PartnerId = p.Id, Status = CareJobStatus.Verified, EtaMinutes = 14,
                    CreatedAt = t, AcceptedAt = t.AddMinutes(3), CheckinAt = t.AddMinutes(20), CheckinDistanceM = 1.8,
                    BeforeAt = t.AddMinutes(22), AfterAt = t.AddMinutes(51), BeforeInApp = true,
                    BeforeHash = "f0e0c0c8f8f0e0c0", AfterHash = "f0e0e0e8f8f8f0e0", BeforeWeed = 0.46, AfterWeed = 0.04,
                    VerifyScore = 96.5, ReportSentAt = t.AddMinutes(51), Note = "Dữ liệu mẫu"
                });
                db.SaveChanges();
            }
        }
    }

    // EnsureCreated không tạo bảng mới trên DB đã tồn tại → chạy script CREATE ... IF NOT EXISTS
    static void EnsureNewTables(SmartCemDb db)
    {
        var script = db.Database.GenerateCreateScript();
        foreach (var raw in Regex.Split(script, @";\s*\r?\n"))
        {
            var stmt = raw.Trim();
            if (stmt.Length == 0) continue;
            if (!Regex.IsMatch(stmt, @"\b(Partners|CareJobs|PlanningProjects)\b")) continue;
            stmt = Regex.Replace(stmt, @"^CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ");
            stmt = Regex.Replace(stmt, @"^CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ");
            stmt = Regex.Replace(stmt, @"^CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ");
#pragma warning disable EF1002
            db.Database.ExecuteSqlRaw(stmt);
#pragma warning restore EF1002
        }
    }
}

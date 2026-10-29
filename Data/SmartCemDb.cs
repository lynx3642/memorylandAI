using Microsoft.EntityFrameworkCore;
using SmartCem.Models;

namespace SmartCem.Data;

public class SmartCemDb : DbContext
{
    public SmartCemDb(DbContextOptions<SmartCemDb> options) : base(options) { }

    public DbSet<Grave> Graves => Set<Grave>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<ZoneMap> ZoneMaps => Set<ZoneMap>();
    public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();
    public DbSet<ServiceOrderFull> ServiceOrdersFull => Set<ServiceOrderFull>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Grave>().HasIndex(g => g.LotCode).IsUnique();
        mb.Entity<Grave>().HasIndex(g => g.Zone);
        mb.Entity<Lot>().HasIndex(l => l.LotCode).IsUnique();
        mb.Entity<Lot>().HasOne(l => l.Grave).WithMany().HasForeignKey(l => l.GraveId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<ZoneMap>().HasIndex(z => z.Code).IsUnique();
        mb.Entity<ServiceOrderFull>().HasIndex(o => o.OrderCode).IsUnique();
        mb.Entity<ServicePackage>().HasIndex(p => p.Code).IsUnique();
    }
}

public static class DbSeeder
{
    public static void Seed(SmartCemDb db)
    {
        if (db.Zones.Any()) return;

        var zones = new Zone[] {
            new() { Code="A", Name="Khu Thiên Ân",   TotalLots=200, Description="Khu phần A – vị trí trung tâm, gần cổng chính" },
            new() { Code="B", Name="Khu Vĩnh Phúc",  TotalLots=300, Description="Khu phần B – khu vực phía đông, diện tích rộng" },
            new() { Code="C", Name="Khu Bình An",    TotalLots=150, Description="Khu phần C – khu vực phía tây, yên tĩnh" },
            new() { Code="D", Name="Khu Thiên Thần", TotalLots=80,  Description="Khu phần D – khu vực phía nam, gần vườn hoa" },
            new() { Code="E", Name="Khu Phúc Lộc",   TotalLots=400, Description="Khu phần E – khu mở rộng, hạ tầng hiện đại" },
        };
        db.Zones.AddRange(zones);

        var today = DateTime.UtcNow.Date;

        var graves = new List<Grave>
        {
            new() {
                FullName="Nguyễn Văn An", Alias="Cụ Ba",
                BornYear=1940, DiedYear=2018,
                DiedDate=new DateTime(2018, 11, 5), LunarDeathDate="28/09 Âm lịch",
                Zone="A", LotCode="A-01-01", GraveType="Mộ đơn", AreaM2=4.0,
                Hometown="Hà Nội",
                ContactName="Nguyễn Thế Bình", ContactRelation="Con trưởng", ContactPhone="0912.345.001", ContactAddress="Số 12 Quán Thánh, Ba Đình, Hà Nội",
                ContractType="Thuê 20 năm", ContractStartDate=new DateTime(2018, 11, 10), ContractEndDate=today.AddDays(25), // Sắp hết hạn trong 25 ngày!
                Status=GraveStatus.Active, Notes="Gia đình thường cúng rằm tháng 7 và tiết Thanh minh"
            },
            new() {
                FullName="Trần Thị Bích", Alias="Bà Hai",
                BornYear=1955, DiedYear=2020,
                DiedDate=new DateTime(2020, 10, 31), LunarDeathDate="15/09 Âm lịch", // Ngày giỗ sắp tới trong vài ngày!
                Zone="A", LotCode="A-01-02", GraveType="Mộ đôi", AreaM2=7.0,
                Hometown="Nam Định",
                ContactName="Trần Văn Cường", ContactRelation="Con trai", ContactPhone="0983.111.002", ContactAddress="Phường Lộc Vượng, TP. Nam Định",
                ContractType="Vĩnh viễn", ContractStartDate=new DateTime(2020, 11, 1),
                Status=GraveStatus.Active, Notes="Đã làm lễ tạ mộ năm 2023"
            },
            new() {
                FullName="Lê Văn Hùng", Alias="",
                BornYear=1948, DiedYear=2019,
                DiedDate=new DateTime(2019, 11, 12), LunarDeathDate="16/10 Âm lịch", // Giỗ sắp tới trong 2 tuần!
                Zone="B", LotCode="B-02-01", GraveType="Mộ đơn", AreaM2=3.5,
                Hometown="Thanh Hóa",
                ContactName="Lê Thu Hà", ContactRelation="Con gái", ContactPhone="0904.555.003", ContactAddress="P402 Chung cư Sunrise, Cầu Giấy, Hà Nội",
                ContractType="Thuê 20 năm", ContractStartDate=new DateTime(2019, 11, 15), ContractEndDate=today.AddDays(-10), // Đã hết hạn cách đây 10 ngày!
                Status=GraveStatus.Active, Notes="Cần liên hệ gia đình gia hạn phí quản lý"
            },
            new() {
                FullName="Phạm Thị Lan", Alias="Cụ bà Lan",
                BornYear=1960, DiedYear=2022,
                DiedDate=new DateTime(2022, 5, 20), LunarDeathDate="20/04 Âm lịch",
                Zone="B", LotCode="B-02-02", GraveType="Mộ đơn", AreaM2=3.5,
                Hometown="Hải Phòng",
                ContactName="Phạm Văn Dũng", ContactRelation="Cháu đích tôn", ContactPhone="0977.888.004", ContactAddress="Lê Chân, TP. Hải Phòng",
                ContractType="Vĩnh viễn", ContractStartDate=new DateTime(2022, 5, 25),
                Status=GraveStatus.Active, Notes="Gói chăm sóc dâng hoa hàng tháng"
            },
            new() {
                FullName="Hoàng Văn Thắng", Alias="",
                BornYear=1935, DiedYear=2015,
                DiedDate=new DateTime(2015, 11, 8), LunarDeathDate="27/09 Âm lịch", // Giỗ sắp tới!
                Zone="C", LotCode="C-01-01", GraveType="Mộ gia tộc", AreaM2=15.0,
                Hometown="Thừa Thiên Huế",
                ContactName="Hoàng Tuấn Minh", ContactRelation="Trưởng tộc", ContactPhone="0913.999.005", ContactAddress="TP. Huế",
                ContractType="Vĩnh viễn", ContractStartDate=new DateTime(2015, 11, 10),
                Status=GraveStatus.Active, Notes="Khuôn viên gia tộc họ Hoàng"
            },
            new() {
                FullName="Vũ Thị Hoa", Alias="",
                BornYear=1970, DiedYear=2023,
                DiedDate=new DateTime(2023, 8, 14), LunarDeathDate="28/06 Âm lịch",
                Zone="A", LotCode="A-02-01", GraveType="Mộ đơn", AreaM2=3.5,
                Hometown="Hà Nội",
                ContactName="Vũ Mạnh Tuấn", ContactRelation="Chồng", ContactPhone="0915.222.006", ContactAddress="Đống Đa, Hà Nội",
                ContractType="Thuê 30 năm", ContractStartDate=new DateTime(2023, 8, 20), ContractEndDate=new DateTime(2053, 8, 20),
                Status=GraveStatus.Pending, Notes="Đang chờ hoàn thiện ốp đá hoa cương"
            },
            new() {
                FullName="Đặng Văn Long", Alias="",
                BornYear=1952, DiedYear=2021,
                DiedDate=new DateTime(2021, 12, 1), LunarDeathDate="27/10 Âm lịch",
                Zone="E", LotCode="E-01-01", GraveType="Mộ đơn", AreaM2=4.0,
                Hometown="Bắc Giang",
                ContactName="Đặng Thị Nga", ContactRelation="Con gái", ContactPhone="0966.777.007", ContactAddress="TP. Bắc Giang",
                ContractType="Thuê 20 năm", ContractStartDate=new DateTime(2021, 12, 5), ContractEndDate=today.AddDays(45), // Sắp hết hạn trong 45 ngày
                Status=GraveStatus.Active, Notes="Đăng ký dịch vụ thắp hương ngày rằm và mùng 1"
            },
            new() {
                FullName="Bùi Thị Tuyết", Alias="",
                BornYear=1945, DiedYear=2016,
                DiedDate=new DateTime(2016, 4, 18), LunarDeathDate="12/03 Âm lịch",
                Zone="C", LotCode="C-02-01", GraveType="Mộ đơn", AreaM2=3.5,
                Hometown="Hà Nam",
                ContactName="Bùi Văn Sang", ContactRelation="Em trai", ContactPhone="0903.444.008", ContactAddress="Phủ Lý, Hà Nam",
                ContractType="Vĩnh viễn", ContractStartDate=new DateTime(2016, 4, 25),
                Status=GraveStatus.Active, Notes="Cụ bà thích hoa cúc vàng"
            },
        };
        db.Graves.AddRange(graves);

        db.AuditLogs.AddRange(
            new AuditLog { Action="add",     Content="Thêm hồ sơ: Nguyễn Văn An (A-01-01)", PerformedBy="Admin", At=DateTime.UtcNow.AddDays(-10) },
            new AuditLog { Action="import",  Content="Import 50 hồ sơ từ Excel – Khu Vĩnh Phúc", PerformedBy="Admin", At=DateTime.UtcNow.AddDays(-7) },
            new AuditLog { Action="qr",      Content="Xuất QR: Trần Thị Bích (A-01-02)", PerformedBy="Staff", At=DateTime.UtcNow.AddDays(-3) },
            new AuditLog { Action="service", Content="Tạo đơn dịch vụ SVO-2024-00001: Vệ sinh định kỳ", PerformedBy="Staff", At=DateTime.UtcNow.AddDays(-2) },
            new AuditLog { Action="payment", Content="Thanh toán đơn SVO-2024-00001: 350,000 VND (bank)", PerformedBy="Admin", At=DateTime.UtcNow.AddDays(-1) },
            new AuditLog { Action="edit",    Content="Cập nhật thời hạn hợp đồng: Lê Văn Hùng (B-02-01)", PerformedBy="Admin", At=DateTime.UtcNow.AddHours(-12) }
        );
        db.SaveChanges();

        // ── ZoneMaps ────────────────────────────────────────────────
        var zoneMaps = new List<ZoneMap>
        {
            new() { Code="A", Label="Khu Thiên Ân",  X=2,  Y=2,  W=28, H=44, Color="#2E7D6B", Rows=5, Cols=8  },
            new() { Code="B", Label="Khu Vĩnh Phúc", X=32, Y=2,  W=34, H=44, Color="#2563A8", Rows=5, Cols=10 },
            new() { Code="C", Label="Khu Bình An",   X=68, Y=2,  W=28, H=44, Color="#8B3A52", Rows=5, Cols=7  },
            new() { Code="D", Label="Khu Thiên Thần",X=2,  Y=52, W=20, H=42, Color="#C9A84C", Rows=4, Cols=5  },
            new() { Code="E", Label="Khu Phúc Lộc",  X=25, Y=52, W=71, H=42, Color="#5B3FA8", Rows=6, Cols=14 },
        };
        db.ZoneMaps.AddRange(zoneMaps);
        db.SaveChanges();

        // ── Lots Generation ─────────────────────────────────────────
        var graveLookup = db.Graves.ToList();
        var rng = new Random(42);
        var lots = new List<Lot>();

        foreach (var zm in zoneMaps)
        {
            double gapX = 0.6, gapY = 0.6;
            double cellW = Math.Round((zm.W - gapX * (zm.Cols + 1)) / zm.Cols, 3);
            double cellH = Math.Round((zm.H - gapY * (zm.Rows + 1)) / zm.Rows, 3);

            for (int r = 0; r < zm.Rows; r++)
            {
                for (int c = 0; c < zm.Cols; c++)
                {
                    var code = $"{zm.Code}-{r+1:D2}-{c+1:D2}";
                    double px = Math.Round(zm.X + gapX + c * (cellW + gapX), 3);
                    double py = Math.Round(zm.Y + gapY + r * (cellH + gapY), 3);

                    var grave = graveLookup.FirstOrDefault(g => g.LotCode == code);
                    var st = grave != null      ? LotStatus.Occupied
                           : rng.Next(0, 14)==0 ? LotStatus.Reserved
                           : LotStatus.Empty;

                    lots.Add(new Lot {
                        LotCode = code, ZoneCode = zm.Code,
                        X = px, Y = py, W = cellW, H = cellH,
                        Status = st, GraveId = grave?.Id,
                        Row = r + 1, Col = c + 1
                    });
                }
            }
        }
        db.Lots.AddRange(lots);
        db.SaveChanges();

        // ── Seed ServicePackages ────────────────────────────────────
        var packages = new List<ServicePackage>
        {
            new() { Code="SVC-01", Name="Vệ sinh mộ định kỳ",        Category="cleaning",     Price=350000,  Unit="lần",   IsRecurring=true,  RecurringDays=30,  IconClass="fa-broom",         Color="#2E7D6B", Description="Làm sạch bia mộ, khu vực xung quanh, nhổ cỏ dại và đánh bóng lư hương" },
            new() { Code="SVC-02", Name="Dâng hoa tươi & Nhang đèn",  Category="flower",       Price=150000,  Unit="lần",   IsRecurring=false, IconClass="fa-fan",           Color="#C9A84C", Description="Cắm hoa cúc/hoa hồng tươi theo mùa, dâng 3 nén hương và quả tươi" },
            new() { Code="SVC-03", Name="Gói chăm sóc tháng",         Category="maintenance",  Price=1200000, Unit="tháng", IsRecurring=true,  RecurringDays=30,  IconClass="fa-star",          Color="#5B3FA8", Description="Vệ sinh 2 lần/tháng + dâng hoa tươi ngày Rằm và Mùng 1" },
            new() { Code="SVC-04", Name="Gói chăm sóc năm trọn gói",  Category="maintenance",  Price=12000000,Unit="năm",   IsRecurring=true,  RecurringDays=365, IconClass="fa-crown",         Color="#8B3A52", Description="Vệ sinh định kỳ + dâng hoa ngày lễ tết + cúng giỗ + chụp ảnh báo cáo gia quyến" },
            new() { Code="SVC-05", Name="Cắt tỉa & Chăm sóc cây cảnh",Category="cleaning",     Price=200000,  Unit="lần",   IsRecurring=false, IconClass="fa-scissors",      Color="#2563A8", Description="Cắt tỉa cây xanh khuôn viên, xới đất bón phân, phun chống mối mọt" },
            new() { Code="SVC-06", Name="Lễ cúng giỗ chu đáo tại mộ", Category="ceremony",     Price=2500000, Unit="lần",   IsRecurring=false, IconClass="fa-hands-praying",  Color="#C9A84C", Description="Tổ chức lễ cúng ngày giỗ: mâm ngũ quả, mâm cỗ chay/mặn, hoa tươi, nhang đèn, đọc sớ" },
            new() { Code="SVC-07", Name="Chụp ảnh & Quay video báo cáo", Category="other",      Price=300000,  Unit="lần",   IsRecurring=false, IconClass="fa-camera",        Color="#7A6020", Description="Ghi lại hình ảnh, video chất lượng cao sau khi làm lễ hoặc vệ sinh gửi qua Zalo/Email" },
            new() { Code="SVC-08", Name="Tu bổ – Khắc lại bia mộ",   Category="maintenance",  Price=1500000, Unit="lần",   IsRecurring=false, IconClass="fa-hammer",        Color="#2E7D6B", Description="Sơn lại chữ chìm/nổi, đánh bóng đá hoa cương, gia cố chân bia" },
        };
        db.ServicePackages.AddRange(packages);
        db.SaveChanges();

        // ── Seed ServiceOrdersFull ──────────────────────────────────
        var svPackages = db.ServicePackages.ToList();
        var svGraves   = db.Graves.ToList();
        var orderSeed  = new List<ServiceOrderFull>
        {
            new() { OrderCode="SVO-2024-00001", GraveId=svGraves[0].Id, PackageId=svPackages[0].Id, ServiceName=svPackages[0].Name, Price=350000, Status=ServiceOrderFullStatus.Done, AssignedTo="NV. Bảo", ScheduledAt=DateTime.UtcNow.AddDays(-2), CompletedAt=DateTime.UtcNow.AddDays(-2), CompletionNote="Đã dọn sạch cỏ, lau bóng lư hương, gửi ảnh Zalo cho anh Bình", IsPaid=true, PaymentMethod="bank", PaidAt=DateTime.UtcNow.AddDays(-2) },
            new() { OrderCode="SVO-2024-00002", GraveId=svGraves[1].Id, PackageId=svPackages[1].Id, ServiceName=svPackages[1].Name, Price=150000, Status=ServiceOrderFullStatus.InProgress, AssignedTo="NV. Lan", ScheduledAt=DateTime.UtcNow.AddDays(1), Notes="Cắm hoa cúc vàng đại đóa chuẩn bị ngày giỗ", IsPaid=true, PaymentMethod="cash", PaidAt=DateTime.UtcNow.AddDays(-1) },
            new() { OrderCode="SVO-2024-00003", GraveId=svGraves[2].Id, PackageId=svPackages[2].Id, ServiceName=svPackages[2].Name, Price=1200000, Status=ServiceOrderFullStatus.Confirmed, AssignedTo="NV. Nam", ScheduledAt=DateTime.UtcNow.AddDays(3), Notes="Gói chăm sóc tháng – Chị Hà đăng ký", IsPaid=false },
            new() { OrderCode="SVO-2024-00004", GraveId=svGraves[3].Id, PackageId=svPackages[4].Id, ServiceName=svPackages[4].Name, Price=200000, Status=ServiceOrderFullStatus.Pending, ScheduledAt=DateTime.UtcNow.AddDays(4), Notes="Yêu cầu cắt tỉa lại cây tùng trước mộ" },
            new() { OrderCode="SVO-2024-00005", GraveId=svGraves[4].Id, PackageId=svPackages[5].Id, ServiceName=svPackages[5].Name, Price=2500000, Status=ServiceOrderFullStatus.Confirmed, AssignedTo="NV. Toàn", ScheduledAt=DateTime.UtcNow.AddDays(6), Notes="Lễ cúng giỗ lần thứ 10 – Gia đình đặt mâm cỗ chay đặc biệt", IsPaid=true, PaymentMethod="qr", PaidAt=DateTime.UtcNow },
            new() { OrderCode="SVO-2024-00006", GraveId=svGraves[6].Id, PackageId=svPackages[3].Id, ServiceName=svPackages[3].Name, Price=12000000, Discount=1000000, Status=ServiceOrderFullStatus.InProgress, AssignedTo="NV. Bảo", ScheduledAt=DateTime.UtcNow.AddDays(2), Notes="Gói chăm sóc năm trọn gói cho cụ Long", IsPaid=true, PaymentMethod="bank", PaidAt=DateTime.UtcNow.AddDays(-15) },
            new() { OrderCode="SVO-2024-00007", GraveId=svGraves[7].Id, PackageId=svPackages[0].Id, ServiceName=svPackages[0].Name, Price=350000, Status=ServiceOrderFullStatus.Cancelled, Notes="Thân nhân hoãn lại do có việc đột xuất", IsPaid=false },
        };
        db.ServiceOrdersFull.AddRange(orderSeed);
        db.SaveChanges();
    }
}

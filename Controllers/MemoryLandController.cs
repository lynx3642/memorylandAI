using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCem.Data;
using SmartCem.Models;
using SmartCem.Services;

namespace SmartCem.Controllers;

/// <summary>MemoryLand AI – 8 module AI lõi + MemoryLand App + Partner App (mô phỏng).</summary>
[ApiController]
[Route("api/ml")]
public class MemoryLandController(SmartCemDb db) : ControllerBase
{
    static readonly ConcurrentDictionary<int, (Site site, TerrainResult terrain, object plan)> Cache = new();
    static readonly string[] ActiveStates = ["Dispatched", "Accepted", "CheckedIn", "BeforeDone"];

    (Site site, TerrainResult terrain, object plan) Analysis(int seed) => Cache.GetOrAdd(seed, s =>
    {
        var site = SiteSim.Generate(s);
        var terrain = TerrainAi.Analyze(site);
        return (site, terrain, PlannerAi.Run(site, terrain));
    });

    // ═════════════ 1) Khối AI Core ═════════════════════════════════════════

    [HttpGet("modules")]
    public async Task<IActionResult> Modules()
    {
        var (site, terrain, _) = Analysis(7);
        var jobs = await db.CareJobs.ToListAsync();
        var verified = jobs.Count(j => j.Status == CareJobStatus.Verified);
        var rejected = jobs.Count(j => j.Status == CareJobStatus.Rejected);
        var partners = await db.Partners.CountAsync();
        var kb = AssistantAi.Kb.Count;
        object M(int n, string id, string name, string tech, string input, string output, string icon, string stat, string statLabel, string page)
            => new { n, id, name, tech, input, output, icon, stat, statLabel, page };
        return Ok(new[]
        {
            M(1, "vision", "AI Vision", "YOLOv8-seg · Mask R-CNN · SAM (mô phỏng)", "Ảnh trực giao 1–2 cm, mây điểm 3D", "Tọa độ, kích thước, phân loại từng ngôi mộ", "fa-eye",
                $"{site.Graves.Average(g => g.Conf):P1}", "độ tin cậy TB / mAP mục tiêu > 91%", "ai.html#vision"),
            M(2, "terrain", "AI Terrain", "Phân tích địa hình · D8 flow", "Mô hình số bề mặt DSM/DTM", "Độ dốc, hướng dòng chảy, vùng ngập úng", "fa-mountain",
                $"{terrain.Channels.Count} ô", "rãnh thoát nước · " + terrain.Flood.Count + " ô nguy cơ ngập", "ai.html#terrain"),
            M(3, "planner", "AI Planner", "Quy hoạch tối ưu ràng buộc (OR-Tools style)", "Ranh giới, hiện trạng, quy chuẩn", "2–3 phương án phân lô tối ưu quỹ đất", "fa-draw-polygon",
                "+20–30%", "số ô mộ so với hiện trạng", "ai.html#planner"),
            M(4, "3d", "AI 3D", "Dựng hình 3D trước – sau (canvas 3D)", "Hiện trạng + phương án", "Mô phỏng 3D phục vụ họp dân", "fa-cube",
                "3 phương án", "xem 3D xoay tự do", "ai.html#viewer3d"),
            M(5, "forecast", "AI Forecast", "Holt-Winters · xu hướng + mùa vụ", "Dữ liệu an táng 36 tháng", "Dự báo thời điểm hết đất theo khu", "fa-chart-line",
                "5 khu", "dự báo 24 tháng", "ai.html#forecast"),
            M(6, "verify", "AI Verify", "Hash EXIF · GPS · so sánh ảnh (CLIP/Siamese mô phỏng)", "Ảnh nghiệm thu, GPS, QR", "Thẩm định sạch sẽ, chống nhầm mộ", "fa-shield-halved",
                $"{verified} ✓ / {rejected} ✗", "ca nghiệm thu đạt / từ chối", "app.html#partner"),
            M(7, "assistant", "AI Assistant", "LLM tiếng Việt + RAG (truy hồi nội bộ)", "Văn bản nghi lễ, phong tục", "Tư vấn văn khấn, nhắc lịch Âm 24/7", "fa-comments",
                $"{kb} chủ đề", "kho tri thức nghi lễ + lịch Âm", "app.html#customer"),
            M(8, "dispatch", "AI Dispatch", "Ghép cặp bipartite (Hungarian)", "Vị trí đối tác, đánh giá sao, đơn hàng", "Điều phối đối tác nhận việc nhanh nhất", "fa-route",
                $"{partners} đối tác", "tối ưu khoảng cách × đánh giá × tải", "app.html#admin"),
        });
    }

    [HttpGet("drone/plan")]
    public IActionResult DronePlan([FromQuery] double areaHa = 1.0, [FromQuery] double altitude = 80, [FromQuery] int overlapFront = 80, [FromQuery] int overlapSide = 70)
    {
        areaHa = Math.Clamp(areaHa, 0.2, 20); altitude = Math.Clamp(altitude, 40, 120);
        double gsd = altitude * 0.0137;                                  // cm/pixel (camera 45MP, 35 mm)
        double footW = gsd * 8192 / 100, footH = gsd * 5460 / 100;         // m
        double side = Math.Sqrt(areaHa * 10000);
        double lineGap = footW * (1 - overlapSide / 100.0), shotGap = footH * (1 - overlapFront / 100.0);
        int lines = (int)Math.Ceiling(side / lineGap) + 1, perLine = (int)Math.Ceiling(side / shotGap) + 1;
        double path = lines * side + (lines - 1) * lineGap, speed = Math.Min(12, shotGap / 1.6);
        double flightMin = path / speed / 60 + 3;
        int gcp = areaHa <= 1 ? 4 : 6;
        double setup = gcp * 4 + 8;
        double proc = lines * perLine * 0.04 + 12;
        double manualDays = areaHa * 6;
        return Ok(new
        {
            areaHa, altitude, gsdCm = Math.Round(gsd, 2), lines, photos = lines * perLine,
            lineSpacingM = Math.Round(lineGap, 1), shotSpacingM = Math.Round(shotGap, 1), pathKm = Math.Round(path / 1000, 2),
            speedMs = Math.Round(speed, 1), flightMin = Math.Round(flightMin), gcp, setupMin = Math.Round(setup),
            processingMin = Math.Round(proc), totalMin = Math.Round(flightMin + setup + proc),
            manualDays = Math.Round(manualDays, 1), savedPct = Math.Round(100 * (1 - (flightMin + setup + proc) / 60.0 / 8 / manualDays), 0),
            rtkAccuracyCm = 2.0, sideM = Math.Round(side, 1)
        });
    }

    [HttpGet("site")]
    public IActionResult SiteData([FromQuery] int seed = 7)
    {
        var (s, _, _) = Analysis(seed);
        return Ok(new
        {
            width = s.W, height = s.H, cell = s.Cell, cols = s.Cols, rows = s.Rows,
            graves = s.Graves.Select(g => new { g.Id, x = Math.Round(g.X, 2), y = Math.Round(g.Y, 2), w = g.W, d = g.D, a = Math.Round(g.Angle, 1), type = g.Type, conf = g.Conf, weed = g.Weed }),
            trees = s.Trees.Select(t => new { x = Math.Round(t.X, 2), y = Math.Round(t.Y, 2), r = Math.Round(t.R, 2) }),
            dem = s.Dem,
            summary = s.Graves.GroupBy(g => g.Type).Select(g => new { type = g.Key, count = g.Count() }),
            minZ = s.Dem.Min(), maxZ = s.Dem.Max()
        });
    }

    [HttpGet("terrain")]
    public IActionResult TerrainData([FromQuery] int seed = 7)
    {
        var (_, t, _) = Analysis(seed);
        return Ok(new
        {
            slope = t.Slope, acc = t.Acc, channels = t.Channels, flood = t.Flood,
            maxSlope = Math.Round(t.MaxSlope, 1), avgSlope = Math.Round(t.AvgSlope, 2), pits = t.Pits
        });
    }

    [HttpGet("planner")]
    public IActionResult PlannerData([FromQuery] int seed = 7) => Ok(Analysis(seed).plan);

    [HttpGet("forecast")]
    public async Task<IActionResult> Forecast()
    {
        var zones = await db.Zones.OrderBy(z => z.Code).ToListAsync();
        var fill = new Dictionary<string, double> { ["A"] = 0.72, ["B"] = 0.55, ["C"] = 0.64, ["D"] = 0.81, ["E"] = 0.38 };
        return Ok(zones.Select(z => ForecastAi.Run(z.Code, z.TotalLots, fill.GetValueOrDefault(z.Code, 0.5), DateTime.Now)));
    }

    [HttpGet("lunar")]
    public IActionResult LunarToday([FromQuery] DateTime? date)
    {
        var d = (date ?? DateTime.Now).Date; var l = Lunar.FromSolar(d);
        return Ok(new { solar = d.ToString("yyyy-MM-dd"), day = l.Day, month = l.Month, year = l.Year, leap = l.Leap, yearName = l.YearName, text = $"{l.Day:D2}/{l.Month:D2} Âm lịch · năm {l.YearName}" });
    }

    [HttpPost("assistant")]
    public async Task<IActionResult> Assistant([FromBody] AssistantRequest req)
    {
        Grave? g = req.GraveId is null ? null : await db.Graves.FindAsync(req.GraveId);
        DateTime? next = null;
        var dm = Lunar.ParseDayMonth(g?.LunarDeathDate);
        if (dm is not null) next = Lunar.NextSolarOf(dm.Value.d, dm.Value.m, DateTime.Now);
        var pk = await db.ServicePackages.Where(p => p.IsActive && (p.Code.StartsWith("ML-") || p.Code == "SVC-06")).OrderBy(p => p.Price).ToListAsync();
        string PackagesText() => "Bảng giá hiện hành:\n" + string.Join("\n", pk.Select(p => $"• {p.Name}: {p.Price:N0}đ/{p.Unit}"));
        var reply = AssistantAi.Reply(req.Message, PackagesText, g?.FullName, next, g?.LunarDeathDate);
        await Task.Delay(350);   // mô phỏng độ trễ suy luận
        return Ok(reply);
    }

    // ═════════════ 2) MemoryLand App (khách hàng) ═══════════════════════════

    async Task<Dictionary<int, (double lat, double lng, string lot)>> GraveGpsMap()
    {
        var lots = await db.Lots.ToListAsync();
        var graves = await db.Graves.ToListAsync();
        var map = new Dictionary<int, (double, double, string)>();
        foreach (var g in graves)
        {
            var l = lots.FirstOrDefault(x => x.GraveId == g.Id) ?? lots.FirstOrDefault(x => x.LotCode == g.LotCode);
            var (lat, lng) = l is null ? Geo.LotGps(50, 50) : Geo.LotGps(l.X + l.W / 2, l.Y + l.H / 2);
            map[g.Id] = (lat, lng, g.LotCode);
        }
        return map;
    }

    [HttpGet("app/graves")]
    public async Task<IActionResult> AppGraves()
    {
        var gps = await GraveGpsMap();
        var graves = await db.Graves.Where(g => g.Status != GraveStatus.Archived).OrderBy(g => g.Id).ToListAsync();
        return Ok(graves.Select(g =>
        {
            DateTime? next = null; int? days = null;
            var dm = Lunar.ParseDayMonth(g.LunarDeathDate);
            if (dm is not null) { next = Lunar.NextSolarOf(dm.Value.d, dm.Value.m, DateTime.Now); if (next is not null) days = (next.Value - DateTime.Now.Date).Days; }
            return new
            {
                g.Id, g.FullName, g.Alias, g.Zone, g.LotCode, g.BornYear, g.DiedYear, g.LunarDeathDate, g.ContactName, g.ContactRelation, g.ContactPhone,
                lat = gps[g.Id].lat, lng = gps[g.Id].lng,
                nextAnniversary = next?.ToString("yyyy-MM-dd"), daysUntil = days
            };
        }));
    }

    [HttpGet("reminders")]
    public async Task<IActionResult> Reminders([FromQuery] int withinDays = 90)
    {
        var graves = await db.Graves.ToListAsync();
        var list = new List<object>();
        foreach (var g in graves)
        {
            var dm = Lunar.ParseDayMonth(g.LunarDeathDate);
            if (dm is null) continue;
            var next = Lunar.NextSolarOf(dm.Value.d, dm.Value.m, DateTime.Now);
            if (next is null) continue;
            var days = (next.Value - DateTime.Now.Date).Days;
            if (days <= withinDays) list.Add(new { g.Id, g.FullName, g.LotCode, g.LunarDeathDate, solar = next.Value.ToString("yyyy-MM-dd"), daysUntil = days, g.ContactName, g.ContactPhone });
        }
        return Ok(list.OrderBy(x => (int)x.GetType().GetProperty("daysUntil")!.GetValue(x)!));
    }

    [HttpGet("app/catalog")]
    public async Task<IActionResult> Catalog()
        => Ok(await db.ServicePackages.Where(p => p.IsActive).OrderBy(p => p.Price)
            .Select(p => new { p.Id, p.Code, p.Name, p.Description, p.Category, p.Price, p.Unit, p.IsRecurring, p.IconClass, p.Color, featured = p.Code.StartsWith("ML-") }).ToListAsync());

    [HttpPost("app/book")]
    public async Task<IActionResult> Book([FromBody] BookRequest req)
    {
        var g = await db.Graves.FindAsync(req.GraveId);
        var pkg = await db.ServicePackages.FindAsync(req.PackageId);
        if (g is null || pkg is null) return NotFound(new { error = "Không tìm thấy mộ phần hoặc gói dịch vụ" });

        int seq = await db.ServiceOrdersFull.CountAsync() + 1; string code;
        do { code = $"SVO-{DateTime.Now.Year}-{seq++:D5}"; } while (await db.ServiceOrdersFull.AnyAsync(o => o.OrderCode == code));
        var order = new ServiceOrderFull
        {
            OrderCode = code, GraveId = g.Id, PackageId = pkg.Id, ServiceName = pkg.Name, Price = pkg.Price,
            Status = ServiceOrderFullStatus.Confirmed, ScheduledAt = req.ScheduledAt ?? DateTime.Now.AddDays(1), Notes = req.Note,
            IsPaid = true, PaymentMethod = req.PaymentMethod, PaidAt = DateTime.UtcNow
        };
        db.ServiceOrdersFull.Add(order);
        await db.SaveChangesAsync();

        int jseq = await db.CareJobs.CountAsync() + 1; string jcode;
        do { jcode = $"CJ-{DateTime.Now.Year}-{jseq++:D5}"; } while (await db.CareJobs.AnyAsync(j => j.JobCode == jcode));
        var job = new CareJob { JobCode = jcode, GraveId = g.Id, OrderId = order.Id, Note = req.Note };
        db.CareJobs.Add(job);
        await db.SaveChangesAsync();

        var assignment = await DispatchOne(job, null);
        db.AuditLogs.Add(new AuditLog { GraveId = g.Id, Action = "service", Content = $"MemoryLand App: đặt {pkg.Name} ({code}) · AI Dispatch → {assignment?.partner}", PerformedBy = g.ContactName ?? "Khách hàng" });
        await db.SaveChangesAsync();
        return Ok(new { jobId = job.Id, jobCode = job.JobCode, orderCode = code, price = pkg.Price, dispatch = assignment });
    }

    async Task<dynamic?> DispatchOne(CareJob job, ISet<int>? exclude)
    {
        var gps = await GraveGpsMap();
        var partners = await db.Partners.ToListAsync();
        var loads = await db.CareJobs.Where(j => j.PartnerId != null && j.Id != job.Id && ActiveStates.Contains(j.Status.ToString())).ToListAsync();
        var dps = partners.Select(p => new DispatchPartner(p.Id, p.Lat, p.Lng, p.Rating, loads.Count(j => j.PartnerId == p.Id), p.Available)).ToList();
        var g = gps[job.GraveId];
        var r = DispatchAi.Assign([new DispatchJob(job.Id, g.lat, g.lng)], dps, exclude).First();
        if (r.PartnerId is null) { job.Status = CareJobStatus.Unassigned; job.PartnerId = null; await db.SaveChangesAsync(); return null; }
        job.PartnerId = r.PartnerId; job.Status = CareJobStatus.Dispatched; job.EtaMinutes = r.EtaMin; job.DispatchScore = r.Cost;
        var order = job.OrderId is null ? null : await db.ServiceOrdersFull.FindAsync(job.OrderId);
        var pn = partners.First(p => p.Id == r.PartnerId);
        if (order is not null) order.AssignedTo = pn.Name;
        await db.SaveChangesAsync();
        return new { partnerId = pn.Id, partner = pn.Name, distanceKm = r.DistanceKm, etaMin = r.EtaMin, cost = r.Cost };
    }

    // ═════════════ 3) Công việc / Partner App ═══════════════════════════════

    object JobDto(CareJob j, Dictionary<int, Grave> graves, Dictionary<int, Partner> partners, Dictionary<int, (double lat, double lng, string lot)> gps, Dictionary<int, ServiceOrderFull> orders)
    {
        var g = graves.GetValueOrDefault(j.GraveId); var p = j.PartnerId is null ? null : partners.GetValueOrDefault(j.PartnerId.Value);
        var o = j.OrderId is null ? null : orders.GetValueOrDefault(j.OrderId.Value);
        object? verify = string.IsNullOrEmpty(j.VerifyJson) ? null : JsonSerializer.Deserialize<object>(j.VerifyJson);
        var gp = gps.GetValueOrDefault(j.GraveId);
        return new
        {
            j.Id, j.JobCode, status = j.Status.ToString(), j.GraveId,
            grave = g is null ? null : new { g.FullName, g.LotCode, g.Zone, g.ContactName, g.ContactPhone, g.LunarDeathDate, lat = gp.lat, lng = gp.lng },
            partner = p is null ? null : new { p.Id, p.Name, p.Phone, p.Rating, p.Color, p.Area },
            order = o is null ? null : new { o.Id, o.OrderCode, o.ServiceName, price = o.Price, o.Status, o.IsPaid, scheduledAt = o.ScheduledAt },
            j.EtaMinutes, j.DispatchScore, j.CreatedAt, j.AcceptedAt, j.CheckinAt, j.CheckinDistanceM, j.BeforeAt, j.AfterAt,
            j.BeforeWeed, j.AfterWeed, j.BeforeThumb, j.AfterThumb, j.VerifyScore, verify, j.ReportSentAt, j.Refunded, j.Note
        };
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> Jobs([FromQuery] int? graveId, [FromQuery] int? partnerId, [FromQuery] string? status)
    {
        var q = db.CareJobs.AsQueryable();
        if (graveId is not null) q = q.Where(j => j.GraveId == graveId);
        if (partnerId is not null) q = q.Where(j => j.PartnerId == partnerId);
        var list = await q.OrderByDescending(j => j.Id).Take(60).ToListAsync();
        if (!string.IsNullOrEmpty(status)) list = list.Where(j => status.Split(',').Contains(j.Status.ToString())).ToList();
        return Ok(await Shape(list));
    }

    async Task<IEnumerable<object>> Shape(List<CareJob> list)
    {
        var graves = await db.Graves.ToDictionaryAsync(g => g.Id);
        var partners = await db.Partners.ToDictionaryAsync(p => p.Id);
        var orders = await db.ServiceOrdersFull.ToDictionaryAsync(o => o.Id);
        var gps = await GraveGpsMap();
        return list.Select(j => JobDto(j, graves, partners, gps, orders)).ToList();
    }

    [HttpGet("jobs/{id:int}")]
    public async Task<IActionResult> Job(int id)
    {
        var j = await db.CareJobs.FindAsync(id);
        return j is null ? NotFound() : Ok((await Shape([j])).First());
    }

    [HttpPost("jobs/{id:int}/accept")]
    public async Task<IActionResult> Accept(int id)
    {
        var j = await db.CareJobs.FindAsync(id);
        if (j is null) return NotFound();
        if (j.Status != CareJobStatus.Dispatched) return BadRequest(new { error = "Đơn không ở trạng thái chờ nhận" });
        j.Status = CareJobStatus.Accepted; j.AcceptedAt = DateTime.UtcNow;
        var o = j.OrderId is null ? null : await db.ServiceOrdersFull.FindAsync(j.OrderId);
        if (o is not null) { o.Status = ServiceOrderFullStatus.InProgress; o.StartedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync();
        return Ok((await Shape([j])).First());
    }

    [HttpPost("jobs/{id:int}/checkin")]
    public async Task<IActionResult> Checkin(int id, [FromBody] CheckinRequest req)
    {
        var j = await db.CareJobs.FindAsync(id);
        if (j is null) return NotFound();
        if (j.Status != CareJobStatus.Accepted) return BadRequest(new { error = "Hãy nhận đơn trước khi check-in" });
        var gps = (await GraveGpsMap())[j.GraveId];
        var dist = Geo.Haversine(req.Lat, req.Lng, gps.lat, gps.lng);
        bool qrOk = string.Equals(req.Qr?.Trim(), gps.lot, StringComparison.OrdinalIgnoreCase);
        bool gpsOk = dist <= 5;
        if (!qrOk || !gpsOk)
            return Ok(new { ok = false, distanceM = Math.Round(dist, 1), qrOk, gpsOk,
                message = !gpsOk ? $"Sai vị trí: bạn cách mộ {dist:0.0} m (cho phép ≤ 5 m)" : $"Mã QR không khớp mộ {gps.lot}" });
        j.CheckinAt = DateTime.UtcNow; j.CheckinDistanceM = Math.Round(dist, 2); j.Status = CareJobStatus.CheckedIn;
        await db.SaveChangesAsync();
        return Ok(new { ok = true, distanceM = Math.Round(dist, 1), qrOk, gpsOk, message = $"Đã xác nhận đúng mộ {gps.lot} (lệch {dist:0.0} m)", job = (await Shape([j])).First() });
    }

    [HttpPost("jobs/{id:int}/photo")]
    public async Task<IActionResult> Photo(int id, [FromBody] PhotoRequest req)
    {
        var j = await db.CareJobs.FindAsync(id);
        if (j is null) return NotFound();
        var gps = (await GraveGpsMap())[j.GraveId];
        var hash = (req.Hash ?? "").ToLowerInvariant();
        if (hash.Length != 16 || !hash.All(Uri.IsHexDigit)) return BadRequest(new { error = "Hash ảnh không hợp lệ" });
        var capturedAt = req.CapturedAt.ToUniversalTime();

        if (req.Kind == "before")
        {
            if (j.Status != CareJobStatus.CheckedIn) return BadRequest(new { error = "Cần check-in tại mộ trước khi chụp ảnh" });
            j.BeforeHash = hash; j.BeforeWeed = req.Weed; j.BeforeAt = capturedAt; j.BeforeInApp = req.InApp; j.BeforeThumb = req.Thumb;
            j.Status = CareJobStatus.BeforeDone;
            await db.SaveChangesAsync();
            return Ok(new { ok = true, stage = "before", job = (await Shape([j])).First() });
        }
        if (req.Kind != "after") return BadRequest(new { error = "kind phải là before/after" });
        if (j.Status != CareJobStatus.BeforeDone) return BadRequest(new { error = "Cần chụp ảnh trước khi làm" });

        j.AfterHash = hash; j.AfterWeed = req.Weed; j.AfterAt = capturedAt; j.AfterThumb = req.Thumb;
        var afterDist = Geo.Haversine(req.Lat, req.Lng, gps.lat, gps.lng);
        var (pass, score, layers) = VerifyAi.Run(j.CheckinDistanceM ?? 99, afterDist, j.BeforeInApp, req.InApp, j.BeforeAt ?? capturedAt, capturedAt,
            j.AcceptedAt ?? DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, j.BeforeHash ?? "", hash, j.BeforeWeed ?? 0, req.Weed);
        j.VerifyScore = score;
        j.VerifyJson = JsonSerializer.Serialize(new { pass, score, layers, at = DateTime.UtcNow }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var order = j.OrderId is null ? null : await db.ServiceOrdersFull.FindAsync(j.OrderId);
        var partner = j.PartnerId is null ? null : await db.Partners.FindAsync(j.PartnerId);
        if (pass)
        {
            j.Status = CareJobStatus.Verified; j.ReportSentAt = DateTime.UtcNow;
            if (order is not null)
            {
                order.Status = ServiceOrderFullStatus.Done; order.CompletedAt = DateTime.UtcNow;
                order.CompletionNote = $"AI Verify {score:0.0}/100 – đã gửi báo cáo ảnh trước/sau cho gia đình";
            }
            if (partner is not null) partner.CompletedJobs++;
        }
        else
        {
            j.Status = CareJobStatus.Rejected;
            if (order is not null) order.Notes = (order.Notes + " | AI Verify từ chối nghiệm thu – chờ xử lý hoàn tiền/làm lại").Trim(' ', '|');
        }
        db.AuditLogs.Add(new AuditLog { GraveId = j.GraveId, Action = "verify", Content = $"AI Verify {j.JobCode}: {(pass ? "ĐẠT" : "KHÔNG ĐẠT")} ({score:0.0}/100)", PerformedBy = partner?.Name ?? "Đối tác" });
        await db.SaveChangesAsync();
        return Ok(new { ok = true, stage = "after", pass, score, layers, job = (await Shape([j])).First() });
    }

    [HttpPost("jobs/{id:int}/redispatch")]
    public async Task<IActionResult> Redo(int id)
    {
        var j = await db.CareJobs.FindAsync(id);
        if (j is null) return NotFound();
        if (j.Status != CareJobStatus.Rejected && j.Status != CareJobStatus.Unassigned) return BadRequest(new { error = "Chỉ làm lại với đơn bị từ chối" });
        var prev = j.PartnerId;
        j.PreviousPartnerId = prev;
        j.BeforeHash = j.AfterHash = j.BeforeThumb = j.AfterThumb = j.VerifyJson = null;
        j.BeforeWeed = j.AfterWeed = j.VerifyScore = null; j.BeforeAt = j.AfterAt = j.CheckinAt = j.AcceptedAt = null; j.CheckinDistanceM = null; j.Refunded = false;
        var a = await DispatchOne(j, prev is null ? null : new HashSet<int> { prev.Value });
        return Ok(new { ok = true, dispatch = a, job = (await Shape([j])).First() });
    }

    [HttpPost("jobs/{id:int}/refund")]
    public async Task<IActionResult> Refund(int id)
    {
        var j = await db.CareJobs.FindAsync(id);
        if (j is null) return NotFound();
        j.Refunded = true;
        var o = j.OrderId is null ? null : await db.ServiceOrdersFull.FindAsync(j.OrderId);
        if (o is not null) { o.Status = ServiceOrderFullStatus.Cancelled; o.IsPaid = false; o.Notes = (o.Notes + " | Hoàn tiền 100% theo cam kết").Trim(' ', '|'); }
        db.AuditLogs.Add(new AuditLog { GraveId = j.GraveId, Action = "payment", Content = $"Hoàn tiền 100% đơn {o?.OrderCode} ({o?.Price:N0}đ) do AI Verify từ chối", PerformedBy = "Hệ thống" });
        await db.SaveChangesAsync();
        return Ok(new { ok = true, refunded = o?.Price ?? 0 });
    }

    // ═════════════ 4) Đối tác & điều phối ═══════════════════════════════════

    [HttpGet("partners")]
    public async Task<IActionResult> Partners()
    {
        var jobs = await db.CareJobs.ToListAsync();
        var list = await db.Partners.OrderBy(p => p.Id).ToListAsync();
        return Ok(list.Select(p => new
        {
            p.Id, p.Name, p.Phone, p.Area, p.Lat, p.Lng, p.Rating, p.CompletedJobs, p.Available, p.Color,
            activeJobs = jobs.Count(j => j.PartnerId == p.Id && ActiveStates.Contains(j.Status.ToString()))
        }));
    }

    [HttpPost("partners/{id:int}/toggle")]
    public async Task<IActionResult> TogglePartner(int id)
    {
        var p = await db.Partners.FindAsync(id);
        if (p is null) return NotFound();
        p.Available = !p.Available; await db.SaveChangesAsync();
        return Ok(new { p.Id, p.Available });
    }

    /// <summary>Chạy lại AI Dispatch cho mọi đơn chưa được nhận (tối ưu toàn cục bằng Hungarian).</summary>
    [HttpPost("dispatch/rebalance")]
    public async Task<IActionResult> Rebalance()
    {
        var pending = await db.CareJobs.Where(j => j.Status == CareJobStatus.Dispatched || j.Status == CareJobStatus.Unassigned).ToListAsync();
        var gps = await GraveGpsMap();
        var partners = await db.Partners.ToListAsync();
        var busy = await db.CareJobs.Where(j => j.PartnerId != null && (j.Status == CareJobStatus.Accepted || j.Status == CareJobStatus.CheckedIn || j.Status == CareJobStatus.BeforeDone)).ToListAsync();
        var dps = partners.Select(p => new DispatchPartner(p.Id, p.Lat, p.Lng, p.Rating, busy.Count(j => j.PartnerId == p.Id), p.Available)).ToList();
        var res = DispatchAi.Assign(pending.Select(j => new DispatchJob(j.Id, gps[j.GraveId].lat, gps[j.GraveId].lng)).ToList(), dps);
        foreach (var r in res)
        {
            var j = pending.First(x => x.Id == r.JobId);
            j.PartnerId = r.PartnerId; j.EtaMinutes = r.EtaMin; j.DispatchScore = r.Cost;
            j.Status = r.PartnerId is null ? CareJobStatus.Unassigned : CareJobStatus.Dispatched;
            var o = j.OrderId is null ? null : await db.ServiceOrdersFull.FindAsync(j.OrderId);
            if (o is not null && r.PartnerId is not null) o.AssignedTo = partners.First(p => p.Id == r.PartnerId).Name;
        }
        await db.SaveChangesAsync();
        return Ok(new { reassigned = res.Count, assigned = res.Count(r => r.PartnerId != null), results = res });
    }

    /// <summary>Mô phỏng lô đơn ngẫu nhiên (không lưu DB) để xem thuật toán ghép cặp.</summary>
    [HttpPost("dispatch/simulate")]
    public async Task<IActionResult> SimulateDispatch([FromQuery] int count = 6, [FromQuery] int seed = 0)
    {
        var rng = seed == 0 ? new Random() : new Random(seed);
        var partners = await db.Partners.ToListAsync();
        var dps = partners.Select(p => new DispatchPartner(p.Id, p.Lat, p.Lng, p.Rating, 0, p.Available)).ToList();
        count = Math.Clamp(count, 1, 8);
        var jobs = Enumerable.Range(1, count).Select(i =>
        {
            var (lat, lng) = Geo.LotGps(rng.NextDouble() * 100, rng.NextDouble() * 100);
            return new DispatchJob(i, lat, lng);
        }).ToList();
        var res = DispatchAi.Assign(jobs, dps);
        return Ok(new
        {
            jobs = jobs.Select(j => new { j.Id, j.Lat, j.Lng }),
            results = res.Select(r => new { r.JobId, r.PartnerId, r.DistanceKm, r.EtaMin, r.Cost }),
            totalCost = Math.Round(res.Sum(r => r.Cost), 2)
        });
    }

    // ═════════════ 5) B2G – dự án quy hoạch & số liệu kinh doanh ════════════

    static readonly string[] StepNames = ["Khảo sát Drone RTK", "AI nhận diện hiện trạng", "AI tối ưu phân lô", "Mô phỏng 3D họp dân", "Cắm mốc thực địa", "Số hóa & bàn giao GIS"];

    [HttpGet("projects")]
    public async Task<IActionResult> Projects()
        => Ok((await db.PlanningProjects.OrderByDescending(p => p.StartedAt).ToListAsync())
            .Select(p => new { p.Id, p.Cemetery, p.Commune, p.Province, p.AreaHa, p.Fee, p.Step, stepName = StepNames[Math.Clamp(p.Step, 1, 6) - 1], p.SlotsGained, p.StartedAt, p.FinishedAt, done = p.Step >= 6 }));

    [HttpPost("projects")]
    public async Task<IActionResult> NewProject([FromBody] NewProjectRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Cemetery)) return BadRequest(new { error = "Thiếu tên nghĩa trang" });
        var p = new PlanningProject { Cemetery = r.Cemetery, Commune = r.Commune, Province = r.Province, AreaHa = r.AreaHa, Fee = r.Fee };
        db.PlanningProjects.Add(p);
        db.AuditLogs.Add(new AuditLog { Action = "add", Content = $"Ký hợp đồng quy hoạch B2G: {r.Cemetery} ({r.AreaHa} ha, {r.Fee:N0}đ)", PerformedBy = "Admin" });
        await db.SaveChangesAsync();
        return Ok(p);
    }

    [HttpPost("projects/{id:int}/advance")]
    public async Task<IActionResult> Advance(int id)
    {
        var p = await db.PlanningProjects.FindAsync(id);
        if (p is null) return NotFound();
        if (p.Step >= 6) return BadRequest(new { error = "Dự án đã bàn giao" });
        p.Step++;
        if (p.Step == 3 && p.SlotsGained == 0) p.SlotsGained = (int)(p.AreaHa * 330);
        if (p.Step == 6) p.FinishedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { Action = "edit", Content = $"Dự án {p.Cemetery}: bước {p.Step}/6 – {StepNames[p.Step - 1]}", PerformedBy = "Admin" });
        await db.SaveChangesAsync();
        return Ok(p);
    }

    [HttpGet("business")]
    public async Task<IActionResult> Business()
    {
        var orders = await db.ServiceOrdersFull.ToListAsync();
        var jobs = await db.CareJobs.ToListAsync();
        var projects = await db.PlanningProjects.ToListAsync();
        var partners = await db.Partners.ToListAsync();
        var b2c = orders.Where(o => o.IsPaid && o.Status != ServiceOrderFullStatus.Cancelled).Sum(o => o.Price - (o.Discount ?? 0));
        var b2g = projects.Sum(p => p.Fee);
        decimal commission = Math.Round(b2c * 0.12m);
        return Ok(new
        {
            b2gRevenue = b2g, b2cRevenue = b2c, partnerCommission = commission, saasRevenue = 5 * 300000m * 12,
            projects = projects.Count, projectsDone = projects.Count(p => p.Step >= 6), slotsGained = projects.Sum(p => p.SlotsGained),
            orders = orders.Count, jobs = jobs.Count, verified = jobs.Count(j => j.Status == CareJobStatus.Verified), rejected = jobs.Count(j => j.Status == CareJobStatus.Rejected),
            partners = partners.Count, avgRating = Math.Round(partners.Average(p => p.Rating), 2),
            activeUsers = await db.Graves.Select(g => g.ContactPhone).Distinct().CountAsync()
        });
    }
}

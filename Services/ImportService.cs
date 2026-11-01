using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using SmartCem.Data;
using SmartCem.Models;

namespace SmartCem.Services;

public interface IImportService
{
    Task<ImportResultDto> ImportFromFileAsync(IFormFile file, string? performedBy);
    Task<byte[]> GenerateExcelTemplateAsync();
    Task<ImportResultDto?> GetJobStatusAsync(int jobId);
    Task<IEnumerable<ImportResultDto>> GetRecentJobsAsync(int count = 10);
}

// CSV row mapping class
public class GraveCsvRow
{
    public string FullName { get; set; } = "";
    public string? Alias { get; set; }
    public int? BornYear { get; set; }
    public int? DiedYear { get; set; }
    public string Zone { get; set; } = "";
    public string LotCode { get; set; } = "";
    public string? Hometown { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? Notes { get; set; }
    public string? Status { get; set; }
}

public class ImportService(SmartCemDb db, IWebHostEnvironment env) : IImportService
{
    // ─── Main dispatcher ────────────────────────────────────────────
    public async Task<ImportResultDto> ImportFromFileAsync(IFormFile file, string? performedBy)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileType = ext switch
        {
            ".xlsx" or ".xls" => ImportFileType.Excel,
            ".csv"            => ImportFileType.Csv,
            ".json"           => ImportFileType.Json,
            _                 => throw new InvalidOperationException($"Định dạng '{ext}' không được hỗ trợ. Dùng .xlsx, .csv hoặc .json")
        };

        // Create job record
        var job = new ImportJob
        {
            FileName = file.FileName, FileType = fileType,
            Status = ImportJobStatus.Processing, StartedAt = DateTime.UtcNow
        };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync();

        try
        {
            List<GraveCsvRow> rows = fileType switch
            {
                ImportFileType.Excel => await ParseExcelAsync(file),
                ImportFileType.Csv   => await ParseCsvAsync(file),
                ImportFileType.Json  => await ParseJsonAsync(file),
                _                    => []
            };

            job.TotalRows = rows.Count;
            var errors = new StringBuilder();
            int imported = 0, failed = 0;

            foreach (var (row, idx) in rows.Select((r, i) => (r, i + 1)))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(row.FullName))
                    { errors.AppendLine($"Dòng {idx}: Thiếu FullName"); failed++; continue; }
                    if (string.IsNullOrWhiteSpace(row.Zone))
                    { errors.AppendLine($"Dòng {idx}: Thiếu Zone"); failed++; continue; }
                    if (string.IsNullOrWhiteSpace(row.LotCode))
                    { errors.AppendLine($"Dòng {idx}: Thiếu LotCode"); failed++; continue; }

                    // Skip duplicates
                    if (await db.Graves.AnyAsync(g => g.LotCode == row.LotCode))
                    { errors.AppendLine($"Dòng {idx}: LotCode '{row.LotCode}' đã tồn tại – bỏ qua"); failed++; continue; }

                    Enum.TryParse<GraveStatus>(row.Status ?? "Pending", true, out var status);
                    db.Graves.Add(new Grave
                    {
                        FullName = row.FullName.Trim(), Alias = row.Alias?.Trim(),
                        BornYear = row.BornYear, DiedYear = row.DiedYear,
                        Zone = row.Zone.Trim().ToUpper(), LotCode = row.LotCode.Trim().ToUpper(),
                        Hometown = row.Hometown?.Trim(),
                        ContactName = row.ContactName?.Trim(), ContactPhone = row.ContactPhone?.Trim(),
                        Notes = row.Notes?.Trim(), Status = status
                    });
                    imported++;

                    // Batch save every 50
                    if (imported % 50 == 0) await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    errors.AppendLine($"Dòng {idx}: {ex.Message}");
                    failed++;
                }
            }

            await db.SaveChangesAsync();

            // Log the import
            db.AuditLogs.Add(new AuditLog
            {
                Action = "import",
                Content = $"Import '{file.FileName}': {imported} thành công, {failed} lỗi",
                PerformedBy = performedBy
            });

            job.Imported = imported;
            job.Failed = failed;
            job.Status = ImportJobStatus.Done;
            job.ErrorLog = errors.Length > 0 ? errors.ToString() : null;
            job.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // ── Sync lots on map + broadcast SSE ───────────────
            if (imported > 0)
            {
                var newGraves = await db.Graves
                    .Where(g => g.CreatedAt >= job.StartedAt)
                    .ToListAsync();

                foreach (var g in newGraves)
                {
                    var lot = await db.Lots.FirstOrDefaultAsync(l => l.LotCode == g.LotCode);
                    if (lot != null && lot.Status != LotStatus.Occupied)
                    {
                        lot.Status  = LotStatus.Occupied;
                        lot.GraveId = g.Id;
                        lot.UpdatedAt = DateTime.UtcNow;
                    }
                    else if (lot == null)
                    {
                        var zone = await db.ZoneMaps.FirstOrDefaultAsync(z => z.Code == g.Zone);
                        if (zone != null)
                        {
                            db.Lots.Add(new Lot {
                                LotCode = g.LotCode, ZoneCode = g.Zone,
                                X = zone.X + 1, Y = zone.Y + 1, W = 2.5, H = 3.5,
                                Status = LotStatus.Occupied, GraveId = g.Id
                            });
                        }
                    }
                }
                await db.SaveChangesAsync();

                var zoneStats = await db.Lots
                    .GroupBy(l => l.ZoneCode)
                    .Select(g2 => new {
                        zone     = g2.Key,
                        total    = g2.Count(),
                        occupied = g2.Count(l => l.Status == LotStatus.Occupied),
                        reserved = g2.Count(l => l.Status == LotStatus.Reserved),
                        empty    = g2.Count(l => l.Status == LotStatus.Empty)
                    }).ToListAsync();

                var newGraveIds = newGraves.Select(g => (int?)g.Id).ToList();
                var updatedLots = await db.Lots
                    .Include(l => l.Grave)
                    .Where(l => newGraveIds.Contains(l.GraveId))
                    .Select(l => new {
                        id       = l.Id,
                        lotCode  = l.LotCode,
                        zoneCode = l.ZoneCode,
                        x = l.X, y = l.Y, w = l.W, h = l.H,
                        status   = l.Status.ToString(),
                        graveId  = l.GraveId,
                        graveName= l.Grave != null ? l.Grave.FullName : null
                    }).ToListAsync();

                SmartCem.Controllers.RealtimeController.Broadcast("import_complete", new {
                    jobId    = job.Id,
                    fileName = file.FileName,
                    imported, failed,
                    updatedLots,
                    zoneStats
                });
            }
        }
        catch (Exception ex)
        {
            job.Status = ImportJobStatus.Failed;
            job.ErrorLog = ex.Message;
            job.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return ToDto(job);
    }

    // ─── Excel parser ────────────────────────────────────────────────
    private static async Task<List<GraveCsvRow>> ParseExcelAsync(IFormFile file)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;
        using var pkg = new ExcelPackage(ms);
        var ws = pkg.Workbook.Worksheets[0];
        var rows = new List<GraveCsvRow>();

        // Detect header row (row 1)
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= ws.Dimension?.Columns; col++)
        {
            var h = ws.Cells[1, col].GetValue<string>();
            if (!string.IsNullOrWhiteSpace(h)) headers[h.Trim()] = col;
        }

        string? GetCell(ExcelWorksheet ws, int row, string key)
        {
            if (headers.TryGetValue(key, out int col))
                return ws.Cells[row, col].GetValue<string>();
            return null;
        }

        for (int row = 2; row <= (ws.Dimension?.Rows ?? 1); row++)
        {
            var fn = GetCell(ws, row, "FullName") ?? GetCell(ws, row, "Họ tên");
            if (string.IsNullOrWhiteSpace(fn)) continue;

            rows.Add(new GraveCsvRow
            {
                FullName    = fn,
                Alias       = GetCell(ws, row, "Alias") ?? GetCell(ws, row, "Tên gọi"),
                BornYear    = int.TryParse(GetCell(ws, row, "BornYear") ?? GetCell(ws, row, "Năm sinh"), out var by) ? by : null,
                DiedYear    = int.TryParse(GetCell(ws, row, "DiedYear") ?? GetCell(ws, row, "Năm mất"),  out var dy) ? dy : null,
                Zone        = GetCell(ws, row, "Zone")        ?? GetCell(ws, row, "Khu") ?? "",
                LotCode     = GetCell(ws, row, "LotCode")     ?? GetCell(ws, row, "Số lô") ?? "",
                Hometown    = GetCell(ws, row, "Hometown")    ?? GetCell(ws, row, "Quê quán"),
                ContactName = GetCell(ws, row, "ContactName") ?? GetCell(ws, row, "Người liên hệ"),
                ContactPhone= GetCell(ws, row, "ContactPhone")?? GetCell(ws, row, "Điện thoại"),
                Notes       = GetCell(ws, row, "Notes")       ?? GetCell(ws, row, "Ghi chú"),
                Status      = GetCell(ws, row, "Status")      ?? GetCell(ws, row, "Tình trạng"),
            });
        }
        return rows;
    }

    // ─── CSV parser ──────────────────────────────────────────────────
    private static async Task<List<GraveCsvRow>> ParseCsvAsync(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HeaderValidated = null, MissingFieldFound = null };
        using var csv = new CsvReader(reader, config);
        var records = new List<GraveCsvRow>();
        await foreach (var rec in csv.GetRecordsAsync<GraveCsvRow>())
            records.Add(rec);
        return records;
    }

    // ─── JSON parser ─────────────────────────────────────────────────
    private static async Task<List<GraveCsvRow>> ParseJsonAsync(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return await JsonSerializer.DeserializeAsync<List<GraveCsvRow>>(stream, opts) ?? [];
    }

    // ─── Excel template generator ────────────────────────────────────
    public async Task<byte[]> GenerateExcelTemplateAsync()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Danh sách mộ phần");

        // Headers
        var headers = new[] { "FullName", "Alias", "BornYear", "DiedYear", "Zone", "LotCode", "Hometown", "ContactName", "ContactPhone", "Notes", "Status" };
        var viHeaders = new[] { "Họ tên (*)", "Tên gọi", "Năm sinh", "Năm mất", "Khu (*)", "Số lô (*)", "Quê quán", "Người liên hệ", "Điện thoại", "Ghi chú", "Tình trạng" };

        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cells[1, i + 1].Value = headers[i];
            ws.Cells[2, i + 1].Value = viHeaders[i];
            ws.Cells[1, i + 1].Style.Font.Bold = true;
            ws.Cells[2, i + 1].Style.Font.Italic = true;
            ws.Cells[2, i + 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
        }

        // Sample data rows
        var samples = new object[][]
        {
            ["Nguyễn Văn A", "Ông A", 1950, 2020, "A", "A-01-01", "Hà Nội", "Nguyễn Thị B", "0877xxxxxx", "Liệt sĩ", "Active"],
            ["Trần Thị B",   "",      1960, 2022, "B", "B-01-01", "Nam Định","Trần Văn C",  "0877xxxxxx", "",       "Pending"],
        };
        for (int r = 0; r < samples.Length; r++)
            for (int c = 0; c < samples[r].Length; c++)
                ws.Cells[r + 3, c + 1].Value = samples[r][c];

        // Header styling
        using var headerRange = ws.Cells[1, 1, 1, headers.Length];
        headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(201, 168, 76));

        ws.Cells.AutoFitColumns();
        await Task.CompletedTask;
        return await pkg.GetAsByteArrayAsync();
    }

    public async Task<ImportResultDto?> GetJobStatusAsync(int jobId)
    {
        var job = await db.ImportJobs.FindAsync(jobId);
        return job is null ? null : ToDto(job);
    }

    public async Task<IEnumerable<ImportResultDto>> GetRecentJobsAsync(int count = 10)
    {
        var jobs = await db.ImportJobs
            .OrderByDescending(j => j.StartedAt).Take(count).ToListAsync();
        return jobs.Select(ToDto);
    }

    private static ImportResultDto ToDto(ImportJob j) => new(
        j.Id, j.FileName, j.TotalRows, j.Imported, j.Failed,
        j.Status.ToString(), j.ErrorLog
    );
}

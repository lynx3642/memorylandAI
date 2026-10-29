using Microsoft.AspNetCore.Mvc;
using SmartCem.Models;
using SmartCem.Services;

namespace SmartCem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GravesController(IGraveService svc, IQrService qrSvc, IWebHostEnvironment env) : ControllerBase
{
    private string GetRequestBaseUrl() =>
        $"{Request.Scheme}://{Request.Host}";

    // GET /api/graves?q=&zone=A&status=Active&contractType=&anniversarySoon=false&expiringSoon=false&page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string? zone,
        [FromQuery] string? status,
        [FromQuery] string? contractType,
        [FromQuery] bool? anniversarySoon,
        [FromQuery] bool? expiringSoon,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var (items, total) = await svc.SearchAsync(q, zone, status, contractType, anniversarySoon, expiringSoon, page, pageSize);
        return Ok(new {
            items, total, page, pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var g = await svc.GetByIdAsync(id);
        return g is null ? NotFound() : Ok(g);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGraveRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var g = await svc.CreateAsync(req, "Admin");
        return CreatedAtAction(nameof(Get), new { id = g.Id }, g);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateGraveRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var g = await svc.UpdateAsync(id, req, "Admin");
        return g is null ? NotFound() : Ok(g);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await svc.DeleteAsync(id, "Admin");
        return ok ? NoContent() : NotFound();
    }

    // GET /api/graves/anniversaries?days=30
    [HttpGet("anniversaries")]
    public async Task<IActionResult> GetAnniversaries([FromQuery] int days = 30)
    {
        var list = await svc.GetUpcomingAnniversariesAsync(days);
        return Ok(list);
    }

    // GET /api/graves/expiring-contracts?days=60
    [HttpGet("expiring-contracts")]
    public async Task<IActionResult> GetExpiringContracts([FromQuery] int days = 60)
    {
        var list = await svc.GetExpiringContractsAsync(days);
        return Ok(list);
    }

    // GET /api/graves/export-excel
    [HttpGet("export-excel")]
    public async Task<IActionResult> ExportExcel()
    {
        var bytes = await svc.ExportGravesExcelAsync();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"SmartCem_DanhSachMoPhan_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    // POST /api/graves/5/photo
    [HttpPost("{id:int}/photo")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadPhoto(int id, IFormFile photo)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest("Không có file ảnh");

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return BadRequest("Chỉ chấp nhận JPG/PNG/WEBP");

        var dir = Path.Combine(env.WebRootPath, "uploads", "photos");
        Directory.CreateDirectory(dir);
        var fileName = $"grave_{id}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using var fs = System.IO.File.Create(fullPath);
        await photo.CopyToAsync(fs);

        var relative = $"/uploads/photos/{fileName}";
        await svc.SetPhotoAsync(id, relative);
        return Ok(new { path = relative });
    }

    // POST /api/graves/5/qr  → tạo QR
    [HttpPost("{id:int}/qr")]
    public async Task<IActionResult> GenerateQr(int id)
    {
        try
        {
            var path = await qrSvc.GenerateAndSaveQrAsync(id, GetRequestBaseUrl());
            var resolvedBase = qrSvc.ResolveBaseUrl(GetRequestBaseUrl());
            return Ok(new {
                path,
                qrUrl   = $"{resolvedBase}/grave-detail.html?id={id}",
                message = $"QR trỏ tới: {resolvedBase}/grave-detail.html?id={id}"
            });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // GET /api/graves/5/qr/download  (download PNG)
    [HttpGet("{id:int}/qr/download")]
    public async Task<IActionResult> DownloadQr(int id)
    {
        var grave = await svc.GetByIdAsync(id);
        if (grave is null) return NotFound();

        var resolvedBase = qrSvc.ResolveBaseUrl(GetRequestBaseUrl());
        var content = $"{resolvedBase}/grave-detail.html?id={id}";
        var bytes = qrSvc.GenerateQrBytes(content);

        return File(bytes, "image/png", $"QR_{grave.FullName}_{grave.LotCode}.png");
    }

    // GET /api/graves/5/qr/info
    [HttpGet("{id:int}/qr/info")]
    public IActionResult QrInfo(int id)
    {
        var resolved = qrSvc.ResolveBaseUrl(GetRequestBaseUrl());
        return Ok(new {
            resolvedBaseUrl = resolved,
            qrTargetUrl     = $"{resolved}/grave-detail.html?id={id}",
            hint = resolved.Contains("localhost")
                ? "⚠ Đang dùng localhost – điện thoại KHÔNG quét được nếu không cùng mạng LAN. Hãy cấu hình IP máy chủ."
                : "✓ Có thể quét bằng điện thoại di động"
        });
    }
}

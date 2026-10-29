using Microsoft.AspNetCore.Mvc;
using SmartCem.Models;
using SmartCem.Services;

namespace SmartCem.Controllers;

// ─── Service Packages ────────────────────────────────────────────────
[ApiController]
[Route("api/service-packages")]
public class ServicePackagesController(IServiceService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
        => Ok(await svc.GetPackagesAsync(activeOnly));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var p = await svc.GetPackageAsync(id);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServicePackage pkg)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await svc.CreatePackageAsync(pkg);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ServicePackage pkg)
    {
        var result = await svc.UpdatePackageAsync(id, pkg);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await svc.DeletePackageAsync(id);
        return ok ? NoContent() : NotFound();
    }
}

// ─── Service Orders ──────────────────────────────────────────────────
[ApiController]
[Route("api/service-orders")]
public class ServiceOrdersController(IServiceService svc, IWebHostEnvironment env) : ControllerBase
{
    // GET /api/service-orders?status=Pending&page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] int? graveId,
        [FromQuery] int? packageId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var (items, total) = await svc.GetOrdersAsync(status, graveId, packageId, search, page, pageSize);
        return Ok(new {
            items, total, page, pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var o = await svc.GetOrderAsync(id);
        return o is null ? NotFound() : Ok(o);
    }

    // GET /api/service-orders/5/receipt
    [HttpGet("{id:int}/receipt")]
    public async Task<IActionResult> GetReceipt(int id)
    {
        var receipt = await svc.GetReceiptAsync(id);
        return receipt is null ? NotFound() : Ok(receipt);
    }

    // GET /api/service-orders/export-excel
    [HttpGet("export-excel")]
    public async Task<IActionResult> ExportExcel()
    {
        var bytes = await svc.ExportOrdersExcelAsync();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"SmartCem_DonDichVu_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await svc.CreateOrderAsync(req);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    // PATCH /api/service-orders/5/status
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest req)
    {
        var result = await svc.UpdateOrderStatusAsync(id, req);
        return result is null ? NotFound() : Ok(result);
    }

    // POST /api/service-orders/5/pay
    [HttpPost("{id:int}/pay")]
    public async Task<IActionResult> MarkPaid(int id, [FromBody] MarkPaidRequest req)
    {
        var result = await svc.MarkPaidAsync(id, req);
        return result is null ? NotFound() : Ok(result);
    }

    // DELETE /api/service-orders/5/cancel
    [HttpDelete("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromQuery] string? reason)
    {
        var ok = await svc.CancelOrderAsync(id, reason);
        return ok ? NoContent() : NotFound();
    }

    // POST /api/service-orders/5/photo  (ảnh sau khi hoàn thành)
    [HttpPost("{id:int}/photo")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadPhoto(int id, IFormFile photo)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest("Không có file ảnh");

        var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(ext))
            return BadRequest("Chỉ chấp nhận JPG/PNG/WEBP");

        var dir = Path.Combine(env.WebRootPath, "uploads", "service");
        Directory.CreateDirectory(dir);
        var fileName = $"svc_{id}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using var fs = System.IO.File.Create(fullPath);
        await photo.CopyToAsync(fs);

        var relative = $"/uploads/service/{fileName}";
        var result = await svc.SetPhotoAsync(id, relative);
        return result is null ? NotFound() : Ok(new { path = relative });
    }

    // GET /api/service-orders/stats
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
        => Ok(await svc.GetStatsAsync());
}

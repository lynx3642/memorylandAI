using Microsoft.AspNetCore.Mvc;
using SmartCem.Services;

namespace SmartCem.Controllers;

// ─── Import Controller ──────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
public class ImportController(IImportService importSvc) : ControllerBase
{
    // POST /api/import/upload
    // Accepts: multipart/form-data  file=<Excel|CSV|JSON>
    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)] // 50 MB
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Không có file được gửi lên" });

        try
        {
            var result = await importSvc.ImportFromFileAsync(file, "api-user");
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // GET /api/import/template  (tải file Excel mẫu)
    [HttpGet("template")]
    public async Task<IActionResult> DownloadTemplate()
    {
        var bytes = await importSvc.GenerateExcelTemplateAsync();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "SmartCem_Import_Template.xlsx");
    }

    // GET /api/import/jobs
    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobs([FromQuery] int count = 10)
    {
        var jobs = await importSvc.GetRecentJobsAsync(count);
        return Ok(jobs);
    }

    // GET /api/import/jobs/5
    [HttpGet("jobs/{id:int}")]
    public async Task<IActionResult> GetJob(int id)
    {
        var job = await importSvc.GetJobStatusAsync(id);
        return job is null ? NotFound() : Ok(job);
    }
}

// ─── Dashboard Controller ───────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
public class DashboardController(IGraveService svc) : ControllerBase
{
    // GET /api/dashboard
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var dto = await svc.GetDashboardAsync();
        return Ok(dto);
    }
}

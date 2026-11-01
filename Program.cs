using Microsoft.EntityFrameworkCore;
using SmartCem.Data;
using SmartCem.Services;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new() { Title = "SmartCem API", Version = "v1", Description = "Hệ thống Quản lý Nghĩa trang Thông minh" });
});

// EF Core (SQLite – chạy không cần cài SQL Server)
builder.Services.AddDbContext<SmartCemDb>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=smartcem.db"));

// App services
builder.Services.AddScoped<IGraveService, GraveService>();
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddScoped<IQrService, QrService>();
builder.Services.AddScoped<ILotMapService, LotMapService>();
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// Auto-create & seed DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SmartCemDb>();
    db.Database.EnsureCreated();
    DbSeeder.Seed(db);
    MemoryLandSeeder.Seed(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartCem v1"));
}

app.UseCors();
app.UseStaticFiles();
app.MapControllers();

// Serve SPA index.html for all non-API routes
app.MapFallbackToFile("index.html");

app.Run();

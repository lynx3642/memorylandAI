using QRCoder;
using SmartCem.Data;
using System.Net;
using System.Net.Sockets;

namespace SmartCem.Services;

public interface IQrService
{
    Task<string> GenerateAndSaveQrAsync(int graveId, string requestBaseUrl);
    byte[] GenerateQrBytes(string content);
    string ResolveBaseUrl(string requestBaseUrl);
}

public class QrService(
    SmartCemDb db,
    IWebHostEnvironment env,
    IGraveService graveService,
    IConfiguration config) : IQrService
{
    /// <summary>
    /// Trả về base URL thực để QR code trỏ tới.
    /// Thứ tự ưu tiên:
    ///   1. appsettings.json → SmartCem:BaseUrl  (admin tự điền IP LAN)
    ///   2. X-Forwarded-For / X-Real-IP header   (qua ngrok / tunnel)
    ///   3. Auto-detect IP LAN của máy chủ       (cùng mạng WiFi)
    ///   4. Fallback: dùng host từ request        (localhost – chỉ test trên máy)
    /// </summary>
    public string ResolveBaseUrl(string requestBaseUrl)
    {
        // 1. Config override (ưu tiên cao nhất)
        var configured = config["SmartCem:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('/');

        // 2. Nếu request đã có IP thực (không phải localhost) → dùng luôn
        if (!requestBaseUrl.Contains("localhost") &&
            !requestBaseUrl.Contains("127.0.0.1") &&
            !requestBaseUrl.Contains("::1"))
            return requestBaseUrl.TrimEnd('/');

        // 3. Auto-detect IP LAN
        var lanIp = GetLanIpAddress();
        if (lanIp != null)
        {
            // Lấy port từ requestBaseUrl
            var uri = new Uri(requestBaseUrl);
            var port = uri.Port;
            return port is 80 or 443
                ? $"{uri.Scheme}://{lanIp}"
                : $"{uri.Scheme}://{lanIp}:{port}";
        }

        // 4. Fallback
        return requestBaseUrl.TrimEnd('/');
    }

    private static string? GetLanIpAddress()
    {
        try
        {
            // Kết nối thử đến 8.8.8.8 để lấy IP interface đang dùng
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.IP);
            socket.Connect("8.8.8.8", 80);
            var ep = socket.LocalEndPoint as IPEndPoint;
            return ep?.Address.ToString();
        }
        catch { return null; }
    }

    public byte[] GenerateQrBytes(string content)
    {
        using var qrGen = new QRCodeGenerator();
        using var qrData = qrGen.CreateQrCode(content, QRCodeGenerator.ECCLevel.H);
        using var qrCode = new PngByteQRCode(qrData);
        // Màu ink đậm (#1a1a2e) trên nền trắng, size pixel 8 → ảnh 360×360
        return qrCode.GetGraphic(8, [26, 26, 46], [255, 255, 255]);
    }

    public async Task<string> GenerateAndSaveQrAsync(int graveId, string requestBaseUrl)
    {
        var grave = await db.Graves.FindAsync(graveId)
            ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ #{graveId}");

        var baseUrl = ResolveBaseUrl(requestBaseUrl);
        var content = $"{baseUrl}/grave-detail.html?id={grave.Id}";

        var bytes = GenerateQrBytes(content);

        var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "qr");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"qr_{graveId}_{DateTime.UtcNow:yyyyMMddHHmmss}.png";
        var filePath = Path.Combine(uploadsDir, fileName);
        await File.WriteAllBytesAsync(filePath, bytes);

        var relativePath = $"/uploads/qr/{fileName}";
        await graveService.SetQrAsync(graveId, relativePath);

        // Trả về cả URL thực để frontend hiển thị
        return relativePath;
    }
}

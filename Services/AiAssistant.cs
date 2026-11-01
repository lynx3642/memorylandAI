using System.Globalization;
using System.Text;

namespace SmartCem.Services;

public record KbEntry(string Id, string Title, string[] Keywords, string Answer, string Source);
public record AssistantReply(string Answer, List<string> Sources, List<string> Suggestions, string Intent, double Confidence);

/// <summary>AI Assistant (mô phỏng LLM tiếng Việt + RAG): truy hồi tri thức nghi lễ bằng so khớp từ khoá có trọng số.</summary>
public static class AssistantAi
{
    public static string Fold(string s)
    {
        var norm = s.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in norm) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }

    public static readonly List<KbEntry> Kb =
    [
        new("gio", "Cúng giỗ", ["gio", "cung gio", "ngay gio", "mam co", "le gio"],
            "Cúng giỗ thường làm vào đúng ngày mất theo Âm lịch (có thể làm sớm hơn 1 ngày, không nên làm muộn). Mâm lễ gồm: hương, hoa tươi, trầu cau, rượu, nước, mâm ngũ quả và mâm cơm mặn hoặc chay tuỳ gia đình. Tại mộ, nên dọn dẹp sạch sẽ trước khi dâng lễ, thắp hương khấn rồi hoá sớ.",
            "Phong tục cúng giỗ Việt Nam"),
        new("vankhan", "Văn khấn tại mộ", ["van khan", "khan", "doc khan", "bai khan", "khan mo"],
            "Mẫu văn khấn rút gọn tại mộ:\n\"Nam mô A Di Đà Phật (3 lần). Hôm nay là ngày … tháng … năm … (Âm lịch). Tín chủ con là … ngụ tại … Nhân ngày giỗ (hoặc tiết …) của hương linh …, con thành tâm sắm lễ, hương hoa trà quả dâng lên trước mộ. Kính mời hương linh về thụ hưởng, phù hộ độ trì cho con cháu bình an, mạnh khoẻ, làm ăn thuận lợi. Cẩn cáo!\"\nBạn có thể nhờ đối tác MemoryLand đọc văn khấn thay trong gói Hiếu Đạo.",
            "Kho văn khấn MemoryLand (RAG)"),
        new("thanhminh", "Tiết Thanh minh", ["thanh minh", "tao mo", "tiet thanh minh", "tet thanh minh"],
            "Tiết Thanh minh rơi vào khoảng 4–6/4 Dương lịch (khoảng tháng 3 Âm lịch). Đây là dịp tảo mộ: phát quang cỏ, đắp lại mộ, thắp hương mời tổ tiên. Gói \"Lễ Tết\" của MemoryLand đã gồm dọn mộ, hoa tươi, hương và ảnh nghiệm thu.",
            "Lịch tiết khí & phong tục"),
        new("ram", "Rằm và mùng 1", ["ram", "mung 1", "mong 1", "ngay ram", "mung mot", "thap huong"],
            "Mùng 1 và ngày Rằm (15 Âm lịch) là dịp thắp hương, dâng hoa quả tươi, nước sạch. Với mộ phần chỉ cần hương, hoa, nước và hoa quả đơn giản; không cần cỗ mặn. Rằm tháng Bảy (Vu lan) nên dâng hoa và lễ chay.",
            "Phong tục Rằm – mùng 1"),
        new("tet", "Tảo mộ cuối năm", ["tet", "cuoi nam", "thang chap", "ong cong", "23 thang chap"],
            "Tảo mộ dịp Tết thường làm từ 23 đến 30 tháng Chạp (sau lễ ông Công ông Táo), để đón Tết sạch sẽ. Sau khi dọn mộ, thắp hương mời tổ tiên về ăn Tết. Đặt gói Lễ Tết sớm vì dịp này đối tác rất đông việc.",
            "Phong tục Tết cổ truyền"),
        new("levat", "Lễ vật & hoa", ["le vat", "hoa", "vang ma", "dot vang ma", "mam ngu qua", "qua", "dang le"],
            "Lễ vật tại mộ nên giản dị, thanh tịnh: hoa tươi (cúc vàng, cúc trắng, huệ), hương, trà/rượu, hoa quả (mâm ngũ quả). MemoryLand khuyến khích dâng hoa tươi thay vì đốt vàng mã quá mức để giữ vệ sinh và an toàn phòng cháy ở nghĩa trang.",
            "Hướng dẫn lễ vật văn minh"),
        new("don", "Dọn mộ & vệ sinh", ["don mo", "ve sinh", "co dai", "lau bia", "sach se", "nhat co"],
            "Dịch vụ dọn mộ gồm: nhổ cỏ, quét lá, lau bia và lư hương, thu gom rác. Đối tác chụp ảnh TRƯỚC và SAU ngay tại mộ; AI Verify đối chiếu GPS, thời gian và hình ảnh trước khi gửi báo cáo cho gia đình trong vòng 15 phút.",
            "Quy trình dịch vụ MemoryLand"),
        new("verify", "Xác thực & chống gian lận", ["xac thuc", "ai verify", "gian lan", "nham mo", "bang chung", "minh chung", "tin tuong"],
            "AI Verify kiểm định 3 lớp: (1) GPS + quét QR tại mộ, sai lệch phải ≤ 5 m; (2) ảnh chụp trực tiếp trong app, siêu dữ liệu EXIF, thời gian thực, không dùng ảnh cũ; (3) AI so sánh ảnh trước/sau – cùng một vị trí và mộ sạch hơn rõ rệt. Nếu không đạt, đơn sẽ bị từ chối và gia đình được hoàn tiền 100%.",
            "Cam kết Đúng mộ – Đúng việc – Đúng hẹn"),
        new("hoantien", "Hoàn tiền & hủy lịch", ["hoan tien", "huy", "doi lich", "bao hanh", "khieu nai", "sai sot", "boi hoan"],
            "Bạn có thể huỷ hoặc đổi lịch miễn phí trước giờ hẹn 24 giờ. Nếu phát hiện sai sót (nhầm mộ, thiếu việc, ảnh không đạt), MemoryLand bồi hoàn 100% giá trị đơn hoặc điều đối tác khác làm lại không tính phí.",
            "Chính sách bồi hoàn 100%"),
        new("camoc", "Cải táng & tu bổ", ["cai tang", "tu bo", "xay mo", "sua mo", "sat lo", "khac bia"],
            "Cải táng (bốc mộ) cần xin phép UBND xã/phường và chọn ngày tốt, thường làm vào mùa khô. Tu bổ, sơn lại chữ bia, gia cố chân mộ có thể đặt trong gói Tu bổ của MemoryLand. Hãy chụp ảnh hiện trạng trước và thống nhất phương án với dòng họ.",
            "Quy định quản lý nghĩa trang"),
        new("quyhoach", "Quy hoạch nghĩa trang (B2G)", ["quy hoach", "phan lo", "drone", "ban do so", "gis", "uy ban", "ubnd"],
            "MemoryLand AI hỗ trợ UBND xã quy hoạch nghĩa trang bằng Drone RTK + AI Vision + AI Planner: khảo sát 1 ngày thay vì 7 ngày, tối ưu 20–30% quỹ đất, mô phỏng 3D trước–sau để họp dân, cấp mã định danh GIS cho từng ô mộ.",
            "Dịch vụ 1 – Quy hoạch phân lô"),
    ];

    public static AssistantReply Reply(string message, Func<string> packagesText, string? graveName, DateTime? nextGio, string? lunarGio)
    {
        var q = Fold(message ?? "");
        var today = DateTime.Now;
        var l = Lunar.FromSolar(today);

        // Ý định động: lịch âm hôm nay
        if (q.Contains("am lich") && (q.Contains("hom nay") || q.Contains("bay gio") || q.Contains("ngay may")))
            return new($"Hôm nay {today:dd/MM/yyyy} là ngày {l.Day:D2}/{l.Month:D2} Âm lịch, năm {l.YearName}.",
                ["Lịch Âm – thuật toán thiên văn (UTC+7)"], ["Ngày giỗ gần nhất là khi nào?", "Gợi ý lễ vật Rằm"], "lunar", 0.98);

        // Ý định động: ngày giỗ của mộ đang chọn
        if ((q.Contains("gio") && (q.Contains("khi nao") || q.Contains("bao gio") || q.Contains("ngay nao") || q.Contains("gan nhat") || q.Contains("sap")))
            || q.Contains("nhac lich"))
        {
            if (graveName is null)
                return new("Bạn hãy chọn một mộ phần trong ứng dụng để tôi tra ngày giỗ chính xác nhé.", ["Hồ sơ mộ phần"], ["Văn khấn ngày giỗ"], "anniversary", 0.7);
            if (nextGio is null)
                return new($"Hồ sơ {graveName} chưa có ngày giỗ Âm lịch. Bạn có thể bổ sung trong Quản trị → Danh sách mộ phần.", ["Hồ sơ mộ phần"], [], "anniversary", 0.75);
            var days = (nextGio.Value.Date - today.Date).Days;
            return new($"Giỗ {graveName} ({lunarGio} Âm lịch) năm nay rơi vào {nextGio:dddd, dd/MM/yyyy} Dương lịch — còn {days} ngày. Tôi gợi ý đặt gói Lễ Tết/Giỗ trước ít nhất 3 ngày để đối tác kịp chuẩn bị lễ vật.",
                ["Hồ sơ mộ phần", "Lịch Âm – thuật toán thiên văn (UTC+7)"], ["Văn khấn ngày giỗ", "Giá các gói dịch vụ"], "anniversary", 0.95);
        }

        // Ý định động: giá/gói
        if (q.Contains("gia") || q.Contains("goi") || q.Contains("bao nhieu") || q.Contains("chi phi") || q.Contains("dat lich"))
            return new(packagesText() + "\nBạn bấm tab \"Đặt dịch vụ\" để đặt ngay, hệ thống AI Dispatch sẽ chọn đối tác gần nhất.",
                ["Bảng giá MemoryLand"], ["Quy trình nghiệm thu", "Chính sách hoàn tiền"], "pricing", 0.9);

        // Truy hồi tri thức
        var tokens = q.Split([' ', ',', '.', '?', '!', ';'], StringSplitOptions.RemoveEmptyEntries);
        KbEntry? best = null; double bestScore = 0;
        foreach (var e in Kb)
        {
            double sc = 0;
            foreach (var k in e.Keywords)
            {
                if (q.Contains(k)) sc += 1.0 + k.Split(' ').Length * 0.6;
                else foreach (var t in tokens) if (t.Length >= 3 && k.Contains(t)) sc += 0.25;
            }
            if (sc > bestScore) { bestScore = sc; best = e; }
        }
        if (best is not null && bestScore >= 0.9)
            return new(best.Answer, [best.Source], Kb.Where(k => k.Id != best.Id).OrderBy(_ => Guid.NewGuid()).Take(2).Select(k => k.Title).ToList(), best.Id, Math.Min(0.98, 0.55 + bestScore * 0.12));

        return new("Xin lỗi, tôi chưa tìm thấy thông tin chính xác cho câu hỏi này. Bạn có thể hỏi về: ngày giỗ, văn khấn, lễ vật, Tiết Thanh minh, dọn mộ, giá dịch vụ, hoàn tiền… hoặc gọi hotline 1900 6868 để được hỗ trợ.",
            ["—"], ["Ngày giỗ gần nhất", "Văn khấn tại mộ", "Giá các gói dịch vụ"], "fallback", 0.2);
    }
}

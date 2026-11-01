# MemoryLand AI (SmartCem) – ASP.NET Core

Hệ thống quản lý nghĩa trang và chăm sóc mộ từ xa, viết bằng **C# ASP.NET Core**.

## Ai dùng trang nào

| Người dùng | Trang | Việc chính |
|---|---|---|
| Ban quản lý nghĩa trang | `/` | Hồ sơ mộ, bản đồ phân lô, quy hoạch & dự báo hết đất, đơn chăm sóc, đối tác & điều phối |
| Gia đình (con cháu xa quê) | `/gia-dinh.html` | Tra cứu mộ, đặt dịch vụ, theo dõi đơn + ảnh trước/sau, hỏi văn khấn |
| Đối tác dọn mộ địa phương | `/doi-tac.html` | Nhận việc, check-in QR + GPS tại mộ, chụp ảnh trước/sau để nghiệm thu |
| Người quét QR trên bia | `/grave-detail.html?id=…` | Xem thông tin công khai của mộ, đặt dịch vụ |

Luồng một đơn: gia đình đặt (hoặc ban quản lý tạo hộ) → hệ thống giao cho đối tác gần nhất → đối tác nhận việc, check-in, chụp ảnh → hệ thống kiểm tra ảnh/vị trí → gửi báo cáo cho gia đình. Không đạt thì ban quản lý giao người khác hoặc hoàn tiền.

---

## 🚀 Chạy nhanh

```bash
# Yêu cầu: .NET 8 SDK (https://dotnet.microsoft.com/download)
dotnet run --project SmartCem.csproj
# Mở trình duyệt: http://localhost:5000
# Swagger API docs: http://localhost:5000/swagger
```

---

## ⭐ Tính năng nổi bật: Upload & Import hàng loạt

### Định dạng được hỗ trợ
| Định dạng | Mô tả |
|-----------|-------|
| `.xlsx` / `.xls` | Excel – hỗ trợ tên cột tiếng Anh và tiếng Việt |
| `.csv` | CSV UTF-8 với header row |
| `.json` | Mảng JSON object |

### API endpoints chính

#### Import
```
POST  /api/import/upload           Upload file Excel/CSV/JSON
GET   /api/import/template         Tải file Excel mẫu
GET   /api/import/jobs             Lịch sử import (20 job gần nhất)
GET   /api/import/jobs/{id}        Trạng thái 1 job
```

#### Hồ sơ mộ phần
```
GET    /api/graves                 Danh sách (filter: q, zone, status, page, pageSize)
GET    /api/graves/{id}            Chi tiết 1 hồ sơ
POST   /api/graves                 Thêm mới
PUT    /api/graves/{id}            Cập nhật
DELETE /api/graves/{id}            Xóa
POST   /api/graves/{id}/photo      Upload ảnh (JPG/PNG/WEBP ≤ 10MB)
POST   /api/graves/{id}/qr         Tạo & lưu QR PNG
GET    /api/graves/{id}/qr/download Tải QR trực tiếp
```

#### Dashboard
```
GET    /api/dashboard              Thống kê tổng quan
```

---

## 📋 Cấu trúc cột Excel import

| Cột | Tên tiếng Anh | Tên tiếng Việt | Bắt buộc |
|-----|---------------|----------------|----------|
| Họ tên | FullName | Họ tên | ✅ |
| Tên gọi | Alias | Tên gọi | |
| Năm sinh | BornYear | Năm sinh | |
| Năm mất | DiedYear | Năm mất | |
| Khu | Zone | Khu | ✅ |
| Số lô | LotCode | Số lô | ✅ |
| Quê quán | Hometown | Quê quán | |
| Người liên hệ | ContactName | Người liên hệ | |
| Điện thoại | ContactPhone | Điện thoại | |
| Ghi chú | Notes | Ghi chú | |
| Tình trạng | Status | Tình trạng | |

### Ví dụ JSON import
```json
[
  {
    "FullName": "Nguyễn Văn A",
    "BornYear": 1950,
    "DiedYear": 2020,
    "Zone": "A",
    "LotCode": "A-01-01",
    "Hometown": "Hà Nội",
    "Status": "Active"
  }
]
```

---

## 🏗️ Kiến trúc

```
SmartCem/
├── Program.cs                    Entry point, DI, middleware
├── SmartCem.csproj               NuGet packages
├── appsettings.json              SQLite connection string
│
├── Models/
│   └── Models.cs                 Grave, Zone, ServiceOrder, AuditLog, ImportJob, DTOs
│
├── Data/
│   └── SmartCemDb.cs             EF Core DbContext + Seeder
│
├── Services/
│   ├── GraveService.cs           CRUD, search, dashboard
│   ├── ImportService.cs          ⭐ Excel/CSV/JSON import + template generator
│   └── QrService.cs              QR PNG generator (QRCoder)
│
├── Controllers/
│   ├── GravesController.cs       REST API hồ sơ + photo + QR
│   └── ImportDashboardController.cs  Import API + Dashboard API
│
└── wwwroot/
    ├── index.html                SPA frontend (HTML/CSS/JS thuần)
    └── uploads/                  Auto-created: photos/, qr/
```

---

## 📦 NuGet packages
- `Microsoft.EntityFrameworkCore.Sqlite` – ORM + SQLite (zero-config)
- `EPPlus` – đọc/ghi Excel .xlsx
- `CsvHelper` – parse CSV
- `QRCoder` – tạo QR PNG
- `Swashbuckle.AspNetCore` – Swagger UI

---

## 💡 Logic import thông minh

1. **Tự nhận dạng cột** – hỗ trợ cả tên cột tiếng Anh (`FullName`) và tiếng Việt (`Họ tên`)
2. **Batch save mỗi 50 dòng** – tránh timeout với file lớn
3. **Bỏ qua trùng LotCode** – không rollback toàn bộ, log lỗi từng dòng
4. **Job tracking** – mỗi lần import tạo 1 `ImportJob` lưu kết quả, xem lại bất kỳ lúc nào
5. **Audit log** – mọi hành động đều ghi vào `AuditLogs`

---

## 🕊️ MemoryLand AI (bản mô phỏng theo `vippro.docx`)

Chạy xong mở các trang (không cần dịch vụ trả phí, dữ liệu Drone/thanh toán/GPS đều mô phỏng):

| Trang | Nội dung |
|-------|----------|
| `/ai.html` | **AI Core Studio** – 8 module AI, quy trình 6 bước: Drone RTK → AI Vision → AI Terrain (D8) → AI Planner (3 phương án) → 3D trước/sau → cắm mốc & bàn giao GIS (CSV/GeoJSON) + AI Forecast |
| `/app.html` | **MemoryLand App** (khách) ⇄ **Partner App** (đối tác) chạy song song + AI Assistant (lịch Âm, văn khấn) + AI Dispatch (Hungarian) + **AI Verify 3 lớp** (thử 5 kịch bản gian lận); tab **Điều phối** có bản đồ, nhắc giỗ theo lịch Âm |
| `/business.html` | Dashboard kinh doanh: TAM/SAM/SOM, doanh thu, tài chính 3 năm, mô phỏng hòa vốn, dự án B2G 6 bước, BMC, lộ trình, rủi ro |

API mới: `/api/ml/*` (modules, drone/plan, site, terrain, planner, forecast, assistant, lunar, reminders, app/*, jobs/*, partners, dispatch/*, projects, business). Bảng mới (`Partners`, `CareJobs`, `PlanningProjects`) tự tạo trên DB cũ.

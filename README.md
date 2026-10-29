# SmartCem 4.0 – ASP.NET Core

Hệ thống Quản lý Nghĩa trang Thông minh được viết bằng **C# ASP.NET Core 8**  
(Code lại từ bản HTML thuần + nâng cấp tính năng Import dữ liệu hàng loạt)

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

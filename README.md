# Network Download Tray

Ứng dụng Windows WPF đo **download Mbps**, cập nhật mỗi giây và hiển thị số nguyên ở system tray.

## Sử dụng

- Mở cửa sổ để xem tốc độ, adapter được tính và cấu hình.
- Minimize to Tray hoặc nút X: ẩn cửa sổ khi tray đã sẵn sàng.
- Chuột phải icon → Open / Exit.
- Khi tray chưa tạo được, cửa sổ được giữ mở; trạng thái và log giúp chẩn đoán.
- Ứng dụng chỉ cho một instance trong mỗi Windows session.

## Adapter

Bảng hiển thị Included, Name, Mode, Type, Status, Description, Received bytes, Reason, ID.
Chọn dòng rồi chuột phải:

- Include: đưa adapter này vào tổng download nếu đang Up và có counter hợp lệ.
- Exclude: bỏ adapter này.
- Automatic: áp dụng lọc loại/status/tên driver.

Override được lưu theo ID. Bộ lọc tên chỉ là heuristic, không chứng minh adapter vật lý.
Không cộng cả adapter vật lý lẫn filter/VPN mang cùng lưu lượng nếu muốn tránh đếm trùng.
Received bytes là counter tích lũy, không phải Mbps. Đo tổng lưu lượng nhận trên interface,
có thể bao gồm LAN; không phải tốc độ Internet riêng từng ứng dụng.

Bảng giữ dòng/selection, cập nhật khoảng 5 giây khi hiện. Refresh để đọc ngay.
Filter All/Included/Excluded và Copy selected giúp kiểm tra ID/driver.

## Settings và startup

- Start with Windows: đăng ký HKCU Run cho EXE hiện tại, không cần Administrator.
- Start minimized to tray: lần mở sau ẩn cửa sổ nếu đăng ký tray thành công.
- Show adapter diagnostics: bật/tắt bảng.

Settings: `%AppData%\NetworkDownloadTray\settings.json`.
Log lỗi giới hạn dung lượng: `%LocalAppData%\NetworkDownloadTray\app.log`.

Ứng dụng đọc đăng ký startup khi mở, không tự bật lại bằng giá trị JSON cũ.
Checkbox cho biết Run entry; Windows Task Manager có thể disable startup độc lập.
Đặt bản publish ở vị trí cố định, bật lại checkbox nếu chuyển thư mục để cập nhật đường dẫn.
Muốn gỡ: bỏ Start with Windows, chọn Exit rồi xóa thư mục app. Settings/log được giữ lại;
có thể xóa hai thư mục riêng của app nếu không cần.

## Pixel icon

Giữ chữ số pixel custom theo kiểu đã chọn, canvas logic 16×16, vàng trên nền trong suốt.
Render theo kích thước small icon tương ứng DPI primary taskbar. Màn hình phụ có DPI khác
cần kiểm tra thực tế. Không ghi file ICO tạm.

1–3 chữ số dùng glyph 5×7; 4 chữ số dùng 3×7 để không bị cắt.
Icon giới hạn 9999 Mbps; tooltip và cửa sổ giữ số đầy đủ. `...` nghĩa là lấy mốc;
`-` nghĩa là không có dữ liệu. Không đổi sang Gbps hoặc thêm upload.

## Build và test

Cần Windows và .NET 10 SDK, Visual Studio hỗ trợ .NET 10 với workload desktop.

```powershell
dotnet restore NetworkDownloadTray.sln
dotnet test NetworkDownloadTray.sln -c Release
```

Test mặc định gồm network policy/counter, settings/store giả, ICO alpha/mask,
2.000 chu kỳ icon và WPF smoke. Không thay đổi startup thật.
Test tray native cần desktop tương tác:

```powershell
$env:NDT_DESKTOP_TESTS = '1'
dotnet test NetworkDownloadTray.sln -c Release --filter FullyQualifiedName~NativeTrayTests
Remove-Item Env:NDT_DESKTOP_TESTS
```

Test này tạo tray icon tạm và gửi TaskbarCreated vào HWND của nó, không restart Explorer.
Nếu chạy CI/sandbox không hỗ trợ Shell, để test này skip và kiểm tra trên desktop.

## Publish

Self-contained single-file (không cần runtime cài sẵn):

```powershell
dotnet publish NetworkDownloadTray.csproj -p:PublishProfile=win-x64
```

Kết quả: `outputs\win-x64`. Restore cần tải runtime từ NuGet. Không bật trimming.

Bản dùng .NET Desktop Runtime 10 x64 cài sẵn:

```powershell
dotnet publish NetworkDownloadTray.csproj -p:PublishProfile=portable
```

Kết quả: `outputs\portable`. Copy **toàn bộ thư mục**, không copy riêng EXE.
Trong Visual Studio chọn Publish → profile tương ứng.

## Kiến trúc và tiến độ

- NetworkStatisticsProvider → NetworkSpeedReader / AdapterCounterTracker → DownloadMonitorService.
- App nối snapshot với TrayIconService và MainWindow.
- IStartupStore / SettingsService tách Registry và file khỏi test.
- TrayIconService chuyển ownership Icon cho H.NotifyIcon 2.4.1.
- PLAN.md và IMPLEMENTATION_HISTORY.md ghi tiến độ và giới hạn kiểm chứng.
- RELEASE_CHECKLIST.md ghi các kiểm tra Windows thật cần thực hiện trước release.

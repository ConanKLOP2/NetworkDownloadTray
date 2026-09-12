# NetworkDownloadTray

Ứng dụng WPF chạy nền trong System Tray, chỉ hiển thị tốc độ download hiện tại theo Mbps.

## Công nghệ

- .NET 10 Windows
- WPF
- H.NotifyIcon.Wpf
- CommunityToolkit.Mvvm
- System.Net.NetworkInformation

## Chức năng MVP

- Đọc `BytesReceived` từ các network adapter đang hoạt động.
- Tính tốc độ download bằng delta giữa hai lần đo.
- Cập nhật mỗi giây.
- Hiển thị số Mbps nguyên trên icon System Tray.
- Hiển thị adapter và tốc độ trong tooltip.
- Bỏ qua Loopback và Tunnel.

## Chạy project

Cần cài .NET 10 SDK trên Windows, sau đó chạy:

```powershell
dotnet restore
dotnet run
```

Đóng gói một file `.exe`:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Lưu ý

Lần đo đầu tiên chỉ dùng để lấy mốc. Tốc độ thực tế xuất hiện từ lần đo thứ hai.

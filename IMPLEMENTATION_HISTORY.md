# NetworkDownloadTray — Implementation History

File này ghi lại tuần tự mọi thay đổi theo `PLAN.md`.

## 2026-09-12 — Khởi tạo kế hoạch

- Tạo `PLAN.md`.
- Tạo `IMPLEMENTATION_HISTORY.md`.
- Xác định ưu tiên P0:
  - Lifetime của icon tạo từ `Icon.FromHandle`.
  - Lifecycle `OnClosing` và `Application.Shutdown`.
  - Ghi ICO tạm mỗi giây.
- Chưa đánh dấu bước code nào hoàn thành.
- Ghi chú môi trường: cần kiểm tra khả năng build/test bằng .NET SDK trước khi kết luận.

### 2026-09-12 — P0.1 — Sửa lifetime tray icon

- Mục tiêu: không trả về `Icon` còn phụ thuộc vào handle của `Bitmap` sắp bị dispose.
- File thay đổi: `Services/TrayIconRenderer.cs`.
- Thay đổi chính: tạo icon tạm từ handle, clone icon, sau đó dispose icon tạm an toàn.
- Kiểm tra đã chạy: kiểm tra tĩnh source; chưa build vì môi trường hiện chưa có `dotnet` CLI.
- Kết quả: đã loại bỏ lỗi lifetime rõ ràng trong `Icon.FromHandle`.
- Vấn đề còn lại: pipeline ICO/ImageSource và alpha cần được kiểm tra khi build/runtime thật.
- Bước tiếp theo: P0.2 — sửa lifecycle đóng cửa sổ và Exit.

### 2026-09-12 — P0.2 — Sửa lifecycle đóng cửa sổ và Exit

- Mục tiêu: phân biệt rõ `Hide to tray` với shutdown thật.
- Files thay đổi: `App.xaml.cs`, `MainWindow.xaml.cs`, `Services/DownloadMonitorService.cs`.
- Thay đổi chính: thêm `App.IsExiting` và `ShutdownApplication()`. Nút Exit và menu tray dùng shutdown tập trung; nút X chỉ ẩn cửa sổ. Event được unsubscribe chỉ khi cửa sổ thực sự đóng.
- Kiểm tra đã chạy: kiểm tra tĩnh source; chưa build vì môi trường chưa có `dotnet` CLI.
- Kết quả: loại bỏ logic `OnClosing` luôn hủy shutdown.
- Vấn đề còn lại: cần kiểm tra runtime bằng Visual Studio để xác nhận menu Exit kết thúc process.
- Bước tiếp theo: P0.3 — loại bỏ I/O ICO lặp lại mỗi giây hoặc cache icon an toàn.

### 2026-09-12 — P0.3 — Cache tray icon theo giá trị hiển thị

- Mục tiêu: tránh render bitmap và ghi file ICO mỗi giây khi số hiển thị không đổi.
- File thay đổi: `Services/DownloadMonitorService.cs`.
- Thay đổi chính: lưu `_lastIconKey` và `_lastIconSource`; chỉ tạo `Icon`/`BitmapImage` khi chuỗi tray thay đổi.
- Kiểm tra đã chạy: kiểm tra tĩnh source; chưa build vì môi trường chưa có `dotnet` CLI.
- Kết quả: giảm I/O và giảm số lần tạo GDI/WPF image trong các tick ổn định.
- Vấn đề còn lại: cần kiểm tra alpha/lifetime thực tế trong Visual Studio; bước P0.1 đã clone icon handle trước dispose bitmap.
- Bước tiếp theo: P1.1 — gộp việc đọc adapter và diagnostics trong một lần lấy mẫu.

### 2026-09-12 — P1.1 — Gộp network sample và diagnostics

- Mục tiêu: không enumerate network adapter hai lần trong cùng một tick.
- Files thay đổi: `Models/NetworkReadResult.cs`, `Services/NetworkSpeedReader.cs`, `Services/DownloadMonitorService.cs`.
- Thay đổi chính: thêm `ReadWithDiagnostics()`, đọc adapter/statistics một lần rồi trả về cả `DownloadSpeedSnapshot` và diagnostics.
- Kiểm tra đã chạy: kiểm tra tĩnh source; chưa build vì môi trường chưa có `dotnet` CLI.
- Kết quả: giảm số lần gọi network API trong mỗi chu kỳ.
- Vấn đề còn lại: giữ `Read()`/`GetDiagnostics()` cũ để tương thích nội bộ; cần xóa hoặc chuyển thành wrapper sau khi test mới ổn định.
- Bước tiếp theo: P1.2 — giảm refresh toàn bộ DataGrid mỗi giây.

### 2026-09-12 — P1.2 — Giảm refresh DataGrid

- Mục tiêu: tránh reset toàn bộ bảng adapter mỗi giây.
- File thay đổi: `MainWindow.xaml.cs`.
- Thay đổi chính: giới hạn cập nhật `AdapterGrid.ItemsSource` tối đa một lần mỗi 5 giây; tốc độ vẫn cập nhật mỗi giây.
- Kiểm tra đã chạy: kiểm tra tĩnh source; chưa build vì môi trường chưa có `dotnet` CLI.
- Kết quả: giảm nhấp nháy và giảm chi phí refresh UI.
- Vấn đề còn lại: bước sau có thể chuyển sang `ObservableCollection` để cập nhật từng dòng nếu cần realtime đầy đủ.
- Bước tiếp theo: P1.3 — cải thiện adapter filtering và lưu override theo interface Id.

## 2026-09-12 — Tiếp tục P1.3 / P1.4 và kiểm chứng tự động

- Môi trường hiện có .NET SDK 10.0.400. Đặt DOTNET_CLI_HOME trong work/dotnet-home để chạy trong sandbox.
- P1.3: tách AdapterFilter, lưu AdapterOverrides theo interface ID. Chọn dòng trong bảng rồi mở menu Include / Exclude / Automatic. Override không ép adapter Down thành Up; thiếu statistics vẫn bị loại.
- Tách AdapterCounterTracker; tính delta riêng mỗi ID. Adapter mới chỉ tạo baseline, counter giảm bị bỏ qua, khoảng đo trên 10 giây reset baseline. Xóa các hàm reader cũ trùng logic.
- P1.4: SettingsService nhận đường dẫn tùy chọn cho test, lưu file tạm cùng thư mục rồi move thay thế, dọn file tạm; load chịu JSON lỗi/null/thiếu quyền. UI báo lỗi lưu thay vì crash. Chặn checkbox tự ghi settings trong lúc khởi tạo.
- Sửa làm tròn midpoint theo AwayFromZero, chống NaN/Infinity.
- Hiệu chỉnh kết luận P0.1 cũ: GetHicon tạo handle độc lập, không phải handle phụ thuộc lifetime Bitmap. Clone đơn thuần chưa giải phóng native handle ban đầu; đã thêm DestroyIcon trong finally. Chưa tuyên bố giải quyết alpha đen hoặc stress-test GDI.
- Bảng giữ selected ID khi refresh, không refresh lúc menu đang mở hoặc cửa sổ ẩn.
- Build/test: dotnet test NetworkDownloadTray.sln --no-restore thành công sau thay đổi cuối; 12 passed, 0 failed, 0 skipped. Chưa kiểm tra trực quan tray/menu và đăng nhập Windows trong phiên này.
- Còn lại: P1.5 kiểm tra system events/Explorer, P1.6 tách tray service, P1.7 mở rộng test renderer/startup, các bước P2. P0.3 vẫn còn file ICO khi giá trị thay đổi, chưa phải pipeline memory-only.

## 2026-09-12 — Hoàn thiện các bước còn lại (kết quả mới nhất)

### P1.5 — System events và recovery

- DownloadMonitorService đăng ký PowerModeChanged khi Start, bỏ đăng ký khi Dispose. Reset generation/baseline để kết quả trước resume hoặc đổi cấu hình không được công bố sau đó.
- Mỗi adapter giữ counter riêng; mất kết nối/counter reset/đọc lỗi không được tính thành lưu lượng mới.
- H.NotifyIcon 2.4.1 đã có TaskbarCreated handler. TrayIconService bổ sung retry khi IsCreated=false, cách tối thiểu 5 giây.
- App xử lý SessionEnding, giữ cửa sổ hiện nếu chưa đăng ký được tray; chặn chạy trùng bằng mutex theo Windows session.
- Chưa sleep/restart Explorer/đăng xuất thật. Native tray test trong sandbox trả TryCreate failed; đây là kiểm tra chưa qua, không phải chứng cứ lỗi renderer.

### P1.6 — Service boundaries

- Tách INetworkStatisticsProvider/NetworkStatisticsProvider, reader thuần logic có clock inject, monitor background không phụ thuộc tray, ITrayIconService/TrayIconService.
- Tách IStartupStore/RegistryStartupStore, WindowsStartupService chỉ kiểm tra/lưu command hợp lệ.
- MainWindow nhận service qua constructor. App chịu composition/lifecycle.
- Đọc thống kê IP chung; lấy adapter/statistics một lần mỗi sample.
- Timer không chồng lần đọc; chạy sampling trên Task.Run, trả snapshot về dispatcher, không công bố sau Dispose.

### P1.7 — Verification

- Tests mới: reader gọi provider một lần, virtual duplicate, read failure, override, alpha/mask từng pixel tại 16/20/24/32/48/64px, 4 digits, handle sau 2.000 icon, startup path quoting/invalid path, WPF hide/reopen/resume/exit, atomic settings.
- Smoke WPF dùng fake tray/store để không sửa Registry và không phụ thuộc Shell. Render screenshot ở work/test-artifacts/window-smoke.png; đã xem ảnh.
- Smoke đã bắt lỗi DataGridCheckBoxColumn TwoWay lên Included readonly; sửa Mode=OneWay và chạy lại thành công.
- NativeTrayTests là opt-in bằng NDT_DESKTOP_TESTS=1; gửi TaskbarCreated chỉ tới HWND của app test, không broadcast hay kill Explorer.

### P2.1 — Pixel renderer và I/O

- Giữ glyph custom 16x16, màu vàng. 4 chữ số dùng bộ 3x7; >9999 giới hạn trên icon, tooltip/cửa sổ vẫn là Mbps đầy đủ.
- Lấy kích thước small icon theo DPI của primary taskbar. Vẽ pixel trực tiếp, chỉ alpha 0/255.
- Encode ICO 32bpp + AND mask thủ công trong memory. Bỏ Bitmap.GetHicon, ICO file, BitmapImage/URI.
- TaskbarIcon.Icon là API được XML/source phiên bản 2.4.1 xác nhận. OnIconChanged dispose icon trước; service chuyển ownership cho thư viện và không dispose icon đang dùng.
- Kiểm tra không tăng GDI/USER handles vượt ngưỡng trong 2.000 lần tạo/hủy; kiểm tra decode góc alpha=0 thành công.

### P2.2 — UI/settings

- ObservableCollection/AdapterRow giữ đối tượng dòng; chỉ refresh membership khi Include thay đổi. Không refresh bảng khi cửa sổ ẩn/menu đang mở.
- Thêm filter All/Included/Excluded, Refresh, Copy selected, Mode và ID; chuột phải chọn đúng dòng.
- Settings có Show adapter diagnostics, đường dẫn startup thật. Không tự ghi Registry mỗi lần mở app; checkbox Startup là đăng ký Run entry, không khẳng định Task Manager chưa disable.
- Tách settings và actions thành hàng riêng, style DataGrid dark, XAML đã format dễ bảo trì.
- Save settings dùng file tạm cùng thư mục, flush-to-disk, move/replace; bắt lỗi và khôi phục lựa chọn UI nếu lưu thất bại.

### P2.3 — Build/publish

- Tạo win-x64.pubxml self-contained single-file không trim và portable.pubxml framework-dependent.
- Portable publish thành công ở outputs/portable. Cần .NET Desktop Runtime 10 x64, giữ nguyên các DLL bên cạnh EXE.
- Self-contained thử restore/publish thất bại do TLS NU1301/SEC_E_NO_CREDENTIALS. Đã xin network/cache permission, thử lại dotnet/curl và Node HTTPS nhưng không tải được. Không thay đổi xác minh TLS hay giả lập runtime pack.
- Chưa có EXE self-contained đã xác minh. Ghi gate này mở trong PLAN.md.
- Giữ profile FolderProfile sẵn có. Bảo toàn VisualStudio .gitignore; bổ sung work/outputs và chỉ whitelist hai profile không chứa bí mật.

### P2.4 — Documentation

- README, RELEASE_CHECKLIST và hướng dẫn trong outputs/portable mô tả thao tác, giới hạn icon, cấu hình, tests, publish và uninstall.
- Không tạo installer/đăng ký startup thật trong phiên kiểm tra.
- Kết quả test cuối và bản đóng gói được bổ sung sau bước verification cuối.

## Template cập nhật mỗi bước

### [Ngày] — [Mã bước] — [Tên bước]

- Mục tiêu:
- Files thay đổi:
- Thay đổi chính:
- Kiểm tra đã chạy:
- Kết quả:
- Vấn đề còn lại:
- Bước tiếp theo:

## 2026-09-12 — Final optimization audit

- Phạm vi: rà toàn bộ code theo PLAN.md, kiểm tra các đường network sampling, tray icon, WPF lifecycle, settings/startup, tests và publish artifacts.
- Kiểm tra: `dotnet test NetworkDownloadTray.sln -c Release --no-restore` và `dotnet build NetworkDownloadTray.sln -c Release --no-restore`.
- Kết quả: 33 tests passed, 0 failed, 1 skipped có chủ đích; build Release 0 warning / 0 error.
- Static review: không còn `Bitmap.GetHicon`, ICO file tạm, enumerate adapter trùng trong một sample, hoặc refresh DataGrid toàn bộ theo từng tick. Renderer alpha/mask và cache theo text + DPI vẫn đúng thiết kế.
- Kết luận: code đã đạt các mục tối ưu bắt buộc trong plan. Còn lại chỉ là release gates cần xác nhận trên Windows desktop thật: native tray, Explorer restart, sleep/resume, startup login, DPI đa màn hình và self-contained publish khi môi trường NuGet/TLS hoạt động.

## 2026-09-23 — R2 — Tối ưu hiệu năng vòng 2 (multi-agent)

- Mục tiêu: giảm chi phí mỗi tick 1 giây (CPU, allocation, wake-up) và bộ nhớ thường trú, không đổi hành vi người dùng. Chi tiết kế hoạch, số đo và phân công agent trong `OPTIMIZATION_PLAN.md`.
- Files thay đổi: `Services/NetworkStatisticsProvider.cs`, `Services/Native/IpHelperApi.cs` (mới), `Services/NetworkSpeedReader.cs`, `Services/AdapterCounterTracker.cs`, `Services/DownloadMonitorService.cs`, `Services/TrayIconRenderer.cs`, `Services/TrayIconService.cs`, `Services/SettingsService.cs`, `App.xaml.cs`, `Models/AdapterRow.cs`, `NetworkDownloadTray.csproj`, `Properties/PublishProfiles/win-x64.pubxml`, tests (`ProviderTests`, `MonitorTests`, `AdapterRowTests` mới; mở rộng `AdapterTests`, `ReaderTests`, `RendererTests`, `DesktopIntegrationTests`).
- Thay đổi chính:
  - Provider lai: danh sách adapter từ `GetAllNetworkInterfaces()` được cache (làm mới khi NetworkChange, sau 30 s, hoặc khi mất GUID); trạng thái và InOctets lấy bằng một lần `GetIfTable2` mỗi tick, ghép theo Guid; fallback về đường managed khi native lỗi. Id adapter giữ nguyên `NetworkInterface.Id`.
  - Tracker double-buffer, 0 byte/tick. Reader đọc provider ngoài phạm vi UI thread chờ; `ConfigureAdapters`/`Reset` không khóa, mẫu đang đọc dở bị bỏ để tránh spike.
  - Tạm dừng lấy mẫu khi khóa phiên, tiếp tục khi mở khóa. Suspend chỉ reset (xem bên dưới).
  - Icon: bỏ `Clone()`, encode ICO vào một `byte[]` (golden hash bảo đảm giống từng byte); tooltip chỉ gửi khi đổi.
  - Cửa sổ chính tạo lười khi khởi động thu nhỏ; `AdapterRow` chỉ notify thuộc tính thay đổi; `JsonSerializerOptions` static.
  - Build: `ConcurrentGarbageCollection=false`, `SatelliteResourceLanguages=en`, `TieredPGO`; win-x64 bật ReadyToRun.
- Kiểm tra đã chạy: `dotnet build -c Release` (0 warning / 0 error); `dotnet test -c Release` (59 passed, 0 failed, 1 skipped có chủ đích); harness so sánh baseline 7dc41c6 và bản mới trên cùng máy.
- Kết quả: CPU mỗi tick 15.6–17.6 ms → ~0.9–1.0 ms; allocation 111 KB → 11 KB; tạo icon 32 px 0.95–1.34 ms → 0.45–0.61 ms; khởi động thu nhỏ bớt ~26 MB private bytes.
- Review độc lập: sửa lỗi MED — dừng timer khi Suspend có thể khiến app ngừng cập nhật mãi mãi vì `SystemEvents` không báo `PBT_APMRESUMEAUTOMATIC`; nay Suspend chỉ reset. Sửa LOW — cache adapter chỉ đánh dấu mới sau khi enumerate thành công.
- Vấn đề còn lại: trạng thái adapter không có dòng `GetIfTable2` (không gặp trên máy này) có thể trễ tới 30 s; win-x64 lớn hơn 16 MB do ReadyToRun; lần Open đầu sau khởi động thu nhỏ mất ~1–2 s.
- Bước tiếp theo: kiểm tra trên desktop thật — native tray test (`NDT_DESKTOP_TESTS=1`), restart Explorer, khóa/mở khóa, sleep/wake.

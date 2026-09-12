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

## Template cập nhật mỗi bước

### [Ngày] — [Mã bước] — [Tên bước]

- Mục tiêu:
- Files thay đổi:
- Thay đổi chính:
- Kiểm tra đã chạy:
- Kết quả:
- Vấn đề còn lại:
- Bước tiếp theo:

# NetworkDownloadTray — Optimization & Hardening Plan

## Mục tiêu

Ổn định hóa ứng dụng hiển thị tốc độ download Mbps trong System Tray, giảm lỗi runtime, giảm chi phí cập nhật, bảo toàn alpha của icon và đủ an toàn để publish sử dụng lâu dài.

## Nguyên tắc thực hiện

1. Xử lý lỗi runtime và lifecycle trước tối ưu giao diện.
2. Mỗi bước phải có thay đổi nhỏ, có kiểm tra riêng.
3. Không thay đổi hành vi người dùng ngoài phạm vi bước đang xử lý.
4. Cập nhật `IMPLEMENTATION_HISTORY.md` sau mỗi bước.
5. Nếu build/test không chạy được do môi trường, ghi rõ blocker thay vì đoán kết quả.

## Trạng thái

- [x] Tạo kế hoạch và nhật ký thực hiện.
- [x] P0.1 — Sửa lifetime của tray icon và loại bỏ handle/resource leak.
- [x] P0.2 — Sửa lifecycle đóng cửa sổ và Exit.
- [x] P0.3 — Bỏ I/O ICO lặp lại mỗi giây hoặc cache icon an toàn.
- [x] P1.1 — Gộp việc đọc adapter và diagnostics trong một lần lấy mẫu.
- [x] P1.2 — Giảm refresh DataGrid và tránh nhấp nháy UI.
- [ ] P1.3 — Cải thiện adapter filter và lưu override theo interface Id.
- [ ] P1.4 — Làm settings save atomic và chịu lỗi tốt hơn.
- [ ] P1.5 — Xử lý sleep/resume, taskbar restart và adapter reset.
- [ ] P1.6 — Tách service để dễ test và giảm phụ thuộc WPF.
- [ ] P1.7 — Bổ sung unit tests cho reader, renderer, settings và startup abstraction.
- [ ] P2.1 — Tối ưu pixel renderer/cache icon.
- [ ] P2.2 — Hoàn thiện UI diagnostics/settings.
- [ ] P2.3 — Tạo publish profile và kiểm tra publish sạch.
- [ ] P2.4 — Rà soát tài liệu sử dụng và quy trình release.

## Chi tiết từng bước

### P0.1 — Tray icon lifetime

Kiểm tra và sửa việc tạo `Icon` từ `Bitmap.GetHicon()`, bảo đảm bitmap/handle/icon có lifetime hợp lệ, không leak GDI và không làm nền alpha bị đen. Thêm test hoặc diagnostic phù hợp.

Tiêu chí hoàn thành:

- Không dùng handle của bitmap đã bị dispose.
- Icon cập nhật ổn định qua nhiều chu kỳ.
- Không tăng GDI handles bất thường khi chạy lâu.

### P0.2 — Window closing và Exit

Tách trạng thái `Hide to tray` và `Application.Shutdown()`. Nút/menu Exit phải thoát thật; nút đóng cửa sổ chỉ ẩn khi chưa yêu cầu thoát.

Tiêu chí hoàn thành:

- Menu `Exit` kết thúc process.
- Nút `Exit` kết thúc process.
- Nút `X` chỉ ẩn cửa sổ.
- Monitor và tray icon được dispose đúng một lần.

### P0.3 — Icon update/cache

Không ghi file ICO mỗi giây. Ưu tiên `GeneratedIconSource` hoặc cache theo text; nếu cần pixel renderer thì dùng pipeline có alpha đúng và chỉ render khi giá trị hiển thị thay đổi.

### P1.1 — Một lần đọc adapter

Tạo kết quả lấy mẫu chứa cả tốc độ và diagnostics, tránh gọi `GetAllNetworkInterfaces()` hai lần trong cùng tick.

### P1.2 — DataGrid

Chỉ cập nhật bảng khi danh sách adapter hoặc thông tin cần hiển thị thay đổi. Giữ selection/scroll position và tránh thay toàn bộ `ItemsSource` mỗi giây.

### P1.3 — Adapter filtering

Tách bộ lọc thành policy rõ ràng, bổ sung override theo `NetworkInterface.Id`, tránh phụ thuộc hoàn toàn vào tên driver.

### P1.4 — Settings

Ghi settings qua file tạm rồi replace, bắt đủ lỗi I/O, giữ app hoạt động nếu settings hỏng.

### P1.5 — System events

Reset mẫu khi sleep/resume, xử lý adapter reset, mất mạng và Explorer/taskbar restart.

### P1.6 — Service boundaries

Tách network reader, sampling service, tray icon service, startup service và settings service để giảm coupling.

### P1.7 — Tests

Bổ sung test cho counter reset, adapter filtering, renderer bounds/alpha, settings lỗi, startup abstraction và lifecycle logic.

### P2 — Polish và release

Tối ưu renderer, hoàn thiện diagnostics/settings, tạo publish profile `win-x64`, kiểm tra self-contained single-file và viết hướng dẫn release.

## Quy trình kiểm tra sau mỗi bước

1. Clean/Rebuild solution.
2. Chạy unit tests.
3. Chạy app bằng F5.
4. Kiểm tra tray icon, tooltip, tốc độ và Exit.
5. Ghi kết quả vào `IMPLEMENTATION_HISTORY.md`.

# Release verification

## Tự động

- [x] Build Debug và Release.
- [x] Test công thức, reset/hotplug, filter/override.
- [x] Test settings hỏng/round-trip và startup store giả.
- [x] Test ICO alpha/mask, 1–4 chữ số, DPI sizes, 2.000 lần tạo/hủy icon.
- [x] WPF smoke: hiện số, ẩn/mở lại cửa sổ, reset measurements, shutdown.
- [x] Publish framework-dependent portable.
- [ ] Publish win-x64 self-contained: đang bị TLS NuGet chặn trong môi trường agent.

## Desktop thực tế — chưa xác nhận trong phiên này

- [ ] Test native tray opt-in pass trong terminal Windows tương tác.
- [ ] Chạy EXE đã publish; kiểm tra Open/Exit, X, minimize.
- [ ] Không có nền đen trên taskbar sáng/tối; đọc được 0, 46, 403, 1000.
- [ ] 100%, 125%, 150%, 200% DPI; primary/secondary monitors.
- [ ] Sleep/resume: mẫu đầu đo lại, không spike, icon vẫn hoạt động.
- [ ] Restart Explorer: icon tự xuất hiện trở lại.
- [ ] Wi-Fi/Ethernet/VPN đổi trạng thái: không đếm trùng, không cộng counter lịch sử.
- [ ] Auto start trỏ đúng bản publish; đăng xuất/đăng nhập chạy đúng.
- [ ] Disable trong Task Manager không bị ứng dụng tự bật lại.
- [ ] Đóng bằng Exit kết thúc process; không còn icon sau khi rê chuột.
- [ ] Chạy vài giờ: memory/USER/GDI handle không tăng đều.

## Đóng gói

1. Run tests Release.
2. Publish profile phù hợp.
3. Copy toàn bộ output vào thư mục ổn định.
4. Kiểm tra desktop ở trên rồi mới phân phối.
5. Nếu chưa tải được runtime, chỉ phân phối portable và ghi rõ cần .NET Desktop Runtime 10 x64.
6. Installer/signing không được thực hiện trong phiên này; release hiện là folder/zip.

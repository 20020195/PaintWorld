# Paint Game - Project Context & Rules

## 🎮 Tổng quan dự án (Project Overview)
Đây là một game tô màu (Coloring Book/Paint) 2D cơ bản trong Unity. Người chơi sẽ chọn một bức tranh đen trắng (có sẵn viền đen), chọn màu, và click vào các vùng trống để đổ màu (Flood Fill). Trò chơi có hỗ trợ Multi-Scene với Menu, Chọn tranh, và màn hình Tô màu.

---

## 🏗️ Kiến trúc & Flow Game (Architecture & Game Flow)

### 1. Game Flow
Dự án vận hành qua 3 Scene chính, cần được load thông qua `SceneManager` theo chuẩn thứ tự trong Build Settings:
- **`MainMenu` (Scene 0):** Màn hình chờ → Nút "Bắt đầu chơi" gọi `SceneManager.LoadScene("PictureSelect")`.
- **`PictureSelect` (Scene 1):** Màn hình Carousel chọn tranh. Khi người chơi click vào một bức tranh `PictureCarousel` sẽ lưu thông tin tranh vào **`GameData`** và chuyển sang `PaintScene`.
- **`PaintScene` (Scene 2):** Màn hình tô màu chính. Đọc ảnh từ `GameData`. Nếu người chơi ấn "← Trở về", nó gọi `SceneManager.LoadScene("PictureSelect")`.

### 2. Các Script Quan Trọng (Core Scripts)

#### Data & Quản lý Scene
- **`GameData.cs`**: `static class` đóng vai trò là Data Bus truyền dữ liệu (Sprite, Total Regions, Tên Tranh) giữa `PictureSelect` và `PaintScene`. Tuyệt đối không xóa hay thay đổi cơ chế truyền static này nếu không có class quản lý State thay thế.
- **`MainMenuManager.cs`**: Xử lý logic cơ bản của màn hình Menu (Bắt đầu, Thoát).
- **`GameUIManager.cs`**: Quản lý UI On-screen của `PaintScene` (nút Trở về, Hủy bước, Hoàn thành, Màn hình Win).

#### Picture Selection (Carousel)
- **`PictureCarousel.cs`**: Xử lý logic vuốt/scroll ngang, tự động snap (hút) về bức tranh gần nhất.
- **`CarouselDragProxy.cs`**: Truyền event Drag từ Viewport (bị block bởi RectMask2D) xuống cho `PictureCarousel`.
- **`PictureCard.cs`**: Chứa data của từng bức tranh (Sprite, Name) và xử lý hiệu ứng Scale/Alpha khi tranh đó nằm ở giữa hay bị cuộn ra rìa.

#### Paint Logic (Cốt lõi)
- **`PaintController.cs`**: Xử lý toàn bộ logic Flood Fill.
  - Sử dụng mảng Pixel (`Color32[]`) để tính toán nhòe màu và tạo Texture mới.
  - Hỗ trợ **Multiply Blending** (Hòa trộn với độ sáng của pixel đen gốc) thay vì Solid Color để các nét viền không bị "nhem" (Artifacts/Halos).
  - Có tích hợp Stack giữ lại trạng thái (10 bước gần nhất) để hỗ trợ **Undo**.
- **`RGBColorPicker.cs`**: Màn hình Popup cho phép người chơi pha màu RGB tùy chỉnh.
- **`ColorPaletteUI.cs`**: Thanh màu Preset bên dưới, chứa nút `+` gọi `RGBColorPicker`.
- **`CameraZoomPan.cs`**: Điều khiển Camera Orthographic trong `PaintScene`. Hỗ trợ cuộn chuột để zoom thẳng vào vị trí con trỏ (Zoom to Point) và kéo chuột (chuột giữa/chuột phải) để Pan. *Sử dụng Screen Space Delta để tránh bị giật.*

#### Tự động hóa Editor (Window setup)
Sử dụng các Tool Scripts nằm trong thư mục `Editor/` để tự động hóa xây dựng UI (vì UI của Unity sinh ra rất phức tạp để AI tự tạo lại thủ công).
- **`MainMenuSetup.cs`**: Sinh tự động Scene MainMenu.
- **`PictureSelectSetup.cs`**: Sinh tự động Scene PictureSelect.
- **`PaintSceneSetup.cs`**: Sinh tự động Scene PaintScene.
*Luôn ưu tiên chạy các Editor tool này khi bị hỏng Scene hoặc UI thay vì cố manual add Component lại.*

---

## 📝 Quy tắc Lập Trình (Coding Rules & Best Practices)

1. **Unity UI Event System:**
   - Khi thiết kế UI chồng lên Game (ví dụ: RGB Picker nổi lên trên bức tranh), **Bắt buộc** phải sử dụng `EventSystem.current.IsPointerOverGameObject()` trong hàm click thực tế của World (như `OnMouseDown` của `PaintController` hay Raycast kéo Camera trong `CameraZoomPan`) để block Raycast xuyên thấu. Nếu không, bấm vào UI cũng làm bức tranh bị tô màu.

2. **Xử lý Texture (Texture Manipulation):**
   - Import ảnh nét viền (Outlines) với thuộc tính `isReadable = true` và `Format = RGBA 32 bit` (trong meta properties).
   - KHÔNG dùng `Graphics.CopyTexture` giữa Texture Import và Runtime Texture do khác biệt byte nén. LUÔN dùng `Texture2D.GetPixels32()` và `Texture2D.SetPixels32()`.

3. **Tối ưu hóa Thuật Toán Flood Fill:**
   - Dùng vòng lặp Stack (khử đệ quy) để tránh StackOverflow.
   - Để tránh "răng cưa / nhem viền lốm đốm", phải kiểm tra biên và dung sai (tolerance) rộng `fillTolerance = 0.8`.
   - Lưu trữ pixel ảnh gốc `originalPixels`. Khi chạy Flood Fill, lấy màu fill nhân với độ sáng gốc thay vì đè thành khối màu đặc.

4. **Quản lý Camera (Zoom & Pan):**
   - Orthographic Camera Scaling.
   - Pan phải dùng Window delta (`Input.GetAxis("Mouse X")` nhân với tham chiếu `orthographicSize / ScreenHeight`) để tránh hiệu ứng phản hồi (feedback loop / jitter) khiến màn hình bị giật. KHÔNG dùng delta `ScreenToWorldPoint`.

5. **Lịch sử Undo:**
   - Chỉ lưu tối đa `MAX_UNDO_STEPS = 10` bằng cách lưu mảng `Color32[]`. Vượt mức sẽ tự động xóa phần tử mảng cũ nhất bằng `RemoveAt(0)` để tránh rò rỉ RAM (OOM).

6. **Không tự động Win:**
   - Người chơi tô xong một vùng chỉ +1 vào Progress. Phải ấn nút **"Hoàn thành"** thủ công thì mới xác nhận thắng. (Cho phép tô lại, tô đè tùy thích).

---
*File này đóng vai trò hướng dẫn trực tiếp cho LLM ở các phiên làm việc tiếp theo.*

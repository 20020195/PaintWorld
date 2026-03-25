# Paint Game - Project Context & Rules

## 🎮 Tổng quan dự án (Project Overview)
Đây là một game tô màu (Coloring Book/Paint) 2D cơ bản trong Unity. Người chơi sẽ chọn một bức tranh đen trắng (có sẵn viền đen), chọn màu, và click vào các vùng trống để đổ màu (Flood Fill). Trò chơi có hỗ trợ Multi-Scene với Menu, Chọn tranh, và màn hình Tô màu.

---

## 🏗️ Kiến trúc & Flow Game (Architecture & Game Flow)

### 1. Game Flow
Dự án vận hành qua 3 Scene chính, cần được load thông qua `SceneManager` theo chuẩn thứ tự trong Build Settings:
- **`MainMenu` (Scene 0):** Màn hình chờ → Nút "Bắt đầu chơi" gọi `SceneManager.LoadScene("PictureSelect")`.
- **`PictureSelect` (Scene 1):** Màn hình Carousel chọn tranh. Khi người chơi click vào một bức tranh `PictureCarousel` sẽ lưu thông tin tranh vào **`GameData`** và chuyển sang `PaintScene`.
- **`PaintScene` (Scene 2):** Màn hình tô màu chính. Đọc ảnh từ `GameData`. Hỗ trợ 2 lớp vẽ (Outline phía trên, ColorLayer phía dưới).

### 2. Các Script Quan Trọng (Core Scripts)

#### Data & Quản lý Scene
- **`GameData.cs`**: `static class` đóng vai trò là Data Bus truyền dữ liệu (Sprite, Tên Tranh) giữa `PictureSelect` và `PaintScene`. Tuyệt đối không xóa hay thay đổi cơ chế truyền static này nếu không có class quản lý State thay thế.
- **`SaveSystem.cs`**: Utility xử lý I/O lưu tiến trình tô màu (`Texture2D.EncodeToPNG()`). Lưu 2 file: `_save.png` (raw color layer cho PaintScene) và `_preview.png` (bản gộp viền đen cho Gallery thumbnail).
- **`MainMenuManager.cs`**: Xử lý logic cơ bản của màn hình Menu (Bắt đầu, Thoát).
- **`GameUIManager.cs`**: Quản lý UI On-screen của `PaintScene` (nút Trở về, Hủy bước, Xóa màu/Reset, Hoàn thành, Màn hình Win, Popup Xác nhận Lưu). Điều khiển thu phóng thanh Toolbar bên trái khi chọn Brush/Fill.

#### Picture Selection (Carousel)
- **`PictureData.cs`**: `ScriptableObject` lưu trữ dữ liệu gốc của các bức tranh (Name, Outline Sprite).
- **`PictureCarousel.cs`**: Xử lý logic cuộn ngang, tự động snap. Sinh lặp (Spawn) UI thẻ ảnh động từ mảng `PictureData[]`. Tích hợp load ảnh `_preview.png` từ `SaveSystem` hiển thị trạng thái đã tô.
- **`CarouselDragProxy.cs`**: Truyền event Drag từ Viewport (bị block bởi RectMask2D) xuống cho `PictureCarousel`.
- **`PictureCard.cs`**: Component gắn trên thẻ UI để gán data visual.

#### Paint Logic (Cốt lõi)
- **`PaintController.cs`**: Xử lý toàn bộ logic Flood Fill và Brush (Cọ vẽ).
  - **Cơ chế 2 Layer**: Lớp trên cùng (`SpriteRenderer` chính) là ảnh Outline đã xử lý trong suốt các vùng trắng. Lớp dưới cùng (`ColorLayer`) là `Texture2D` trắng để tô màu.
  - **Flood Fill**: Tô màu đặc (Solid) vào lớp dưới. Sử dụng BFS (Breadth-First Search) khử đệ quy. Sau khi fill, thuật toán tự động thực hiện **Dilation (Thẩm thấu) 3px** ăn vào dưới lớp viền để triệt tiêu viền trắng (white halo). Kick-off fill chỉ xảy ra ở `TouchPhase.Ended` để tránh nhầm với thao tác Zoom.
  - **Brush**: Vẽ tự do bằng cách click-drag. Sử dụng nội suy (Interpolation) để nối các điểm. Đã tối ưu hóa tính toán: chỉ gọi `Texture2D.Apply()` đúng 1 lần cuối frame thay vì gọi trong vòng lặp nội suy (giảm lag 10-20x cho cọ nhỏ). Cọ vẽ được phép tô đè lên vùng viền (bleed under stencil) để đảm bảo độ mịn.
  - **Undo (Diff-based)**: Lưu cấu trúc `PixelDiff[] {idx, oldColor}` thay vì snapshot toàn bộ `Color32[]`. Tiết kiệm **10-50x** bộ nhớ cho mobile.
  - **Save/Load State**: Sử dụng mảng pixels đọc/ghi qua png, có sinh lớp Composite (Gộp nền + viền) lúc `SaveCurrentProgress()` làm Thumbnail. Tự động xử lý **Contrast Crushing** (`InverseLerp(0.2, 0.8)`) khi tách layer để loại bỏ nhiễu nén (Artifacts) từ định dạng JPEG/DXT.
  - **Mobile input**: `Input.simulateMouseWithTouches = false`. Toàn bộ touch painting xử lý trong `Update()`. Có cơ chế **Multi-touch Protection**: Nếu phát hiện >1 ngón tay (Zoom/Pan), hệ thống lập tức hủy nét vẽ hiện tại và backtrack (`Revert`) lại các pixel vừa bị tô nhầm.
- **`RGBColorPicker.cs`**: Màn hình Popup cho phép người chơi pha màu RGB tùy chỉnh.
- **`ColorPaletteUI.cs`**: Thanh màu Preset bên dưới, chứa nút `+` gọi `RGBColorPicker`.
- **`CameraZoomPan.cs`**: Điều khiển Camera Orthographic trong `PaintScene`.
  - **PC**: Scroll wheel để zoom, chuột giữa/phải để pan.
  - **Mobile**: Pinch 2 ngón để zoom, kéo 1 ngón để pan. 1-finger pan bị tắt khi `PaintController.IsBrushActive == true` (tránh camera dịch chuyển khi đang vẽ).
  - Dùng `#if UNITY_EDITOR || UNITY_STANDALONE` để biên dịch đúng input code theo platform.

#### Tự động hóa Editor (Window setup)
Sử dụng các Tool Scripts nằm trong thư mục `Editor/` để tự động hóa xây dựng UI (vì UI của Unity sinh ra rất phức tạp để AI tự tạo lại thủ công).
- **`MainMenuSetup.cs`**: Sinh tự động Scene MainMenu.
- **`PictureSelectSetup.cs`**: Sinh tự động Scene PictureSelect.
- **`PaintSceneSetup.cs`**: Sinh tự động Scene PaintScene.
*Luôn ưu tiên chạy các Editor tool này khi bị hỏng Scene hoặc UI thay vì cố manual add Component lại.*

### 3. Cấu trúc Thư mục (Directory Structure)
Các script được phân loại trong `Assets/Scripts/`:
- **`Controller/`**: `PaintController`, `CameraZoomPan`.
- **`UI/`**: `GameUIManager`, `ColorPaletteUI`, `RGBColorPicker`, `MainMenuManager`.
- **`Gallery/`**: `PictureCarousel`, `PictureCard`, `PictureData`, `CarouselDragProxy`.
- **`Data/`**: `GameData`, `SaveSystem`.
- **`Editor/`**: Các script setup scene.

---

## 📝 Quy tắc Lập Trình (Coding Rules & Best Practices)

1. **🚨 Luôn cân nhắc tối ưu cho Mobile:**
   - Đây là game mắm nhàc (children) chạy trên điện thoại. Mọi thứ phải cân nhắc đến RAM và CPU hạn chế của thiết bị mobile.
   - **Input**: Dùng `Input.simulateMouseWithTouches = false` và xử lý touch thủ công trong `Update()`. Không dựa vào `OnMouseDown` trên mobile.
   - **UI Blocking**: Dùng `EventSystem.RaycastAll()` thay vì `IsPointerOverGameObject(fingerId)` vì chính xác hơn trên mobile.
   - **Memory**: Ưu tiên cấu trúc diff/patch hơn là lưu full snapshot. Tránh các phép tính tốn kém mỗi frame nếu không cần thiết.
   - **Platform detection**: Sử dụng `#if UNITY_EDITOR || UNITY_STANDALONE` để tách biệt logic PC và Mobile chứ không pha trộn.

2. **Unity UI Event System:**
   - Để tránh touch trên UI xâm nhập vào game world bín dưới do `simulateMouseWithTouches`, hãy dùng `EventSystem.RaycastAll()` tại `TouchPhase.Began` để phát hiện và blacklist touch ID đó.

3. **Xử lý Texture & Outline Extraction:**
   - Import ảnh nét viền (Outlines) với thuộc tính `isReadable = true`.
   - **Outline Alpha Extraction**: Khi tách viền, sử dụng thuật toán Perceptual Luminance kết hợp Contrast Crushing (`InverseLerp(0.2f, 0.8f)`). Điều này ép các vùng nhiễu xám (Artifacts) về đúng đen đặc hoặc trắng trong suốt, giải quyết triệt để lỗi lốm đốm trên viền nén dxt.
   - KHÔNG dùng `Graphics.CopyTexture` giữa Texture Import và Runtime Texture do khác biệt byte nén. LUÔN dùng `Texture2D.GetPixels32()` và `Texture2D.SetPixels32()`.

4. **Tối ưu hóa Thuật Toán Flood Fill:**
   - Dùng vòng lặp Stack (khử đệ quy) để tránh StackOverflow.
   - **Thẩm thấu (Dilation)**: Phải thực hiện BFS lan tỏa 3px từ biên vùng fill lấn vào vùng viền tối. Điều này che lấp các pixel Anti-aliasing của ảnh gốc, loại bỏ "viền trắng" bao quanh vùng màu.
   - **Chống nhầm lẫn**: Chỉ thực hiện Fill ở `TouchPhase.Ended`. Sử dụng cờ `wasMultiTouchThisGesture` để chặn Fill nếu người chơi vừa thực hiện Zoom/Pan trước đó.
   - **Hệ thống 2 Layer**: Đổ màu đặc (Solid Color) trực tiếp vào lớp `ColorLayer` nằm dưới. 🔍 Kiểm tra va chạm với `originalPixels` (ảnh gốc) thay vì `pixels` (ảnh đang tô) để cho phép tô đè màu khác lên mực đen mà không bị kẹt.

5. **Quản lý Camera & Multi-touch:**
   - Orthographic Camera Scaling.
   - **Multi-touch Handling**: Trong `PaintController`, sử dụng cờ hiệu `wasMultiTouchThisGesture`. Nếu `Input.touchCount > 1`, lập tức tắt `isDraggingBrush` và trả lại màu cũ (`Revert`) cho các pixel bị chạm nhầm. Toàn bộ logic tô màu phải bị khóa cho đến khi người chơi nhấc hết các ngón tay lên.
   - Pan phải dùng **Local Coordinate Delta** (tính toán dựa trên `transform.InverseTransformPoint` và vị trí chuột cũ) để tránh hiện tượng rung lắc (jitter).

6. **Lịch sử Undo (Diff-based):**
   - Lưu cấu trúc `PixelDiff[] {int idx, Color32 oldColor}` thay vì snapshot toàn bộ `Color32[]`. Tiết kiệm 10-50x bộ nhớ cho mobile.
   - FloodFill trả về diff ngay trong quá trình fill. Brush lưu `strokeOriginals` (Dictionary pixel đầu tiên bị chạm) và commit 1 lần khi nhấc ngón tay.
   - `maxUndoSteps` là `public` field có thể chỉnh sử a trong Inspector. Mặc định là 20.

7. **Không tự động Win & Loại bỏ Progress Bar:**
   - Game hiện chú trọng tô tự do (Sandbox). Bỏ hoàn toàn khái niệm biến đếm `totalRegions`.
   - Người chơi tự ấn nút **"Hoàn thành"** thủ công thì mới Show Win Screen và tự động làm sạch file Save. Tùy ý tô đi tô lại bằng nút Reset (Xóa màu).

---
*File này đóng vai trò hướng dẫn trực tiếp cho LLM ở các phiên làm việc tiếp theo.*

# Neon Pulse Fitness

Playable desktop rhythm-fitness prototype for Unity 2022.3 LTS.

## No-code Gameplay Control Center

Người dùng không cần mở hoặc sửa code:

1. Trong Unity chọn `Tools > Neon Pulse > Gameplay Control Center`.
2. Nếu chưa biết chỉnh gì, chọn preset `DỄ`, `CHUẨN` hoặc `KHÓ`.
3. Bấm `LƯU`, `KIỂM TRA`, sau đó bấm `CHƠI THỬ`.

Tool quản lý tập trung nhịp độ, tốc độ vật thể, timing, phím điều khiển, điểm/combo,
camera shake, màu/shader/VFX, object pool và toàn bộ danh sách Beatmap. Cấu hình được
lưu tại `Assets/NeonPulse/Resources/NeonPulseGameConfig.asset` khi mở Tool lần đầu.

Tick `Tự động chơi` trong tab `BẮT ĐẦU` để game tự đánh và tự né đúng nhịp. Ở chế độ
này, text hướng dẫn phím và bảng gợi ý hành động sẽ tự động được ẩn.

Beatmap được chia thành ba luồng độc lập trong Tool: vòng đấm, cửa/chướng ngại và gạch
nhịp. Mỗi luồng có danh sách, spawn index và object pool riêng. Tile bay sát mặt đường
tới hàng receptor 4 lane; ngay khi chạm vạch, tile biến mất, phát particle theo màu,
làm receptor pulse và tự cộng điểm/combo.

## Controls

- `Q` / Left Arrow: left punch (cyan)
- `E` / Right Arrow: right punch (magenta)
- `F` or Q+E together: both-hands punch
- Hold `S` / Down Arrow until the overhead obstacle passes: duck
- Hold `Space` / `W` until the low obstacle passes: jump
- Hold `A` until the wall passes: dodge left
- Hold `D` until the wall passes: dodge right
- `R` / Enter: restart

## Architecture

- `IPlayerInputProvider` isolates gameplay from keyboard/webcam implementations.
- `NeonPulseGameConfig` is the single ScriptableObject data source used by the Tool and runtime.
- Spawning and movement use `AudioSettings.dspTime` to avoid cumulative beat drift.
- A fixed-capacity pool owns all travelling notes and obstacles.
- `RhythmScore` publishes score/judgement events to the HUD and feedback systems.
- All visuals, TextMeshPro UI, particles, materials, and fallback audio are generated
  at runtime from original code and Unity primitives.

The current implementation intentionally has no Odin Inspector or DOTween references
because neither dependency is installed in this repository. This keeps the prototype
compilable without paid or external packages as required by `AI_CONTEXT.md`.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace NeonPulse.EditorTools
{
    /// <summary>Single no-code editor window for tuning and testing the complete prototype.</summary>
    public sealed class NeonPulseGameplayControlCenter : EditorWindow
    {
        private const string ConfigDirectory = "Assets/NeonPulse/Resources";
        private const string ConfigPath = ConfigDirectory + "/NeonPulseGameConfig.asset";
        private const string GameplayScenePath = "Assets/NeonPulse/Scenes/NeonPulseGameplay.unity";

        private static readonly string[] Tabs =
        {
            "BẮT ĐẦU", "NHỊP ĐỘ", "ĐIỀU KHIỂN", "ĐIỂM SỐ", "CAMERA & VFX", "BEATMAP"
        };

        private static readonly string[] GameplayModeLabels = { "ĐẤM", "CHÉM" };

        private NeonPulseGameConfig config;
        private SerializedObject serializedConfig;
        private ReorderableList punchEventList;
        private ReorderableList obstacleEventList;
        private ReorderableList rhythmTileEventList;
        private Vector2 scrollPosition;
        private int selectedTab;
        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;

        [MenuItem("Tools/Neon Pulse/Gameplay Control Center", priority = 1)]
        public static void OpenWindow()
        {
            NeonPulseGameplayControlCenter window = GetWindow<NeonPulseGameplayControlCenter>();
            window.titleContent = new GUIContent("Gameplay Control");
            window.minSize = new Vector2(720f, 580f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadOrCreateConfiguration();
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (config == null || serializedConfig == null)
            {
                EditorGUILayout.HelpBox("Không thể tải Gameplay Configuration.", MessageType.Error);
                if (GUILayout.Button("TẠO LẠI CẤU HÌNH"))
                {
                    LoadOrCreateConfiguration();
                }

                return;
            }

            serializedConfig.UpdateIfRequiredOrScript();
            DrawHeader();
            selectedTab = GUILayout.Toolbar(selectedTab, Tabs, GUILayout.Height(30f));

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.Space(10f);
            switch (selectedTab)
            {
                case 0: DrawGettingStarted(); break;
                case 1: DrawRhythm(); break;
                case 2: DrawControls(); break;
                case 3: DrawScoring(); break;
                case 4: DrawCameraAndVisuals(); break;
                case 5: DrawBeatmap(); break;
            }

            EditorGUILayout.Space(16f);
            EditorGUILayout.EndScrollView();

            if (serializedConfig.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(config);
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("NEON PULSE", titleStyle);
            EditorGUILayout.LabelField("GAMEPLAY CONTROL CENTER", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Không cần sửa code. Chọn mục bên dưới, chỉnh thông số, bấm LƯU rồi bấm CHƠI THỬ. " +
                "Các thay đổi gameplay được áp dụng khi bắt đầu lượt chơi mới.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = EditorApplication.isPlaying ? new Color(1f, 0.35f, 0.35f) : new Color(0.2f, 1f, 0.75f);
            if (GUILayout.Button(EditorApplication.isPlaying ? "■ DỪNG" : "▶ CHƠI THỬ", GUILayout.Height(38f)))
            {
                TogglePlayMode();
            }

            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("MỞ SCENE", GUILayout.Height(38f)))
            {
                OpenGameplayScene();
            }

            if (GUILayout.Button("LƯU", GUILayout.Height(38f)))
            {
                SaveConfiguration(true);
            }

            if (GUILayout.Button("KIỂM TRA", GUILayout.Height(38f)))
            {
                ValidateConfiguration(true);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8f);
        }

        private void DrawGettingStarted()
        {
            DrawSection("LOẠI GAMEPLAY MÀN CHƠI");
            SerializedProperty gameplayMode = serializedConfig.FindProperty("gameplayMode");
            EditorGUILayout.LabelField("Gameplay chính", EditorStyles.boldLabel);
            gameplayMode.enumValueIndex = GUILayout.Toolbar(
                gameplayMode.enumValueIndex,
                GameplayModeLabels,
                GUILayout.Height(38f));
            EditorGUILayout.HelpBox(
                gameplayMode.enumValueIndex == (int)CombatGameplayMode.Slash
                    ? "CHÉM: target đổi thành khối vuông có vạch chém ngẫu nhiên; player cầm kiếm và vung theo hướng ngẫu nhiên."
                    : "ĐẤM: giữ nguyên target tròn và animation găng tay hiện tại.",
                MessageType.Info);

            DrawSection("CHẾ ĐỘ TỰ ĐỘNG");
            SerializedProperty autoPlay = serializedConfig.FindProperty("autoPlay");
            EditorGUILayout.PropertyField(autoPlay, new GUIContent(
                "Tự động chơi",
                "Tự đánh đúng nhịp, tự giữ né/nhảy/cúi và ẩn toàn bộ text hướng dẫn điều khiển."));
            if (autoPlay.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "AUTO PLAY đang bật: game tự thao tác và ẩn hướng dẫn. Bạn vẫn có thể dùng phím Chơi lại.",
                    MessageType.Info);
            }

            DrawSection("1. CHỌN ĐỘ KHÓ NHANH");
            EditorGUILayout.HelpBox("Nếu chưa biết chỉnh gì, hãy chọn DỄ rồi bấm CHƠI THỬ.", MessageType.None);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("DỄ\nChậm, dễ trúng", GUILayout.Height(62f))) ApplyDifficultyPreset(90f, 7.5f, 0.11f, 0.21f, 0.36f);
            if (GUILayout.Button("CHUẨN\nCân bằng", GUILayout.Height(62f))) ApplyDifficultyPreset(105f, 6f, 0.08f, 0.16f, 0.28f);
            if (GUILayout.Button("KHÓ\nNhanh, chính xác", GUILayout.Height(62f))) ApplyDifficultyPreset(125f, 5f, 0.06f, 0.12f, 0.21f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(18f);
            DrawSection("2. QUY TRÌNH 3 BƯỚC");
            EditorGUILayout.LabelField("① Chọn preset hoặc chỉnh các tab phía trên", EditorStyles.largeLabel);
            EditorGUILayout.LabelField("② Bấm LƯU và KIỂM TRA", EditorStyles.largeLabel);
            EditorGUILayout.LabelField("③ Bấm CHƠI THỬ", EditorStyles.largeLabel);

            EditorGUILayout.Space(18f);
            DrawSection("CÔNG CỤ AN TOÀN");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Chọn file cấu hình"))
            {
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }

            GUI.backgroundColor = new Color(1f, 0.75f, 0.3f);
            if (GUILayout.Button("Khôi phục mặc định"))
            {
                ResetAllSettings();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRhythm()
        {
            DrawSection("TỐC ĐỘ BÀI CHƠI");
            SerializedProperty rhythm = serializedConfig.FindProperty("rhythm");
            DrawField(rhythm, "bpm", "Tốc độ nhạc (BPM)", "Số lớn hơn = game nhanh hơn.");
            DrawField(rhythm, "songBeats", "Độ dài bài (số beat)", "Beatmap phải nằm trong khoảng này.");
            DrawField(rhythm, "travelBeats", "Thời gian vật thể bay tới", "Số lớn hơn = nhìn thấy vật thể sớm hơn.");
            DrawField(rhythm, "tileWaveStartBeat", "Beat bắt đầu đợt gạch", "Gạch chỉ xuất hiện trong khoảng đợt này.");
            DrawField(rhythm, "tileWaveEndBeat", "Beat kết thúc đợt gạch", "Target tròn và tường sẽ không xuất hiện chồng với đợt gạch.");
            DrawField(rhythm, "countdownDuration", "Đếm ngược trước khi chơi");
            DrawField(rhythm, "resultDelay", "Chờ trước khi hiện kết quả");

            DrawSection("VỊ TRÍ VẬT THỂ");
            DrawField(rhythm, "spawnZ", "Vị trí xuất hiện");
            DrawField(rhythm, "hitZ", "Vạch đánh / né");
            DrawField(rhythm, "despawnZ", "Vị trí biến mất");
            DrawField(rhythm, "labelVisibleZ", "Khoảng cách hiện chữ hướng dẫn");

            DrawSection("ĐỘ DỄ KHI BẮT NHỊP");
            EditorGUILayout.HelpBox("Cửa sổ thời gian càng lớn thì càng dễ đánh trúng.", MessageType.None);
            DrawField(rhythm, "perfectWindow", "Perfect (giây)");
            DrawField(rhythm, "greatWindow", "Great (giây)");
            DrawField(rhythm, "goodWindow", "Good (giây)");

            DrawSection("GIỮ PHÍM ĐỂ NÉ");
            DrawField(rhythm, "holdWindowLead", "Cho phép giữ sớm");
            DrawField(rhythm, "holdInputGrace", "Thời gian cho phép phản ứng");
            DrawField(rhythm, "holdWindowTrail", "Phải giữ thêm sau khi qua vạch");
        }

        private void DrawControls()
        {
            bool isSlashMode = IsSlashMode();
            DrawSection(isSlashMode ? "PHÍM CHÉM" : "PHÍM ĐẤM");
            SerializedProperty input = serializedConfig.FindProperty("input");
            DrawField(input, "leftPunch", isSlashMode ? "Chém kiếm trái" : "Đấm tay trái");
            DrawField(input, "leftPunchAlternative", isSlashMode ? "Phím phụ kiếm trái" : "Phím phụ tay trái");
            DrawField(input, "rightPunch", isSlashMode ? "Chém kiếm phải" : "Đấm tay phải");
            DrawField(input, "rightPunchAlternative", isSlashMode ? "Phím phụ kiếm phải" : "Phím phụ tay phải");
            DrawField(input, "bothPunch", isSlashMode ? "Chém hai kiếm" : "Đấm hai tay");

            DrawSection("PHÍM NÉ — PHẢI GIỮ ĐẾN KHI VẬT CẢN QUA");
            DrawField(input, "duck", "Cúi");
            DrawField(input, "duckAlternative", "Phím phụ cúi");
            DrawField(input, "jump", "Nhảy");
            DrawField(input, "jumpAlternative", "Phím phụ nhảy");
            DrawField(input, "dodgeLeft", "Né trái");
            DrawField(input, "dodgeRight", "Né phải");

            DrawSection("HỆ THỐNG");
            DrawField(input, "restart", "Chơi lại");
            DrawField(input, "restartAlternative", "Phím phụ chơi lại");
        }

        private void DrawScoring()
        {
            DrawSection("ĐIỂM CƠ BẢN");
            SerializedProperty scoring = serializedConfig.FindProperty("scoring");
            DrawField(scoring, "perfectPoints", "Điểm Perfect");
            DrawField(scoring, "greatPoints", "Điểm Great");
            DrawField(scoring, "goodPoints", "Điểm Good");

            DrawSection("THƯỞNG COMBO MỖI LẦN TRÚNG");
            EditorGUILayout.HelpBox("Điểm nhận được = điểm cơ bản + combo hiện tại × thưởng combo.", MessageType.None);
            DrawField(scoring, "perfectComboBonus", "Thưởng Perfect");
            DrawField(scoring, "greatComboBonus", "Thưởng Great");
            DrawField(scoring, "goodComboBonus", "Thưởng Good");
        }

        private void DrawCameraAndVisuals()
        {
            bool isSlashMode = IsSlashMode();
            DrawSection("CẢM GIÁC DI CHUYỂN");
            SerializedProperty cameraFeel = serializedConfig.FindProperty("cameraFeel");
            DrawField(cameraFeel, "standingHeight", "Độ cao đứng của player", "Tăng để nâng camera và toàn bộ góc nhìn player.");
            DrawField(cameraFeel, "poseSmoothing", "Độ mượt chuyển động");
            DrawField(cameraFeel, "dodgeDistance", "Khoảng cách né trái / phải");
            DrawField(cameraFeel, "duckDistance", "Khoảng cách cúi");
            DrawField(cameraFeel, "jumpDistance", "Khoảng cách nhảy");
            DrawField(cameraFeel, "punchDistance", isSlashMode ? "Tầm vung kiếm" : "Tầm đấm găng tay");
            DrawField(cameraFeel, "punchDuration", isSlashMode ? "Thời gian nhát chém" : "Thời gian cú đấm");

            DrawSection("RUNG CAMERA");
            DrawField(cameraFeel, "punchShakeAmplitude", isSlashMode ? "Độ rung khi chém" : "Độ rung khi đấm");
            DrawField(cameraFeel, "punchShakeDuration", isSlashMode ? "Thời gian rung khi chém" : "Thời gian rung khi đấm");
            DrawField(cameraFeel, "bothPunchShakeAmplitude", isSlashMode ? "Độ rung khi chém hai kiếm" : "Độ rung khi đấm hai tay");
            DrawField(cameraFeel, "bothPunchShakeDuration", isSlashMode ? "Thời gian rung hai kiếm" : "Thời gian rung hai tay");
            DrawField(cameraFeel, "rhythmTileShakeAmplitude", "Độ rung khi gạch chạm vạch");
            DrawField(cameraFeel, "rhythmTileShakeDuration", "Thời gian rung khi gạch chạm vạch");
            DrawField(cameraFeel, "failShakeAmplitude", "Độ rung khi sai nhịp");
            DrawField(cameraFeel, "failShakeDuration", "Thời gian rung khi sai");

            DrawSection("MÀU, SHADER & VFX");
            SerializedProperty visuals = serializedConfig.FindProperty("visuals");
            DrawField(visuals, "backgroundTexture", "Ảnh nền gameplay");
            DrawField(visuals, "cyan", "Màu tay trái");
            DrawField(visuals, "magenta", "Màu tay phải");
            DrawField(visuals, "purple", "Màu đường hầm");
            DrawField(visuals, "yellow", "Màu Perfect / vạch đánh");
            DrawField(visuals, "obstacle", "Màu vật cản");
            DrawField(visuals, "neonIntensity", "Độ sáng Neon");
            DrawField(visuals, "beatPulseIntensity", "Độ nhấp nháy theo nhịp");
            DrawField(visuals, "targetGlowScale", "Độ lớn viền sáng vật thể");
            DrawField(visuals, "rhythmTileLength", "Độ dài viên gạch bay");
            DrawField(visuals, "judgementLinePulseStrength", "Độ nảy của vạch khi trúng");
            DrawField(visuals, "screenFlashDuration", "Thời gian lóe màn hình");
            DrawField(visuals, "screenFlashIntensity", "Cường độ lóe màn hình");
            DrawField(visuals, "hitParticleCount", "Số hạt VFX khi trúng");
            DrawField(visuals, "audioVolume", "Âm lượng");

            DrawSection("HIỆU NĂNG / OBJECT POOL");
            EditorGUILayout.HelpBox("Chỉ tăng nếu Beatmap dày làm thiếu vật thể hoặc VFX.", MessageType.None);
            DrawField(visuals, "travellerPoolCapacity", "Số vật thể tối đa cùng lúc");
            DrawField(visuals, "rhythmTilePoolCapacity", "Số gạch hiển thị tối đa");
            DrawField(visuals, "hitVfxPoolCapacity", "Số VFX dùng lại");
        }

        private void DrawBeatmap()
        {
            bool isSlashMode = IsSlashMode();
            DrawSection(isSlashMode
                ? "1. KHỐI VUÔNG CHÉM / TARGET — NGOÀI ĐỢT GẠCH"
                : "1. VÒNG ĐẤM / TARGET — NGOÀI ĐỢT GẠCH");
            EditorGUILayout.HelpBox(
                (isSlashMode
                    ? "Slash dùng lại action LeftPunch, RightPunch, BothPunch để tương thích beatmap cũ; runtime sẽ hiển thị kiếm và khối vuông. "
                    : "Chỉ dùng LeftPunch, RightPunch hoặc BothPunch. ") +
                "Event chồng thời gian hiển thị với đợt gạch sẽ không spawn.",
                MessageType.Info);
            punchEventList.DoLayoutList();

            DrawSection("2. CÁNH CỬA / CHƯỚNG NGẠI — NGOÀI ĐỢT GẠCH");
            EditorGUILayout.HelpBox(
                "Chỉ dùng Duck, Jump, DodgeLeft hoặc DodgeRight. Cửa có thể đi cùng target nhưng không chồng với đợt gạch.",
                MessageType.Info);
            obstacleEventList.DoLayoutList();

            DrawSection("3. GẠCH NHỊP — THEO ĐỢT GIỮA MÀN");
            EditorGUILayout.HelpBox(
                "Mỗi lần spawn cần đúng 2 dòng cùng Beat. Runtime giữ hai gạch ngang hàng và chọn ngẫu nhiên lane trái 0/1, lane phải 2/3 để thay đổi khoảng cách.",
                MessageType.Info);
            rhythmTileEventList.DoLayoutList();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("SẮP XẾP THEO BEAT", GUILayout.Height(32f)))
            {
                serializedConfig.ApplyModifiedProperties();
                Undo.RecordObject(config, "Sort Neon Pulse Beatmap");
                config.SortBeatmap();
                EditorUtility.SetDirty(config);
                serializedConfig.Update();
            }

            if (GUILayout.Button("KHÔI PHỤC BEATMAP MẪU", GUILayout.Height(32f)))
            {
                ResetBeatmap();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void BuildBeatmapLists()
        {
            punchEventList = BuildGameplayEventList("punchEvents");
            obstacleEventList = BuildGameplayEventList("obstacleEvents");
            rhythmTileEventList = BuildRhythmTileEventList();
        }

        private ReorderableList BuildGameplayEventList(string propertyName)
        {
            SerializedProperty events = serializedConfig.FindProperty(propertyName);
            ReorderableList list = new ReorderableList(serializedConfig, events, true, true, true, true);
            list.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(new Rect(rect.x + 18f, rect.y, 100f, rect.height), "BEAT");
                EditorGUI.LabelField(new Rect(rect.x + 128f, rect.y, 80f, rect.height), "LANE 0–3");
                EditorGUI.LabelField(new Rect(rect.x + 220f, rect.y, rect.width - 220f, rect.height), "HÀNH ĐỘNG");
            };
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                SerializedProperty element = events.GetArrayElementAtIndex(index);
                rect.y += 2f;
                float lineHeight = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, 100f, lineHeight), element.FindPropertyRelative("Beat"), GUIContent.none);
                EditorGUI.PropertyField(new Rect(rect.x + 110f, rect.y, 80f, lineHeight), element.FindPropertyRelative("Lane"), GUIContent.none);
                EditorGUI.PropertyField(new Rect(rect.x + 200f, rect.y, rect.width - 200f, lineHeight), element.FindPropertyRelative("Action"), GUIContent.none);
            };
            list.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
            return list;
        }

        private ReorderableList BuildRhythmTileEventList()
        {
            SerializedProperty events = serializedConfig.FindProperty("rhythmTileEvents");
            ReorderableList list = new ReorderableList(serializedConfig, events, true, true, true, true);
            list.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(new Rect(rect.x + 18f, rect.y, 100f, rect.height), "BEAT");
                EditorGUI.LabelField(new Rect(rect.x + 128f, rect.y, 80f, rect.height), "LANE 0–3");
                EditorGUI.LabelField(new Rect(rect.x + 220f, rect.y, rect.width - 220f, rect.height), "MÀU GẠCH");
            };
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                SerializedProperty element = events.GetArrayElementAtIndex(index);
                rect.y += 2f;
                float lineHeight = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, 100f, lineHeight), element.FindPropertyRelative("Beat"), GUIContent.none);
                EditorGUI.PropertyField(new Rect(rect.x + 110f, rect.y, 80f, lineHeight), element.FindPropertyRelative("Lane"), GUIContent.none);
                EditorGUI.PropertyField(new Rect(rect.x + 200f, rect.y, rect.width - 200f, lineHeight), element.FindPropertyRelative("Color"), GUIContent.none);
            };
            list.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
            return list;
        }

        private void ApplyDifficultyPreset(float bpm, float travelBeats, float perfect, float great, float good)
        {
            Undo.RecordObject(config, "Apply Gameplay Difficulty");
            SerializedProperty rhythm = serializedConfig.FindProperty("rhythm");
            rhythm.FindPropertyRelative("bpm").floatValue = bpm;
            rhythm.FindPropertyRelative("travelBeats").floatValue = travelBeats;
            rhythm.FindPropertyRelative("perfectWindow").floatValue = perfect;
            rhythm.FindPropertyRelative("greatWindow").floatValue = great;
            rhythm.FindPropertyRelative("goodWindow").floatValue = good;
            serializedConfig.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            ShowNotification(new GUIContent("Đã áp dụng độ khó. Bấm CHƠI THỬ!"));
        }

        private void ResetAllSettings()
        {
            if (!EditorUtility.DisplayDialog("Khôi phục mặc định?", "Toàn bộ thông số và Beatmap hiện tại sẽ trở về bản mẫu.", "Khôi phục", "Hủy"))
            {
                return;
            }

            Undo.RecordObject(config, "Reset Neon Pulse Configuration");
            config.ResetToDefaults();
            EditorUtility.SetDirty(config);
            serializedConfig.Update();
            BuildBeatmapLists();
            SaveConfiguration(false);
        }

        private void ResetBeatmap()
        {
            if (!EditorUtility.DisplayDialog("Khôi phục Beatmap mẫu?", "Chỉ danh sách Beatmap sẽ được thay thế; các thông số khác được giữ nguyên.", "Khôi phục", "Hủy"))
            {
                return;
            }

            serializedConfig.ApplyModifiedProperties();
            Undo.RecordObject(config, "Reset Neon Pulse Beatmap");
            config.ResetBeatmapToDefaults();
            EditorUtility.SetDirty(config);
            serializedConfig.Update();
            BuildBeatmapLists();
        }

        private void TogglePlayMode()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }

            SaveConfiguration(false);
            if (!ValidateConfiguration(false) || !OpenGameplayScene())
            {
                return;
            }

            EditorApplication.isPlaying = true;
        }

        private bool OpenGameplayScene()
        {
            if (EditorApplication.isPlaying)
            {
                ShowNotification(new GUIContent("Hãy dừng Play trước khi đổi Scene."));
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            return true;
        }

        private bool ValidateConfiguration(bool showSuccess)
        {
            serializedConfig.ApplyModifiedProperties();
            bool valid = config.ValidateConfiguration(out string message);
            if (!valid || showSuccess)
            {
                EditorUtility.DisplayDialog(valid ? "Sẵn sàng" : "Cần sửa", message, "Đã hiểu");
            }

            return valid;
        }

        private void SaveConfiguration(bool notify)
        {
            serializedConfig.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            if (notify)
            {
                ShowNotification(new GUIContent("Đã lưu Gameplay Configuration"));
            }
        }

        private void LoadOrCreateConfiguration()
        {
            config = AssetDatabase.LoadAssetAtPath<NeonPulseGameConfig>(ConfigPath);
            if (config == null)
            {
                if (!AssetDatabase.IsValidFolder(ConfigDirectory))
                {
                    AssetDatabase.CreateFolder("Assets/NeonPulse", "Resources");
                }

                config = CreateInstance<NeonPulseGameConfig>();
                config.ResetToDefaults();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }

            serializedConfig = new SerializedObject(config);
            BuildBeatmapLists();
        }

        private void EnsureStyles()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 24,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.1f, 0.95f, 0.9f) }
                };
            }

            if (sectionStyle == null)
            {
                sectionStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13,
                    normal = { textColor = new Color(1f, 0.65f, 0.12f) }
                };
            }
        }

        private void DrawSection(string title)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, sectionStyle);
            EditorGUILayout.Space(2f);
        }

        private bool IsSlashMode()
        {
            SerializedProperty property = serializedConfig.FindProperty("gameplayMode");
            return property != null && property.enumValueIndex == (int)CombatGameplayMode.Slash;
        }

        private static void DrawField(SerializedProperty parent, string propertyName, string label, string tooltip = null)
        {
            SerializedProperty property = parent.FindPropertyRelative(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
            }
        }
    }

    [CustomEditor(typeof(NeonPulseGameConfig))]
    public sealed class NeonPulseGameConfigInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Dùng Gameplay Control Center để chỉnh dễ dàng và có hướng dẫn tiếng Việt.", MessageType.Info);
            if (GUILayout.Button("MỞ GAMEPLAY CONTROL CENTER", GUILayout.Height(36f)))
            {
                NeonPulseGameplayControlCenter.OpenWindow();
            }

            EditorGUILayout.Space(6f);
            DrawDefaultInspector();
        }
    }
}

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
        private const string LevelDirectory = "Assets/NeonPulse/Levels";
        private const string GameplayScenePath = "Assets/NeonPulse/Scenes/NeonPulseGameplay.unity";

        private static readonly string[] Tabs =
        {
            "LEVEL & PHASE", "NHỊP & ĐIỂM", "ĐIỀU KHIỂN", "HÌNH ẢNH & VFX"
        };

        private NeonPulseGameConfig config;
        private SerializedObject serializedConfig;
        private SerializedObject serializedLevel;
        private ReorderableList phaseList;
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
            SyncLevelEditor();
            DrawHeader();
            selectedTab = GUILayout.Toolbar(selectedTab, Tabs, GUILayout.Height(30f));

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.Space(10f);
            switch (selectedTab)
            {
                case 0: DrawLevelAndPhases(); break;
                case 1: DrawRhythm(); DrawScoring(); break;
                case 2: DrawControls(); break;
                case 3: DrawCameraAndVisuals(); break;
            }

            EditorGUILayout.Space(16f);
            EditorGUILayout.EndScrollView();

            if (serializedConfig.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(config);
            }

            if (serializedLevel != null && serializedLevel.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(serializedLevel.targetObject);
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

        private void DrawLevelAndPhases()
        {
            DrawSection("LEVEL CUSTOM ĐỂ CHƠI THỬ");
            SerializedProperty levelProperty = serializedConfig.FindProperty("levelDefinition");
            EditorGUILayout.PropertyField(levelProperty, new GUIContent("Level Definition"));
            if (GUILayout.Button("TẠO LEVEL MẪU MỚI", GUILayout.Height(34f)))
            {
                CreateLevelAsset();
                return;
            }

            if (serializedLevel == null)
            {
                EditorGUILayout.HelpBox("Kéo một file NeonPulseLevel vào ô trên hoặc tạo Level mẫu. Mỗi phase chỉnh riêng tốc độ bay, nhịp spawn và số object mỗi wave.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "Khoảng spawn là giá trị mong muốn và được tự quy đổi sang số beat nguyên gần nhất theo BPM. Với action giữ tư thế, hệ thống tự tăng số beat nếu cần để tránh chồng input.",
                MessageType.Info);
            serializedLevel.UpdateIfRequiredOrScript();
            EditorGUILayout.PropertyField(serializedLevel.FindProperty("levelName"), new GUIContent("Tên Level"));
            EditorGUILayout.PropertyField(serializedLevel.FindProperty("phaseTransitionRestSeconds"), new GUIContent("Nghỉ khi chuyển phase (giây)"));

            DrawSection("DỮ LIỆU GEN NHẠC BẰNG AI");
            SerializedProperty musicGeneration = serializedLevel.FindProperty("musicGeneration");
            EditorGUILayout.PropertyField(musicGeneration.FindPropertyRelative("beatsPerBar"), new GUIContent("Số beat mỗi bar"));
            EditorGUILayout.PropertyField(musicGeneration.FindPropertyRelative("genre"), new GUIContent("Thể loại nhạc"));
            EditorGUILayout.PropertyField(musicGeneration.FindPropertyRelative("mood"), new GUIContent("Mood"));
            EditorGUILayout.PropertyField(musicGeneration.FindPropertyRelative("musicalKey"), new GUIContent("Tông nhạc dùng chung"));
            EditorGUILayout.PropertyField(musicGeneration.FindPropertyRelative("maximumSegmentDurationSeconds"),
                new GUIContent("Giới hạn generator (giây)", "Chỉ dùng để cảnh báo khi một phase dài hơn giới hạn của công cụ gen nhạc."));
            EditorGUILayout.PropertyField(musicGeneration.FindPropertyRelative("additionalPrompt"), new GUIContent("Yêu cầu thêm"));
            EditorGUILayout.PropertyField(musicGeneration.FindPropertyRelative("instrumentalOnly"), new GUIContent("Chỉ nhạc không lời"));
            EditorGUILayout.HelpBox(
                "Exporter tạo một JSON riêng cho mỗi phase. Mỗi file có duration, BPM, khoảng action theo beat, thời điểm spawn/hit và prompt gen một clip độc lập.",
                MessageType.Info);
            if (GUILayout.Button("XUẤT DATA GEN NHẠC THEO TỪNG PHASE", GUILayout.Height(34f)))
            {
                serializedConfig.ApplyModifiedProperties();
                serializedLevel.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(serializedLevel.targetObject);
                NeonPulseMusicDataExporter.Export(serializedLevel.targetObject as NeonPulseLevelDefinition, config);
            }

            EditorGUILayout.Space(6f);
            phaseList.DoLayoutList();

            NeonPulseLevelDefinition level = serializedLevel.targetObject as NeonPulseLevelDefinition;
            if (level != null && GUILayout.Button("KIỂM TRA LEVEL", GUILayout.Height(30f)))
            {
                if (level.ValidateDefinition(out string message))
                {
                    EditorUtility.DisplayDialog("Level hợp lệ", message, "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Level cần chỉnh", message, "OK");
                }
            }
        }

        private void DrawRhythm()
        {
            DrawSection("TỐC ĐỘ BÀI CHƠI");
            SerializedProperty rhythm = serializedConfig.FindProperty("rhythm");
            DrawField(rhythm, "bpm", "Tốc độ nhạc (BPM)", "Số lớn hơn = game nhanh hơn.");
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
            DrawSection("PHÍM ĐẤM / CHÉM");
            SerializedProperty input = serializedConfig.FindProperty("input");
            DrawField(input, "leftPunch", "Đấm / chém trái");
            DrawField(input, "leftPunchAlternative", "Phím phụ trái");
            DrawField(input, "rightPunch", "Đấm / chém phải");
            DrawField(input, "rightPunchAlternative", "Phím phụ phải");
            DrawField(input, "bothPunch", "Đấm / chém hai tay");
            DrawField(input, "overheadClap", "Vỗ tay trên đầu");

            DrawSection("PHÍM NÉ — PHẢI GIỮ ĐẾN KHI VẬT CẢN QUA");
            DrawField(input, "duck", "Cúi");
            DrawField(input, "duckAlternative", "Phím phụ cúi");
            DrawField(input, "jump", "Nhảy");
            DrawField(input, "jumpAlternative", "Phím phụ nhảy");
            DrawField(input, "dodgeLeft", "Né trái");
            DrawField(input, "dodgeRight", "Né phải");
            DrawField(input, "leftLegDrawUp", "Co chân trái");
            DrawField(input, "rightLegDrawUp", "Co chân phải");

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
            DrawSection("CẢM GIÁC DI CHUYỂN");
            SerializedProperty cameraFeel = serializedConfig.FindProperty("cameraFeel");
            DrawField(cameraFeel, "standingHeight", "Độ cao đứng của player", "Tăng để nâng camera và toàn bộ góc nhìn player.");
            DrawField(cameraFeel, "distanceToJudgementLine", "Khoảng cách camera tới vạch", "Giảm để đưa camera lại gần vạch hơn.");
            DrawField(cameraFeel, "poseSmoothing", "Độ mượt chuyển động");
            DrawField(cameraFeel, "dodgeDistance", "Khoảng cách né trái / phải");
            DrawField(cameraFeel, "duckDistance", "Khoảng cách cúi");
            DrawField(cameraFeel, "jumpDistance", "Khoảng cách nhảy");
            DrawField(cameraFeel, "punchDistance", "Tầm hành động tay");
            DrawField(cameraFeel, "punchDuration", "Thời gian hành động tay");

            DrawSection("RUNG CAMERA");
            DrawField(cameraFeel, "punchShakeAmplitude", "Độ rung khi đấm / chém");
            DrawField(cameraFeel, "punchShakeDuration", "Thời gian rung khi đấm / chém");
            DrawField(cameraFeel, "bothPunchShakeAmplitude", "Độ rung khi dùng hai tay");
            DrawField(cameraFeel, "bothPunchShakeDuration", "Thời gian rung hai tay");
            DrawField(cameraFeel, "rhythmTileShakeAmplitude", "Độ rung khi gạch chạm vạch");
            DrawField(cameraFeel, "rhythmTileShakeDuration", "Thời gian rung khi gạch chạm vạch");
            DrawField(cameraFeel, "failShakeAmplitude", "Độ rung khi sai nhịp");
            DrawField(cameraFeel, "failShakeDuration", "Thời gian rung khi sai");

            DrawSection("MÀU, SHADER & VFX");
            SerializedProperty visuals = serializedConfig.FindProperty("visuals");
            DrawField(visuals, "hudFont", "Font chữ HUD");
            DrawField(visuals, "backgroundTexture", "Ảnh nền gameplay");
            DrawField(visuals, "showPunchHands", "Hiện nắm đấm góc nhìn thứ nhất");
            DrawField(visuals, "punchHitVfxPrefab", "VFX khi đấm vỡ khối");
            DrawField(visuals, "overheadClapHitVfxPrefab", "VFX khi vỗ tay vỡ khối");
            DrawField(visuals, "showSlashWeapons", "Hiện kiếm góc nhìn thứ nhất");
            DrawField(visuals, "slashHitVfxPrefab", "VFX khi chém vỡ khối");
            DrawField(visuals, "rhythmTileHitVfxPrefab", "VFX khi dậm chân");
            DrawField(visuals, "footprintIconTexture", "Icon bàn chân trên gạch");
            DrawField(visuals, "punchIconTexture", "Icon trên khối đấm");
            DrawField(visuals, "overheadClapTargetIcon", "Icon trên khối vỗ tay");
            DrawField(visuals, "swordIconTexture", "Icon trên khối chém");
            DrawField(visuals, "leftPunchActionIcon", "Icon động tác đấm trái");
            DrawField(visuals, "rightPunchActionIcon", "Icon động tác đấm phải");
            DrawField(visuals, "leftSlashActionIcon", "Icon động tác chém trái");
            DrawField(visuals, "rightSlashActionIcon", "Icon động tác chém phải");
            DrawField(visuals, "leftDodgeActionIcon", "Icon né tường trái");
            DrawField(visuals, "rightDodgeActionIcon", "Icon né tường phải");
            DrawField(visuals, "jumpActionIcon", "Icon động tác nhảy");
            DrawField(visuals, "duckActionIcon", "Icon động tác cúi");
            DrawField(visuals, "overheadClapActionIcon", "Icon timing vỗ tay trên đầu");
            DrawField(visuals, "leftLegDrawUpActionIcon", "Icon timing co chân trái");
            DrawField(visuals, "rightLegDrawUpActionIcon", "Icon timing co chân phải");
            DrawField(visuals, "legDrawUpTileIcon", "Icon co chân trên viên gạch");
            DrawField(visuals, "cyan", "Màu tay trái");
            DrawField(visuals, "magenta", "Màu tay phải");
            DrawField(visuals, "purple", "Màu đường hầm");
            DrawField(visuals, "yellow", "Màu Perfect / vạch đánh");
            DrawField(visuals, "obstacle", "Màu vật cản");
            DrawField(visuals, "neonIntensity", "Độ sáng Neon");
            DrawField(visuals, "beatPulseIntensity", "Độ nhấp nháy theo nhịp");
            DrawField(visuals, "targetGlowScale", "Độ lớn viền sáng vật thể");
            DrawField(visuals, "overheadClapTargetHeight", "Độ cao khối vỗ tay");
            DrawField(visuals, "rhythmTileLength", "Độ dài viên gạch bay");
            DrawField(visuals, "judgementLinePulseStrength", "Độ đậm highlight ô tại vạch");
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

        private void SyncLevelEditor()
        {
            SerializedProperty levelProperty = serializedConfig.FindProperty("levelDefinition");
            NeonPulseLevelDefinition selectedLevel = levelProperty != null
                ? levelProperty.objectReferenceValue as NeonPulseLevelDefinition
                : null;
            if (selectedLevel == null)
            {
                serializedLevel = null;
                return;
            }

            if (serializedLevel == null || serializedLevel.targetObject != selectedLevel)
            {
                serializedLevel = new SerializedObject(selectedLevel);
                BuildPhaseList();
            }
        }

        private void BuildPhaseList()
        {
            SerializedProperty phases = serializedLevel.FindProperty("phases");
            phaseList = new ReorderableList(serializedLevel, phases, true, true, true, true)
            {
                elementHeight = 164f
            };
            phaseList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "PHASE — action quyết định tên và gameplay");
            phaseList.drawElementCallback = (rect, index, active, focused) =>
            {
                SerializedProperty phase = phases.GetArrayElementAtIndex(index);
                SerializedProperty action = phase.FindPropertyRelative("action");
                SerializedProperty duration = phase.FindPropertyRelative("durationSeconds");
                SerializedProperty flySpeed = phase.FindPropertyRelative("flySpeed");
                SerializedProperty spawnInterval = phase.FindPropertyRelative("spawnIntervalSeconds");
                SerializedProperty objectsPerWave = phase.FindPropertyRelative("objectsPerWave");
                SerializedProperty holdDuration = phase.FindPropertyRelative("holdDurationSeconds");
                SerializedProperty musicClip = phase.FindPropertyRelative("musicClip");
                rect.y += 3f;
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), action,
                    new GUIContent("Action"));

                LevelPhaseAction phaseAction = (LevelPhaseAction)action.enumValueIndex;
                GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.2f, 0.95f, 0.85f) } };
                EditorGUI.LabelField(new Rect(rect.x, rect.y + 23f, rect.width, EditorGUIUtility.singleLineHeight),
                    NeonPulseLevelPhase.GetDisplayName(phaseAction), nameStyle);
                EditorGUI.PropertyField(new Rect(rect.x, rect.y + 45f, rect.width * 0.49f, EditorGUIUtility.singleLineHeight),
                    duration, new GUIContent(GetDurationLabel(phaseAction)));
                EditorGUI.PropertyField(new Rect(rect.x + rect.width * 0.51f, rect.y + 45f, rect.width * 0.49f, EditorGUIUtility.singleLineHeight),
                    flySpeed, new GUIContent(GetSpeedLabel(phaseAction)));
                EditorGUI.PropertyField(new Rect(rect.x, rect.y + 67f, rect.width * 0.49f, EditorGUIUtility.singleLineHeight),
                    spawnInterval, new GUIContent(
                        "Khoảng spawn mong muốn (giây)",
                        "Runtime và exporter sẽ quy đổi sang số beat nguyên gần nhất."));

                using (new EditorGUI.DisabledScope(!SupportsMultipleObjects(phaseAction)))
                {
                    EditorGUI.PropertyField(new Rect(rect.x + rect.width * 0.51f, rect.y + 67f, rect.width * 0.49f, EditorGUIUtility.singleLineHeight),
                        objectsPerWave, new GUIContent("Object mỗi wave"));
                }

                if (phaseAction == LevelPhaseAction.LegDrawUp)
                {
                    EditorGUI.PropertyField(new Rect(rect.x, rect.y + 91f, rect.width, EditorGUIUtility.singleLineHeight),
                        holdDuration, new GUIContent("Thời gian co chân tối đa n (random 1→n giây)"));
                }

                float musicY = phaseAction == LevelPhaseAction.LegDrawUp ? 115f : 91f;
                EditorGUI.PropertyField(new Rect(rect.x, rect.y + musicY, rect.width, EditorGUIUtility.singleLineHeight),
                    musicClip, new GUIContent("Nhạc phase"));
                EditorGUI.LabelField(new Rect(rect.x, rect.y + musicY + 24f, rect.width, EditorGUIUtility.singleLineHeight),
                    GetPhaseDescription(phaseAction), EditorStyles.miniLabel);
            };
        }

        private static string GetDurationLabel(LevelPhaseAction action)
        {
            switch (action)
            {
                case LevelPhaseAction.RhythmTiles: return "Thời gian dậm chân";
                case LevelPhaseAction.PunchObjects: return "Thời gian đấm";
                case LevelPhaseAction.SlashObjects: return "Thời gian chém";
                case LevelPhaseAction.DodgeWalls: return "Thời gian né";
                case LevelPhaseAction.OverheadClap: return "Thời gian vỗ tay";
                case LevelPhaseAction.LegDrawUp: return "Thời gian phase co chân";
                default: return "Thời gian tổng hợp";
            }
        }

        private static string GetSpeedLabel(LevelPhaseAction action)
        {
            switch (action)
            {
                case LevelPhaseAction.RhythmTiles: return "Tốc độ gạch";
                case LevelPhaseAction.PunchObjects: return "Tốc độ vật thể";
                case LevelPhaseAction.SlashObjects: return "Tốc độ vật thể";
                case LevelPhaseAction.DodgeWalls: return "Tốc độ tường";
                case LevelPhaseAction.OverheadClap: return "Tốc độ vật thể";
                case LevelPhaseAction.LegDrawUp: return "Tốc độ gạch co chân";
                default: return "Tốc độ tổng hợp";
            }
        }

        private static string GetPhaseDescription(LevelPhaseAction action)
        {
            switch (action)
            {
                case LevelPhaseAction.RhythmTiles: return "Hai tay được ẩn; gạch và lane được random.";
                case LevelPhaseAction.PunchObjects: return "Random mục tiêu, tay đấm và lane.";
                case LevelPhaseAction.SlashObjects: return "Random mục tiêu, hướng chém và lane.";
                case LevelPhaseAction.DodgeWalls: return "Random hướng né trái, phải, cúi hoặc nhảy.";
                case LevelPhaseAction.OverheadClap: return "Khối vỗ tay random lane trái/phải, cao vừa tầm hai tay trên đầu.";
                case LevelPhaseAction.LegDrawUp: return "Random chân trái/phải; chiều dài gạch = tốc độ × thời gian co chân.";
                default: return "Mỗi wave random một action cụ thể đang có trong Level.";
            }
        }

        private static bool SupportsMultipleObjects(LevelPhaseAction action)
        {
            return action == LevelPhaseAction.PunchObjects ||
                   action == LevelPhaseAction.SlashObjects ||
                   action == LevelPhaseAction.RandomMixed;
        }

        private void CreateLevelAsset()
        {
            if (!AssetDatabase.IsValidFolder(LevelDirectory))
            {
                AssetDatabase.CreateFolder("Assets/NeonPulse", "Levels");
            }

            NeonPulseLevelDefinition level = CreateInstance<NeonPulseLevelDefinition>();
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(LevelDirectory + "/NeonPulseLevel.asset");
            AssetDatabase.CreateAsset(level, assetPath);
            SerializedProperty levelProperty = serializedConfig.FindProperty("levelDefinition");
            levelProperty.objectReferenceValue = level;
            serializedConfig.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            serializedLevel = new SerializedObject(level);
            BuildPhaseList();
            Selection.activeObject = level;
            EditorGUIUtility.PingObject(level);
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

    [CustomEditor(typeof(NeonPulseLevelDefinition))]
    public sealed class NeonPulseLevelDefinitionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("levelName"), new GUIContent("Tên Level"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("phaseTransitionRestSeconds"),
                new GUIContent("Nghỉ khi chuyển phase (giây)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("musicGeneration"),
                new GUIContent("Cấu hình AI Gen nhạc"), true);
            EditorGUILayout.Space(8f);

            SerializedProperty phases = serializedObject.FindProperty("phases");
            for (int index = 0; index < phases.arraySize; index++)
            {
                SerializedProperty phase = phases.GetArrayElementAtIndex(index);
                SerializedProperty action = phase.FindPropertyRelative("action");
                SerializedProperty duration = phase.FindPropertyRelative("durationSeconds");
                SerializedProperty speed = phase.FindPropertyRelative("flySpeed");
                SerializedProperty spawnInterval = phase.FindPropertyRelative("spawnIntervalSeconds");
                SerializedProperty objectsPerWave = phase.FindPropertyRelative("objectsPerWave");
                SerializedProperty holdDuration = phase.FindPropertyRelative("holdDurationSeconds");
                SerializedProperty musicClip = phase.FindPropertyRelative("musicClip");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(action, new GUIContent("Action"));
                LevelPhaseAction phaseAction = (LevelPhaseAction)action.enumValueIndex;
                EditorGUILayout.LabelField(NeonPulseLevelPhase.GetDisplayName(phaseAction), EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(duration, new GUIContent(GetDurationLabel(phaseAction)));
                EditorGUILayout.PropertyField(speed, new GUIContent(GetSpeedLabel(phaseAction)));
                EditorGUILayout.PropertyField(spawnInterval, new GUIContent(
                    "Khoảng spawn mong muốn (giây)",
                    "Runtime và exporter sẽ quy đổi sang số beat nguyên gần nhất."));
                using (new EditorGUI.DisabledScope(!SupportsMultipleObjects(phaseAction)))
                {
                    EditorGUILayout.PropertyField(objectsPerWave, new GUIContent("Object mỗi wave"));
                }
                if (phaseAction == LevelPhaseAction.LegDrawUp)
                {
                    EditorGUILayout.PropertyField(holdDuration, new GUIContent("Thời gian co chân tối đa n (random 1→n giây)"));
                }
                EditorGUILayout.PropertyField(musicClip, new GUIContent("Nhạc phase"));
                if (GUILayout.Button("XÓA PHASE " + (index + 1)))
                {
                    phases.DeleteArrayElementAtIndex(index);
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ THÊM PHASE", GUILayout.Height(28f)))
            {
                phases.InsertArrayElementAtIndex(phases.arraySize);
                SerializedProperty newPhase = phases.GetArrayElementAtIndex(phases.arraySize - 1);
                newPhase.FindPropertyRelative("action").enumValueIndex = (int)LevelPhaseAction.PunchObjects;
                newPhase.FindPropertyRelative("durationSeconds").floatValue = 12f;
                newPhase.FindPropertyRelative("flySpeed").floatValue = 12f;
                newPhase.FindPropertyRelative("spawnIntervalSeconds").floatValue = 1f;
                newPhase.FindPropertyRelative("objectsPerWave").intValue = 1;
                newPhase.FindPropertyRelative("holdDurationSeconds").floatValue = 1.2f;
                newPhase.FindPropertyRelative("musicClip").objectReferenceValue = null;
            }

            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space(8f);
            if (GUILayout.Button("MỞ GAMEPLAY CONTROL CENTER"))
            {
                NeonPulseGameplayControlCenter.OpenWindow();
            }
        }

        private static string GetDurationLabel(LevelPhaseAction action)
        {
            switch (action)
            {
                case LevelPhaseAction.RhythmTiles: return "Thời gian dậm chân";
                case LevelPhaseAction.PunchObjects: return "Thời gian đấm";
                case LevelPhaseAction.SlashObjects: return "Thời gian chém";
                case LevelPhaseAction.OverheadClap: return "Thời gian vỗ tay";
                case LevelPhaseAction.LegDrawUp: return "Thời gian phase co chân";
                default: return "Thời gian né";
            }
        }

        private static string GetSpeedLabel(LevelPhaseAction action)
        {
            switch (action)
            {
                case LevelPhaseAction.RhythmTiles: return "Tốc độ gạch";
                case LevelPhaseAction.DodgeWalls: return "Tốc độ tường";
                case LevelPhaseAction.LegDrawUp: return "Tốc độ gạch co chân";
                default: return "Tốc độ vật thể";
            }
        }

        private static bool SupportsMultipleObjects(LevelPhaseAction action)
        {
            return action == LevelPhaseAction.PunchObjects || action == LevelPhaseAction.SlashObjects;
        }
    }
}

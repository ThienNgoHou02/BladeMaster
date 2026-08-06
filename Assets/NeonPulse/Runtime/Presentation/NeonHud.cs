using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonPulse
{
    /// <summary>Runtime-built TextMeshPro HUD with score, combo, feedback, controls and results.</summary>
    public sealed class NeonHud : MonoBehaviour
    {
        private const float ProgressBarWidth = 720f;
        private const float LevelProgressBarHeight = 13f;

        private TMP_FontAsset fontAsset;
        private Font sourceFont;
        private bool ownsFontAsset;
        private TextMeshProUGUI scoreText;
        private TextMeshProUGUI comboText;
        private TextMeshProUGUI feedbackText;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI levelProgressText;
        private TextMeshProUGUI phaseProgressText;
        private Image levelProgressFill;
        private GameObject nextActionPanel;
        private TextMeshProUGUI nextActionText;
        private Image nextActionFill;
        private GameObject countdownPanel;
        private TextMeshProUGUI countdownText;
        private GameObject resultPanel;
        private TextMeshProUGUI resultText;
        private float feedbackTimer;
        private float comboPulseTimer;
        private Color comboPulseColor = Color.white;
        private int lastScoreValue;
        private bool nextActionReady;
        private GameplayAction lastNextAction;
        private bool hasNextAction;
        private int lastCountdownValue = -1;
        private RhythmSettings timing;
        private InputBindingSettings bindings;
        private string restartLabel;
        private bool showGuidance;
        private bool isSlashMode;
        private int displayedPhaseIndex = -1;
        private int displayedPhaseCount = -1;
        private string displayedPhaseName;

        /// <summary>Creates all HUD objects and runtime font resources.</summary>
        public void Build(RuntimeMaterialLibrary materials, NeonPulseGameConfig config)
        {
            timing = config.Rhythm;
            bindings = config.Input;
            restartLabel = GetKeyLabel(bindings.Restart);
            showGuidance = !config.AutoPlay;
            isSlashMode = config.GameplayMode == CombatGameplayMode.Slash;
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            CreateRuntimeFont();

            scoreText = CreateText("Score", transform, "ĐIỂM 000000\nTRÚNG 0/0", 32f, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(scoreText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -48f), new Vector2(560f, 115f));

            comboText = CreateText("Combo", transform, "0\nCOMBO\nMAX 0", 52f, TextAlignmentOptions.TopRight, Color.white);
            comboText.lineSpacing = -18f;
            SetRect(comboText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-210f, -52f), new Vector2(380f, 190f));

            feedbackText = CreateText("Feedback", transform, string.Empty, 58f, TextAlignmentOptions.Center, materials.CyanColor);
            feedbackText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            feedbackText.lineSpacing = -12f;
            SetRect(feedbackText.rectTransform, new Vector2(0.5f, 0.76f), new Vector2(0.5f, 0.76f), Vector2.zero, new Vector2(950f, 155f));

            if (showGuidance)
            {
                statusText = CreateText("Controls", transform,
                    BuildControlGuide(),
                    25f, TextAlignmentOptions.Center, new Color(0.8f, 0.85f, 1f, 0.9f));
                SetRect(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1450f, 92f));
            }

            CreateNextActionPanel(materials);
            CreateLevelProgressPanel(materials);
            CreateCountdownPanel(materials);
            CreateResultPanel(materials);
        }

        /// <summary>Updates the score and combo labels only when score state changes.</summary>
        public void SetScore(ScoreSnapshot snapshot)
        {
            if (scoreText == null || comboText == null)
            {
                return;
            }

            int hitCount = snapshot.Perfect + snapshot.Great + snapshot.Good;
            int totalCount = hitCount + snapshot.Miss;
            float weightedHits = snapshot.Perfect + snapshot.Great * 0.75f + snapshot.Good * 0.5f;
            float accuracy = totalCount > 0 ? weightedHits / totalCount * 100f : 100f;
            scoreText.SetText("ĐIỂM {0:000000}\nTRÚNG {1}/{2}  •  {3:0}%", snapshot.Score, hitCount, totalCount, accuracy);
            comboText.SetText("{0}\nCOMBO\nMAX {1}", snapshot.Combo, snapshot.MaxCombo);
            lastScoreValue = snapshot.Score;
        }

        /// <summary>Updates level and phase progress without creating formatted strings each frame.</summary>
        public void SetLevelProgress(int phaseIndex, int phaseCount, string phaseName, float levelProgress)
        {
            if (levelProgressText == null || phaseProgressText == null || levelProgressFill == null)
            {
                return;
            }

            levelProgressText.SetText("LEVEL  {0:0}%", levelProgress * 100f);
            bool phaseChanged = displayedPhaseIndex != phaseIndex || displayedPhaseCount != phaseCount || displayedPhaseName != phaseName;
            if (phaseChanged)
            {
                displayedPhaseIndex = phaseIndex;
                displayedPhaseCount = phaseCount;
                displayedPhaseName = phaseName;
                phaseProgressText.text = "PHASE " + phaseIndex + "/" + phaseCount + "  •  " + phaseName;
            }

            SetProgressBarWidth(levelProgressFill, levelProgress, LevelProgressBarHeight, 31f);
        }

        /// <summary>Switches labels between punch and slash phases while keeping a single HUD instance.</summary>
        public void SetActionMode(CombatGameplayMode mode)
        {
            bool useSlash = mode == CombatGameplayMode.Slash;
            if (isSlashMode == useSlash)
            {
                return;
            }

            isSlashMode = useSlash;
            if (statusText != null)
            {
                statusText.text = BuildControlGuide();
            }
        }

        /// <summary>Shows short accuracy feedback using cached UI components.</summary>
        public void ShowJudgement(AccuracyGrade grade, GameplayAction action, ScoreSnapshot snapshot, RuntimeMaterialLibrary materials)
        {
            if (feedbackText == null || materials == null)
            {
                return;
            }

            int gainedScore = Mathf.Max(0, snapshot.Score - lastScoreValue);
            switch (grade)
            {
                case AccuracyGrade.Perfect:
                    feedbackText.SetText("CHÍNH XÁC  +{0}\nCOMBO {1}", gainedScore, snapshot.Combo);
                    feedbackText.color = materials.YellowColor;
                    break;
                case AccuracyGrade.Great:
                    feedbackText.SetText("TRÚNG  +{0}\nCOMBO {1}", gainedScore, snapshot.Combo);
                    feedbackText.color = materials.CyanColor;
                    break;
                case AccuracyGrade.Good:
                    feedbackText.SetText("TRÚNG  +{0}\nCOMBO {1}", gainedScore, snapshot.Combo);
                    feedbackText.color = Color.white;
                    break;
                default:
                    feedbackText.text = "TRƯỢT NHỊP\nCOMBO BỊ NGẮT";
                    feedbackText.color = new Color(1f, 0.12f, 0.22f, 1f);
                    break;
            }

            comboPulseColor = feedbackText.color;
            comboPulseTimer = 0.42f;
            feedbackText.alpha = 1f;
            feedbackText.rectTransform.localScale = Vector3.one * 1.18f;
            feedbackTimer = 0.65f;
        }

        /// <summary>Explains that an input was recognized but did not match the current beat.</summary>
        public void ShowMistimedInput(RuntimeMaterialLibrary materials)
        {
            if (feedbackText == null || materials == null)
            {
                return;
            }

            feedbackText.text = "CHƯA ĐÚNG NHỊP";
            feedbackText.color = new Color(1f, 0.35f, 0.2f, 1f);
            feedbackText.alpha = 1f;
            feedbackText.rectTransform.localScale = Vector3.one;
            feedbackTimer = 0.42f;
        }

        /// <summary>Shows the closest required action and fills toward its hit time.</summary>
        public void SetUpcomingAction(GameplayAction action, float secondsUntilHit, float approachDuration, RuntimeMaterialLibrary materials)
        {
            if (!showGuidance)
            {
                HideUpcomingAction();
                return;
            }

            if (nextActionPanel == null || nextActionText == null || nextActionFill == null || materials == null)
            {
                return;
            }

            bool holdAction = RequiresHold(action);
            bool ready = holdAction
                ? secondsUntilHit <= timing.HoldWindowLead && secondsUntilHit >= -timing.HoldWindowTrail - 0.02f
                : Mathf.Abs(secondsUntilHit) <= timing.GoodWindow;
            if (!hasNextAction || lastNextAction != action || nextActionReady != ready)
            {
                nextActionText.text = GetActionPrompt(action, ready);
                nextActionText.color = GetActionColor(action, materials);
                lastNextAction = action;
                nextActionReady = ready;
                hasNextAction = true;
            }

            nextActionFill.color = ready ? materials.YellowColor : GetActionColor(action, materials);
            nextActionFill.fillAmount = 1f - Mathf.Clamp01(secondsUntilHit / Mathf.Max(0.01f, approachDuration));
            nextActionPanel.SetActive(true);
        }

        /// <summary>Confirms that an obstacle key is active and reminds the player not to release it.</summary>
        public void ShowHoldConfirmed(GameplayAction action, RuntimeMaterialLibrary materials)
        {
            if (!showGuidance)
            {
                return;
            }

            if (nextActionText == null || materials == null)
            {
                return;
            }

            switch (action)
            {
                case GameplayAction.Duck: nextActionText.text = "ĐANG GIỮ " + GetKeyLabel(bindings.Duck) + " — ĐỪNG THẢ"; break;
                case GameplayAction.Jump: nextActionText.text = "ĐANG GIỮ " + GetKeyLabel(bindings.Jump) + " — ĐỪNG THẢ"; break;
                case GameplayAction.DodgeLeft: nextActionText.text = "ĐANG GIỮ " + GetKeyLabel(bindings.DodgeLeft) + " — ĐỪNG THẢ"; break;
                default: nextActionText.text = "ĐANG GIỮ " + GetKeyLabel(bindings.DodgeRight) + " — ĐỪNG THẢ"; break;
            }

            nextActionText.color = materials.YellowColor;
            nextActionReady = true;
            lastNextAction = action;
            hasNextAction = true;
        }

        /// <summary>Hides the next-action cue when no active chart event remains.</summary>
        public void HideUpcomingAction()
        {
            hasNextAction = false;
            if (nextActionPanel != null)
            {
                nextActionPanel.SetActive(false);
            }
        }

        /// <summary>Displays a three-second onboarding countdown before the DSP clock starts.</summary>
        public void SetCountdown(float secondsUntilStart)
        {
            if (countdownPanel == null || countdownText == null)
            {
                return;
            }

            if (secondsUntilStart <= 0f)
            {
                countdownPanel.SetActive(false);
                lastCountdownValue = -1;
                return;
            }

            countdownPanel.SetActive(true);
            int value = Mathf.Max(1, Mathf.CeilToInt(secondsUntilStart));
            if (value != lastCountdownValue)
            {
                countdownText.SetText("{0}", value);
                lastCountdownValue = value;
            }
        }

        /// <summary>Updates song progress without string creation.</summary>
        /// <summary>Shows the final run summary and restart hint.</summary>
        public void ShowResults(ScoreSnapshot snapshot)
        {
            if (resultPanel == null || resultText == null)
            {
                return;
            }

            resultPanel.SetActive(true);
            resultText.SetText(
                "HOÀN THÀNH BÀI TẬP\n\nĐIỂM  {0:000000}\nCOMBO CAO NHẤT  {1}\n\nCHÍNH XÁC  {2}     RẤT TỐT  {3}\nTỐT  {4}     TRƯỢT  {5}\n\nNHẤN " + restartLabel + " ĐỂ CHƠI LẠI",
                snapshot.Score,
                snapshot.MaxCombo,
                snapshot.Perfect,
                snapshot.Great,
                snapshot.Good,
                snapshot.Miss);
        }

        /// <summary>Hides the results and resets run-only visuals.</summary>
        public void ResetRun()
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }

            if (feedbackText != null)
            {
                feedbackText.text = "CHUẨN BỊ";
                feedbackText.color = Color.white;
                feedbackText.alpha = 1f;
                feedbackTimer = 1.2f;
            }

            HideUpcomingAction();

            if (levelProgressFill != null)
            {
                SetProgressBarWidth(levelProgressFill, 0f, LevelProgressBarHeight, 31f);
            }

        }

        private void Update()
        {
            if (feedbackTimer > 0f && feedbackText != null)
            {
                feedbackTimer -= Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(feedbackTimer / 0.65f);
                feedbackText.alpha = normalized;
                feedbackText.rectTransform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.18f, normalized);
            }

            if (hasNextAction && nextActionPanel != null)
            {
                float pulse = nextActionReady ? 1f + Mathf.Sin(Time.unscaledTime * 18f) * 0.045f : 1f;
                nextActionPanel.transform.localScale = Vector3.one * pulse;
            }

            if (comboPulseTimer > 0f && comboText != null)
            {
                comboPulseTimer -= Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(comboPulseTimer / 0.42f);
                comboText.rectTransform.localScale = Vector3.one * (1f + normalized * 0.16f);
                comboText.color = Color.Lerp(Color.white, comboPulseColor, normalized);
            }
            else if (comboText != null)
            {
                comboText.rectTransform.localScale = Vector3.one;
                comboText.color = Color.white;
            }
        }

        private void OnDestroy()
        {
            if (ownsFontAsset && fontAsset != null)
            {
                Destroy(fontAsset);
            }

            if (sourceFont != null)
            {
                Destroy(sourceFont);
            }
        }

        private void CreateRuntimeFont()
        {
            sourceFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Liberation Sans" }, 48);
            if (sourceFont != null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
                if (fontAsset != null)
                {
                    fontAsset.name = "Neon Pulse Runtime Font";
                    ownsFontAsset = true;
                    return;
                }
            }

            TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings != null)
            {
                fontAsset = TMP_Settings.defaultFontAsset;
            }
        }

        private TextMeshProUGUI CreateText(string objectName, Transform parent, string content, float size, TextAlignmentOptions alignment, Color color)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null)
            {
                text.font = fontAsset;
            }
            text.text = content;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private void CreateResultPanel(RuntimeMaterialLibrary materials)
        {
            resultPanel = new GameObject("Result Panel", typeof(RectTransform), typeof(Image));
            resultPanel.transform.SetParent(transform, false);
            Image background = resultPanel.GetComponent<Image>();
            background.color = new Color(0.015f, 0.005f, 0.04f, 0.94f);
            SetRect(resultPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 670f));

            resultText = CreateText("Result Text", resultPanel.transform, string.Empty, 43f, TextAlignmentOptions.Center, materials.YellowColor);
            RectTransform rect = resultText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(40f, 35f);
            rect.offsetMax = new Vector2(-40f, -35f);
            resultText.enableWordWrapping = true;
            resultPanel.SetActive(false);
        }

        private void CreateNextActionPanel(RuntimeMaterialLibrary materials)
        {
            nextActionPanel = new GameObject("Next Action Cue", typeof(RectTransform), typeof(Image));
            nextActionPanel.transform.SetParent(transform, false);
            Image background = nextActionPanel.GetComponent<Image>();
            background.color = new Color(0.015f, 0.005f, 0.04f, 0.88f);
            background.raycastTarget = false;
            SetRect(nextActionPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.31f), new Vector2(0.5f, 0.31f), Vector2.zero, new Vector2(650f, 112f));

            nextActionText = CreateText("Next Action Text", nextActionPanel.transform, string.Empty, 40f, TextAlignmentOptions.Center, materials.CyanColor);
            RectTransform textRect = nextActionText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 16f);
            textRect.offsetMax = new Vector2(-16f, -4f);

            GameObject fillObject = new GameObject("Approach Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(nextActionPanel.transform, false);
            nextActionFill = fillObject.GetComponent<Image>();
            nextActionFill.type = Image.Type.Filled;
            nextActionFill.fillMethod = Image.FillMethod.Horizontal;
            nextActionFill.fillOrigin = 0;
            nextActionFill.raycastTarget = false;
            SetRect(fillObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(620f, 9f));
            nextActionPanel.SetActive(false);
        }

        private void CreateLevelProgressPanel(RuntimeMaterialLibrary materials)
        {
            GameObject panel = new GameObject("Level Progress", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            Image background = panel.GetComponent<Image>();
            background.color = new Color(0.015f, 0.005f, 0.04f, 0.82f);
            background.raycastTarget = false;
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -56f), new Vector2(760f, 96f));

            levelProgressText = CreateText("Level Progress Value", panel.transform, "LEVEL  0%", 24f,
                TextAlignmentOptions.Left, materials.YellowColor);
            SetRect(levelProgressText.rectTransform, new Vector2(0f, 0.76f), new Vector2(0f, 0.76f),
                new Vector2(18f, 0f), new Vector2(210f, 30f));

            phaseProgressText = CreateText("Phase Progress Value", panel.transform, "PHASE 1/1", 21f,
                TextAlignmentOptions.Right, Color.white);
            SetRect(phaseProgressText.rectTransform, new Vector2(1f, 0.76f), new Vector2(1f, 0.76f),
                new Vector2(-18f, 0f), new Vector2(440f, 30f));

            GameObject bar = new GameObject("Level Progress Fill", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(panel.transform, false);
            levelProgressFill = bar.GetComponent<Image>();
            levelProgressFill.type = Image.Type.Simple;
            levelProgressFill.color = materials.CyanColor;
            levelProgressFill.raycastTarget = false;
            SetRect(levelProgressFill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 31f), new Vector2(ProgressBarWidth, LevelProgressBarHeight));
            SetProgressBarWidth(levelProgressFill, 0f, LevelProgressBarHeight, 31f);

        }

        private static void SetProgressBarWidth(Image progressBar, float progress, float height, float yPosition)
        {
            RectTransform rect = progressBar.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(20f, yPosition);
            rect.sizeDelta = new Vector2(ProgressBarWidth * Mathf.Clamp01(progress), height);
        }

        private void CreateCountdownPanel(RuntimeMaterialLibrary materials)
        {
            countdownPanel = new GameObject("Start Tutorial", typeof(RectTransform), typeof(Image));
            countdownPanel.transform.SetParent(transform, false);
            Image background = countdownPanel.GetComponent<Image>();
            background.color = new Color(0.012f, 0.003f, 0.035f, 0.94f);
            background.raycastTarget = false;
            SetRect(countdownPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(1020f, 500f));

            countdownText = CreateText("Countdown", countdownPanel.transform, "3", 120f, TextAlignmentOptions.Center, materials.YellowColor);
            SetRect(countdownText.rectTransform, new Vector2(0.5f, 0.75f), new Vector2(0.5f, 0.75f), Vector2.zero, new Vector2(300f, 150f));

            if (showGuidance)
            {
                TextMeshProUGUI guide = CreateText("Quick Guide", countdownPanel.transform,
                    BuildTutorialGuide(),
                    31f, TextAlignmentOptions.Center, Color.white);
                guide.enableWordWrapping = true;
                SetRect(guide.rectTransform, new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), Vector2.zero, new Vector2(940f, 250f));
            }
            countdownPanel.SetActive(false);
        }

        private string GetActionPrompt(GameplayAction action, bool ready)
        {
            string prefix = ready
                ? isSlashMode ? "CHÉM NGAY   " : "ĐẤM NGAY   "
                : "SẮP TỚI   ";
            if (ready)
            {
                switch (action)
                {
                    case GameplayAction.Duck: return "GIỮ NGAY   " + GetKeyLabel(bindings.Duck) + " — CÚI NGƯỜI";
                    case GameplayAction.Jump: return "GIỮ NGAY   " + GetKeyLabel(bindings.Jump) + " — NHẢY";
                    case GameplayAction.DodgeLeft: return "GIỮ NGAY   " + GetKeyLabel(bindings.DodgeLeft) + " — NÉ TRÁI";
                    case GameplayAction.DodgeRight: return "GIỮ NGAY   " + GetKeyLabel(bindings.DodgeRight) + " — NÉ PHẢI";
                }
            }

            switch (action)
            {
                case GameplayAction.LeftPunch: return prefix + GetKeyLabel(bindings.LeftPunch) + (isSlashMode ? " — KIẾM TRÁI" : " — TAY TRÁI");
                case GameplayAction.RightPunch: return prefix + GetKeyLabel(bindings.RightPunch) + (isSlashMode ? " — KIẾM PHẢI" : " — TAY PHẢI");
                case GameplayAction.BothPunch: return prefix + GetKeyLabel(bindings.BothPunch) + (isSlashMode ? " — HAI KIẾM" : " — CẢ HAI TAY");
                case GameplayAction.Duck: return "SẮP TỚI   " + GetKeyLabel(bindings.Duck) + " — CÚI NGƯỜI";
                case GameplayAction.Jump: return "SẮP TỚI   " + GetKeyLabel(bindings.Jump) + " — NHẢY";
                case GameplayAction.DodgeLeft: return "SẮP TỚI   " + GetKeyLabel(bindings.DodgeLeft) + " — NÉ TRÁI";
                default: return "SẮP TỚI   " + GetKeyLabel(bindings.DodgeRight) + " — NÉ PHẢI";
            }
        }

        private string BuildControlGuide()
        {
            string leftLabel = isSlashMode ? "KIẾM TRÁI" : "TAY TRÁI";
            string rightLabel = isSlashMode ? "KIẾM PHẢI" : "TAY PHẢI";
            string bothLabel = isSlashMode ? "HAI KIẾM" : "CẢ HAI TAY";
            return GetKeyLabel(bindings.LeftPunch) + " / " + GetKeyLabel(bindings.LeftPunchAlternative) + "  " + leftLabel + "     " +
                   GetKeyLabel(bindings.RightPunch) + " / " + GetKeyLabel(bindings.RightPunchAlternative) + "  " + rightLabel + "     " +
                   GetKeyLabel(bindings.BothPunch) + "  " + bothLabel + "\nGIỮ " +
                   GetKeyLabel(bindings.DodgeLeft) + " / " + GetKeyLabel(bindings.DodgeRight) + "  NÉ     GIỮ " +
                   GetKeyLabel(bindings.Duck) + "  CÚI     GIỮ " + GetKeyLabel(bindings.Jump) + "  NHẢY     " +
                   restartLabel + "  CHƠI LẠI";
        }

        private string BuildTutorialGuide()
        {
            string actionGuide = isSlashMode ? "CHÉM KHỐI VUÔNG KHI VÀO CỔNG VÀNG" : "ĐẤM KHI VẬT THỂ VÀO CỔNG VÀNG";
            string leftLabel = isSlashMode ? "KIẾM TRÁI" : "TAY TRÁI";
            string rightLabel = isSlashMode ? "KIẾM PHẢI" : "TAY PHẢI";
            string bothLabel = isSlashMode ? "HAI KIẾM" : "CẢ HAI";
            return actionGuide + "\nGIỮ PHÍM CHO ĐẾN KHI CHƯỚNG NGẠI ĐI QUA\n\n<color=#00fff2>" +
                   GetKeyLabel(bindings.LeftPunch) + "  " + leftLabel + "</color>     <color=#ff08b8>" +
                   GetKeyLabel(bindings.RightPunch) + "  " + rightLabel + "</color>     <color=#ffd10d>" +
                   GetKeyLabel(bindings.BothPunch) + "  " + bothLabel + "</color>\nGIỮ " + GetKeyLabel(bindings.Duck) +
                   "  CÚI     GIỮ " + GetKeyLabel(bindings.Jump) + "  NHẢY     GIỮ " +
                   GetKeyLabel(bindings.DodgeLeft) + " / " + GetKeyLabel(bindings.DodgeRight) + "  NÉ";
        }

        private static string GetKeyLabel(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Space: return "SPACE";
                case KeyCode.Return: return "ENTER";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.UpArrow: return "↑";
                default: return key.ToString().ToUpperInvariant();
            }
        }

        private static Color GetActionColor(GameplayAction action, RuntimeMaterialLibrary materials)
        {
            if (action == GameplayAction.LeftPunch || action == GameplayAction.DodgeLeft)
            {
                return materials.CyanColor;
            }

            if (action == GameplayAction.RightPunch || action == GameplayAction.DodgeRight)
            {
                return materials.MagentaColor;
            }

            return materials.YellowColor;
        }

        private static bool RequiresHold(GameplayAction action)
        {
            return action == GameplayAction.Duck || action == GameplayAction.Jump ||
                   action == GameplayAction.DodgeLeft || action == GameplayAction.DodgeRight;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}

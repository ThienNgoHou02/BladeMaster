using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonPulse
{
    /// <summary>Runtime-built TextMeshPro HUD with score, combo, feedback, controls and results.</summary>
    public sealed class NeonHud : MonoBehaviour
    {
        private TMP_FontAsset fontAsset;
        private Font sourceFont;
        private bool ownsFontAsset;
        private TextMeshProUGUI scoreText;
        private TextMeshProUGUI comboText;
        private TextMeshProUGUI feedbackText;
        private TextMeshProUGUI statusText;
        private GameObject nextActionPanel;
        private TextMeshProUGUI nextActionText;
        private Image nextActionFill;
        private GameObject countdownPanel;
        private TextMeshProUGUI countdownText;
        private GameObject resultPanel;
        private TextMeshProUGUI resultText;
        private Image progressFill;
        private float feedbackTimer;
        private bool nextActionReady;
        private GameplayAction lastNextAction;
        private bool hasNextAction;
        private int lastCountdownValue = -1;

        /// <summary>Creates all HUD objects and runtime font resources.</summary>
        public void Build(RuntimeMaterialLibrary materials)
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            CreateRuntimeFont();

            TextMeshProUGUI title = CreateText("Title", transform, "NEON PULSE FITNESS", 48f, TextAlignmentOptions.Center, materials.YellowColor);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(850f, 76f));

            scoreText = CreateText("Score", transform, "ĐIỂM 000000", 38f, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(scoreText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -32f), new Vector2(480f, 65f));

            comboText = CreateText("Combo", transform, "0\nCOMBO", 62f, TextAlignmentOptions.TopRight, Color.white);
            comboText.lineSpacing = -18f;
            SetRect(comboText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-42f, -24f), new Vector2(350f, 150f));

            feedbackText = CreateText("Feedback", transform, string.Empty, 70f, TextAlignmentOptions.Center, materials.CyanColor);
            feedbackText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            SetRect(feedbackText.rectTransform, new Vector2(0.5f, 0.76f), new Vector2(0.5f, 0.76f), Vector2.zero, new Vector2(850f, 105f));

            statusText = CreateText("Controls", transform,
                "Q / ←  TAY TRÁI     E / →  TAY PHẢI     F  CẢ HAI TAY\nGIỮ A / D  NÉ     GIỮ S  CÚI     GIỮ SPACE  NHẢY     R  CHƠI LẠI",
                25f, TextAlignmentOptions.Center, new Color(0.8f, 0.85f, 1f, 0.9f));
            SetRect(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1450f, 92f));

            CreateProgressBar(materials);
            CreateNextActionPanel(materials);
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

            scoreText.SetText("ĐIỂM {0:000000}", snapshot.Score);
            comboText.SetText("{0}\nCOMBO", snapshot.Combo);
        }

        /// <summary>Shows short accuracy feedback using cached UI components.</summary>
        public void ShowJudgement(AccuracyGrade grade, GameplayAction action, RuntimeMaterialLibrary materials)
        {
            if (feedbackText == null || materials == null)
            {
                return;
            }

            switch (grade)
            {
                case AccuracyGrade.Perfect:
                    feedbackText.text = "CHÍNH XÁC";
                    feedbackText.color = materials.YellowColor;
                    break;
                case AccuracyGrade.Great:
                    feedbackText.text = "RẤT TỐT";
                    feedbackText.color = materials.CyanColor;
                    break;
                case AccuracyGrade.Good:
                    feedbackText.text = "TỐT";
                    feedbackText.color = Color.white;
                    break;
                default:
                    feedbackText.text = "TRƯỢT";
                    feedbackText.color = new Color(1f, 0.12f, 0.22f, 1f);
                    break;
            }

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
            if (nextActionPanel == null || nextActionText == null || nextActionFill == null || materials == null)
            {
                return;
            }

            bool holdAction = RequiresHold(action);
            bool ready = holdAction
                ? secondsUntilHit <= GameplayTiming.HoldWindowLead && secondsUntilHit >= -GameplayTiming.HoldWindowTrail - 0.02f
                : Mathf.Abs(secondsUntilHit) <= RhythmScore.GoodWindow;
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
            if (nextActionText == null || materials == null)
            {
                return;
            }

            switch (action)
            {
                case GameplayAction.Duck: nextActionText.text = "ĐANG GIỮ S — ĐỪNG THẢ"; break;
                case GameplayAction.Jump: nextActionText.text = "ĐANG GIỮ SPACE — ĐỪNG THẢ"; break;
                case GameplayAction.DodgeLeft: nextActionText.text = "ĐANG GIỮ A — ĐỪNG THẢ"; break;
                default: nextActionText.text = "ĐANG GIỮ D — ĐỪNG THẢ"; break;
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
            int value = Mathf.Clamp(Mathf.CeilToInt(secondsUntilStart), 1, 3);
            if (value != lastCountdownValue)
            {
                countdownText.SetText("{0}", value);
                lastCountdownValue = value;
            }
        }

        /// <summary>Updates song progress without string creation.</summary>
        public void SetProgress(float normalized)
        {
            if (progressFill != null)
            {
                progressFill.fillAmount = Mathf.Clamp01(normalized);
            }
        }

        /// <summary>Shows the final run summary and restart hint.</summary>
        public void ShowResults(ScoreSnapshot snapshot)
        {
            if (resultPanel == null || resultText == null)
            {
                return;
            }

            resultPanel.SetActive(true);
            resultText.SetText(
                "HOÀN THÀNH BÀI TẬP\n\nĐIỂM  {0:000000}\nCOMBO CAO NHẤT  {1}\n\nCHÍNH XÁC  {2}     RẤT TỐT  {3}\nTỐT  {4}     TRƯỢT  {5}\n\nNHẤN R HOẶC ENTER ĐỂ CHƠI LẠI",
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
            SetProgress(0f);
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

        private void CreateProgressBar(RuntimeMaterialLibrary materials)
        {
            GameObject background = new GameObject("Song Progress Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(transform, false);
            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(0.12f, 0.05f, 0.2f, 0.85f);
            backgroundImage.raycastTarget = false;
            SetRect(background.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(720f, 10f));

            GameObject fill = new GameObject("Song Progress Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(background.transform, false);
            progressFill = fill.GetComponent<Image>();
            progressFill.color = materials.CyanColor;
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = 0;
            progressFill.fillAmount = 0f;
            progressFill.raycastTarget = false;
            RectTransform rect = fill.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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

            TextMeshProUGUI guide = CreateText("Quick Guide", countdownPanel.transform,
                "ĐẤM KHI VẬT THỂ VÀO CỔNG VÀNG\nGIỮ PHÍM CHO ĐẾN KHI CHƯỚNG NGẠI ĐI QUA\n\n<color=#00fff2>Q  TAY TRÁI</color>     <color=#ff08b8>E  TAY PHẢI</color>     <color=#ffd10d>F  CẢ HAI</color>\nGIỮ S  CÚI     GIỮ SPACE  NHẢY     GIỮ A / D  NÉ",
                31f, TextAlignmentOptions.Center, Color.white);
            guide.enableWordWrapping = true;
            SetRect(guide.rectTransform, new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), Vector2.zero, new Vector2(940f, 250f));
            countdownPanel.SetActive(false);
        }

        private static string GetActionPrompt(GameplayAction action, bool ready)
        {
            if (ready)
            {
                switch (action)
                {
                    case GameplayAction.LeftPunch: return "ĐÁNH NGAY   Q — TAY TRÁI";
                    case GameplayAction.RightPunch: return "ĐÁNH NGAY   E — TAY PHẢI";
                    case GameplayAction.BothPunch: return "ĐÁNH NGAY   F — CẢ HAI TAY";
                    case GameplayAction.Duck: return "GIỮ NGAY   S — CÚI NGƯỜI";
                    case GameplayAction.Jump: return "GIỮ NGAY   SPACE — NHẢY";
                    case GameplayAction.DodgeLeft: return "GIỮ NGAY   A — NÉ TRÁI";
                    default: return "GIỮ NGAY   D — NÉ PHẢI";
                }
            }

            switch (action)
            {
                case GameplayAction.LeftPunch: return "SẮP TỚI   Q — TAY TRÁI";
                case GameplayAction.RightPunch: return "SẮP TỚI   E — TAY PHẢI";
                case GameplayAction.BothPunch: return "SẮP TỚI   F — CẢ HAI TAY";
                case GameplayAction.Duck: return "SẮP TỚI   S — CÚI NGƯỜI";
                case GameplayAction.Jump: return "SẮP TỚI   SPACE — NHẢY";
                case GameplayAction.DodgeLeft: return "SẮP TỚI   A — NÉ TRÁI";
                default: return "SẮP TỚI   D — NÉ PHẢI";
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

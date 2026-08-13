using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonPulse
{
    /// <summary>Runtime-built HUD with combo, action cues, feedback, controls and results.</summary>
    public sealed class NeonHud : MonoBehaviour
    {
        private const int RadialTextureSize = 128;
        private const float ActionIconSlotSpacing = 96f;
        private const float ActionIconMaxSize = 148f;
        private const float JudgementFeedbackDuration = 0.85f;
        private const float FeedbackStartOffsetX = -35f;

        private static readonly string[] PerfectFeedbackFormats =
        {
            "PERFECT  +{0}",
            "AWESOME  +{0}",
            "AMAZING  +{0}",
            "EXCELLENT  +{0}"
        };

        private static readonly string[] GreatFeedbackFormats =
        {
            "GREAT  +{0}",
            "NICE  +{0}",
            "AWESOME  +{0}",
            "WELL DONE  +{0}"
        };

        private static readonly string[] GoodFeedbackFormats =
        {
            "GOOD  +{0}",
            "NICE  +{0}",
            "KEEP GOING  +{0}"
        };

        private TMP_FontAsset fontAsset;
        private Font runtimeSourceFont;
        private bool ownsFontAsset;
        private TextMeshProUGUI comboText;
        private TextMeshProUGUI feedbackText;
        private TextMeshProUGUI statusText;
        private GameObject nextActionPanel;
        private TextMeshProUGUI nextActionText;
        private Image nextActionFill;
        private Image secondaryActionFill;
        private RectTransform primaryActionSlot;
        private RectTransform secondaryActionSlot;
        private RawImage primaryActionIcon;
        private RawImage secondaryActionIcon;
        private GameObject countdownPanel;
        private TextMeshProUGUI countdownText;
        private GameObject resultPanel;
        private TextMeshProUGUI resultText;
        private float feedbackTimer;
        private string lastFeedbackFormat;
        private float comboPulseTimer;
        private Color comboPulseColor = Color.white;
        private int lastScoreValue;
        private bool nextActionReady;
        private bool currentCueHasIcon;
        private GameplayAction lastNextAction;
        private bool lastCueUsesSlashMode;
        private bool hasNextAction;
        private int lastCountdownValue = -1;
        private RhythmSettings timing;
        private InputBindingSettings bindings;
        private string restartLabel;
        private bool showGuidance;
        private bool isSlashMode;
        private VisualSettings visualSettings;
        private Texture2D radialProgressTexture;
        private Sprite radialProgressSprite;
        private Vector2 feedbackRestPosition;

        /// <summary>Creates all HUD objects and runtime font resources.</summary>
        public void Build(RuntimeMaterialLibrary materials, NeonPulseGameConfig config)
        {
            timing = config.Rhythm;
            bindings = config.Input;
            visualSettings = config.Visuals;
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

            CreateRuntimeFont(visualSettings.HudFont);

            comboText = CreateText("Combo", transform, "<size=82>0</size>\n<size=34>COMBO</size>", 52f, TextAlignmentOptions.TopRight, Color.white);
            comboText.lineSpacing = -10f;
            SetRect(comboText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170f, -82f), new Vector2(300f, 150f));

            feedbackText = CreateText("Judgement Feedback", transform, string.Empty, 66f, TextAlignmentOptions.Left, materials.CyanColor);
            feedbackText.fontStyle = FontStyles.Normal;
            feedbackRestPosition = new Vector2(70f, -165f);
            SetRect(feedbackText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), feedbackRestPosition, new Vector2(620f, 120f));
            feedbackText.rectTransform.pivot = new Vector2(0f, 0.5f);

            if (showGuidance)
            {
                statusText = CreateText("Controls", transform,
                    BuildControlGuide(),
                    25f, TextAlignmentOptions.Center, new Color(0.8f, 0.85f, 1f, 0.9f));
                SetRect(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1450f, 92f));
            }

            CreateNextActionPanel(materials);
            CreateCountdownPanel(materials);
            CreateResultPanel(materials);
        }

        /// <summary>Updates the combo label and cached score only when score state changes.</summary>
        public void SetScore(ScoreSnapshot snapshot)
        {
            if (comboText == null)
            {
                return;
            }

            comboText.SetText("<size=82>{0}</size>\n<size=34>COMBO</size>", snapshot.Combo);
            lastScoreValue = snapshot.Score;
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
                    feedbackText.SetText(GetRandomFeedbackFormat(PerfectFeedbackFormats), gainedScore);
                    feedbackText.color = materials.YellowColor;
                    break;
                case AccuracyGrade.Great:
                    feedbackText.SetText(GetRandomFeedbackFormat(GreatFeedbackFormats), gainedScore);
                    feedbackText.color = materials.CyanColor;
                    break;
                case AccuracyGrade.Good:
                    feedbackText.SetText(GetRandomFeedbackFormat(GoodFeedbackFormats), gainedScore);
                    feedbackText.color = Color.white;
                    break;
                default:
                    feedbackText.text = "MISS";
                    feedbackText.color = new Color(1f, 0.12f, 0.22f, 1f);
                    break;
            }

            comboPulseColor = feedbackText.color;
            comboPulseTimer = 0.42f;
            feedbackText.alpha = 1f;
            feedbackText.rectTransform.anchoredPosition = feedbackRestPosition + new Vector2(FeedbackStartOffsetX, 0f);
            feedbackText.rectTransform.localScale = Vector3.one * 0.82f;
            feedbackTimer = JudgementFeedbackDuration;
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
            feedbackText.rectTransform.anchoredPosition = feedbackRestPosition + new Vector2(FeedbackStartOffsetX, 0f);
            feedbackText.rectTransform.localScale = Vector3.one * 0.82f;
            feedbackTimer = JudgementFeedbackDuration;
        }

        /// <summary>Shows the closest required action and fills toward its hit time.</summary>
        public void SetUpcomingAction(
            GameplayAction action,
            float secondsUntilHit,
            float approachDuration,
            RuntimeMaterialLibrary materials,
            float holdDuration = 0f)
        {
            if (nextActionPanel == null || nextActionText == null || nextActionFill == null || materials == null)
            {
                return;
            }

            bool holdAction = RequiresHold(action);
            float effectiveHoldDuration = holdDuration > 0f ? holdDuration : timing.HoldWindowTrail;
            bool ready = holdAction
                ? secondsUntilHit <= timing.HoldWindowLead && secondsUntilHit >= -effectiveHoldDuration - 0.02f
                : Mathf.Abs(secondsUntilHit) <= timing.GoodWindow;
            bool cueChanged = !hasNextAction || lastNextAction != action || lastCueUsesSlashMode != isSlashMode;
            if (cueChanged)
            {
                currentCueHasIcon = ConfigureActionIcons(action);
                nextActionText.gameObject.SetActive(!currentCueHasIcon && showGuidance);
                lastCueUsesSlashMode = isSlashMode;
            }

            if (!currentCueHasIcon && !showGuidance)
            {
                HideUpcomingAction();
                return;
            }

            if (cueChanged || nextActionReady != ready)
            {
                if (!currentCueHasIcon)
                {
                    nextActionText.text = GetActionPrompt(action, ready);
                }

                nextActionText.color = GetActionColor(action, materials);
                lastNextAction = action;
                nextActionReady = ready;
                hasNextAction = true;
            }

            Color progressColor = ready ? materials.YellowColor : GetActionColor(action, materials);
            float progress = 1f - Mathf.Clamp01(secondsUntilHit / Mathf.Max(0.01f, approachDuration));
            nextActionFill.color = progressColor;
            nextActionFill.fillAmount = progress;
            secondaryActionFill.color = progressColor;
            secondaryActionFill.fillAmount = progress;
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
                case GameplayAction.DodgeRight: nextActionText.text = "ĐANG GIỮ " + GetKeyLabel(bindings.DodgeRight) + " — ĐỪNG THẢ"; break;
                case GameplayAction.LeftLegDrawUp: nextActionText.text = "ĐANG GIỮ " + GetKeyLabel(bindings.LeftLegDrawUp) + " — CO CHÂN TRÁI"; break;
                default: nextActionText.text = "ĐANG GIỮ " + GetKeyLabel(bindings.RightLegDrawUp) + " — CO CHÂN PHẢI"; break;
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
                feedbackText.rectTransform.anchoredPosition = feedbackRestPosition;
                feedbackText.rectTransform.localScale = Vector3.one;
                feedbackTimer = 1.2f;
            }

            lastFeedbackFormat = null;

            HideUpcomingAction();

        }

        private void Update()
        {
            if (feedbackTimer > 0f && feedbackText != null)
            {
                feedbackTimer -= Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(feedbackTimer / JudgementFeedbackDuration);
                float enterProgress = 1f - Mathf.Clamp01((normalized - 0.72f) / 0.28f);
                float exitProgress = 1f - Mathf.Clamp01(normalized / 0.32f);
                feedbackText.alpha = 1f - exitProgress;
                feedbackText.rectTransform.anchoredPosition = feedbackRestPosition + new Vector2(
                    Mathf.Lerp(FeedbackStartOffsetX, 0f, enterProgress),
                    Mathf.Lerp(0f, 22f, exitProgress));
                feedbackText.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, enterProgress);
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

            if (runtimeSourceFont != null)
            {
                Destroy(runtimeSourceFont);
            }

            if (radialProgressSprite != null)
            {
                Destroy(radialProgressSprite);
            }

            if (radialProgressTexture != null)
            {
                Destroy(radialProgressTexture);
            }
        }

        private void CreateRuntimeFont(Font configuredFont)
        {
            Font sourceFont = configuredFont;
            if (sourceFont == null)
            {
                runtimeSourceFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Liberation Sans" }, 48);
                sourceFont = runtimeSourceFont;
            }

            if (sourceFont != null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
                if (fontAsset != null)
                {
                    fontAsset.name = configuredFont != null ? configuredFont.name + " TMP Runtime" : "Neon Pulse Runtime Font";
                    fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
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

        private string GetRandomFeedbackFormat(string[] formats)
        {
            int index = Random.Range(0, formats.Length);
            string selectedFormat = formats[index];
            if (formats.Length > 1 && selectedFormat == lastFeedbackFormat)
            {
                selectedFormat = formats[(index + 1) % formats.Length];
            }

            lastFeedbackFormat = selectedFormat;
            return selectedFormat;
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
            CreateRadialProgressSprite();

            nextActionPanel = new GameObject("Next Action Cue", typeof(RectTransform));
            nextActionPanel.transform.SetParent(transform, false);
            SetRect(nextActionPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -205f), new Vector2(390f, 190f));

            primaryActionSlot = CreateActionIconSlot("Primary Action Icon", nextActionPanel.transform,
                out primaryActionIcon, out nextActionFill);
            secondaryActionSlot = CreateActionIconSlot("Secondary Action Icon", nextActionPanel.transform,
                out secondaryActionIcon, out secondaryActionFill);

            nextActionText = CreateText("Next Action Text", nextActionPanel.transform, string.Empty, 40f, TextAlignmentOptions.Center, materials.CyanColor);
            SetRect(nextActionText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(650f, 112f));
            nextActionText.gameObject.SetActive(false);
            nextActionPanel.SetActive(false);
        }

        private RectTransform CreateActionIconSlot(string objectName, Transform parent, out RawImage icon, out Image progressFill)
        {
            GameObject slotObject = new GameObject(objectName, typeof(RectTransform));
            slotObject.transform.SetParent(parent, false);
            RectTransform slot = slotObject.GetComponent<RectTransform>();
            SetRect(slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(184f, 184f));

            GameObject ringBackgroundObject = new GameObject("Ring Background", typeof(RectTransform), typeof(Image));
            ringBackgroundObject.transform.SetParent(slot, false);
            Image ringBackground = ringBackgroundObject.GetComponent<Image>();
            ringBackground.sprite = radialProgressSprite;
            ringBackground.color = new Color(1f, 1f, 1f, 0.18f);
            ringBackground.raycastTarget = false;
            SetRect(ringBackground.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(184f, 184f));

            GameObject progressObject = new GameObject("Radial Progress", typeof(RectTransform), typeof(Image));
            progressObject.transform.SetParent(slot, false);
            progressFill = progressObject.GetComponent<Image>();
            progressFill.sprite = radialProgressSprite;
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Radial360;
            progressFill.fillOrigin = (int)Image.Origin360.Top;
            progressFill.fillClockwise = true;
            progressFill.fillAmount = 0f;
            progressFill.raycastTarget = false;
            SetRect(progressFill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(184f, 184f));

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
            iconObject.transform.SetParent(slot, false);
            icon = iconObject.GetComponent<RawImage>();
            icon.uvRect = new Rect(0f, 0f, 1f, 1f);
            icon.color = Color.white;
            icon.raycastTarget = false;
            SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(148f, 148f));

            return slot;
        }

        private void CreateRadialProgressSprite()
        {
            radialProgressTexture = new Texture2D(RadialTextureSize, RadialTextureSize, TextureFormat.RGBA32, false)
            {
                name = "Action Cue Radial Progress",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[RadialTextureSize * RadialTextureSize];
            float center = (RadialTextureSize - 1) * 0.5f;
            float outerRadiusSquared = 62f * 62f;
            float innerRadiusSquared = 53f * 53f;
            for (int y = 0; y < RadialTextureSize; y++)
            {
                float deltaY = y - center;
                for (int x = 0; x < RadialTextureSize; x++)
                {
                    float deltaX = x - center;
                    float distanceSquared = deltaX * deltaX + deltaY * deltaY;
                    byte alpha = distanceSquared <= outerRadiusSquared && distanceSquared >= innerRadiusSquared
                        ? (byte)255
                        : (byte)0;
                    pixels[y * RadialTextureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }

            radialProgressTexture.SetPixels32(pixels);
            radialProgressTexture.Apply(false, true);
            radialProgressSprite = Sprite.Create(radialProgressTexture,
                new Rect(0f, 0f, RadialTextureSize, RadialTextureSize), new Vector2(0.5f, 0.5f), 100f);
            radialProgressSprite.name = "Action Cue Radial Progress Sprite";
        }

        private bool ConfigureActionIcons(GameplayAction action)
        {
            Texture2D primaryTexture = null;
            Texture2D secondaryTexture = null;
            switch (action)
            {
                case GameplayAction.OverheadClap:
                    primaryTexture = visualSettings.OverheadClapActionIcon;
                    break;
                case GameplayAction.LeftPunch when !isSlashMode:
                    primaryTexture = visualSettings.LeftPunchActionIcon;
                    break;
                case GameplayAction.RightPunch when !isSlashMode:
                    primaryTexture = visualSettings.RightPunchActionIcon;
                    break;
                case GameplayAction.BothPunch when !isSlashMode:
                    primaryTexture = visualSettings.LeftPunchActionIcon;
                    secondaryTexture = visualSettings.RightPunchActionIcon;
                    break;
                case GameplayAction.LeftPunch:
                    primaryTexture = visualSettings.LeftSlashActionIcon;
                    break;
                case GameplayAction.RightPunch:
                    primaryTexture = visualSettings.RightSlashActionIcon;
                    break;
                case GameplayAction.BothPunch:
                    primaryTexture = visualSettings.LeftSlashActionIcon;
                    secondaryTexture = visualSettings.RightSlashActionIcon;
                    break;
                case GameplayAction.DodgeLeft:
                    primaryTexture = visualSettings.LeftDodgeActionIcon;
                    break;
                case GameplayAction.DodgeRight:
                    primaryTexture = visualSettings.RightDodgeActionIcon;
                    break;
                case GameplayAction.Jump:
                    primaryTexture = visualSettings.JumpActionIcon;
                    break;
                case GameplayAction.Duck:
                    primaryTexture = visualSettings.DuckActionIcon;
                    break;
                case GameplayAction.LeftLegDrawUp:
                    primaryTexture = visualSettings.LeftLegDrawUpActionIcon;
                    break;
                case GameplayAction.RightLegDrawUp:
                    primaryTexture = visualSettings.RightLegDrawUpActionIcon;
                    break;
            }

            bool hasPrimaryIcon = primaryTexture != null;
            bool hasSecondaryIcon = secondaryTexture != null;
            primaryActionSlot.gameObject.SetActive(hasPrimaryIcon);
            secondaryActionSlot.gameObject.SetActive(hasSecondaryIcon);
            if (!hasPrimaryIcon)
            {
                return false;
            }

            ConfigureIconTexture(primaryActionIcon, primaryTexture);
            primaryActionSlot.anchoredPosition = hasSecondaryIcon
                ? new Vector2(-ActionIconSlotSpacing, 0f)
                : Vector2.zero;
            if (hasSecondaryIcon)
            {
                ConfigureIconTexture(secondaryActionIcon, secondaryTexture);
                secondaryActionSlot.anchoredPosition = new Vector2(ActionIconSlotSpacing, 0f);
            }

            return true;
        }

        private static void ConfigureIconTexture(RawImage icon, Texture2D texture)
        {
            icon.texture = texture;
            icon.uvRect = new Rect(0f, 0f, 1f, 1f);

            float aspectRatio = (float)texture.width / texture.height;
            icon.rectTransform.sizeDelta = aspectRatio >= 1f
                ? new Vector2(ActionIconMaxSize, ActionIconMaxSize / aspectRatio)
                : new Vector2(ActionIconMaxSize * aspectRatio, ActionIconMaxSize);
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
                    case GameplayAction.LeftLegDrawUp: return "GIỮ NGAY   " + GetKeyLabel(bindings.LeftLegDrawUp) + " — CO CHÂN TRÁI";
                    case GameplayAction.RightLegDrawUp: return "GIỮ NGAY   " + GetKeyLabel(bindings.RightLegDrawUp) + " — CO CHÂN PHẢI";
                }
            }

            switch (action)
            {
                case GameplayAction.OverheadClap: return (ready ? "VỖ TAY NGAY   " : "SẮP TỚI   ") + GetKeyLabel(bindings.OverheadClap) + " — TRÊN ĐẦU";
                case GameplayAction.LeftPunch: return prefix + GetKeyLabel(bindings.LeftPunch) + (isSlashMode ? " — KIẾM TRÁI" : " — TAY TRÁI");
                case GameplayAction.RightPunch: return prefix + GetKeyLabel(bindings.RightPunch) + (isSlashMode ? " — KIẾM PHẢI" : " — TAY PHẢI");
                case GameplayAction.BothPunch: return prefix + GetKeyLabel(bindings.BothPunch) + (isSlashMode ? " — HAI KIẾM" : " — CẢ HAI TAY");
                case GameplayAction.Duck: return "SẮP TỚI   " + GetKeyLabel(bindings.Duck) + " — CÚI NGƯỜI";
                case GameplayAction.Jump: return "SẮP TỚI   " + GetKeyLabel(bindings.Jump) + " — NHẢY";
                case GameplayAction.DodgeLeft: return "SẮP TỚI   " + GetKeyLabel(bindings.DodgeLeft) + " — NÉ TRÁI";
                case GameplayAction.DodgeRight: return "SẮP TỚI   " + GetKeyLabel(bindings.DodgeRight) + " — NÉ PHẢI";
                case GameplayAction.LeftLegDrawUp: return "SẮP TỚI   " + GetKeyLabel(bindings.LeftLegDrawUp) + " — CO CHÂN TRÁI";
                default: return "SẮP TỚI   " + GetKeyLabel(bindings.RightLegDrawUp) + " — CO CHÂN PHẢI";
            }
        }

        private string BuildControlGuide()
        {
            string leftLabel = isSlashMode ? "KIẾM TRÁI" : "TAY TRÁI";
            string rightLabel = isSlashMode ? "KIẾM PHẢI" : "TAY PHẢI";
            string bothLabel = isSlashMode ? "HAI KIẾM" : "CẢ HAI TAY";
            return GetKeyLabel(bindings.LeftPunch) + " / " + GetKeyLabel(bindings.LeftPunchAlternative) + "  " + leftLabel + "     " +
                   GetKeyLabel(bindings.RightPunch) + " / " + GetKeyLabel(bindings.RightPunchAlternative) + "  " + rightLabel + "     " +
                   GetKeyLabel(bindings.BothPunch) + "  " + bothLabel + "     " +
                   GetKeyLabel(bindings.OverheadClap) + "  VỖ TAY TRÊN ĐẦU\nGIỮ " +
                   GetKeyLabel(bindings.DodgeLeft) + " / " + GetKeyLabel(bindings.DodgeRight) + "  NÉ     GIỮ " +
                   GetKeyLabel(bindings.Duck) + "  CÚI     GIỮ " + GetKeyLabel(bindings.Jump) + "  NHẢY     " +
                   "GIỮ " + GetKeyLabel(bindings.LeftLegDrawUp) + " / " + GetKeyLabel(bindings.RightLegDrawUp) + "  CO CHÂN     " +
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
                   GetKeyLabel(bindings.BothPunch) + "  " + bothLabel + "</color>     <color=#b866ff>" +
                   GetKeyLabel(bindings.OverheadClap) + "  VỖ TAY TRÊN ĐẦU</color>\nGIỮ " + GetKeyLabel(bindings.Duck) +
                   "  CÚI     GIỮ " + GetKeyLabel(bindings.Jump) + "  NHẢY     GIỮ " +
                   GetKeyLabel(bindings.DodgeLeft) + " / " + GetKeyLabel(bindings.DodgeRight) + "  NÉ\nGIỮ " +
                   GetKeyLabel(bindings.LeftLegDrawUp) + "  CO CHÂN TRÁI     GIỮ " +
                   GetKeyLabel(bindings.RightLegDrawUp) + "  CO CHÂN PHẢI";
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
            if (action == GameplayAction.LeftPunch || action == GameplayAction.DodgeLeft ||
                action == GameplayAction.LeftLegDrawUp)
            {
                return materials.CyanColor;
            }

            if (action == GameplayAction.RightPunch || action == GameplayAction.DodgeRight ||
                action == GameplayAction.RightLegDrawUp)
            {
                return materials.MagentaColor;
            }

            if (action == GameplayAction.OverheadClap)
            {
                return materials.PurpleColor;
            }

            return materials.YellowColor;
        }

        private static bool RequiresHold(GameplayAction action)
        {
            return action == GameplayAction.Duck || action == GameplayAction.Jump ||
                   action == GameplayAction.DodgeLeft || action == GameplayAction.DodgeRight ||
                   action == GameplayAction.LeftLegDrawUp || action == GameplayAction.RightLegDrawUp;
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

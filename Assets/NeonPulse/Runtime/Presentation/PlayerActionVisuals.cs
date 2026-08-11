using UnityEngine;
using UnityEngine.Rendering;

namespace NeonPulse
{
    /// <summary>First-person glove or sword feedback that makes keyboard actions immediately visible.</summary>
    public sealed class PlayerActionVisuals
    {
        private const float SlashDurationMultiplier = 1.35f;
        private const float JumpDuration = 0.54f;

        private static readonly Vector3 LeftRestPosition = new Vector3(-0.92f, -0.67f, 1.45f);
        private static readonly Vector3 RightRestPosition = new Vector3(0.92f, -0.67f, 1.45f);
        private static readonly Vector3 LeftOverheadClapPosition = new Vector3(-0.12f, 0.82f, 1.65f);
        private static readonly Vector3 RightOverheadClapPosition = new Vector3(0.12f, 0.82f, 1.65f);

        private readonly Transform leftGlove;
        private readonly Transform rightGlove;
        private readonly Transform cameraTransform;
        private readonly Vector3 cameraRestPosition;
        private readonly Quaternion cameraRestRotation;
        private readonly CameraFeelSettings feel;
        private readonly bool showPunchHands;
        private readonly bool showSlashWeapons;
        private bool usesSwords;
        private bool handsVisible = true;
        private readonly GameObject leftSwordVisual;
        private readonly GameObject rightSwordVisual;
        private float leftPunchTimer;
        private float rightPunchTimer;
        private float overheadClapTimer;
        private float leftSlashDirection = 1f;
        private float rightSlashDirection = -1f;
        private float jumpTimer;
        private bool wasJumpHeld;
        private GameplayAction heldAction;
        private bool hasHeldAction;
        private Vector3 currentBodyOffset;
        private Vector3 currentCameraPostureOffset;
        private Vector3 bodyOffsetVelocity;
        private Vector3 cameraPostureVelocity;
        private float jumpCameraOffset;
        private float currentCameraPostureRoll;
        private float cameraPostureRollVelocity;
        private float shakeTimer;
        private float shakeDuration;
        private float shakeAmplitude;

        /// <summary>Creates the two cached glove hierarchies as children of the gameplay camera.</summary>
        public PlayerActionVisuals(Camera camera, RuntimeMaterialLibrary materials, NeonPulseGameConfig config)
        {
            feel = config != null ? config.CameraFeel : new CameraFeelSettings();
            showPunchHands = config != null && config.Visuals.ShowPunchHands;
            showSlashWeapons = config != null && config.Visuals.ShowSlashWeapons;
            usesSwords = config != null && config.GameplayMode == CombatGameplayMode.Slash;
            if (camera == null || materials == null)
            {
                return;
            }

            cameraTransform = camera.transform;
            cameraRestPosition = cameraTransform.localPosition;
            cameraRestRotation = cameraTransform.localRotation;
            leftGlove = CreateGlove("Left Cyan Glove", cameraTransform, LeftRestPosition, materials.Cyan);
            rightGlove = CreateGlove("Right Magenta Glove", cameraTransform, RightRestPosition, materials.Magenta);
            leftSwordVisual = CreateSword(leftGlove, "Left Sword", materials.Cyan, materials.CyanGlow, materials.Dark);
            rightSwordVisual = CreateSword(rightGlove, "Right Sword", materials.Magenta, materials.MagentaGlow, materials.Dark);
            SetCombatMode(usesSwords ? CombatGameplayMode.Slash : CombatGameplayMode.Punch);
        }

        /// <summary>Switches the cached first-person visual when the active level phase changes.</summary>
        public void SetCombatMode(CombatGameplayMode mode)
        {
            usesSwords = mode == CombatGameplayMode.Slash;
            bool showHandRoots = handsVisible && (usesSwords ? showSlashWeapons : showPunchHands);
            if (leftGlove != null)
            {
                leftGlove.gameObject.SetActive(showHandRoots);
            }

            if (rightGlove != null)
            {
                rightGlove.gameObject.SetActive(showHandRoots);
            }

            if (leftSwordVisual != null)
            {
                leftSwordVisual.SetActive(showHandRoots && usesSwords);
            }

            if (rightSwordVisual != null)
            {
                rightSwordVisual.SetActive(showHandRoots && usesSwords);
            }
        }

        /// <summary>Hides first-person hands for footwork phases without destroying their cached hierarchy.</summary>
        public void SetHandsVisible(bool visible)
        {
            if (handsVisible == visible)
            {
                return;
            }

            handsVisible = visible;
            SetCombatMode(usesSwords ? CombatGameplayMode.Slash : CombatGameplayMode.Punch);
        }

        /// <summary>Starts cached procedural feedback for the requested action.</summary>
        public void Trigger(GameplayAction action, float authoredSlashDirection = 0f)
        {
            switch (action)
            {
                case GameplayAction.LeftPunch:
                    leftPunchTimer = GetAttackDuration();
                    if (usesSwords)
                    {
                        leftSlashDirection = ResolveSlashDirection(authoredSlashDirection);
                    }
                    StartShake(feel.PunchShakeAmplitude, feel.PunchShakeDuration);
                    break;
                case GameplayAction.RightPunch:
                    rightPunchTimer = GetAttackDuration();
                    if (usesSwords)
                    {
                        rightSlashDirection = ResolveSlashDirection(authoredSlashDirection);
                    }
                    StartShake(feel.PunchShakeAmplitude, feel.PunchShakeDuration);
                    break;
                case GameplayAction.BothPunch:
                    leftPunchTimer = GetAttackDuration();
                    rightPunchTimer = GetAttackDuration();
                    if (usesSwords)
                    {
                        leftSlashDirection = ResolveSlashDirection(authoredSlashDirection);
                        rightSlashDirection = -leftSlashDirection;
                    }
                    StartShake(feel.BothPunchShakeAmplitude, feel.BothPunchShakeDuration);
                    break;
                case GameplayAction.OverheadClap:
                    leftPunchTimer = 0f;
                    rightPunchTimer = 0f;
                    overheadClapTimer = feel.PunchDuration;
                    StartShake(feel.BothPunchShakeAmplitude, feel.BothPunchShakeDuration);
                    break;
            }
        }

        /// <summary>Updates the continuously held fitness pose from cached input state.</summary>
        public void SetHeldInput(bool duck, bool jump, bool dodgeLeft, bool dodgeRight)
        {
            if (jump && !wasJumpHeld)
            {
                jumpTimer = JumpDuration;
            }

            wasJumpHeld = jump;
            hasHeldAction = duck || dodgeLeft || dodgeRight;
            if (duck)
            {
                heldAction = GameplayAction.Duck;
            }
            else if (dodgeLeft)
            {
                heldAction = GameplayAction.DodgeLeft;
            }
            else if (dodgeRight)
            {
                heldAction = GameplayAction.DodgeRight;
            }
        }

        /// <summary>Starts a stronger camera shake for missed notes or broken obstacle holds.</summary>
        public void TriggerFailShake()
        {
            StartShake(feel.FailShakeAmplitude, feel.FailShakeDuration);
        }

        /// <summary>Plays a short, subtle camera impact when a rhythm tile reaches the line.</summary>
        public void TriggerRhythmTileImpactShake()
        {
            StartShake(feel.RhythmTileShakeAmplitude, feel.RhythmTileShakeDuration);
        }

        public float JumpLeadTime => JumpDuration * 0.5f;
        public bool UsesSlashVisual => usesSwords;

        /// <summary>Updates glove positions without tweens, coroutines, or per-frame allocations.</summary>
        public void Tick(float deltaTime)
        {
            if (leftGlove == null || rightGlove == null || cameraTransform == null)
            {
                return;
            }

            leftPunchTimer = Mathf.Max(0f, leftPunchTimer - deltaTime);
            rightPunchTimer = Mathf.Max(0f, rightPunchTimer - deltaTime);
            overheadClapTimer = Mathf.Max(0f, overheadClapTimer - deltaTime);
            jumpTimer = Mathf.Max(0f, jumpTimer - deltaTime);

            Vector3 targetBodyOffset = CalculateHeldBodyOffset();
            // Critically damped motion keeps lateral dodges fluid when the player switches side.
            float smoothTime = 1f / Mathf.Max(1f, feel.PoseSmoothing);
            currentBodyOffset = Vector3.SmoothDamp(currentBodyOffset, targetBodyOffset, ref bodyOffsetVelocity, smoothTime, Mathf.Infinity, deltaTime);
            currentCameraPostureOffset = Vector3.SmoothDamp(currentCameraPostureOffset, CalculateCameraPostureOffset(), ref cameraPostureVelocity, smoothTime, Mathf.Infinity, deltaTime);
            currentCameraPostureRoll = Mathf.SmoothDamp(currentCameraPostureRoll, CalculateCameraPostureRoll(), ref cameraPostureRollVelocity, smoothTime, Mathf.Infinity, deltaTime);
            float jumpOffset = CalculateJumpOffset();
            Vector3 displayedBodyOffset = currentBodyOffset + Vector3.up * (jumpOffset * 0.3f);
            jumpCameraOffset = jumpOffset;
            float leftThrust = CalculateThrust(leftPunchTimer);
            float rightThrust = CalculateThrust(rightPunchTimer);
            float overheadClapThrust = CalculateThrust(overheadClapTimer);
            if (overheadClapThrust > 0f)
            {
                leftGlove.localPosition = Vector3.Lerp(
                    LeftRestPosition + displayedBodyOffset,
                    LeftOverheadClapPosition + displayedBodyOffset,
                    overheadClapThrust);
                rightGlove.localPosition = Vector3.Lerp(
                    RightRestPosition + displayedBodyOffset,
                    RightOverheadClapPosition + displayedBodyOffset,
                    overheadClapThrust);
                leftGlove.localRotation = Quaternion.Euler(-18f, 0f, Mathf.Lerp(-8f, -78f, overheadClapThrust));
                rightGlove.localRotation = Quaternion.Euler(-18f, 0f, Mathf.Lerp(8f, 78f, overheadClapThrust));
            }
            else if (usesSwords)
            {
                float leftSlashAngle = CalculateSlashAngle(leftPunchTimer, leftSlashDirection);
                float rightSlashAngle = CalculateSlashAngle(rightPunchTimer, rightSlashDirection);
                leftGlove.localPosition = LeftRestPosition + displayedBodyOffset + CalculateSlashOffset(leftThrust, 0.1f, leftSlashAngle);
                rightGlove.localPosition = RightRestPosition + displayedBodyOffset + CalculateSlashOffset(rightThrust, -0.1f, rightSlashAngle);
                leftGlove.localRotation = Quaternion.Euler(-14f - leftThrust * 24f, 9f + leftSlashDirection * leftThrust * 7f, -14f + leftSlashAngle);
                rightGlove.localRotation = Quaternion.Euler(-14f - rightThrust * 24f, -9f + rightSlashDirection * rightThrust * 7f, 14f + rightSlashAngle);
            }
            else
            {
                leftGlove.localPosition = LeftRestPosition + displayedBodyOffset + CalculatePunchOffset(leftPunchTimer, 0.16f);
                rightGlove.localPosition = RightRestPosition + displayedBodyOffset + CalculatePunchOffset(rightPunchTimer, -0.16f);
                leftGlove.localRotation = Quaternion.Euler(-12f - leftThrust * 22f, 10f, -8f);
                rightGlove.localRotation = Quaternion.Euler(-12f - rightThrust * 22f, -10f, 8f);
            }

            TickCameraShake(deltaTime);
        }

        private Vector3 CalculateHeldBodyOffset()
        {
            if (!hasHeldAction)
            {
                return Vector3.zero;
            }

            switch (heldAction)
            {
                case GameplayAction.Duck:
                    return new Vector3(0f, -feel.DuckDistance * 0.3f, 0f);
                case GameplayAction.DodgeLeft:
                    return new Vector3(-feel.DodgeDistance * 0.16f, 0f, 0f);
                case GameplayAction.DodgeRight:
                    return new Vector3(feel.DodgeDistance * 0.16f, 0f, 0f);
                default:
                    return Vector3.zero;
            }
        }

        private Vector3 CalculateCameraPostureOffset()
        {
            if (!hasHeldAction)
            {
                return Vector3.zero;
            }

            switch (heldAction)
            {
                case GameplayAction.Duck: return new Vector3(0f, -feel.DuckDistance, 0f);
                case GameplayAction.DodgeLeft: return new Vector3(-feel.DodgeDistance, 0f, 0f);
                case GameplayAction.DodgeRight: return new Vector3(feel.DodgeDistance, 0f, 0f);
                default: return Vector3.zero;
            }
        }

        private float CalculateCameraPostureRoll()
        {
            if (!hasHeldAction)
            {
                return 0f;
            }

            switch (heldAction)
            {
                case GameplayAction.DodgeLeft: return feel.DodgeCameraRoll;
                case GameplayAction.DodgeRight: return -feel.DodgeCameraRoll;
                default: return 0f;
            }
        }

        private void StartShake(float amplitude, float duration)
        {
            if (shakeTimer > 0f && shakeAmplitude > amplitude)
            {
                return;
            }

            shakeAmplitude = amplitude;
            shakeDuration = duration;
            shakeTimer = duration;
        }

        private void TickCameraShake(float deltaTime)
        {
            if (shakeTimer <= 0f)
            {
                cameraTransform.localPosition = cameraRestPosition + currentCameraPostureOffset + Vector3.up * jumpCameraOffset;
                cameraTransform.localRotation = cameraRestRotation * Quaternion.Euler(0f, 0f, currentCameraPostureRoll);
                return;
            }

            shakeTimer = Mathf.Max(0f, shakeTimer - deltaTime);
            float envelope = shakeDuration > 0f ? shakeTimer / shakeDuration : 0f;
            float time = Time.unscaledTime;
            float x = Mathf.Sin(time * 91f) * shakeAmplitude * envelope;
            float y = Mathf.Sin(time * 67f + 1.7f) * shakeAmplitude * 0.65f * envelope;
            float roll = Mathf.Sin(time * 79f + 0.4f) * shakeAmplitude * 18f * envelope;
            cameraTransform.localPosition = cameraRestPosition + currentCameraPostureOffset + new Vector3(x, y + jumpCameraOffset, 0f);
            cameraTransform.localRotation = cameraRestRotation * Quaternion.Euler(y * 10f, x * 12f, currentCameraPostureRoll + roll);
        }

        private Vector3 CalculatePunchOffset(float timer, float inwardDirection)
        {
            float thrust = CalculateThrust(timer);
            return new Vector3(inwardDirection * thrust, 0.22f * thrust, feel.PunchDistance * thrust);
        }

        private Vector3 CalculateSlashOffset(float thrust, float inwardDirection, float slashAngle)
        {
            float sweep = Mathf.Sin(slashAngle * Mathf.Deg2Rad) * 0.28f;
            return new Vector3(inwardDirection * thrust + sweep, 0.1f * thrust, feel.PunchDistance * 0.42f * thrust);
        }

        private float CalculateSlashAngle(float timer, float direction)
        {
            float duration = GetAttackDuration();
            if (timer <= 0f || duration <= 0f)
            {
                return 0f;
            }

            float phase = 1f - timer / duration;
            if (phase < 0.18f)
            {
                float windUpPhase = Mathf.SmoothStep(0f, 1f, phase / 0.18f);
                return Mathf.Lerp(0f, -56f, windUpPhase) * direction;
            }

            if (phase < 0.6f)
            {
                float attackPhase = Mathf.SmoothStep(0f, 1f, (phase - 0.18f) / 0.42f);
                return Mathf.Lerp(-56f, 72f, attackPhase) * direction;
            }

            float recoverPhase = Mathf.SmoothStep(0f, 1f, (phase - 0.6f) / 0.4f);
            return Mathf.Lerp(72f, 0f, recoverPhase) * direction;
        }

        private static float ResolveSlashDirection(float authoredDirection)
        {
            if (Mathf.Abs(authoredDirection) > 0.01f)
            {
                return Mathf.Sign(authoredDirection);
            }

            return Random.value < 0.5f ? -1f : 1f;
        }

        private float CalculateThrust(float timer)
        {
            if (timer <= 0f)
            {
                return 0f;
            }

            float phase = 1f - timer / GetAttackDuration();
            return Mathf.Sin(phase * Mathf.PI);
        }

        private float CalculateJumpOffset()
        {
            if (jumpTimer <= 0f)
            {
                return 0f;
            }

            float phase = 1f - jumpTimer / JumpDuration;
            return Mathf.Sin(phase * Mathf.PI) * feel.JumpDistance * 0.62f;
        }

        private float GetAttackDuration()
        {
            return usesSwords ? feel.PunchDuration * SlashDurationMultiplier : feel.PunchDuration;
        }

        private static Transform CreateGlove(string objectName, Transform parent, Vector3 localPosition, Material material)
        {
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            CreatePart(root.transform, PrimitiveType.Cube, "Palm", Vector3.zero, new Vector3(0.52f, 0.34f, 0.62f), material);
            CreatePart(root.transform, PrimitiveType.Sphere, "Knuckle 1", new Vector3(-0.19f, 0.18f, 0.18f), Vector3.one * 0.25f, material);
            CreatePart(root.transform, PrimitiveType.Sphere, "Knuckle 2", new Vector3(0f, 0.2f, 0.22f), Vector3.one * 0.27f, material);
            CreatePart(root.transform, PrimitiveType.Sphere, "Knuckle 3", new Vector3(0.19f, 0.18f, 0.18f), Vector3.one * 0.25f, material);
            return root.transform;
        }

        private static GameObject CreateSword(Transform hand, string objectName, Material bladeMaterial, Material glowMaterial, Material handleMaterial)
        {
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(hand, false);
            root.transform.localPosition = new Vector3(0f, 0.08f, 0.1f);

            CreatePart(root.transform, PrimitiveType.Cube, "Handle", new Vector3(0f, 0.2f, 0f), new Vector3(0.12f, 0.4f, 0.12f), handleMaterial);
            CreatePart(root.transform, PrimitiveType.Cube, "Guard", new Vector3(0f, 0.43f, 0f), new Vector3(0.42f, 0.08f, 0.14f), bladeMaterial);
            CreatePart(root.transform, PrimitiveType.Cube, "Blade Glow", new Vector3(0f, 1.04f, 0f), new Vector3(0.16f, 1.18f, 0.07f), glowMaterial);
            CreatePart(root.transform, PrimitiveType.Cube, "Blade", new Vector3(0f, 1.04f, -0.045f), new Vector3(0.08f, 1.14f, 0.05f), bladeMaterial);
            return root;
        }

        private static void CreatePart(Transform parent, PrimitiveType type, string objectName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            if (part.TryGetComponent(out Collider partCollider))
            {
                Object.Destroy(partCollider);
            }

            if (part.TryGetComponent(out Renderer partRenderer))
            {
                partRenderer.sharedMaterial = material;
                partRenderer.shadowCastingMode = ShadowCastingMode.Off;
                partRenderer.receiveShadows = false;
            }
        }
    }
}

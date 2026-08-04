using UnityEngine;
using UnityEngine.Rendering;

namespace NeonPulse
{
    /// <summary>First-person glove or sword feedback that makes keyboard actions immediately visible.</summary>
    public sealed class PlayerActionVisuals
    {
        private static readonly Vector3 LeftRestPosition = new Vector3(-0.92f, -0.67f, 1.45f);
        private static readonly Vector3 RightRestPosition = new Vector3(0.92f, -0.67f, 1.45f);

        private readonly Transform leftGlove;
        private readonly Transform rightGlove;
        private readonly Transform cameraTransform;
        private readonly Vector3 cameraRestPosition;
        private readonly Quaternion cameraRestRotation;
        private readonly CameraFeelSettings feel;
        private readonly bool usesSwords;
        private float leftPunchTimer;
        private float rightPunchTimer;
        private float leftSlashDirection = 1f;
        private float rightSlashDirection = -1f;
        private GameplayAction heldAction;
        private bool hasHeldAction;
        private Vector3 currentBodyOffset;
        private Vector3 currentCameraPostureOffset;
        private float shakeTimer;
        private float shakeDuration;
        private float shakeAmplitude;

        /// <summary>Creates the two cached glove hierarchies as children of the gameplay camera.</summary>
        public PlayerActionVisuals(Camera camera, RuntimeMaterialLibrary materials, NeonPulseGameConfig config)
        {
            feel = config != null ? config.CameraFeel : new CameraFeelSettings();
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
            if (usesSwords)
            {
                CreateSword(leftGlove, "Left Sword", materials.Cyan, materials.CyanGlow, materials.Dark);
                CreateSword(rightGlove, "Right Sword", materials.Magenta, materials.MagentaGlow, materials.Dark);
            }
        }

        /// <summary>Starts cached procedural feedback for the requested action.</summary>
        public void Trigger(GameplayAction action)
        {
            switch (action)
            {
                case GameplayAction.LeftPunch:
                    leftPunchTimer = feel.PunchDuration;
                    if (usesSwords)
                    {
                        leftSlashDirection = Random.value < 0.5f ? -1f : 1f;
                    }
                    StartShake(feel.PunchShakeAmplitude, feel.PunchShakeDuration);
                    break;
                case GameplayAction.RightPunch:
                    rightPunchTimer = feel.PunchDuration;
                    if (usesSwords)
                    {
                        rightSlashDirection = Random.value < 0.5f ? -1f : 1f;
                    }
                    StartShake(feel.PunchShakeAmplitude, feel.PunchShakeDuration);
                    break;
                case GameplayAction.BothPunch:
                    leftPunchTimer = feel.PunchDuration;
                    rightPunchTimer = feel.PunchDuration;
                    if (usesSwords)
                    {
                        leftSlashDirection = Random.value < 0.5f ? -1f : 1f;
                        rightSlashDirection = -leftSlashDirection;
                    }
                    StartShake(feel.BothPunchShakeAmplitude, feel.BothPunchShakeDuration);
                    break;
            }
        }

        /// <summary>Updates the continuously held fitness pose from cached input state.</summary>
        public void SetHeldInput(bool duck, bool jump, bool dodgeLeft, bool dodgeRight)
        {
            hasHeldAction = duck || jump || dodgeLeft || dodgeRight;
            if (duck)
            {
                heldAction = GameplayAction.Duck;
            }
            else if (jump)
            {
                heldAction = GameplayAction.Jump;
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

        /// <summary>Updates glove positions without tweens, coroutines, or per-frame allocations.</summary>
        public void Tick(float deltaTime)
        {
            if (leftGlove == null || rightGlove == null || cameraTransform == null)
            {
                return;
            }

            leftPunchTimer = Mathf.Max(0f, leftPunchTimer - deltaTime);
            rightPunchTimer = Mathf.Max(0f, rightPunchTimer - deltaTime);

            Vector3 targetBodyOffset = CalculateHeldBodyOffset();
            float smoothing = 1f - Mathf.Exp(-feel.PoseSmoothing * deltaTime);
            currentBodyOffset = Vector3.Lerp(currentBodyOffset, targetBodyOffset, smoothing);
            currentCameraPostureOffset = Vector3.Lerp(currentCameraPostureOffset, CalculateCameraPostureOffset(), smoothing);
            float leftThrust = CalculateThrust(leftPunchTimer);
            float rightThrust = CalculateThrust(rightPunchTimer);
            if (usesSwords)
            {
                leftGlove.localPosition = LeftRestPosition + currentBodyOffset + CalculateSlashOffset(leftThrust, 0.12f);
                rightGlove.localPosition = RightRestPosition + currentBodyOffset + CalculateSlashOffset(rightThrust, -0.12f);
                leftGlove.localRotation = Quaternion.Euler(-16f, 10f, -18f + CalculateSlashAngle(leftPunchTimer, leftSlashDirection));
                rightGlove.localRotation = Quaternion.Euler(-16f, -10f, 18f + CalculateSlashAngle(rightPunchTimer, rightSlashDirection));
            }
            else
            {
                leftGlove.localPosition = LeftRestPosition + currentBodyOffset + CalculatePunchOffset(leftPunchTimer, 0.16f);
                rightGlove.localPosition = RightRestPosition + currentBodyOffset + CalculatePunchOffset(rightPunchTimer, -0.16f);
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
                case GameplayAction.Jump:
                    return new Vector3(0f, feel.JumpDistance * 0.29f, 0f);
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
                case GameplayAction.Jump: return new Vector3(0f, feel.JumpDistance, 0f);
                case GameplayAction.DodgeLeft: return new Vector3(-feel.DodgeDistance, 0f, 0f);
                case GameplayAction.DodgeRight: return new Vector3(feel.DodgeDistance, 0f, 0f);
                default: return Vector3.zero;
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
                cameraTransform.localPosition = cameraRestPosition + currentCameraPostureOffset;
                cameraTransform.localRotation = cameraRestRotation;
                return;
            }

            shakeTimer = Mathf.Max(0f, shakeTimer - deltaTime);
            float envelope = shakeDuration > 0f ? shakeTimer / shakeDuration : 0f;
            float time = Time.unscaledTime;
            float x = Mathf.Sin(time * 91f) * shakeAmplitude * envelope;
            float y = Mathf.Sin(time * 67f + 1.7f) * shakeAmplitude * 0.65f * envelope;
            float roll = Mathf.Sin(time * 79f + 0.4f) * shakeAmplitude * 18f * envelope;
            cameraTransform.localPosition = cameraRestPosition + currentCameraPostureOffset + new Vector3(x, y, 0f);
            cameraTransform.localRotation = cameraRestRotation * Quaternion.Euler(y * 10f, x * 12f, roll);
        }

        private Vector3 CalculatePunchOffset(float timer, float inwardDirection)
        {
            float thrust = CalculateThrust(timer);
            return new Vector3(inwardDirection * thrust, 0.22f * thrust, feel.PunchDistance * thrust);
        }

        private Vector3 CalculateSlashOffset(float thrust, float inwardDirection)
        {
            return new Vector3(inwardDirection * thrust, 0.12f * thrust, feel.PunchDistance * 0.48f * thrust);
        }

        private float CalculateSlashAngle(float timer, float direction)
        {
            if (timer <= 0f || feel.PunchDuration <= 0f)
            {
                return 0f;
            }

            float phase = 1f - timer / feel.PunchDuration;
            if (phase < 0.72f)
            {
                float attackPhase = Mathf.SmoothStep(0f, 1f, phase / 0.72f);
                return Mathf.Lerp(-72f, 68f, attackPhase) * direction;
            }

            float recoverPhase = Mathf.SmoothStep(0f, 1f, (phase - 0.72f) / 0.28f);
            return Mathf.Lerp(68f, 0f, recoverPhase) * direction;
        }

        private float CalculateThrust(float timer)
        {
            if (timer <= 0f)
            {
                return 0f;
            }

            float phase = 1f - timer / feel.PunchDuration;
            return Mathf.Sin(phase * Mathf.PI);
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

        private static void CreateSword(Transform hand, string objectName, Material bladeMaterial, Material glowMaterial, Material handleMaterial)
        {
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(hand, false);
            root.transform.localPosition = new Vector3(0f, 0.18f, 0.12f);

            CreatePart(root.transform, PrimitiveType.Cube, "Handle", new Vector3(0f, 0.35f, 0f), new Vector3(0.16f, 0.7f, 0.16f), handleMaterial);
            CreatePart(root.transform, PrimitiveType.Cube, "Guard", new Vector3(0f, 0.72f, 0f), new Vector3(0.62f, 0.12f, 0.2f), bladeMaterial);
            CreatePart(root.transform, PrimitiveType.Cube, "Blade Glow", new Vector3(0f, 1.72f, 0f), new Vector3(0.25f, 1.95f, 0.1f), glowMaterial);
            CreatePart(root.transform, PrimitiveType.Cube, "Blade", new Vector3(0f, 1.72f, -0.06f), new Vector3(0.13f, 1.9f, 0.08f), bladeMaterial);
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

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonPulse
{
    /// <summary>A pooled note or obstacle whose Z position is derived from absolute song time.</summary>
    public sealed class BeatTraveller : MonoBehaviour
    {
        private static readonly float[] LaneX = { -2.7f, -0.9f, 0.9f, 2.7f };

        private readonly GameObject[] variants = new GameObject[7];
        private GameplayAction action;
        private float targetTime;
        private float spawnTime;
        private float spawnZ;
        private bool initialized;
        private float hitZ;
        private float despawnZ;
        private float labelVisibleZ;
        private float targetGlowScale;
        private bool isSlashMode;
        private Transform leftSlashIndicator;
        private Transform rightSlashIndicator;
        private Transform bothLeftSlashIndicator;
        private Transform bothRightSlashIndicator;
        private TextMeshPro actionLabel;
        private Transform actionLabelTransform;

        public GameplayAction Action => action;
        public float TargetTime => targetTime;
        public float SlashDirection { get; private set; }
        public bool IsActive { get; private set; }
        public bool RequiresHold => IsObstacle(action);
        public bool HoldEvaluationStarted { get; private set; }
        public bool HoldInputConfirmed { get; private set; }

        /// <summary>Builds all visual variants once so pooled reuse only toggles cached objects.</summary>
        public void Initialize(RuntimeMaterialLibrary materials, NeonPulseGameConfig config)
        {
            if (initialized || materials == null || config == null)
            {
                return;
            }

            hitZ = config.Rhythm.HitZ;
            despawnZ = config.Rhythm.DespawnZ;
            labelVisibleZ = config.Rhythm.LabelVisibleZ;
            targetGlowScale = config.Visuals.TargetGlowScale;
            isSlashMode = config.GameplayMode == CombatGameplayMode.Slash;

            if (isSlashMode)
            {
                variants[(int)GameplayAction.LeftPunch] = CreateSlashVariant("Left Slash", materials.Cyan, materials.CyanGlow, out leftSlashIndicator);
                variants[(int)GameplayAction.RightPunch] = CreateSlashVariant("Right Slash", materials.Magenta, materials.MagentaGlow, out rightSlashIndicator);
                variants[(int)GameplayAction.BothPunch] = CreateSlashPairVariant(
                    materials.Cyan, materials.Magenta, materials.CyanGlow, materials.MagentaGlow);
            }
            else
            {
                variants[(int)GameplayAction.LeftPunch] = CreatePunchVariant("Left Punch", materials.Cyan, materials.CyanGlow);
                variants[(int)GameplayAction.RightPunch] = CreatePunchVariant("Right Punch", materials.Magenta, materials.MagentaGlow);
                variants[(int)GameplayAction.BothPunch] = CreatePairVariant(materials.Cyan, materials.Magenta, materials.CyanGlow, materials.MagentaGlow);
            }
            variants[(int)GameplayAction.Duck] = CreateBarVariant("Duck Gate", materials.Obstacle, materials.ObstacleGlow, new Vector3(7.8f, 1.15f, 0.75f), new Vector3(0f, 3.25f, 0f));
            variants[(int)GameplayAction.Jump] = CreateBarVariant("Jump Gate", materials.Obstacle, materials.ObstacleGlow, new Vector3(7.8f, 1.05f, 0.75f), new Vector3(0f, 0.35f, 0f));
            variants[(int)GameplayAction.DodgeLeft] = CreateDodgeVariant("Dodge Left", materials.Obstacle, materials.ObstacleGlow, materials.Cyan, true);
            variants[(int)GameplayAction.DodgeRight] = CreateDodgeVariant("Dodge Right", materials.Obstacle, materials.ObstacleGlow, materials.Magenta, false);

            for (int i = 0; i < variants.Length; i++)
            {
                variants[i].SetActive(false);
            }

            if (!config.AutoPlay)
            {
                CreateActionLabel();
            }
            initialized = true;
            gameObject.SetActive(false);
        }

        /// <summary>Activates this object for a chart event without allocating memory.</summary>
        public void Spawn(BeatmapEvent chartEvent, float eventTime, float eventSpawnTime, float startZ)
        {
            action = chartEvent.Action;
            targetTime = eventTime;
            spawnTime = eventSpawnTime;
            spawnZ = startZ;
            HoldEvaluationStarted = false;
            HoldInputConfirmed = false;
            SlashDirection = 0f;

            float x = IsObstacle(action) || action == GameplayAction.BothPunch
                ? 0f
                : LaneX[Mathf.Clamp(chartEvent.Lane, 0, 3)];
            transform.SetPositionAndRotation(new Vector3(x, 0f, startZ), Quaternion.identity);
            ConfigureSlashIndicators();
            variants[(int)action].SetActive(true);
            ConfigureActionLabel(action);
            IsActive = true;
            gameObject.SetActive(true);
        }

        /// <summary>Updates movement from absolute time, preventing cumulative rhythm drift.</summary>
        public void Tick(float songTime)
        {
            float travelDuration = targetTime - spawnTime;
            float normalized = travelDuration > 0.001f ? (songTime - spawnTime) / travelDuration : 1f;
            float z = normalized <= 1f
                ? Mathf.LerpUnclamped(spawnZ, hitZ, normalized)
                : Mathf.LerpUnclamped(hitZ, despawnZ, (songTime - targetTime) / 0.45f);

            Vector3 position = transform.position;
            position.z = z;
            transform.position = position;

            if (actionLabelTransform != null)
            {
                actionLabelTransform.rotation = Quaternion.identity;
                bool shouldShowLabel = z <= labelVisibleZ && z >= -1f;
                if (actionLabel.gameObject.activeSelf != shouldShowLabel)
                {
                    actionLabel.gameObject.SetActive(shouldShowLabel);
                }
            }
        }

        /// <summary>Marks the beginning of the continuous hold-validation window.</summary>
        public void BeginHoldEvaluation()
        {
            HoldEvaluationStarted = true;
        }

        /// <summary>Marks that the correct obstacle key is held inside its activation grace period.</summary>
        public void ConfirmHoldInput()
        {
            HoldInputConfirmed = true;
        }

        /// <summary>Deactivates the current visual before returning this object to its pool.</summary>
        public void Despawn()
        {
            if (!initialized)
            {
                return;
            }

            variants[(int)action].SetActive(false);
            if (actionLabel != null)
            {
                actionLabel.gameObject.SetActive(false);
            }

            IsActive = false;
            HoldEvaluationStarted = false;
            HoldInputConfirmed = false;
            gameObject.SetActive(false);
        }

        private void CreateActionLabel()
        {
            GameObject labelObject = new GameObject("Action Prompt", typeof(RectTransform));
            labelObject.transform.SetParent(transform, false);
            actionLabelTransform = labelObject.transform;
            actionLabel = labelObject.AddComponent<TextMeshPro>();

            TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings != null && TMP_Settings.defaultFontAsset != null)
            {
                actionLabel.font = TMP_Settings.defaultFontAsset;
            }

            actionLabel.fontSize = 5.2f;
            actionLabel.fontStyle = FontStyles.Bold;
            actionLabel.alignment = TextAlignmentOptions.Center;
            actionLabel.enableWordWrapping = false;
            actionLabel.outlineWidth = 0.18f;
            actionLabel.outlineColor = new Color32(8, 2, 22, 255);
            actionLabel.rectTransform.sizeDelta = new Vector2(18f, 7f);
            actionLabelTransform.localScale = Vector3.one * 0.11f;
            labelObject.SetActive(false);
        }

        private void ConfigureActionLabel(GameplayAction value)
        {
            if (actionLabel == null)
            {
                return;
            }

            switch (value)
            {
                case GameplayAction.LeftPunch:
                    actionLabel.text = isSlashMode ? "Q\nKIEM TRAI" : "Q\nTAY TRAI";
                    actionLabel.color = new Color(0.02f, 1f, 0.95f, 1f);
                    actionLabelTransform.localPosition = new Vector3(0f, 3.05f, -0.3f);
                    break;
                case GameplayAction.RightPunch:
                    actionLabel.text = isSlashMode ? "E\nKIEM PHAI" : "E\nTAY PHAI";
                    actionLabel.color = new Color(1f, 0.03f, 0.72f, 1f);
                    actionLabelTransform.localPosition = new Vector3(0f, 3.05f, -0.3f);
                    break;
                case GameplayAction.BothPunch:
                    actionLabel.text = isSlashMode ? "F\nHAI KIEM" : "F\nCA HAI TAY";
                    actionLabel.color = new Color(1f, 0.82f, 0.05f, 1f);
                    actionLabelTransform.localPosition = new Vector3(0f, 3.05f, -0.3f);
                    break;
                case GameplayAction.Duck:
                    actionLabel.text = "GIU S\nCUI NGUOI";
                    actionLabel.color = Color.white;
                    actionLabelTransform.localPosition = new Vector3(0f, 4.35f, -0.45f);
                    break;
                case GameplayAction.Jump:
                    actionLabel.text = "GIU SPACE\nNHAY";
                    actionLabel.color = new Color(1f, 0.82f, 0.05f, 1f);
                    actionLabelTransform.localPosition = new Vector3(0f, 2.15f, -0.45f);
                    break;
                case GameplayAction.DodgeLeft:
                    actionLabel.text = "GIU A   <<<\nNE TRAI";
                    actionLabel.color = new Color(0.02f, 1f, 0.95f, 1f);
                    actionLabelTransform.localPosition = new Vector3(-1.8f, 4.6f, -0.45f);
                    break;
                default:
                    actionLabel.text = ">>>   GIU D\nNE PHAI";
                    actionLabel.color = new Color(1f, 0.03f, 0.72f, 1f);
                    actionLabelTransform.localPosition = new Vector3(1.8f, 4.6f, -0.45f);
                    break;
            }

            actionLabel.gameObject.SetActive(true);
        }

        private GameObject CreatePunchVariant(string objectName, Material material, Material glowMaterial)
        {
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 1.8f, 0f);

            Vector3 cubeScale = new Vector3(1.15f, 1.15f, 0.72f);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Punch Cube Aura", Vector3.zero, cubeScale * targetGlowScale, glowMaterial);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Punch Cube", new Vector3(0f, 0f, -0.06f), cubeScale, material);

            return root;
        }

        private GameObject CreatePairVariant(Material leftMaterial, Material rightMaterial, Material leftGlow, Material rightGlow)
        {
            GameObject root = new GameObject("Both Punch");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            Vector3 cubeScale = new Vector3(1f, 1f, 0.68f);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Left Cube Aura", new Vector3(-1.25f, 0f, 0f), cubeScale * targetGlowScale, leftGlow);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Right Cube Aura", new Vector3(1.25f, 0f, 0f), cubeScale * targetGlowScale, rightGlow);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Left Cube", new Vector3(-1.25f, 0f, -0.06f), cubeScale, leftMaterial);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Right Cube", new Vector3(1.25f, 0f, -0.06f), cubeScale, rightMaterial);
            return root;
        }

        private GameObject CreateSlashVariant(string objectName, Material material, Material glowMaterial, out Transform indicator)
        {
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 1.8f, 0f);

            CreatePrimitive(root.transform, PrimitiveType.Cube, "Slash Aura", Vector3.zero, new Vector3(1.4f, 1.4f, 0.38f) * targetGlowScale, glowMaterial);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Slash Block", new Vector3(0f, 0f, -0.08f), new Vector3(1.4f, 1.4f, 0.38f), material);
            indicator = CreatePrimitive(root.transform, PrimitiveType.Cube, "Slash Direction", new Vector3(0f, 0f, -0.31f), new Vector3(0.14f, 1.05f, 0.06f), glowMaterial).transform;
            return root;
        }

        private GameObject CreateSlashPairVariant(Material leftMaterial, Material rightMaterial, Material leftGlow, Material rightGlow)
        {
            GameObject root = new GameObject("Both Slash");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 1.8f, 0f);

            CreateSlashBlock(root.transform, "Left", new Vector3(-1.25f, 0f, 0f), leftMaterial, leftGlow, out bothLeftSlashIndicator);
            CreateSlashBlock(root.transform, "Right", new Vector3(1.25f, 0f, 0f), rightMaterial, rightGlow, out bothRightSlashIndicator);
            return root;
        }

        private void CreateSlashBlock(
            Transform parent,
            string prefix,
            Vector3 localPosition,
            Material material,
            Material glowMaterial,
            out Transform indicator)
        {
            CreatePrimitive(parent, PrimitiveType.Cube, prefix + " Slash Aura", localPosition, new Vector3(1.2f, 1.2f, 0.34f) * targetGlowScale, glowMaterial);
            CreatePrimitive(parent, PrimitiveType.Cube, prefix + " Slash Block", localPosition + new Vector3(0f, 0f, -0.08f), new Vector3(1.2f, 1.2f, 0.34f), material);
            indicator = CreatePrimitive(parent, PrimitiveType.Cube, prefix + " Slash Direction", localPosition + new Vector3(0f, 0f, -0.29f), new Vector3(0.13f, 0.9f, 0.05f), glowMaterial).transform;
        }

        private void ConfigureSlashIndicators()
        {
            if (!isSlashMode || IsObstacle(action))
            {
                return;
            }

            float primaryAngle = Random.value < 0.5f ? -45f : 45f;
            SlashDirection = Mathf.Sign(primaryAngle);
            Transform primaryIndicator = action == GameplayAction.LeftPunch ? leftSlashIndicator : rightSlashIndicator;
            if (action == GameplayAction.BothPunch)
            {
                primaryIndicator = bothLeftSlashIndicator;
            }

            if (primaryIndicator != null)
            {
                primaryIndicator.localRotation = Quaternion.Euler(0f, 0f, primaryAngle);
            }

            if (action == GameplayAction.BothPunch && bothRightSlashIndicator != null)
            {
                bothRightSlashIndicator.localRotation = Quaternion.Euler(0f, 0f, -primaryAngle);
            }
        }

        private GameObject CreateBarVariant(string objectName, Material material, Material glowMaterial, Vector3 scale, Vector3 localPosition)
        {
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(transform, false);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Gate Aura", localPosition, scale + new Vector3(0.28f, 0.2f, 0.14f), glowMaterial);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Gate", localPosition, scale, material);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Gate Accent", localPosition + new Vector3(0f, 0f, -0.45f), new Vector3(scale.x + 0.2f, 0.12f, 0.12f), glowMaterial);
            return root;
        }

        private GameObject CreateDodgeVariant(string objectName, Material material, Material glowMaterial, Material safeMaterial, bool openingOnLeft)
        {
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(transform, false);
            float wallX = openingOnLeft ? 1.75f : -1.75f;
            float openingCenterX = openingOnLeft ? -2.1f : 2.1f;
            float openingBoundaryX = openingOnLeft ? -0.48f : 0.48f;
            float outerBoundaryX = openingOnLeft ? -3.82f : 3.82f;
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Wall Aura", new Vector3(wallX, 1.8f, 0.04f), new Vector3(4.72f, 4.42f, 0.78f), glowMaterial);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Top Aura", new Vector3(0f, 4.05f, 0.04f), new Vector3(8.22f, 0.43f, 0.78f), glowMaterial);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Wall", new Vector3(wallX, 1.8f, 0f), new Vector3(4.5f, 4.2f, 0.65f), material);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Top", new Vector3(0f, 4.05f, 0f), new Vector3(8f, 0.25f, 0.65f), material);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Safe Opening Inner", new Vector3(openingBoundaryX, 1.85f, -0.38f), new Vector3(0.1f, 3.8f, 0.12f), safeMaterial);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Safe Opening Outer", new Vector3(outerBoundaryX, 1.85f, -0.38f), new Vector3(0.1f, 3.8f, 0.12f), safeMaterial);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Safe Opening Top", new Vector3(openingCenterX, 3.72f, -0.38f), new Vector3(3.35f, 0.1f, 0.12f), safeMaterial);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Safe Path", new Vector3(openingCenterX, 0.04f, -0.2f), new Vector3(3.15f, 0.07f, 1.6f), safeMaterial);
            return root;
        }

        private static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string objectName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;

            if (primitive.TryGetComponent(out Collider primitiveCollider))
            {
                Object.Destroy(primitiveCollider);
            }

            if (primitive.TryGetComponent(out Renderer primitiveRenderer))
            {
                primitiveRenderer.sharedMaterial = material;
                primitiveRenderer.shadowCastingMode = ShadowCastingMode.Off;
                primitiveRenderer.receiveShadows = false;
            }

            return primitive;
        }

        private static bool IsObstacle(GameplayAction value)
        {
            return value == GameplayAction.Duck || value == GameplayAction.Jump ||
                   value == GameplayAction.DodgeLeft || value == GameplayAction.DodgeRight;
        }
    }

    /// <summary>Fixed-capacity pool for all travelling gameplay objects.</summary>
    public sealed class BeatTravellerPool
    {
        private readonly Queue<BeatTraveller> available;
        private readonly Transform root;

        public BeatTravellerPool(
            int capacity,
            Transform parent,
            RuntimeMaterialLibrary materials,
            NeonPulseGameConfig config,
            string poolName = "Traveller Pool")
        {
            int safeCapacity = Mathf.Max(8, capacity);
            available = new Queue<BeatTraveller>(safeCapacity);
            GameObject rootObject = new GameObject(poolName);
            rootObject.transform.SetParent(parent, false);
            root = rootObject.transform;

            for (int i = 0; i < safeCapacity; i++)
            {
                GameObject pooledObject = new GameObject("Beat Traveller " + i);
                pooledObject.transform.SetParent(root, false);
                BeatTraveller traveller = pooledObject.AddComponent<BeatTraveller>();
                traveller.Initialize(materials, config);
                available.Enqueue(traveller);
            }
        }

        public int AvailableCount => available.Count;

        /// <summary>Gets an available object, or null if the authored density exceeds pool capacity.</summary>
        public BeatTraveller Rent()
        {
            return available.Count > 0 ? available.Dequeue() : null;
        }

        /// <summary>Returns an object for reuse.</summary>
        public void Return(BeatTraveller traveller)
        {
            if (traveller == null)
            {
                return;
            }

            traveller.Despawn();
            traveller.transform.SetParent(root, false);
            available.Enqueue(traveller);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonPulse
{
    /// <summary>Independent pooled runway tile used only to visualize rhythm timing.</summary>
    public sealed class RhythmLaneTile : MonoBehaviour
    {
        private static readonly float[] LaneX = { -2.7f, -0.9f, 0.9f, 2.7f };

        private Renderer tileRenderer;
        private Renderer glowRenderer;
        private Transform footprintRoot;
        private RuntimeMaterialLibrary materials;
        private float targetTime;
        private float spawnTime;
        private float spawnZ;
        private float hitZ;

        public bool IsActive { get; private set; }
        public bool ReachedJudgementLine { get; private set; }
        public Color FeedbackColor { get; private set; }

        /// <summary>Builds one reusable tile with no dependency on gameplay obstacle objects.</summary>
        public void Initialize(RuntimeMaterialLibrary materialLibrary, NeonPulseGameConfig config)
        {
            materials = materialLibrary;
            hitZ = config.Rhythm.HitZ;

            GameObject glow = CreateCube(
                transform,
                "Tile Glow",
                new Vector3(0f, 0.075f, 0f),
                new Vector3(1.62f, 0.035f, config.Visuals.RhythmTileLength + 0.2f),
                materials.CyanGlow);
            glowRenderer = glow.GetComponent<Renderer>();

            GameObject tile = CreateCube(
                transform,
                "Tile Body",
                new Vector3(0f, 0.14f, 0f),
                new Vector3(1.42f, 0.16f, config.Visuals.RhythmTileLength),
                materials.Cyan);
            tileRenderer = tile.GetComponent<Renderer>();

            CreateCube(
                transform,
                "Tile Highlight",
                new Vector3(0f, 0.235f, -config.Visuals.RhythmTileLength * 0.32f),
                new Vector3(1.08f, 0.025f, config.Visuals.RhythmTileLength * 0.16f),
                materials.Footprint);

            footprintRoot = CreateFootprint(transform, materials.Footprint);

            gameObject.SetActive(false);
        }

        /// <summary>Starts a visual tile for one chart event, independent from its gameplay traveller.</summary>
        public void Spawn(RhythmTileEvent chartEvent, float eventTime, float eventSpawnTime, float startZ)
        {
            targetTime = eventTime;
            spawnTime = eventSpawnTime;
            spawnZ = startZ;
            ReachedJudgementLine = false;
            IsActive = true;

            int lane = Mathf.Clamp(chartEvent.Lane, 0, LaneX.Length - 1);
            transform.SetPositionAndRotation(new Vector3(LaneX[lane], 0f, startZ), Quaternion.identity);
            footprintRoot.localRotation = Quaternion.Euler(0f, lane < 2 ? -9f : 9f, 0f);
            SetTileMaterials(chartEvent.Color);
            gameObject.SetActive(true);
        }

        /// <summary>Moves straight toward the judgement line using the same absolute DSP timing as gameplay.</summary>
        public void Tick(float songTime)
        {
            if (!IsActive)
            {
                return;
            }

            if (songTime >= targetTime)
            {
                Vector3 contactPosition = transform.position;
                contactPosition.z = hitZ;
                transform.position = contactPosition;
                ReachedJudgementLine = true;
                return;
            }

            float travelDuration = targetTime - spawnTime;
            float normalized = travelDuration > 0.001f ? (songTime - spawnTime) / travelDuration : 1f;
            float z = Mathf.LerpUnclamped(spawnZ, hitZ, normalized);

            Vector3 position = transform.position;
            position.z = z;
            transform.position = position;
        }

        /// <summary>Resets runtime state before returning this tile to its dedicated pool.</summary>
        public void ResetForPool()
        {
            IsActive = false;
            ReachedJudgementLine = false;
            gameObject.SetActive(false);
        }

        private void SetTileMaterials(RhythmTileColor color)
        {
            Material body;
            Material glow;
            switch (color)
            {
                case RhythmTileColor.Cyan:
                    body = materials.Cyan;
                    glow = materials.CyanGlow;
                    FeedbackColor = materials.CyanColor;
                    break;
                case RhythmTileColor.Magenta:
                    body = materials.Magenta;
                    glow = materials.MagentaGlow;
                    FeedbackColor = materials.MagentaColor;
                    break;
                case RhythmTileColor.Purple:
                    body = materials.Purple;
                    glow = materials.PurpleGlow;
                    FeedbackColor = materials.PurpleColor;
                    break;
                default:
                    body = materials.Yellow;
                    glow = materials.YellowGlow;
                    FeedbackColor = materials.YellowColor;
                    break;
            }

            tileRenderer.sharedMaterial = body;
            glowRenderer.sharedMaterial = glow;
        }

        private static GameObject CreateCube(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            return CreatePrimitive(parent, PrimitiveType.Cube, objectName, localPosition, localScale, Quaternion.identity, material);
        }

        private static Transform CreateFootprint(Transform parent, Material material)
        {
            GameObject root = new GameObject("Footprint");
            root.transform.SetParent(parent, false);

            CreatePrimitive(
                root.transform,
                PrimitiveType.Capsule,
                "Foot Sole",
                new Vector3(0f, 0.29f, -0.08f),
                new Vector3(0.46f, 0.42f, 0.08f),
                Quaternion.Euler(90f, 0f, 0f),
                material);
            CreatePrimitive(root.transform, PrimitiveType.Sphere, "Foot Heel", new Vector3(0f, 0.29f, -0.43f), new Vector3(0.4f, 0.08f, 0.32f), Quaternion.identity, material);
            CreatePrimitive(root.transform, PrimitiveType.Sphere, "Toe 1", new Vector3(-0.2f, 0.29f, 0.34f), new Vector3(0.13f, 0.07f, 0.13f), Quaternion.identity, material);
            CreatePrimitive(root.transform, PrimitiveType.Sphere, "Toe 2", new Vector3(-0.1f, 0.29f, 0.4f), new Vector3(0.15f, 0.07f, 0.15f), Quaternion.identity, material);
            CreatePrimitive(root.transform, PrimitiveType.Sphere, "Toe 3", new Vector3(0.02f, 0.29f, 0.43f), new Vector3(0.17f, 0.07f, 0.17f), Quaternion.identity, material);
            CreatePrimitive(root.transform, PrimitiveType.Sphere, "Toe 4", new Vector3(0.15f, 0.29f, 0.4f), new Vector3(0.19f, 0.07f, 0.19f), Quaternion.identity, material);
            CreatePrimitive(root.transform, PrimitiveType.Sphere, "Toe 5", new Vector3(0.27f, 0.29f, 0.33f), new Vector3(0.21f, 0.07f, 0.21f), Quaternion.identity, material);
            return root.transform;
        }

        private static GameObject CreatePrimitive(
            Transform parent,
            PrimitiveType type,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localRotation = localRotation;
            primitive.transform.localScale = localScale;

            if (primitive.TryGetComponent(out Collider primitiveCollider))
            {
                Object.Destroy(primitiveCollider);
            }

            Renderer renderer = primitive.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return primitive;
        }
    }

    /// <summary>Dedicated fixed-capacity pool for rhythm display tiles.</summary>
    public sealed class RhythmLaneTilePool
    {
        private readonly Queue<RhythmLaneTile> available;
        private readonly Transform root;

        public RhythmLaneTilePool(int capacity, Transform parent, RuntimeMaterialLibrary materials, NeonPulseGameConfig config)
        {
            int safeCapacity = Mathf.Max(8, capacity);
            available = new Queue<RhythmLaneTile>(safeCapacity);
            GameObject rootObject = new GameObject("Rhythm Tile Display Pool");
            rootObject.transform.SetParent(parent, false);
            root = rootObject.transform;

            for (int index = 0; index < safeCapacity; index++)
            {
                GameObject tileObject = new GameObject("Rhythm Tile " + index);
                tileObject.transform.SetParent(root, false);
                RhythmLaneTile tile = tileObject.AddComponent<RhythmLaneTile>();
                tile.Initialize(materials, config);
                available.Enqueue(tile);
            }
        }

        public RhythmLaneTile Rent()
        {
            return available.Count > 0 ? available.Dequeue() : null;
        }

        public int AvailableCount => available.Count;

        public void Return(RhythmLaneTile tile)
        {
            if (tile == null)
            {
                return;
            }

            tile.ResetForPool();
            tile.transform.SetParent(root, false);
            available.Enqueue(tile);
        }
    }
}

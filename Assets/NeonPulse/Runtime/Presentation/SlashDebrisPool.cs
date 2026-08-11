using UnityEngine;
using UnityEngine.Rendering;

namespace NeonPulse
{
    /// <summary>Fixed-capacity pool for punch fragments and falling slash halves.</summary>
    public sealed class SlashDebrisPool
    {
        private readonly DebrisBurst[] bursts;
        private int nextBurstIndex;

        public SlashDebrisPool(int capacity, Transform parent, RuntimeMaterialLibrary materials)
        {
            int safeCapacity = Mathf.Max(4, capacity);
            bursts = new DebrisBurst[safeCapacity];

            GameObject rootObject = new GameObject("Combat Debris Pool");
            rootObject.transform.SetParent(parent, false);
            for (int index = 0; index < safeCapacity; index++)
            {
                bursts[index] = new DebrisBurst(index, rootObject.transform, materials);
            }
        }

        /// <summary>Reuses the old fragment burst as the impact feedback for punches.</summary>
        public void PlayPunch(Vector3 position, GameplayAction action)
        {
            DebrisBurst burst = bursts[nextBurstIndex];
            nextBurstIndex = (nextBurstIndex + 1) % bursts.Length;
            burst.PlayFragments(position, action);
        }

        /// <summary>Splits each slashed target into two large pieces that fall to the floor.</summary>
        public void PlaySlash(Vector3 position, GameplayAction action, float slashDirection)
        {
            DebrisBurst burst = bursts[nextBurstIndex];
            nextBurstIndex = (nextBurstIndex + 1) % bursts.Length;
            burst.PlaySplit(position, action, slashDirection);
        }

        public void Tick(float deltaTime)
        {
            for (int index = 0; index < bursts.Length; index++)
            {
                bursts[index].Tick(deltaTime);
            }
        }

        public void Clear()
        {
            for (int index = 0; index < bursts.Length; index++)
            {
                bursts[index].Clear();
            }
        }

        private sealed class DebrisBurst
        {
            private const int PieceCount = 12;
            private const float Gravity = 12.5f;
            private const float FloorY = 0.18f;
            private const float VisibleDuration = 1.75f;
            private const float ShrinkStartTime = 1.45f;

            private readonly Transform[] pieces = new Transform[PieceCount];
            private readonly Renderer[] renderers = new Renderer[PieceCount];
            private readonly Vector3[] velocities = new Vector3[PieceCount];
            private readonly Vector3[] angularVelocities = new Vector3[PieceCount];
            private readonly Vector3[] baseScales = new Vector3[PieceCount];
            private readonly RuntimeMaterialLibrary materials;
            private uint randomState;
            private float elapsedTime;
            private int activePieceCount;
            private bool active;

            public DebrisBurst(int burstIndex, Transform parent, RuntimeMaterialLibrary materialLibrary)
            {
                materials = materialLibrary;
                randomState = unchecked((uint)(burstIndex + 1) * 747796405u) | 1u;

                GameObject rootObject = new GameObject("Combat Debris Burst " + burstIndex);
                rootObject.transform.SetParent(parent, false);
                for (int pieceIndex = 0; pieceIndex < PieceCount; pieceIndex++)
                {
                    GameObject pieceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pieceObject.name = "Fragment " + pieceIndex;
                    pieceObject.transform.SetParent(rootObject.transform, false);
                    pieceObject.SetActive(false);

                    if (pieceObject.TryGetComponent(out Collider pieceCollider))
                    {
                        Object.Destroy(pieceCollider);
                    }

                    Renderer pieceRenderer = pieceObject.GetComponent<Renderer>();
                    pieceRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    pieceRenderer.receiveShadows = false;
                    pieces[pieceIndex] = pieceObject.transform;
                    renderers[pieceIndex] = pieceRenderer;
                }
            }

            public void PlayFragments(Vector3 position, GameplayAction action)
            {
                Clear();
                active = true;
                elapsedTime = 0f;
                activePieceCount = PieceCount;

                const float direction = 1f;
                float radians = direction * 45f * Mathf.Deg2Rad;
                Vector2 sliceNormal = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians));
                bool isDoubleTarget = action == GameplayAction.BothPunch;

                for (int index = 0; index < PieceCount; index++)
                {
                    Vector3 localPosition = isDoubleTarget
                        ? CalculateDoubleTargetPiecePosition(index)
                        : CalculateSingleTargetPiecePosition(index);
                    Vector2 positionInBlock = isDoubleTarget
                        ? new Vector2(localPosition.x - (index < PieceCount / 2 ? -1.25f : 1.25f), localPosition.y)
                        : new Vector2(localPosition.x, localPosition.y);
                    float pieceSide = Vector2.Dot(positionInBlock, sliceNormal) >= 0f ? 1f : -1f;
                    float separationSpeed = NextFloat(1.35f, 2.35f);

                    Transform piece = pieces[index];
                    Vector3 scale = isDoubleTarget
                        ? new Vector3(0.42f, 0.32f, 0.25f)
                        : new Vector3(0.27f, 0.34f, 0.27f);
                    piece.position = position + localPosition;
                    piece.rotation = Quaternion.Euler(
                        NextFloat(-10f, 10f),
                        NextFloat(-16f, 16f),
                        direction * 45f + NextFloat(-8f, 8f));
                    piece.localScale = scale;
                    baseScales[index] = scale;

                    velocities[index] = new Vector3(
                        sliceNormal.x * pieceSide * separationSpeed + positionInBlock.x * 0.7f,
                        1.8f + sliceNormal.y * pieceSide * separationSpeed + NextFloat(0.2f, 1.1f),
                        NextFloat(-1.1f, 1.3f));
                    angularVelocities[index] = new Vector3(
                        NextFloat(-240f, 240f),
                        NextFloat(-240f, 240f),
                        NextFloat(-300f, 300f));
                    renderers[index].sharedMaterial = ResolveMaterial(action, index);
                    piece.gameObject.SetActive(true);
                }
            }

            public void PlaySplit(Vector3 position, GameplayAction action, float slashDirection)
            {
                Clear();
                active = true;
                elapsedTime = 0f;

                float direction = Mathf.Abs(slashDirection) > 0.01f ? Mathf.Sign(slashDirection) : 1f;
                float sliceAngle = direction * 45f;
                float radians = sliceAngle * Mathf.Deg2Rad;
                Vector2 sliceNormal = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians));
                bool isDoubleTarget = action == GameplayAction.BothPunch;
                int targetCount = isDoubleTarget ? 2 : 1;
                activePieceCount = targetCount * 2;

                for (int index = 0; index < activePieceCount; index++)
                {
                    int targetIndex = index / 2;
                    float halfSide = (index & 1) == 0 ? -1f : 1f;
                    float targetCenterX = isDoubleTarget ? (targetIndex == 0 ? -1.25f : 1.25f) : 0f;
                    float separation = isDoubleTarget ? 0.27f : 0.36f;
                    Vector3 scale = isDoubleTarget
                        ? new Vector3(0.84f, 0.44f, 0.68f)
                        : new Vector3(1.18f, 0.62f, 0.72f);

                    Transform piece = pieces[index];
                    piece.position = position + new Vector3(
                        targetCenterX + sliceNormal.x * halfSide * separation,
                        sliceNormal.y * halfSide * separation,
                        0f);
                    piece.rotation = Quaternion.Euler(
                        NextFloat(-4f, 4f),
                        NextFloat(-7f, 7f),
                        sliceAngle);
                    piece.localScale = scale;
                    baseScales[index] = scale;

                    velocities[index] = new Vector3(
                        sliceNormal.x * halfSide * NextFloat(0.8f, 1.25f) + targetCenterX * 0.12f,
                        0.35f + sliceNormal.y * halfSide * NextFloat(0.15f, 0.45f),
                        NextFloat(-0.35f, 0.5f));
                    angularVelocities[index] = new Vector3(
                        NextFloat(-75f, 75f),
                        NextFloat(-95f, 95f),
                        halfSide * NextFloat(35f, 85f));
                    renderers[index].sharedMaterial = ResolveMaterial(action, targetIndex * (PieceCount / 2));
                    piece.gameObject.SetActive(true);
                }
            }

            public void Tick(float deltaTime)
            {
                if (!active)
                {
                    return;
                }

                elapsedTime += deltaTime;
                if (elapsedTime >= VisibleDuration)
                {
                    Clear();
                    return;
                }

                float shrink = elapsedTime <= ShrinkStartTime
                    ? 1f
                    : 1f - (elapsedTime - ShrinkStartTime) / (VisibleDuration - ShrinkStartTime);
                float groundDamping = Mathf.Pow(0.08f, deltaTime);
                for (int index = 0; index < activePieceCount; index++)
                {
                    Transform piece = pieces[index];
                    Vector3 velocity = velocities[index];
                    velocity.y -= Gravity * deltaTime;

                    Vector3 position = piece.position + velocity * deltaTime;
                    float minimumY = FloorY + baseScales[index].y * 0.5f;
                    if (position.y < minimumY)
                    {
                        position.y = minimumY;
                        if (velocity.y < 0f)
                        {
                            velocity.y *= -0.22f;
                        }

                        velocity.x *= groundDamping;
                        velocity.z *= groundDamping;
                        angularVelocities[index] *= groundDamping;
                    }

                    piece.position = position;
                    piece.rotation *= Quaternion.Euler(angularVelocities[index] * deltaTime);
                    piece.localScale = baseScales[index] * Mathf.Max(0f, shrink);
                    velocities[index] = velocity;
                }
            }

            public void Clear()
            {
                if (!active)
                {
                    return;
                }

                for (int index = 0; index < PieceCount; index++)
                {
                    pieces[index].gameObject.SetActive(false);
                }

                active = false;
                elapsedTime = 0f;
                activePieceCount = 0;
            }

            private Material ResolveMaterial(GameplayAction action, int pieceIndex)
            {
                if (action == GameplayAction.RightPunch)
                {
                    return materials.Magenta;
                }

                if (action == GameplayAction.BothPunch && pieceIndex >= PieceCount / 2)
                {
                    return materials.Magenta;
                }

                if (action == GameplayAction.OverheadClap)
                {
                    return materials.Purple;
                }

                return materials.Cyan;
            }

            private static Vector3 CalculateSingleTargetPiecePosition(int index)
            {
                int column = index % 4;
                int row = index / 4;
                return new Vector3((column - 1.5f) * 0.32f, (row - 1f) * 0.39f, 0f);
            }

            private static Vector3 CalculateDoubleTargetPiecePosition(int index)
            {
                int blockIndex = index / 6;
                int localIndex = index % 6;
                int column = localIndex % 2;
                int row = localIndex / 2;
                float blockCenterX = blockIndex == 0 ? -1.25f : 1.25f;
                return new Vector3(blockCenterX + (column - 0.5f) * 0.48f, (row - 1f) * 0.38f, 0f);
            }

            private float NextFloat(float minimum, float maximum)
            {
                randomState ^= randomState << 13;
                randomState ^= randomState >> 17;
                randomState ^= randomState << 5;
                float normalized = (randomState & 0x00FFFFFFu) / 16777215f;
                return Mathf.Lerp(minimum, maximum, normalized);
            }
        }
    }
}

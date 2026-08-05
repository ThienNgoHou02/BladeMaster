using UnityEngine;
using UnityEngine.Rendering;

namespace NeonPulse
{
    /// <summary>Prebuilt expanding ring feedback for tile contacts and successful combat impacts.</summary>
    public sealed class RippleVfxPool
    {
        private readonly Ripple[] ripples;
        private int nextRippleIndex;

        public RippleVfxPool(int capacity, Transform parent, RuntimeMaterialLibrary materials)
        {
            int safeCapacity = Mathf.Max(6, capacity);
            ripples = new Ripple[safeCapacity];
            GameObject root = new GameObject("Impact Ripple Pool");
            root.transform.SetParent(parent, false);
            for (int index = 0; index < ripples.Length; index++)
            {
                ripples[index] = new Ripple(index, root.transform, materials.Footprint);
            }
        }

        public void Play(Vector3 position, Color color, bool alignToGround)
        {
            Ripple ripple = ripples[nextRippleIndex];
            nextRippleIndex = (nextRippleIndex + 1) % ripples.Length;
            ripple.Play(position, color, alignToGround);
        }

        public void Tick(float deltaTime)
        {
            for (int index = 0; index < ripples.Length; index++)
            {
                ripples[index].Tick(deltaTime);
            }
        }

        public void Clear()
        {
            for (int index = 0; index < ripples.Length; index++)
            {
                ripples[index].Clear();
            }
        }

        private sealed class Ripple
        {
            private const int SegmentCount = 28;
            private const float Duration = 0.42f;

            private readonly GameObject root;
            private readonly LineRenderer outerRing;
            private readonly LineRenderer innerRing;
            private float elapsed;
            private bool active;

            public Ripple(int index, Transform parent, Material material)
            {
                root = new GameObject("Impact Ripple " + index);
                root.transform.SetParent(parent, false);
                outerRing = CreateRing(root.transform, "Outer Ring", material, 1f, 0.09f);
                innerRing = CreateRing(root.transform, "Inner Ring", material, 0.58f, 0.055f);
                root.SetActive(false);
            }

            public void Play(Vector3 position, Color color, bool alignToGround)
            {
                root.transform.position = alignToGround
                    ? new Vector3(position.x, 0.42f, position.z)
                    : position;
                root.transform.rotation = alignToGround ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
                root.transform.localScale = Vector3.one * 0.28f;
                Color outerColor = color;
                outerColor.a = 0.95f;
                Color innerColor = Color.Lerp(color, Color.white, 0.35f);
                innerColor.a = 0.82f;
                outerRing.startColor = outerColor;
                outerRing.endColor = outerColor;
                innerRing.startColor = innerColor;
                innerRing.endColor = innerColor;
                elapsed = 0f;
                active = true;
                root.SetActive(true);
            }

            public void Tick(float deltaTime)
            {
                if (!active)
                {
                    return;
                }

                elapsed += deltaTime;
                if (elapsed >= Duration)
                {
                    Clear();
                    return;
                }

                float normalized = elapsed / Duration;
                float scale = Mathf.Lerp(0.28f, 2.15f, normalized);
                root.transform.localScale = Vector3.one * scale;
                float alpha = 1f - normalized;
                SetAlpha(outerRing, alpha * 0.95f);
                SetAlpha(innerRing, alpha * 0.72f);
            }

            public void Clear()
            {
                active = false;
                root.SetActive(false);
            }

            private static LineRenderer CreateRing(Transform parent, string name, Material material, float radius, float width)
            {
                GameObject ringObject = new GameObject(name);
                ringObject.transform.SetParent(parent, false);
                LineRenderer ring = ringObject.AddComponent<LineRenderer>();
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = SegmentCount;
                ring.widthMultiplier = width;
                ring.material = material;
                ring.alignment = LineAlignment.View;
                ring.textureMode = LineTextureMode.Stretch;
                ring.shadowCastingMode = ShadowCastingMode.Off;
                ring.receiveShadows = false;
                ring.numCornerVertices = 2;
                ring.numCapVertices = 2;
                for (int index = 0; index < SegmentCount; index++)
                {
                    float angle = index / (float)SegmentCount * Mathf.PI * 2f;
                    ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                }

                return ring;
            }

            private static void SetAlpha(LineRenderer ring, float alpha)
            {
                Color color = ring.startColor;
                color.a = alpha;
                ring.startColor = color;
                ring.endColor = color;
            }
        }
    }
}

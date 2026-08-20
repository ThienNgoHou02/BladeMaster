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
                ripples[index] = new Ripple(index, root.transform, materials.ImpactGlow);
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
            // A denser ring avoids visible polygon corners on mobile-sized screens.
            private const int SegmentCount = 48;
            private const float Duration = 0.52f;

            private readonly GameObject root;
            private readonly LineRenderer outerRing;
            private readonly LineRenderer middleRing;
            private readonly LineRenderer innerRing;
            private float elapsed;
            private bool active;

            public Ripple(int index, Transform parent, Material material)
            {
                root = new GameObject("Impact Ripple " + index);
                root.transform.SetParent(parent, false);
                outerRing = CreateRing(root.transform, "Outer Ring", material, 1f, 0.09f);
                middleRing = CreateRing(root.transform, "Middle Ring", material, 0.76f, 0.07f);
                innerRing = CreateRing(root.transform, "Inner Ring", material, 0.58f, 0.055f);
                root.SetActive(false);
            }

            public void Play(Vector3 position, Color color, bool alignToGround)
            {
                root.transform.position = alignToGround
                    ? new Vector3(position.x, 0.42f, position.z)
                    : position;
                root.transform.rotation = alignToGround ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
                Color outerColor = color;
                outerColor.a = 0.95f;
                Color innerColor = Color.Lerp(color, Color.white, 0.35f);
                innerColor.a = 0.82f;
                Color middleColor = Color.Lerp(color, Color.white, 0.18f);
                middleColor.a = 0.88f;
                outerRing.startColor = outerColor;
                outerRing.endColor = outerColor;
                middleRing.startColor = middleColor;
                middleRing.endColor = middleColor;
                innerRing.startColor = innerColor;
                innerRing.endColor = innerColor;
                elapsed = 0f;
                active = true;
                root.SetActive(true);
                outerRing.transform.localScale = Vector3.one;
                middleRing.transform.localScale = Vector3.one;
                innerRing.transform.localScale = Vector3.one;
                SetAlpha(innerRing, 0f);
                SetAlpha(middleRing, 0f);
                SetAlpha(outerRing, 0f);
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

                TickRing(innerRing, 0f, 0.34f, 0.82f, 0.075f, 0.022f);
                TickRing(middleRing, 0.11f, 0.34f, 0.9f, 0.085f, 0.028f);
                TickRing(outerRing, 0.22f, 0.3f, 0.96f, 0.12f, 0.035f);
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
                ring.sharedMaterial = material;
                ring.alignment = LineAlignment.TransformZ;
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

            private void TickRing(LineRenderer ring, float delay, float waveDuration, float initialAlpha, float startWidth, float endWidth)
            {
                float normalizedTime = Mathf.Clamp01((elapsed - delay) / waveDuration);
                if (elapsed < delay)
                {
                    SetAlpha(ring, 0f);
                    return;
                }

                float expansion = 1f - Mathf.Pow(1f - normalizedTime, 2.4f);
                ring.transform.localScale = Vector3.one * Mathf.Lerp(0.28f, 2.2f, expansion);
                SetAlpha(ring, initialAlpha * (1f - normalizedTime * normalizedTime));
                ring.widthMultiplier = Mathf.Lerp(startWidth, endWidth, expansion);
            }
        }
    }

    /// <summary>Fixed-capacity pool for authored particle prefabs.</summary>
    public sealed class PrefabParticleVfxPool
    {
        private readonly GameObject[] instances;
        private readonly ParticleSystem[][] particleSystems;
        private int nextIndex;

        public PrefabParticleVfxPool(int capacity, Transform parent, GameObject prefab, string poolName)
        {
            if (prefab == null)
            {
                instances = new GameObject[0];
                particleSystems = new ParticleSystem[0][];
                Debug.LogWarning(poolName + " prefab is missing. Assign it in NeonPulseGameConfig.");
                return;
            }

            int safeCapacity = Mathf.Max(1, capacity);
            instances = new GameObject[safeCapacity];
            particleSystems = new ParticleSystem[safeCapacity][];

            GameObject poolRoot = new GameObject(poolName);
            poolRoot.transform.SetParent(parent, false);

            for (int index = 0; index < safeCapacity; index++)
            {
                GameObject instance = Object.Instantiate(prefab, poolRoot.transform);
                instance.name = prefab.name + " " + index;

                ConfigureDisableOnComplete(instance);

                ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                SetSortingOrder(instance, 10);
                StopAndClear(systems);
                instance.SetActive(false);
                instances[index] = instance;
                particleSystems[index] = systems;
            }
        }

        public void Play(Vector3 position, Quaternion rotation)
        {
            if (instances.Length == 0)
            {
                return;
            }

            int index = nextIndex;
            nextIndex = (nextIndex + 1) % instances.Length;

            GameObject instance = instances[index];
            ParticleSystem[] systems = particleSystems[index];
            instance.SetActive(false);
            StopAndClear(systems);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            for (int systemIndex = 0; systemIndex < systems.Length; systemIndex++)
            {
                systems[systemIndex].Play(false);
            }
        }

        public void Clear()
        {
            for (int index = 0; index < instances.Length; index++)
            {
                StopAndClear(particleSystems[index]);
                instances[index].SetActive(false);
            }

            nextIndex = 0;
        }

        private static void StopAndClear(ParticleSystem[] systems)
        {
            for (int index = 0; index < systems.Length; index++)
            {
                systems[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static void ConfigureDisableOnComplete(GameObject instance)
        {
            Component effect = instance.GetComponent("CFXR_Effect");
            if (effect == null)
            {
                return;
            }

            System.Reflection.FieldInfo clearBehaviorField = effect.GetType().GetField("clearBehavior");
            if (clearBehaviorField == null || !clearBehaviorField.FieldType.IsEnum)
            {
                return;
            }

            object disableValue = System.Enum.ToObject(clearBehaviorField.FieldType, 1);
            clearBehaviorField.SetValue(effect, disableValue);
        }

        private static void SetSortingOrder(GameObject instance, int sortingOrder)
        {
            ParticleSystemRenderer[] renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].sortingOrder = sortingOrder;
            }
        }
    }
}

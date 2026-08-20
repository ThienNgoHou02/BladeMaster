using UnityEngine;
using UnityEngine.Rendering;

namespace NeonPulse
{
    /// <summary>Creates inexpensive side scenery and wind streaks to sell forward movement.</summary>
    public sealed class NeonMotionFeedback
    {
        private const int PropsPerSide = 6;
        private const float FirstPropZ = 9f;
        private const float PropSpacing = 9f;
        private const float PropLoopLength = PropsPerSide * PropSpacing;
        private const float DespawnZ = -7f;
        private const float MinimumRunningSpeed = 0.01f;
        private const float MinimumWindEmission = 36f;
        private const float MaximumWindEmission = 72f;
        private const float WindSpeedAtMinimumEmission = 10f;
        private const float WindSpeedAtMaximumEmission = 30f;

        private readonly Transform[] sideProps = new Transform[PropsPerSide * 2];
        private readonly ParticleSystem wind;

        public NeonMotionFeedback(Transform parent, Camera camera, RuntimeMaterialLibrary materials)
        {
            GameObject root = new GameObject("Forward Motion Feedback");
            root.transform.SetParent(parent, false);
            CreateSideProps(root.transform, materials);
            wind = CreateWind(camera, materials.White);
            CreateAmbientMotes(camera, materials.White, materials.CyanColor, materials.MagentaColor);
        }

        public void Tick(float deltaTime, float forwardSpeed)
        {
            float safeSpeed = Mathf.Max(0f, forwardSpeed);
            for (int index = 0; index < sideProps.Length; index++)
            {
                Transform prop = sideProps[index];
                Vector3 position = prop.position;
                position.z -= safeSpeed * deltaTime;
                if (position.z < DespawnZ)
                {
                    position.z += PropLoopLength;
                }

                prop.position = position;
            }

            ParticleSystem.EmissionModule emission = wind.emission;
            if (safeSpeed <= MinimumRunningSpeed)
            {
                emission.rateOverTime = 0f;
                return;
            }

            float normalizedSpeed = Mathf.InverseLerp(
                WindSpeedAtMinimumEmission,
                WindSpeedAtMaximumEmission,
                safeSpeed);
            emission.rateOverTime = Mathf.Lerp(MinimumWindEmission, MaximumWindEmission, normalizedSpeed);
        }

        private void CreateSideProps(Transform parent, RuntimeMaterialLibrary materials)
        {
            for (int index = 0; index < sideProps.Length; index++)
            {
                bool leftSide = index < PropsPerSide;
                int sequence = index % PropsPerSide;
                GameObject propRoot = new GameObject(leftSide
                    ? "Left Moving Pylon " + sequence
                    : "Right Moving Pylon " + sequence);
                propRoot.transform.SetParent(parent, false);

                float side = leftSide ? -1f : 1f;
                float height = 2.15f + sequence % 3 * 0.62f;
                float x = side * (6.35f + (sequence & 1) * 0.8f);
                float stagger = leftSide ? 0f : PropSpacing * 0.5f;
                propRoot.transform.position = new Vector3(x, 0f, FirstPropZ + sequence * PropSpacing + stagger);

                Material accent = leftSide ? materials.Cyan : materials.Magenta;
                CreatePylonPart(
                    propRoot.transform,
                    "Body",
                    new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(0.38f, height, 0.58f),
                    materials.Skyline);
                CreatePylonPart(
                    propRoot.transform,
                    "Inner Light",
                    new Vector3(-side * 0.22f, height * 0.52f, -0.31f),
                    new Vector3(0.055f, height * 0.72f, 0.035f),
                    accent);
                CreatePylonPart(
                    propRoot.transform,
                    "Crown",
                    new Vector3(0f, height + 0.04f, 0f),
                    new Vector3(0.68f, 0.09f, 0.78f),
                    accent);

                sideProps[index] = propRoot.transform;
            }
        }

        private static void CreatePylonPart(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            if (part.TryGetComponent(out Collider collider))
            {
                Object.Destroy(collider);
            }

            if (part.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static ParticleSystem CreateWind(Camera camera, Material particleMaterial)
        {
            GameObject windObject = new GameObject("Speed Wind Particles");
            windObject.transform.SetParent(camera.transform, false);
            windObject.transform.localPosition = new Vector3(0f, 0f, 11f);
            ParticleSystem system = windObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.52f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
            main.maxParticles = 96;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(12f, 6.5f, 0.1f);
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.z = new ParticleSystem.MinMaxCurve(-38f);

            ParticleSystemRenderer renderer = windObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 4f;
            renderer.velocityScale = 0.18f;
            renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return system;
        }

        private static ParticleSystem CreateAmbientMotes(
            Camera camera,
            Material particleMaterial,
            Color cyan,
            Color magenta)
        {
            GameObject moteObject = new GameObject("Ambient Neon Motes");
            moteObject.transform.SetParent(camera.transform, false);
            moteObject.transform.localPosition = new Vector3(0f, 0f, 15f);

            ParticleSystem system = moteObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 4.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.055f);
            main.startColor = new ParticleSystem.MinMaxGradient(cyan, magenta);
            main.maxParticles = 64;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 14f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(11f, 6f, 4f);

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.z = new ParticleSystem.MinMaxCurve(-5.5f, -2.5f);

            ParticleSystemRenderer renderer = moteObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return system;
        }
    }
}

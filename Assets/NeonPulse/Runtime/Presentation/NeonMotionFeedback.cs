using UnityEngine;
using UnityEngine.Rendering;

namespace NeonPulse
{
    /// <summary>Creates inexpensive side scenery and wind streaks to sell forward movement.</summary>
    public sealed class NeonMotionFeedback
    {
        private const int PropsPerSide = 10;
        private const float ResetZ = 58f;
        private const float DespawnZ = -8f;
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
                    position.z = ResetZ + (index % PropsPerSide) * 2.7f;
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
                GameObject prop = GameObject.CreatePrimitive(sequence % 3 == 0 ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                prop.name = leftSide ? "Left Speed Prop " + sequence : "Right Speed Prop " + sequence;
                prop.transform.SetParent(parent, false);
                float x = (leftSide ? -1f : 1f) * (4.8f + (sequence % 3) * 1.15f);
                prop.transform.position = new Vector3(x, 1.1f + (sequence % 4) * 0.42f, 8f + sequence * 5.2f);
                prop.transform.localScale = sequence % 3 == 0
                    ? new Vector3(0.18f, 1.8f + (sequence % 2) * 0.7f, 0.18f)
                    : new Vector3(0.25f + (sequence % 2) * 0.14f, 0.5f + (sequence % 3) * 0.28f, 1.4f + (sequence % 2));
                if (prop.TryGetComponent(out Collider collider))
                {
                    Object.Destroy(collider);
                }

                if (prop.TryGetComponent(out Renderer renderer))
                {
                    renderer.sharedMaterial = leftSide ? materials.Cyan : materials.Magenta;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                sideProps[index] = prop.transform;
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
    }
}

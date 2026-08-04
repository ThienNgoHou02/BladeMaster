using UnityEngine;
using UnityEngine.Rendering;

namespace NeonPulse
{
    /// <summary>Builds the fixed camera and original neon arena entirely from Unity primitives.</summary>
    public static class NeonWorldBuilder
    {
        /// <summary>Creates the arena and returns the gameplay camera.</summary>
        public static Camera Build(
            Transform parent,
            RuntimeMaterialLibrary materials,
            NeonPulseGameConfig config,
            out JudgementLineFeedback judgementLineFeedback)
        {
            DisableSceneCamerasAndLights();
            ConfigureRenderSettings();

            Camera camera = CreateCamera(parent, config.CameraFeel.StandingHeight);
            CreateLighting(parent, materials);
            CreateTrack(parent, materials);
            CreateTunnel(parent, materials);
            judgementLineFeedback = CreateHitLine(parent, materials, config);
            return camera;
        }

        private static void DisableSceneCamerasAndLights()
        {
            Camera[] cameras = Object.FindObjectsOfType<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i].enabled = false;
                if (cameras[i].TryGetComponent(out AudioListener listener))
                {
                    listener.enabled = false;
                }
            }

            Light[] lights = Object.FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].enabled = false;
            }
        }

        private static void ConfigureRenderSettings()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.015f, 0.002f, 0.04f, 1f);
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 72f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.015f, 0.09f, 1f);
        }

        private static Camera CreateCamera(Transform parent, float standingHeight)
        {
            GameObject cameraObject = new GameObject("Fixed First Person Camera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.position = new Vector3(0f, standingHeight, -5.8f);
            cameraObject.transform.rotation = Quaternion.Euler(2.5f, 0f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.002f, 0.001f, 0.009f, 1f);
            camera.fieldOfView = 67f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static void CreateLighting(Transform parent, RuntimeMaterialLibrary materials)
        {
            GameObject keyObject = new GameObject("Purple Key Light");
            keyObject.transform.SetParent(parent, false);
            keyObject.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = materials.PurpleColor;
            key.intensity = 0.7f;
            key.shadows = LightShadows.None;

            GameObject cyanObject = new GameObject("Cyan Fill Light");
            cyanObject.transform.SetParent(parent, false);
            cyanObject.transform.position = new Vector3(-4f, 2f, 4f);
            Light cyan = cyanObject.AddComponent<Light>();
            cyan.type = LightType.Point;
            cyan.color = materials.CyanColor;
            cyan.range = 18f;
            cyan.intensity = 3f;
            cyan.shadows = LightShadows.None;

            GameObject magentaObject = new GameObject("Magenta Fill Light");
            magentaObject.transform.SetParent(parent, false);
            magentaObject.transform.position = new Vector3(4f, 2f, 4f);
            Light magenta = magentaObject.AddComponent<Light>();
            magenta.type = LightType.Point;
            magenta.color = materials.MagentaColor;
            magenta.range = 18f;
            magenta.intensity = 3f;
            magenta.shadows = LightShadows.None;
        }

        private static void CreateTrack(Transform parent, RuntimeMaterialLibrary materials)
        {
            for (int lane = 0; lane < 4; lane++)
            {
                float x = -2.7f + lane * 1.8f;
                CreateCube(parent, "Lane " + (lane + 1), new Vector3(x, -0.12f, 26f), new Vector3(1.7f, 0.18f, 58f), materials.Dark);
            }

            for (int divider = 0; divider <= 4; divider++)
            {
                float x = -3.6f + divider * 1.8f;
                Material dividerMaterial = divider % 2 == 0 ? materials.Cyan : materials.Magenta;
                CreateCube(parent, "Lane Rail " + divider, new Vector3(x, 0.01f, 26f), new Vector3(0.045f, 0.04f, 58f), dividerMaterial);
            }

            for (int z = 4; z <= 56; z += 4)
            {
                CreateCube(parent, "Beat Marker", new Vector3(0f, 0.015f, z), new Vector3(7.25f, 0.025f, 0.04f), materials.Purple);
            }
        }

        private static void CreateTunnel(Transform parent, RuntimeMaterialLibrary materials)
        {
            for (int index = 0; index < 11; index++)
            {
                float z = 3f + index * 5f;
                Material material = index % 2 == 0 ? materials.Purple : materials.Magenta;
                CreateCube(parent, "Tunnel Left", new Vector3(-5.3f, 2.8f, z), new Vector3(0.07f, 5.7f, 0.07f), material);
                CreateCube(parent, "Tunnel Right", new Vector3(5.3f, 2.8f, z), new Vector3(0.07f, 5.7f, 0.07f), material);
                CreateCube(parent, "Tunnel Top", new Vector3(0f, 5.6f, z), new Vector3(10.65f, 0.07f, 0.07f), material);
            }

            CreateCube(parent, "Left Horizon", new Vector3(-5.3f, 2.8f, 30f), new Vector3(0.06f, 0.06f, 58f), materials.Cyan);
            CreateCube(parent, "Right Horizon", new Vector3(5.3f, 2.8f, 30f), new Vector3(0.06f, 0.06f, 58f), materials.Magenta);
        }

        private static JudgementLineFeedback CreateHitLine(Transform parent, RuntimeMaterialLibrary materials, NeonPulseGameConfig config)
        {
            float hitZ = config.Rhythm.HitZ;
            CreateCube(parent, "Judgement Step Backplate", new Vector3(0f, 0.04f, hitZ), new Vector3(8.5f, 0.08f, 0.98f), materials.Dark);
            GameObject glassStep = CreateCube(parent, "Judgement Step Glass", new Vector3(0f, 0.16f, hitZ), new Vector3(8.3f, 0.24f, 0.82f), materials.YellowGlow);
            CreateCube(parent, "Judgement Step Top Glow", new Vector3(0f, 0.29f, hitZ + 0.02f), new Vector3(8.2f, 0.025f, 0.72f), materials.YellowGlow);
            CreateCube(parent, "Judgement Step Front Edge", new Vector3(0f, 0.28f, hitZ - 0.4f), new Vector3(8.15f, 0.12f, 0.11f), materials.Yellow);
            CreateCube(parent, "Judgement Zone Cyan", new Vector3(-2f, 0.32f, hitZ + 0.09f), new Vector3(3.85f, 0.035f, 0.3f), materials.Cyan);
            CreateCube(parent, "Judgement Zone Magenta", new Vector3(2f, 0.32f, hitZ + 0.09f), new Vector3(3.85f, 0.035f, 0.3f), materials.Magenta);
            GameObject core = CreateCube(parent, "Contact Line White", new Vector3(0f, 0.37f, hitZ), new Vector3(8.35f, 0.09f, 0.09f), materials.Footprint);
            CreateCube(parent, "Contact Line Near Rim", new Vector3(0f, 0.34f, hitZ - 0.09f), new Vector3(8.25f, 0.04f, 0.04f), materials.Yellow);
            CreateCube(parent, "Contact Line Far Rim", new Vector3(0f, 0.34f, hitZ + 0.09f), new Vector3(8.25f, 0.04f, 0.04f), materials.Yellow);
            CreateCube(parent, "Hit Portal Left", new Vector3(-3.85f, 2.05f, hitZ), new Vector3(0.09f, 4.1f, 0.14f), materials.Cyan);
            CreateCube(parent, "Hit Portal Right", new Vector3(3.85f, 2.05f, hitZ), new Vector3(0.09f, 4.1f, 0.14f), materials.Magenta);
            CreateCube(parent, "Hit Portal Top", new Vector3(0f, 4.1f, hitZ), new Vector3(7.8f, 0.09f, 0.14f), materials.Yellow);

            Transform[] pads = new Transform[4];
            for (int lane = 0; lane < pads.Length; lane++)
            {
                float x = -2.7f + lane * 1.8f;
                Material laneMaterial = lane < 2 ? materials.Cyan : materials.Magenta;
                CreateCube(parent, "Receptor Base " + lane, new Vector3(x, 0.08f, hitZ - 0.78f), new Vector3(1.66f, 0.16f, 0.62f), materials.Dark);
                GameObject pad = CreateCube(parent, "Receptor Pad " + lane, new Vector3(x, 0.2f, hitZ - 0.58f), new Vector3(1.58f, 0.1f, 0.18f), laneMaterial);
                pads[lane] = pad.transform;
            }

            return new JudgementLineFeedback(core.transform, glassStep.transform, pads, config.Visuals.JudgementLinePulseStrength);
        }

        private static GameObject CreateCube(Transform parent, string objectName, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = objectName;
            cube.transform.SetParent(parent, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;

            if (cube.TryGetComponent(out Collider cubeCollider))
            {
                Object.Destroy(cubeCollider);
            }

            if (cube.TryGetComponent(out Renderer cubeRenderer))
            {
                cubeRenderer.sharedMaterial = material;
                cubeRenderer.shadowCastingMode = ShadowCastingMode.Off;
                cubeRenderer.receiveShadows = false;
            }

            return cube;
        }
    }

    /// <summary>Allocation-free pulse feedback for the foreground judgement line and four receptors.</summary>
    public sealed class JudgementLineFeedback
    {
        private const float PulseDuration = 0.18f;

        private readonly Transform lineCore;
        private readonly Transform lineGlow;
        private readonly Transform[] pads;
        private readonly float[] padTimers;
        private readonly Vector3[] restScales;
        private readonly Vector3 lineCoreRestScale;
        private readonly Vector3 lineGlowRestScale;
        private readonly float pulseStrength;
        private float lineTimer;

        public JudgementLineFeedback(Transform core, Transform glow, Transform[] receptorPads, float configuredPulseStrength)
        {
            lineCore = core;
            lineGlow = glow;
            pads = receptorPads;
            pulseStrength = Mathf.Max(0f, configuredPulseStrength);
            lineCoreRestScale = lineCore.localScale;
            lineGlowRestScale = lineGlow.localScale;
            padTimers = new float[pads.Length];
            restScales = new Vector3[pads.Length];
            for (int index = 0; index < pads.Length; index++)
            {
                restScales[index] = pads[index].localScale;
            }
        }

        /// <summary>Pulses the receptor closest to the successfully judged world X position.</summary>
        public void Pulse(float worldX)
        {
            lineTimer = PulseDuration;
            int lane = Mathf.Clamp(Mathf.RoundToInt((worldX + 2.7f) / 1.8f), 0, pads.Length - 1);
            padTimers[lane] = PulseDuration;
        }

        /// <summary>Returns the line and pads to their cached rest scale without tween allocations.</summary>
        public void Tick(float deltaTime)
        {
            lineTimer = Mathf.Max(0f, lineTimer - deltaTime);
            float normalized = lineTimer / PulseDuration;
            float lineScale = 1f + normalized * pulseStrength;
            lineCore.localScale = new Vector3(
                lineCoreRestScale.x,
                lineCoreRestScale.y * lineScale,
                lineCoreRestScale.z * (1f + normalized * 0.04f));
            lineGlow.localScale = new Vector3(
                lineGlowRestScale.x,
                lineGlowRestScale.y * lineScale,
                lineGlowRestScale.z * lineScale);

            for (int index = 0; index < pads.Length; index++)
            {
                padTimers[index] = Mathf.Max(0f, padTimers[index] - deltaTime);
                float padPulse = padTimers[index] / PulseDuration * pulseStrength;
                Vector3 rest = restScales[index];
                pads[index].localScale = new Vector3(rest.x * (1f + padPulse), rest.y * (1f + padPulse), rest.z * (1f + padPulse));
            }
        }
    }

    /// <summary>Small reusable particle pool for successful and missed hits.</summary>
    public sealed class HitBurstPool
    {
        private readonly ParticleSystem[] systems;
        private int nextIndex;

        public HitBurstPool(int capacity, Transform parent, Material particleMaterial, int burstCount)
        {
            int safeCapacity = Mathf.Max(4, capacity);
            systems = new ParticleSystem[safeCapacity];

            for (int i = 0; i < safeCapacity; i++)
            {
                GameObject effectObject = new GameObject("Hit Burst " + i);
                effectObject.transform.SetParent(parent, false);
                ParticleSystem system = effectObject.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.25f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                int safeBurstCount = Mathf.Clamp(burstCount, 1, 128);
                main.maxParticles = safeBurstCount;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)safeBurstCount) });

                ParticleSystem.ShapeModule shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.3f;

                Gradient fadeGradient = new Gradient();
                fadeGradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(new Color(0.7f, 0.85f, 1f), 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0.8f, 0.55f),
                        new GradientAlphaKey(0f, 1f)
                    });
                ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
                colorOverLifetime.enabled = true;
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeGradient);

                ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                    new AnimationCurve(
                        new Keyframe(0f, 0.25f),
                        new Keyframe(0.18f, 1.2f),
                        new Keyframe(1f, 0f)));

                ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = system.rotationOverLifetime;
                rotationOverLifetime.enabled = true;
                rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

                ParticleSystemRenderer renderer = effectObject.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial = particleMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                systems[i] = system;
            }
        }

        /// <summary>Reuses the next particle system and changes its cached main-module color.</summary>
        public void Play(Vector3 position, Color color)
        {
            ParticleSystem system = systems[nextIndex];
            nextIndex = (nextIndex + 1) % systems.Length;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.transform.position = position;
            ParticleSystem.MainModule main = system.main;
            main.startColor = color;
            system.Play(true);
        }
    }
}

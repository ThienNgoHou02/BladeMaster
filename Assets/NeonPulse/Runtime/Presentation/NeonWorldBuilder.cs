using UnityEngine;
using UnityEngine.Rendering;

namespace NeonPulse
{
    /// <summary>Builds the fixed camera and original neon arena entirely from Unity primitives.</summary>
    public static class NeonWorldBuilder
    {
        /// <summary>Creates the arena and returns the gameplay camera.</summary>
        public static Camera Build(Transform parent, RuntimeMaterialLibrary materials)
        {
            DisableSceneCamerasAndLights();
            ConfigureRenderSettings();

            Camera camera = CreateCamera(parent);
            CreateLighting(parent, materials);
            CreateTrack(parent, materials);
            CreateTunnel(parent, materials);
            CreateHitLine(parent, materials);
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

        private static Camera CreateCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Fixed First Person Camera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.position = new Vector3(0f, 2.35f, -5.8f);
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

        private static void CreateHitLine(Transform parent, RuntimeMaterialLibrary materials)
        {
            CreateCube(parent, "Hit Line", new Vector3(0f, 0.06f, 1.5f), new Vector3(7.4f, 0.07f, 0.18f), materials.Yellow);
            CreateCube(parent, "Hit Portal Left", new Vector3(-3.85f, 2.05f, 1.5f), new Vector3(0.09f, 4.1f, 0.14f), materials.Cyan);
            CreateCube(parent, "Hit Portal Right", new Vector3(3.85f, 2.05f, 1.5f), new Vector3(0.09f, 4.1f, 0.14f), materials.Magenta);
            CreateCube(parent, "Hit Portal Top", new Vector3(0f, 4.1f, 1.5f), new Vector3(7.8f, 0.09f, 0.14f), materials.Yellow);
            CreateCube(parent, "Left Hand Pad", new Vector3(-2.7f, 0.12f, 0.5f), new Vector3(1.65f, 0.08f, 1.4f), materials.Cyan);
            CreateCube(parent, "Right Hand Pad", new Vector3(2.7f, 0.12f, 0.5f), new Vector3(1.65f, 0.08f, 1.4f), materials.Magenta);
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

    /// <summary>Small reusable particle pool for successful and missed hits.</summary>
    public sealed class HitBurstPool
    {
        private readonly ParticleSystem[] systems;
        private int nextIndex;

        public HitBurstPool(int capacity, Transform parent, Material particleMaterial)
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
                main.maxParticles = 32;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

                ParticleSystem.ShapeModule shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.25f;

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

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

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

            Camera camera = CreateCamera(
                parent,
                config.CameraFeel.StandingHeight,
                config.Rhythm.HitZ,
                config.CameraFeel.DistanceToJudgementLine);
            CreateLighting(parent, materials);
            CreateTrack(parent, materials);
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

        private static Camera CreateCamera(Transform parent, float standingHeight, float hitZ, float distanceToJudgementLine)
        {
            GameObject cameraObject = new GameObject("Fixed First Person Camera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.position = new Vector3(0f, standingHeight, hitZ - distanceToJudgementLine);
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
                CreateCube(parent, "Lane Energy Strip " + (lane + 1), new Vector3(x, -0.015f, 26f), new Vector3(0.24f, 0.018f, 58f), materials.TrackAccent);
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

        private static JudgementLineFeedback CreateHitLine(Transform parent, RuntimeMaterialLibrary materials, NeonPulseGameConfig config)
        {
            float hitZ = config.Rhythm.HitZ;
            Renderer[] tiles = new Renderer[4];
            for (int lane = 0; lane < tiles.Length; lane++)
            {
                float x = -2.7f + lane * 1.8f;
                GameObject tile = CreateCube(
                    parent,
                    "Judgement Tile " + lane,
                    new Vector3(x, 0.1f, hitZ),
                    new Vector3(1.62f, 0.12f, 0.82f),
                    materials.JudgementTile);
                tiles[lane] = tile.GetComponent<Renderer>();
            }

            return new JudgementLineFeedback(tiles, config.Visuals.JudgementLinePulseStrength);
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

    /// <summary>Allocation-free color feedback for the four translucent tiles fixed at the judgement line.</summary>
    public sealed class JudgementLineFeedback
    {
        private const float PulseDuration = 0.18f;
        private const float BaseAlpha = 0.2f;
        private const float HighlightAlpha = 0.44f;
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly Color BaseColor = new Color(0.42f, 0.55f, 0.7f, BaseAlpha);

        private readonly Renderer[] tiles;
        private readonly MaterialPropertyBlock[] propertyBlocks;
        private readonly Color[] highlightColors;
        private readonly float[] tileTimers;
        private readonly float highlightStrength;

        public JudgementLineFeedback(Renderer[] judgementTiles, float configuredPulseStrength)
        {
            tiles = judgementTiles;
            highlightStrength = Mathf.Clamp01(configuredPulseStrength * 3f);
            propertyBlocks = new MaterialPropertyBlock[tiles.Length];
            highlightColors = new Color[tiles.Length];
            tileTimers = new float[tiles.Length];
            for (int index = 0; index < tiles.Length; index++)
            {
                propertyBlocks[index] = new MaterialPropertyBlock();
                highlightColors[index] = BaseColor;
                ApplyVisual(index, 0f);
            }
        }

        /// <summary>Changes the fixed judgement tile at the contact lane toward the incoming tile color.</summary>
        public void Highlight(float worldX, Color color)
        {
            int lane = Mathf.Clamp(Mathf.RoundToInt((worldX + 2.7f) / 1.8f), 0, tiles.Length - 1);
            color.a = HighlightAlpha;
            highlightColors[lane] = color;
            tileTimers[lane] = PulseDuration;
        }

        /// <summary>Fades every tile back to its neutral translucent state without tween allocations.</summary>
        public void Tick(float deltaTime)
        {
            for (int index = 0; index < tiles.Length; index++)
            {
                tileTimers[index] = Mathf.Max(0f, tileTimers[index] - deltaTime);
                float normalized = tileTimers[index] / PulseDuration;
                ApplyVisual(index, normalized * normalized);
            }
        }

        private void ApplyVisual(int index, float normalized)
        {
            Color displayColor = Color.Lerp(BaseColor, highlightColors[index], normalized * highlightStrength);
            MaterialPropertyBlock propertyBlock = propertyBlocks[index];
            propertyBlock.SetColor(ColorPropertyId, displayColor);
            tiles[index].SetPropertyBlock(propertyBlock);
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

                ParticleSystem.NoiseModule noise = system.noise;
                noise.enabled = true;
                noise.quality = ParticleSystemNoiseQuality.Low;
                noise.strength = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
                noise.frequency = 0.75f;
                noise.scrollSpeed = 0.25f;

                ParticleSystemRenderer renderer = effectObject.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 2.4f;
                renderer.velocityScale = 0.16f;
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

    /// <summary>Reusable full-screen flash with no per-frame allocation.</summary>
    public sealed class ScreenFlashFeedback
    {
        private const int SortingOrder = 40;

        private readonly Image overlay;
        private readonly float duration;
        private readonly float intensity;
        private Color flashColor;
        private float currentIntensity;
        private float remainingTime;

        public ScreenFlashFeedback(Transform parent, float configuredDuration, float configuredIntensity)
        {
            duration = Mathf.Max(0.01f, configuredDuration);
            intensity = Mathf.Clamp01(configuredIntensity);

            GameObject canvasObject = new GameObject("Screen Flash Feedback");
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            GameObject overlayObject = new GameObject("Flash Overlay", typeof(RectTransform));
            overlayObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rectTransform = (RectTransform)overlayObject.transform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            overlay = overlayObject.AddComponent<Image>();
            overlay.raycastTarget = false;
            overlay.enabled = false;
        }

        /// <summary>Starts or refreshes the flash using the feedback color.</summary>
        public void Play(Color color, float strengthMultiplier = 1f)
        {
            flashColor = color;
            flashColor.a = 1f;
            currentIntensity = intensity * Mathf.Clamp01(strengthMultiplier);
            remainingTime = duration;
            overlay.color = new Color(color.r, color.g, color.b, currentIntensity);
            overlay.enabled = true;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (remainingTime <= 0f)
            {
                return;
            }

            remainingTime = Mathf.Max(0f, remainingTime - unscaledDeltaTime);
            float normalized = remainingTime / duration;
            float alpha = normalized * normalized * currentIntensity;
            overlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
            if (remainingTime <= 0f)
            {
                overlay.enabled = false;
            }
        }

        public void Clear()
        {
            remainingTime = 0f;
            currentIntensity = 0f;
            overlay.enabled = false;
        }
    }
}

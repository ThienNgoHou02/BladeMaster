using UnityEngine;

namespace NeonPulse
{
    /// <summary>Creates and owns all temporary materials used by the runtime-only prototype.</summary>
    public sealed class RuntimeMaterialLibrary
    {
        public readonly Color CyanColor;
        public readonly Color MagentaColor;
        public readonly Color PurpleColor;
        public readonly Color YellowColor;

        private readonly float neonIntensity;
        private readonly float beatPulseIntensity;

        public Material Cyan { get; }
        public Material Magenta { get; }
        public Material Purple { get; }
        public Material Yellow { get; }
        public Material Dark { get; }
        public Material Obstacle { get; }
        public Material White { get; }
        public Material Footprint { get; }
        public Material CyanGlow { get; }
        public Material MagentaGlow { get; }
        public Material PurpleGlow { get; }
        public Material YellowGlow { get; }

        public RuntimeMaterialLibrary(VisualSettings settings)
        {
            VisualSettings safeSettings = settings ?? new VisualSettings();
            CyanColor = safeSettings.Cyan;
            MagentaColor = safeSettings.Magenta;
            PurpleColor = safeSettings.Purple;
            YellowColor = safeSettings.Yellow;
            neonIntensity = safeSettings.NeonIntensity;
            beatPulseIntensity = safeSettings.BeatPulseIntensity;

            Cyan = CreateEmissive("Neon Cyan", CyanColor, neonIntensity);
            Magenta = CreateEmissive("Neon Magenta", MagentaColor, neonIntensity);
            Purple = CreateEmissive("Neon Purple", PurpleColor, neonIntensity * 0.82f);
            Yellow = CreateEmissive("Neon Yellow", YellowColor, neonIntensity * 1.1f);
            Dark = CreateEmissive("Track Black", new Color(0.006f, 0.008f, 0.02f, 1f), 0.02f);
            Obstacle = CreateEmissive("Obstacle Red", safeSettings.Obstacle, neonIntensity * 0.68f);
            White = CreateParticle("Feedback White");
            Footprint = CreateEmissive("Footprint White", Color.white, neonIntensity * 1.35f);
            CyanGlow = CreateGlow("Cyan Aura", CyanColor);
            MagentaGlow = CreateGlow("Magenta Aura", MagentaColor);
            PurpleGlow = CreateGlow("Purple Aura", PurpleColor);
            YellowGlow = CreateGlow("Yellow Aura", YellowColor);
        }

        /// <summary>Pulses shared emission on the beat without creating material instances per renderer.</summary>
        public void SetBeatPulse(float normalizedPulse)
        {
            float pulse = Mathf.Clamp01(normalizedPulse);
            SetEmission(Cyan, CyanColor, neonIntensity + pulse * beatPulseIntensity);
            SetEmission(Magenta, MagentaColor, neonIntensity + pulse * beatPulseIntensity);
            SetEmission(Purple, PurpleColor, neonIntensity * 0.82f + pulse * beatPulseIntensity * 0.8f);
            SetEmission(Yellow, YellowColor, neonIntensity * 1.1f + pulse * beatPulseIntensity * 1.15f);
        }

        /// <summary>Destroys all generated material instances.</summary>
        public void Dispose()
        {
            DestroyMaterial(Cyan);
            DestroyMaterial(Magenta);
            DestroyMaterial(Purple);
            DestroyMaterial(Yellow);
            DestroyMaterial(Dark);
            DestroyMaterial(Obstacle);
            DestroyMaterial(White);
            DestroyMaterial(Footprint);
            DestroyMaterial(CyanGlow);
            DestroyMaterial(MagentaGlow);
            DestroyMaterial(PurpleGlow);
            DestroyMaterial(YellowGlow);
        }

        private static Material CreateEmissive(string materialName, Color color, float intensity)
        {
            Shader shader = Resources.Load<Shader>("NeonPulseUnlit");
            if (shader == null)
            {
                shader = Shader.Find("NeonPulse/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Hidden/InternalErrorShader");
            }

            Material material = new Material(shader)
            {
                name = materialName,
                color = color
            };

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * intensity);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.15f);
                material.SetFloat("_Glossiness", 0.75f);
            }

            return material;
        }

        private static Material CreateGlow(string materialName, Color color)
        {
            Shader shader = Resources.Load<Shader>("NeonPulseGlow");
            if (shader == null)
            {
                shader = Shader.Find("NeonPulse/Glow");
            }

            if (shader == null)
            {
                shader = Shader.Find("Hidden/InternalErrorShader");
            }

            return new Material(shader)
            {
                name = materialName,
                color = new Color(color.r, color.g, color.b, 0.48f)
            };
        }

        private static void SetEmission(Material material, Color color, float intensity)
        {
            if (material != null && material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * intensity);
            }
        }

        private static Material CreateParticle(string materialName)
        {
            Shader shader = Resources.Load<Shader>("NeonPulseParticle");
            if (shader == null)
            {
                shader = Shader.Find("NeonPulse/Particle");
            }

            if (shader == null)
            {
                shader = Shader.Find("Hidden/InternalErrorShader");
            }

            return new Material(shader)
            {
                name = materialName,
                color = Color.white
            };
        }

        private static void DestroyMaterial(Material material)
        {
            if (material != null)
            {
                Object.Destroy(material);
            }
        }
    }
}

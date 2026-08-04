using UnityEngine;

namespace NeonPulse
{
    /// <summary>Creates and owns all temporary materials used by the runtime-only prototype.</summary>
    public sealed class RuntimeMaterialLibrary
    {
        public readonly Color CyanColor = new Color(0.02f, 1f, 0.95f, 1f);
        public readonly Color MagentaColor = new Color(1f, 0.03f, 0.72f, 1f);
        public readonly Color PurpleColor = new Color(0.48f, 0.05f, 1f, 1f);
        public readonly Color YellowColor = new Color(1f, 0.82f, 0.05f, 1f);

        public Material Cyan { get; }
        public Material Magenta { get; }
        public Material Purple { get; }
        public Material Yellow { get; }
        public Material Dark { get; }
        public Material Obstacle { get; }
        public Material White { get; }

        public RuntimeMaterialLibrary()
        {
            Cyan = CreateEmissive("Neon Cyan", CyanColor, 2.2f);
            Magenta = CreateEmissive("Neon Magenta", MagentaColor, 2.2f);
            Purple = CreateEmissive("Neon Purple", PurpleColor, 1.8f);
            Yellow = CreateEmissive("Neon Yellow", YellowColor, 2.4f);
            Dark = CreateEmissive("Track Black", new Color(0.006f, 0.008f, 0.02f, 1f), 0.02f);
            Obstacle = CreateEmissive("Obstacle Red", new Color(1f, 0.06f, 0.2f, 1f), 1.5f);
            White = CreateParticle("Feedback White");
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

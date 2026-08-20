Shader "NeonPulse/Trail Glow"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0.5, 3)) = 1.65
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+25" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
            };

            struct VertexToFragment
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            fixed4 _Color;
            half _Intensity;

            VertexToFragment vert(AppData input)
            {
                VertexToFragment output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(VertexToFragment input) : SV_Target
            {
                return fixed4(input.color.rgb * _Intensity, input.color.a);
            }
            ENDCG
        }
    }
}

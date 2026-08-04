Shader "NeonPulse/Unlit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
            };

            struct VertexToFragment
            {
                float4 vertex : SV_POSITION;
                UNITY_FOG_COORDS(0)
            };

            fixed4 _Color;
            fixed4 _EmissionColor;

            VertexToFragment vert(AppData input)
            {
                VertexToFragment output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                UNITY_TRANSFER_FOG(output, output.vertex);
                return output;
            }

            fixed4 frag(VertexToFragment input) : SV_Target
            {
                fixed4 color = _Color + _EmissionColor * 0.18;
                color.a = _Color.a;
                UNITY_APPLY_FOG(input.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }
}

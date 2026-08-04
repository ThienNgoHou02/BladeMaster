Shader "NeonPulse/Unlit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
        _RimPower ("Rim Power", Range(1, 8)) = 2.4
        _PulseSpeed ("Pulse Speed", Range(0, 12)) = 4
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
                float3 normal : NORMAL;
            };

            struct VertexToFragment
            {
                float4 vertex : SV_POSITION;
                float3 worldPosition : TEXCOORD1;
                half3 worldNormal : TEXCOORD2;
                half3 viewDirection : TEXCOORD3;
                UNITY_FOG_COORDS(0)
            };

            fixed4 _Color;
            fixed4 _EmissionColor;
            half _RimPower;
            half _PulseSpeed;

            VertexToFragment vert(AppData input)
            {
                VertexToFragment output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.viewDirection = UnityWorldSpaceViewDir(output.worldPosition);
                UNITY_TRANSFER_FOG(output, output.vertex);
                return output;
            }

            fixed4 frag(VertexToFragment input) : SV_Target
            {
                half3 normal = normalize(input.worldNormal);
                half3 viewDirection = normalize(input.viewDirection);
                half rim = pow(1.0h - saturate(dot(normal, viewDirection)), _RimPower);
                half scanline = 0.5h + 0.5h * sin(input.worldPosition.y * 13.0h - _Time.y * _PulseSpeed);
                half pulse = 0.75h + 0.25h * sin(_Time.y * _PulseSpeed);
                fixed3 neon = _Color.rgb * 0.38h;
                neon += _EmissionColor.rgb * (0.12h + rim * 0.5h + scanline * 0.06h + pulse * 0.06h);
                fixed4 color = fixed4(neon, _Color.a);
                color.a = _Color.a;
                UNITY_APPLY_FOG(input.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }
}

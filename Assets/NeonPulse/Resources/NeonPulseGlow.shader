Shader "NeonPulse/Glow"
{
    Properties
    {
        _Color ("Glow Color", Color) = (1,1,1,0.5)
        _RimPower ("Rim Power", Range(0.5, 6)) = 1.8
        _PulseSpeed ("Pulse Speed", Range(0, 12)) = 5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        Cull Off
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
                float3 normal : NORMAL;
            };

            struct VertexToFragment
            {
                float4 vertex : SV_POSITION;
                half3 worldNormal : TEXCOORD0;
                half3 viewDirection : TEXCOORD1;
            };

            fixed4 _Color;
            half _RimPower;
            half _PulseSpeed;

            VertexToFragment vert(AppData input)
            {
                VertexToFragment output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                float3 worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.viewDirection = UnityWorldSpaceViewDir(worldPosition);
                return output;
            }

            fixed4 frag(VertexToFragment input) : SV_Target
            {
                half rim = pow(1.0h - saturate(dot(normalize(input.worldNormal), normalize(input.viewDirection))), _RimPower);
                half pulse = 0.75h + 0.25h * sin(_Time.y * _PulseSpeed);
                half alpha = _Color.a * (0.15h + rim * 0.85h) * pulse;
                return fixed4(_Color.rgb * (1.0h + rim), alpha);
            }
            ENDCG
        }
    }
}

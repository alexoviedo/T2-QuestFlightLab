Shader "QuestFlightLab/Production Stable Water"
{
    Properties
    {
        [HideInInspector] _MainTex ("Compatibility", 2D) = "white" {}
        _Color ("Deep water color", Color) = (0.16,0.35,0.43,1)
        _HorizonColor ("Grazing-angle color", Color) = (0.53,0.66,0.69,1)
        _Roughness ("Roughness", Range(0,1)) = 0.46
        _RippleStrength ("Static ripple strength", Range(0,1)) = 0.72
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+5" }
        LOD 90
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _Color;
            fixed4 _HorizonColor;
            half _Roughness;
            half _RippleStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                half3 normal : NORMAL;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                half3 worldNormal : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                // Two static, world-space wave fields break the flat highlight without UV motion,
                // textures, screen-space reflection, or camera-relative state.
                half2 firstWave = half2(
                    sin(input.worldPosition.x * 0.030 + input.worldPosition.z * 0.019),
                    cos(input.worldPosition.x * 0.023 - input.worldPosition.z * 0.033));
                half2 secondWave = half2(
                    cos(input.worldPosition.x * 0.011 - input.worldPosition.z * 0.027),
                    sin(input.worldPosition.x * 0.037 + input.worldPosition.z * 0.013));
                half2 slope = (firstWave * 0.052 + secondWave * 0.027) * _RippleStrength;
                half3 rippleNormal = normalize(half3(-slope.x, 1.0, -slope.y));
                half3 normal = normalize(lerp(normalize(input.worldNormal), rippleNormal, 0.82));
                half3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                half3 viewDirection = normalize(_WorldSpaceCameraPos - input.worldPosition);
                half3 halfDirection = normalize(lightDirection + viewDirection);
                half diffuse = saturate(dot(normal, lightDirection));
                half fresnel = pow(1.0 - saturate(dot(normal, viewDirection)), 3.0);
                half specularPower = lerp(64.0, 10.0, _Roughness);
                half specular = pow(saturate(dot(normal, halfDirection)), specularPower) * (1.0 - _Roughness) * 0.34;
                half broadVariation = 0.5 + 0.5 * sin(input.worldPosition.x * 0.0019 + input.worldPosition.z * 0.0027);
                fixed3 baseColor = _Color.rgb * lerp(0.90, 1.08, broadVariation);
                baseColor = lerp(baseColor, _HorizonColor.rgb, fresnel * 0.38);
                half3 ambient = ShadeSH9(half4(normal, 1.0));
                fixed3 color = baseColor * (ambient + _LightColor0.rgb * diffuse * 0.68) + _LightColor0.rgb * specular;
                fixed4 output = fixed4(color, 1.0);
                UNITY_APPLY_FOG(input.fogCoord, output);
                return output;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}

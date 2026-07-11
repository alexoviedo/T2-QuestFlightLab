Shader "QuestFlightLab/KBDU Stable Water"
{
    Properties
    {
        [HideInInspector] _MainTex ("Compatibility main texture", 2D) = "white" {}
        _Color ("Water color", Color) = (0.10,0.25,0.33,1)
        _Roughness ("Roughness", Range(0,1)) = 0.58
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+5" }
        LOD 80
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
            half _Roughness;

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
                half variation : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.variation = 0.5 + 0.5 * sin(output.worldPosition.x * 0.0037 + output.worldPosition.z * 0.0049);
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                half3 normal = normalize(input.worldNormal);
                half3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                half3 viewDirection = normalize(_WorldSpaceCameraPos - input.worldPosition);
                half3 halfDirection = normalize(lightDirection + viewDirection);
                half diffuse = saturate(dot(normal, lightDirection));
                half specularPower = lerp(44.0, 7.0, _Roughness);
                half specular = pow(saturate(dot(normal, halfDirection)), specularPower) * (1.0 - _Roughness) * 0.22;
                fixed3 baseColor = _Color.rgb * lerp(0.94, 1.04, input.variation);
                half3 ambient = ShadeSH9(half4(normal, 1.0));
                fixed3 color = baseColor * (ambient + _LightColor0.rgb * diffuse * 0.72) + _LightColor0.rgb * specular;
                fixed4 output = fixed4(color, 1.0);
                UNITY_APPLY_FOG(input.fogCoord, output);
                return output;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}

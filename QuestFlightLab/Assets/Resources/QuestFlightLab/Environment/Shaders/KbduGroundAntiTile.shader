Shader "QuestFlightLab/KBDU Ground AntiTile"
{
    Properties
    {
        [HideInInspector] _MainTex ("Compatibility main texture", 2D) = "white" {}
        _DryGrassTex ("Dry prairie (CC0)", 2D) = "white" {}
        _GreenGrassTex ("Sparse green grass (CC0)", 2D) = "white" {}
        _SoilTex ("Dry soil (CC0)", 2D) = "white" {}
        _Tint ("Land-cover tint", Color) = (1,1,1,1)
        _MicroScale ("World micro scale", Float) = 0.0666667
        _MacroScale ("World macro scale", Float) = 0.00454545
        _GreenBlend ("Green blend", Range(0,1)) = 0.16
        _SoilBlend ("Soil blend", Range(0,1)) = 0.22
        _Roughness ("Roughness", Range(0,1)) = 0.82
        _DetailFadeStart ("Detail fade start", Float) = 350
        _DetailFadeEnd ("Detail fade end", Float) = 2400
        [PerRendererData] _BatchUvTransform ("Batch cos/sin/offset", Vector) = (1,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 120
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _DryGrassTex;
            sampler2D _GreenGrassTex;
            sampler2D _SoilTex;
            fixed4 _Tint;
            half _MicroScale;
            half _MacroScale;
            half _GreenBlend;
            half _SoilBlend;
            half _Roughness;
            half _DetailFadeStart;
            half _DetailFadeEnd;
            float4 _BatchUvTransform;

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
                half3 variation : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                float2 macroUv = output.worldPosition.xz * _MacroScale;
                half macro = 0.5 + 0.25 * sin(macroUv.x * 6.2831853) + 0.25 * cos(macroUv.y * 5.117 + macroUv.x * 1.73);
                half mid = 0.5 + 0.5 * sin((macroUv.x * 7.1 - macroUv.y * 5.3) * 6.2831853);
                half cameraDistance = distance(_WorldSpaceCameraPos, output.worldPosition);
                output.variation = half3(saturate(macro), saturate(mid), cameraDistance);
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 world = input.worldPosition.xz;
                float2 variedWorld = float2(
                    world.x * _BatchUvTransform.x - world.y * _BatchUvTransform.y,
                    world.x * _BatchUvTransform.y + world.y * _BatchUvTransform.x) + _BatchUvTransform.zw;
                float2 dryUv = world * _MicroScale;
                float2 greenUv = float2(-variedWorld.y, variedWorld.x) * (_MicroScale * 0.83) + float2(17.31, 9.17);
                float2 soilUv = float2(
                    variedWorld.x * 0.819 + variedWorld.y * 0.574,
                    -variedWorld.x * 0.574 + variedWorld.y * 0.819) * (_MicroScale * 1.13) + float2(31.73, 4.91);

                fixed3 dry = tex2D(_DryGrassTex, dryUv).rgb;
                fixed3 green = tex2D(_GreenGrassTex, greenUv).rgb;
                fixed3 soil = tex2D(_SoilTex, soilUv).rgb;
                half greenWeight = saturate(_GreenBlend + (input.variation.y - 0.5) * 0.22);
                half soilWeight = saturate(_SoilBlend + (input.variation.x - 0.5) * 0.28);
                fixed3 textured = lerp(lerp(dry, green, greenWeight), soil, soilWeight);
                half fade = smoothstep(_DetailFadeStart, max(_DetailFadeStart + 1.0, _DetailFadeEnd), input.variation.z);
                fixed3 farColor = lerp(fixed3(0.55, 0.55, 0.48), fixed3(0.8, 0.79, 0.67), input.variation.x);
                fixed3 albedo = lerp(textured, farColor, fade) * _Tint.rgb;
                albedo *= lerp(0.88, 1.09, input.variation.x);

                half3 normal = normalize(input.worldNormal);
                half3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                half diffuse = saturate(dot(normal, lightDirection));
                half3 viewDirection = normalize(_WorldSpaceCameraPos - input.worldPosition);
                half3 halfDirection = normalize(lightDirection + viewDirection);
                half specularPower = lerp(28.0, 5.0, _Roughness);
                half specular = pow(saturate(dot(normal, halfDirection)), specularPower) * (1.0 - _Roughness) * 0.08;
                half3 ambient = ShadeSH9(half4(normal, 1.0));
                fixed3 color = albedo * (ambient + _LightColor0.rgb * diffuse) + _LightColor0.rgb * specular;
                fixed4 output = fixed4(color, 1.0);
                UNITY_APPLY_FOG(input.fogCoord, output);
                return output;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}

// 3D 모델을 2D 스프라이트로 구울 때 쓰는 툰 셰이더 (굽기 전용, 게임 중에는 쓰지 않음).
// 키라이트 3단 명암 + 하늘/바닥 반구광 + 윗면 하이라이트 + 림라이트 + 채도 보정.
// 외곽선·안쪽 선은 굽기 단계에서 알파·노멀·깊이 경계로 그립니다.
Shader "ShadowRPG/ToonBake"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Color", Color) = (0.30, 0.27, 0.42, 1)
        _MidColor ("Mid Shade", Color) = (0.70, 0.66, 0.80, 1)
        _LightDir ("Light Direction (world)", Vector) = (-0.45, 0.75, -0.55, 0)
        _SkyColor ("Sky Ambient", Color) = (0.20, 0.24, 0.36, 1)
        _GroundColor ("Ground Ambient", Color) = (0.06, 0.04, 0.05, 1)
        _HighlightColor ("Top Highlight", Color) = (1.0, 0.95, 0.85, 1)
        _HighlightAmount ("Top Highlight Amount", Float) = 0.18
        _RimColor ("Rim Color", Color) = (0.35, 0.6, 1.0, 1)
        _RimPower ("Rim Power", Float) = 3.0
        _RimAmount ("Rim Amount", Float) = 1.0
        _Saturation ("Saturation", Float) = 1.12
        _Exposure ("Exposure", Float) = 1.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Toon"
            ZWrite On
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tint, _ShadowColor, _MidColor, _LightDir, _SkyColor, _GroundColor, _HighlightColor, _RimColor;
                float _HighlightAmount, _RimPower, _RimAmount, _Saturation, _Exposure;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 l = normalize(_LightDir.xyz);
                float ndl = dot(n, l);
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb * _Tint.rgb;

                // 3단 명암 (그림자 / 중간 / 밝음) — 경계는 살짝 부드럽게
                float lit = smoothstep(0.08, 0.16, ndl);
                float mid = smoothstep(-0.42, -0.32, ndl);
                half3 shade = lerp(_ShadowColor.rgb, lerp(_MidColor.rgb, 1.0, lit), mid);

                // 반구광: 위를 향한 면은 하늘색, 아래는 바닥색
                half3 ambient = lerp(_GroundColor.rgb, _SkyColor.rgb, n.y * 0.5 + 0.5);
                half3 color = albedo * (shade + ambient) * _Exposure;

                // 윗면 하이라이트 (손그림 채색의 밝은 윗면 느낌)
                float top = smoothstep(0.55, 0.8, n.y) * lit;
                color = lerp(color, color * _HighlightColor.rgb * 1.35, top * _HighlightAmount);

                // 림라이트 (실루엣 뒤쪽 빛)
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float rim = pow(saturate(1.0 - dot(n, v)), _RimPower) * saturate(0.4 - ndl * 0.6);
                color += _RimColor.rgb * rim * _RimAmount;

                // 채도 보정
                half luma = dot(color, half3(0.299, 0.587, 0.114));
                color = lerp(luma.xxx, color, _Saturation);
                return half4(saturate(color), 1);
            }
            ENDHLSL
        }
    }
}

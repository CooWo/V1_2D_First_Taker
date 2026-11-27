// 3D 모델을 2D 스프라이트로 구울 때 쓰는 어두운 툰 셰이더.
// 고정 조명 방향 + 2단 명암 + 어두운 그림자색 + 푸른 림라이트. (외곽선은 굽기 단계에서 알파 경계로 그립니다)
Shader "ShadowRPG/ArtTest/ToonBake"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Color", Color) = (0.34, 0.32, 0.5, 1)
        _MidColor ("Mid Shade", Color) = (0.72, 0.7, 0.85, 1)
        _LightDir ("Light Direction (world)", Vector) = (-0.4, 0.65, -0.75, 0)
        _RimColor ("Rim Color", Color) = (0.35, 0.65, 1.0, 1)
        _RimPower ("Rim Power", Float) = 3.0
        _Exposure ("Exposure", Float) = 1.15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
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
                float4 _Tint, _ShadowColor, _MidColor, _LightDir, _RimColor;
                float _RimPower, _Exposure;
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

                // 2단 명암 (밝음 / 중간 / 그림자)
                float lit = smoothstep(0.05, 0.12, ndl);
                float mid = smoothstep(-0.45, -0.38, ndl);
                half3 shade = lerp(_ShadowColor.rgb, lerp(_MidColor.rgb, 1.0, lit), mid);
                half3 color = albedo * shade * _Exposure;

                // 실루엣 림라이트 (뒤쪽 푸른 빛)
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float rim = pow(saturate(1.0 - dot(n, v)), _RimPower) * saturate(0.35 - ndl * 0.65);
                color += _RimColor.rgb * rim;

                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}

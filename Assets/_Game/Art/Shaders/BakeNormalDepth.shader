// 굽기 전용: 화면 공간 노멀(rgb)과 깊이(a)를 기록합니다. 안쪽 선(접힘·겹침 경계)을 그리는 데 씁니다.
Shader "ShadowRPG/BakeNormalDepth"
{
    Properties { _Near ("Near", Float) = 0.1 _Far ("Far", Float) = 100 }
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

            CBUFFER_START(UnityPerMaterial)
                float _Near, _Far;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalVS : TEXCOORD0; float depth : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(v.normalOS));
                o.depth = -TransformWorldToView(positionWS).z;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalVS);
                return half4(n * 0.5 + 0.5, saturate((i.depth - _Near) / (_Far - _Near)));
            }
            ENDHLSL
        }
    }
}

Shader "Zoo/Ground Blob Shadow"
{
    Properties
    {
        _Color ("Shadow Color", Color) = (0, 0, 0, 0.32)
        _Softness ("Soft Edge", Range(0.05, 0.95)) = 0.42
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-10"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BlobShadow"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Softness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half2 centeredUv = (input.uv - 0.5h) * 2.0h;
                half radiusSquared = dot(centeredUv, centeredUv);
                half innerEdge = saturate(1.0h - _Softness);
                half fade = 1.0h - smoothstep(innerEdge, 1.0h, radiusSquared);
                return half4(_Color.rgb, _Color.a * fade);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

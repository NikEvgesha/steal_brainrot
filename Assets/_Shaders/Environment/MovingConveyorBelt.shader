Shader "StealBrainrot/Environment/MovingConveyorBelt"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.07, 0.105, 0.13, 1)
        _StripeColor ("Stripe Color", Color) = (0.22, 0.31, 0.36, 1)
        _BeltOffset ("Belt Offset", Float) = 0
        _BeltAxis ("Belt Axis", Vector) = (1, 0, 0, 0)
        _StripeDensity ("Stripe Density", Float) = 2.75
        _StripeWidth ("Stripe Width", Range(0.01, 1)) = 0.28
        _StripeSoftness ("Stripe Softness", Range(0.001, 0.5)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _StripeColor;
                float _BeltOffset;
                float4 _BeltAxis;
                float _StripeDensity;
                half _StripeWidth;
                half _StripeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 axis = _BeltAxis.xyz;
                axis = dot(axis, axis) > 0.0001 ? normalize(axis) : float3(1, 0, 0);

                float coord = dot(input.positionOS, axis) * _StripeDensity - _BeltOffset;
                half centered = abs(frac(coord) - 0.5) * 2.0;
                half stripe = 1.0 - smoothstep(_StripeWidth, _StripeWidth + _StripeSoftness, centered);

                half3 color = lerp(_BaseColor.rgb, _StripeColor.rgb, stripe);
                half light = saturate(input.normalWS.y * 0.35 + 0.78);
                return half4(color * light, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

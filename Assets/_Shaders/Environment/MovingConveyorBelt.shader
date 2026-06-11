Shader "StealBrainrot/Environment/MovingConveyorBelt"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.07, 0.105, 0.13, 1)
        _StripeColor ("Stripe Color", Color) = (0.22, 0.31, 0.36, 1)
        _BeltOffset ("Belt Offset", Float) = 0
        _BeltAxis ("Belt Axis", Vector) = (1, 0, 0, 0)
        _BeltSideAxis ("Belt Side Axis", Vector) = (0, 0, 1, 0)
        _BeltHalfWidth ("Belt Half Width", Float) = 1
        _StripeDensity ("Arrow Density", Float) = 0.045
        _StripeWidth ("Arrow Thickness", Range(0.01, 0.5)) = 0.075
        _StripeSoftness ("Arrow Softness", Range(0.001, 0.25)) = 0.02
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
                float4 _BeltSideAxis;
                float _BeltHalfWidth;
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

                float3 sideAxis = _BeltSideAxis.xyz;
                sideAxis = dot(sideAxis, sideAxis) > 0.0001 ? normalize(sideAxis) : normalize(cross(float3(0, 1, 0), axis));
                sideAxis = dot(sideAxis, sideAxis) > 0.0001 ? sideAxis : float3(0, 0, 1);

                float along = dot(input.positionOS, axis);
                float side = dot(input.positionOS, sideAxis);
                float side01 = saturate(abs(side) / max(_BeltHalfWidth, 0.001));
                float cell = frac(along * _StripeDensity - _BeltOffset);
                float arrowCenter = lerp(0.76, 0.18, side01);
                float arrowDistance = abs(cell - arrowCenter);
                arrowDistance = min(arrowDistance, 1.0 - arrowDistance);

                half edgeFade = 1.0 - smoothstep(0.88, 1.02, side01);
                half stripe = (1.0 - smoothstep(_StripeWidth, _StripeWidth + _StripeSoftness, arrowDistance)) * edgeFade;

                half3 color = lerp(_BaseColor.rgb, _StripeColor.rgb, stripe);
                half light = saturate(input.normalWS.y * 0.35 + 0.78);
                return half4(color * light, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

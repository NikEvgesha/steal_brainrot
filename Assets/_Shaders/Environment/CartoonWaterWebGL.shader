Shader "Custom/WebGL/CartoonWater"
{
    Properties
    {
        _MainTex ("Water Tile", 2D) = "white" {}
        _Tint ("Tint", Color) = (0.7, 1, 1, 1)
        _FoamTint ("Foam Tint", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.82
        _FoamStrength ("Foam Strength", Range(0, 2)) = 0.7
        _ScrollA ("Scroll A", Vector) = (0.015, 0.008, 0, 0)
        _ScrollB ("Scroll B", Vector) = (-0.008, 0.012, 0, 0)
        _SecondaryScale ("Secondary Scale", Range(0.5, 3)) = 1.15
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.25)) = 0.015
        _WaveFrequency ("Wave Frequency", Range(0, 4)) = 0.55
        _WaveSpeed ("Wave Speed", Range(0, 6)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half4 _FoamTint;
                half _Alpha;
                half _FoamStrength;
                float4 _ScrollA;
                float4 _ScrollB;
                half _SecondaryScale;
                half _WaveAmplitude;
                half _WaveFrequency;
                half _WaveSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half wave : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                half wave = sin((worldPos.x + worldPos.z) * _WaveFrequency + _Time.y * _WaveSpeed);
                input.positionOS.y += wave * _WaveAmplitude;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.wave = wave;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uvA = input.uv + _ScrollA.xy * _Time.y;
                float2 uvB = input.uv * _SecondaryScale + _ScrollB.xy * _Time.y;
                half4 texA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvA);
                half4 texB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB);
                half4 water = texA * 0.68 + texB * 0.32;

                half foam = saturate((water.r + water.g + water.b - 2.18) * 2.2) * _FoamStrength;
                half3 color = water.rgb * _Tint.rgb;
                color = lerp(color, _FoamTint.rgb, foam);
                color += input.wave * 0.035;

                return half4(saturate(color), _Alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

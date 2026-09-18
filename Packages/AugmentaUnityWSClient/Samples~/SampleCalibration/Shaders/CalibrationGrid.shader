Shader "Augmenta/Calibration Grid"
{
    Properties
    {
        _Color ("Fill Color", Color) = (1, 1, 1, 0.1)
        _LineColor ("Line Color", Color) = (1, 1, 1, 0.5)
        _Size ("Size (meters)", Vector) = (1, 1, 0, 0)
        _LineWidth ("Line Width (pixels)", Float) = 1.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Overlay"
            Tags { "LightMode" = "UniversalForward" }

            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _LineColor;
                float4 _Size;
                float _LineWidth;
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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * _Size.xy;
                float2 d = abs(frac(p - 0.5) - 0.5) / fwidth(p);
                float lineMask = 1 - saturate(min(d.x, d.y) / _LineWidth);
                return lerp(_Color, _LineColor, lineMask);
            }
            ENDHLSL
        }
    }
}

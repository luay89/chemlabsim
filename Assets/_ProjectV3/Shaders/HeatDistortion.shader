// ChemLabSim v3 — Heat Distortion Shader (URP)
// Fullscreen post-process effect that distorts the screen around heat sources.
// Applied via a ScriptableRendererFeature + custom pass.
//
// Properties:
//   _DistortionStrength – overall effect intensity (0 = off)
//   _HeatCenter         – screen-space UV of the heat source
//   _HeatRadius         – radius of distortion in UV space
//   _AnimSpeed          – speed of the ripple animation

Shader "ChemLabSim/HeatDistortion"
{
    Properties
    {
        _MainTex ("Screen Texture", 2D) = "white" {}
        _DistortionStrength ("Distortion Strength", Range(0, 0.1)) = 0.02
        _HeatCenter ("Heat Center UV", Vector) = (0.5, 0.4, 0, 0)
        _HeatRadius ("Heat Radius", Range(0, 1)) = 0.25
        _AnimSpeed ("Animation Speed", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "HeatDistortionPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half   _DistortionStrength;
                half4  _HeatCenter;
                half   _HeatRadius;
                half   _AnimSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 center = _HeatCenter.xy;
                float dist = distance(uv, center);

                if (dist < _HeatRadius && _DistortionStrength > 0.001)
                {
                    half time = _Time.y * _AnimSpeed;

                    // Falloff: stronger near center
                    half falloff = 1.0 - saturate(dist / _HeatRadius);
                    falloff = falloff * falloff; // quadratic falloff

                    // Ripple distortion
                    half ripple = sin(dist * 40.0 - time * 6.0) * _DistortionStrength * falloff;

                    // Heat shimmer (high frequency)
                    half shimmerX = sin(uv.y * 80.0 + time * 8.0) * 0.3;
                    half shimmerY = cos(uv.x * 60.0 + time * 6.5) * 0.3;

                    float2 offset;
                    offset.x = ripple * (1.0 + shimmerX);
                    offset.y = ripple * (1.0 + shimmerY) * 1.3; // stronger vertical

                    uv += offset;
                }

                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

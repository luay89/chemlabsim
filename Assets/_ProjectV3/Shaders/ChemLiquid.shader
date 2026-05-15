// ChemLabSim v3 — Layered Chemistry Liquid Shader (URP)
// 3-band rendering system:
//   Bottom: precipitate (solid)
//   Middle: liquid solution
//   Top: foam / bubbles
//
// Each band has independent:
//   - height (_SolidHeight / _LiquidHeight / _FoamHeight)
//   - color  (_SolidColor / _LiquidColor / _FoamColor)
//   - animation speed (_SolidAnimSpeed / _LiquidAnimSpeed / _FoamAnimSpeed)
//
// Bands are masked in Y (UV.y) with smoothstep transitions (no hard edges).

Shader "ChemLabSim/Liquid"
{
    Properties
    {
        _SolidColor  ("Solid Color", Color) = (0.6, 0.55, 0.5, 0.92)
        _LiquidColor ("Liquid Color", Color) = (0.32, 0.52, 0.72, 0.80)
        _FoamColor   ("Foam Color", Color) = (1, 1, 1, 0.65)

        _SolidHeight  ("Solid Height", Range(0, 1)) = 0.1
        _LiquidHeight ("Liquid Height", Range(0, 1)) = 0.6
        _FoamHeight   ("Foam Height", Range(0, 0.3)) = 0.06

        // Backward-compatible aggregate fill (not used for masking logic).
        _FillAmount ("Fill Amount (Compat)", Range(0, 1)) = 0.6

        _WobbleX ("Wobble X", Float) = 0
        _WobbleZ ("Wobble Z", Float) = 0

        _SolidAnimSpeed  ("Solid Anim Speed", Float) = 0.2
        _LiquidAnimSpeed ("Liquid Anim Speed", Float) = 1.0
        _FoamAnimSpeed   ("Foam Anim Speed", Float) = 1.5

        _BandSoftness ("Band Edge Softness", Range(0.001, 0.05)) = 0.01
        _SolidSettle  ("Solid Settle", Range(0, 1)) = 0.7

        _ContainerBottomY ("Container Bottom Y (WS)", Float) = 0
        _ContainerHeight  ("Container Height (WS)", Float) = 1

        _GlowIntensity ("Glow Intensity", Range(0, 1)) = 0
        _GlowColor ("Glow Color", Color) = (1, 0.5, 0.12, 1)
        _BubbleIntensity ("Bubble Intensity", Range(0, 1)) = 0
        _AnimSpeed ("Animation Speed", Float) = 1.0
        _FresnelPower ("Fresnel Power", Range(0.5, 5)) = 2.5
        _TopColor ("Liquid Top Tint", Color) = (0.45, 0.65, 0.85, 0.9)
        _FoamWidth ("Foam Line Width", Range(0, 0.05)) = 0.015
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ChemLiquid"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
                float2 uv          : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _SolidColor;
                half4  _LiquidColor;
                half4  _FoamColor;

                half   _SolidHeight;
                half   _LiquidHeight;
                half   _FoamHeight;
                half   _FillAmount;

                half   _WobbleX;
                half   _WobbleZ;

                half   _SolidAnimSpeed;
                half   _LiquidAnimSpeed;
                half   _FoamAnimSpeed;
                half   _BandSoftness;
                half   _SolidSettle;
                half   _ContainerBottomY;
                half   _ContainerHeight;

                half   _GlowIntensity;
                half4  _GlowColor;
                half   _BubbleIntensity;
                half   _AnimSpeed;
                half   _FresnelPower;
                half4  _TopColor;
                half   _FoamWidth;
            CBUFFER_END

            // Simple noise for bubble pattern
            half SimpleNoise(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Animated wave function for liquid surface
            half LiquidWave(float3 posWS, half time)
            {
                half speed = max(0.05h, _LiquidAnimSpeed) * max(0.05h, _AnimSpeed);
                half wave = sin(posWS.x * 3.0 + time * speed * 2.0) * _WobbleX;
                wave += cos(posWS.z * 2.5 + time * speed * 1.7) * _WobbleZ;
                wave += sin(posWS.x * 1.2 + posWS.z * 1.8 + time * speed) * 0.02;
                return wave;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionWS = posWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(posWS);
                OUT.uv = IN.uv;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half time = _Time.y;
                half yNorm = saturate((IN.positionWS.y - _ContainerBottomY) / max(_ContainerHeight, 1e-4h));

                // --- Layer boundaries (stacked from bottom to top) ---
                half settleNoise = (SimpleNoise(IN.uv * 10.0 + float2(time * _SolidAnimSpeed * 0.15, 0)) - 0.5) * 0.02 * (1.0 - _SolidSettle);
                half solidTop = saturate(_SolidHeight + settleNoise);

                half liquidTop = saturate(solidTop + _LiquidHeight + LiquidWave(IN.positionWS, time));

                half foamWave = sin((IN.positionWS.x + IN.positionWS.z) * 4.0 + time * _FoamAnimSpeed * 2.2) * _FoamWidth;
                half foamTop = saturate(liquidTop + _FoamHeight + foamWave);

                half edge = max(0.001h, _BandSoftness);
                // smoothstep-only layered blending (no hard band if-branches)
                half solidAccum = smoothstep(0.0h, max(1e-4h, solidTop), yNorm);
                half liquidAccum = smoothstep(solidTop, max(solidTop + 1e-4h, liquidTop), yNorm);
                half foamAccum = smoothstep(liquidTop, max(liquidTop + 1e-4h, liquidTop + _FoamHeight), yNorm);

                half solidMask = saturate(1.0h - solidAccum);
                half liquidMask = saturate(solidAccum - liquidAccum);
                half foamMask = saturate(liquidAccum - foamAccum);

                // Smooth top fade instead of hard discard.
                half coverage = 1.0h - smoothstep(foamTop - edge, foamTop + edge, yNorm);

                // --- Bottom solid band (rough + opaque + settles) ---
                half solidNoise = SimpleNoise(IN.uv * 22.0 + float2(time * _SolidAnimSpeed * 0.3, time * _SolidAnimSpeed * 0.1));
                half4 solidCol = _SolidColor;
                solidCol.rgb *= lerp(0.75h, 1.2h, solidNoise);
                solidCol.a = saturate(_SolidColor.a + 0.15h);

                // --- Middle liquid band (smooth + refractive-ish) ---
                half4 liquidCol = _LiquidColor;
                half distToLiquidSurface = max(0.0h, liquidTop - yNorm);
                half topBlend = saturate(1.0h - distToLiquidSurface * 8.0h);
                liquidCol = lerp(liquidCol, _TopColor, topBlend * 0.3h);

                // pseudo-refraction highlight via animated normal-ish term
                half pseudoRefraction = 0.5h + 0.5h * sin((IN.positionWS.x * 6.0 + IN.positionWS.z * 5.0) + time * _LiquidAnimSpeed * 2.0);
                liquidCol.rgb += pseudoRefraction * 0.04h;

                // --- Top foam band (noisy + fading) ---
                half4 foamCol = _FoamColor;
                float2 foamUV = IN.uv * 28.0;
                foamUV.y -= time * _FoamAnimSpeed * 1.5;
                half foamNoise = SimpleNoise(floor(foamUV));
                half foamGrain = step(0.42h, foamNoise);
                half foamFade = saturate(1.0h - (yNorm - liquidTop) / max(_FoamHeight, 0.001h));
                foamCol.a *= foamFade * lerp(0.45h, 1.0h, foamGrain);
                foamCol.rgb += foamGrain * _BubbleIntensity * 0.20h;

                half4 col = 0;
                col += solidCol * solidMask;
                col += liquidCol * liquidMask;
                col += foamCol * foamMask;

                col.rgb *= coverage;
                col.a *= coverage;

                // Bubble overlay across liquid + foam
                if (_BubbleIntensity > 0.01)
                {
                    float2 bubbleUV = IN.uv * 8.0;
                    bubbleUV.y -= time * (_FoamAnimSpeed + 0.5h);
                    half bubbleNoise = SimpleNoise(floor(bubbleUV));
                    half bubbleMask = step(0.90h, bubbleNoise);
                    half bubbleFade = frac(bubbleUV.y) * (1.0h - frac(bubbleUV.y)) * 4.0h;
                    half bubbleLayerMask = saturate(liquidMask + foamMask);
                    col.rgb += bubbleMask * bubbleFade * _BubbleIntensity * 0.35h * bubbleLayerMask;
                }

                // Glow (energy mapping)
                if (_GlowIntensity > 0.01)
                {
                    half pulse = 0.6 + 0.4 * sin(time * _AnimSpeed * 3.0);
                    half glowMix = _GlowIntensity * pulse;
                    col.rgb = lerp(col.rgb, _GlowColor.rgb, glowMix * 0.6);
                    col.rgb += _GlowColor.rgb * glowMix * 0.15; // additive bloom
                }

                // Fresnel rim
                half fresnel = pow(1.0 - saturate(dot(IN.normalWS, IN.viewDirWS)), _FresnelPower);
                col.rgb += fresnel * 0.15;
                col.a = saturate(col.a + fresnel * 0.1);

                // Fog
                col.rgb = MixFog(col.rgb, IN.fogFactor);

                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}

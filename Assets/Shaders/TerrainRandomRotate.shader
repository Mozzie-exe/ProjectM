Shader "Custom/TerrainRandomRotate"
{
    Properties
    {
        [HideInInspector] _Control("Control", 2D) = "red" {}
        [HideInInspector] _Splat0("Layer 0", 2D) = "grey" {}
        [HideInInspector] _Splat1("Layer 1", 2D) = "grey" {}
        [HideInInspector] _Splat2("Layer 2", 2D) = "grey" {}
        [HideInInspector] _Splat3("Layer 3", 2D) = "grey" {}
        _TileSize("Tile Size (meters)", Float) = 4
        _BlendDepth("Height Blend Depth", Range(0.01, 0.5)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry-100"
            "RenderType" = "Opaque"
            "TerrainCompatible" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Control); SAMPLER(sampler_Control);
            TEXTURE2D(_Splat0);  SAMPLER(sampler_Splat0);
            TEXTURE2D(_Splat1);  SAMPLER(sampler_Splat1);
            TEXTURE2D(_Splat2);  SAMPLER(sampler_Splat2);
            TEXTURE2D(_Splat3);  SAMPLER(sampler_Splat3);

            CBUFFER_START(UnityPerMaterial)
                float _TileSize;
                float _BlendDepth;
            CBUFFER_END

            // ---------- Math ----------
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 Rotate2D(float2 v, float angle)
            {
                float s, c;
                sincos(angle, s, c);
                return float2(c * v.x - s * v.y, s * v.x + c * v.y);
            }

            // ---------- Random rotation per tile (4 blended samples, no seams) ----------
            float4 SampleRandomRotated(Texture2D tex, SamplerState ss, float2 uv)
            {
                float2 cell = floor(uv);
                float2 dx = ddx(uv);
                float2 dy = ddy(uv);

                float4 sum = 0;
                float wSum = 0;

                [unroll] for (int j = 0; j < 2; j++)
                {
                    [unroll] for (int i = 0; i < 2; i++)
                    {
                        float2 corner = cell + float2(i, j);
                        float angle   = Hash21(corner) * 6.2831853;
                        float2 offset = float2(Hash21(corner + 7.1), Hash21(corner + 13.7));

                        float2 d = uv - corner;
                        float w  = (1.0 - abs(d.x)) * (1.0 - abs(d.y));

                        float2 ruv = Rotate2D(d, angle) + offset;
                        float4 c = tex.SampleGrad(ss, ruv, Rotate2D(dx, angle), Rotate2D(dy, angle));

                        sum  += c * w;
                        wSum += w;
                    }
                }
                return sum / max(wSum, 1e-5);
            }

            // ---------- Height-based layer blending ----------
            float4 BlendLayers(float4 w, float4 c0, float4 c1, float4 c2, float4 c3,
                   float4 heights, float depth)
            {
                // Weight multiplies height: unpainted layers (w = 0) can never show through.
                // Height only decides who wins where two layers are both painted.
                float4 a = w * (1.0 + heights);

                float m  = max(max(a.x, a.y), max(a.z, a.w)) - depth;
                float4 b = max(a - m, 0.0);
                b /= max(dot(b, 1.0), 1e-5);

                return c0 * b.x + c1 * b.y + c2 * b.z + c3 * b.w;
            }

            // ---------- Vertex / Fragment ----------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 uvW = IN.positionWS.xz / _TileSize;

                float4 ctrl = SAMPLE_TEXTURE2D(_Control, sampler_Control, IN.uv);

                float4 c0 = SampleRandomRotated(_Splat0, sampler_Splat0, uvW);
                float4 c1 = SampleRandomRotated(_Splat1, sampler_Splat1, uvW);
                float4 c2 = SampleRandomRotated(_Splat2, sampler_Splat2, uvW);
                float4 c3 = SampleRandomRotated(_Splat3, sampler_Splat3, uvW);

                float4 heights = float4(Luminance(c0.rgb), Luminance(c1.rgb),
                                        Luminance(c2.rgb), Luminance(c3.rgb));

                float3 albedo = BlendLayers(ctrl, c0, c1, c2, c3, heights, _BlendDepth).rgb;

                float3 n = normalize(IN.normalWS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));

                float ndl = saturate(dot(n, mainLight.direction));
                float3 direct  = mainLight.color * mainLight.shadowAttenuation * ndl;
                float3 ambient = SampleSH(n);

                return half4(albedo * (direct + ambient), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; };

            V ShadowVert(A IN)
            {
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS   = TransformObjectToWorldNormal(IN.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                V OUT;
                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
                return OUT;
            }

            half4 ShadowFrag(V IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask 0
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };

            V DepthVert(A IN)
            {
                V OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFrag(V IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
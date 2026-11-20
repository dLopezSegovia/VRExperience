// Thin sheet of water that runs up the sand after each breaker and drains back,
// leaving a darker wet band. Lies on a strip mesh conformed to the terrain.
Shader "SameShore/Swash"
{
    Properties
    {
        _WaterColor ("Water color", Color) = (0.04,0.08,0.11,0.45)
        _FoamColor ("Foam color", Color) = (0.6,0.65,0.72,1)
        _WetColor ("Wet sand darkening", Color) = (0,0,0,0.22)
        _ShorePoint ("Shore point (world)", Vector) = (0,0,20,0)
        _ShoreDir ("Direction sea->land (xz)", Vector) = (0,0,-1,0)
        _RunUp ("Run-up distance", Float) = 2.2
        _Period ("Period (s)", Float) = 7
        _WetReach ("Wet band reach", Float) = 2.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "SameShoreNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor, _FoamColor, _WetColor;
                float4 _ShorePoint, _ShoreDir;
                float _RunUp, _Period, _WetReach;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float fog : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 toLand = normalize(_ShoreDir.xz);
                float2 rel = i.positionWS.xz - _ShorePoint.xz;
                float s = dot(rel, toLand);                       // metres up the beach
                float along = dot(rel, float2(-toLand.y, toLand.x));

                float phase = frac(_Time.y / _Period + 0.12 * vnoise(float2(along * 0.15, 0)));
                // fast rush up, slow drain back
                float up = phase < 0.3 ? 1 - pow(1 - phase / 0.3, 2) : 1 - smoothstep(0.3, 1.0, phase);
                float front = -0.6 + (_RunUp + 0.5 * vnoise(float2(along * 0.4, floor(_Time.y / _Period)))) * up;

                float behind = smoothstep(0.08, -0.08, s - front);
                float edge = exp(-abs(front - s) / 0.1) * (1 - smoothstep(0.0, 0.25, s - front));
                // soft, continuous foam lace along the moving front (no hard thresholds -> no blocky cells)
                float n = vnoise(float2(along * 3.1, s * 1.3) + float2(_Time.y * 0.15, 0)) * 0.6
                        + vnoise(float2(along * 9.7, s * 4.1)) * 0.4;
                float foam = saturate(edge * (0.45 + 0.55 * n) * up * 1.2);

                Light L = GetMainLight();
                half3 water = _WaterColor.rgb + L.color * 0.05;
                float aWater = behind * _WaterColor.a * saturate(up * 1.5 + 0.2);
                float wet = smoothstep(_WetReach, _WetReach * 0.3, s) * _WetColor.a;

                half3 col = lerp(_WetColor.rgb, water, saturate(aWater / max(aWater + wet, 1e-3)));
                col = lerp(col, _FoamColor.rgb * (0.3 + 0.7 * L.color), foam);
                float a = saturate(max(max(aWater, wet), foam * 0.9));
                // soften the strip ends
                a *= saturate((s + 1.5) / 0.8);
                col = MixFog(col, i.fog);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}

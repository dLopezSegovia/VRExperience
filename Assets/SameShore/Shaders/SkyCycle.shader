// Same Shore - sky for the day cycle: afternoon -> sunset -> night.
// _Cycle: 0 = afternoon, 1 = sunset, 2 = night (the night uses the project's CC0 starry HDRI).
Shader "SameShore/SkyCycle"
{
    Properties
    {
        _Cycle ("Cycle (0 day, 1 sunset, 2 night)", Range(0, 2)) = 0
        _DayZenith ("Day zenith", Color) = (0.22, 0.45, 0.82, 1)
        _DayHorizon ("Day horizon", Color) = (0.72, 0.84, 0.95, 1)
        _SunsetZenith ("Sunset zenith", Color) = (0.2, 0.24, 0.45, 1)
        _SunsetHorizon ("Sunset horizon", Color) = (1.0, 0.52, 0.28, 1)
        _GroundColor ("Below horizon", Color) = (0.32, 0.36, 0.4, 1)
        _SunDir ("Sun direction (towards the sun)", Vector) = (0, 0.5, 1, 0)
        _SunColor ("Sun color", Color) = (1, 0.95, 0.85, 1)
        _SunSize ("Sun size", Float) = 0.9994
        [NoScaleOffset] _NightTex ("Night panorama (HDR)", 2D) = "black" {}
        _NightExposure ("Night exposure", Float) = 0.32
        _NightTint ("Night tint", Color) = (0.36, 0.4, 0.52, 1)
        _NightRotation ("Night rotation (deg)", Float) = 200
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _Cycle;
            float4 _DayZenith, _DayHorizon, _SunsetZenith, _SunsetHorizon, _GroundColor;
            float4 _SunDir, _SunColor;
            float _SunSize;
            float _NightExposure;
            float4 _NightTint;
            float _NightRotation;
            CBUFFER_END
            TEXTURE2D(_NightTex); SAMPLER(sampler_NightTex);

            struct A { float4 pos : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            V vert(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.dir = i.pos.xyz;
                return o;
            }

            float3 Night(float3 d)
            {
                float r = radians(_NightRotation);
                float s = sin(r), c = cos(r);
                d = float3(c * d.x - s * d.z, d.y, s * d.x + c * d.z);
                float2 uv = float2(atan2(d.x, d.z) / (2 * PI) + 0.5, asin(clamp(d.y, -1, 1)) / PI + 0.5);
                return SAMPLE_TEXTURE2D_LOD(_NightTex, sampler_NightTex, uv, 0).rgb * _NightExposure * _NightTint.rgb;
            }

            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sun = normalize(_SunDir.xyz);
                float dayK = saturate(_Cycle);                 // 0 day -> 1 sunset
                float nightK = saturate(_Cycle - 1);           // 0 sunset -> 1 night
                float h = saturate(d.y);
                float horizonBand = pow(1 - h, 4);
                float3 zen = lerp(_DayZenith.rgb, _SunsetZenith.rgb, dayK);
                float3 hor = lerp(_DayHorizon.rgb, _SunsetHorizon.rgb, dayK);
                // the horizon glows more on the sun's side at sunset
                float sunSide = saturate(dot(normalize(float3(d.x, 0, d.z)), normalize(float3(sun.x, 0, sun.z))) * 0.5 + 0.5);
                hor = lerp(hor, hor * lerp(1, 0.55 + 0.9 * sunSide, dayK), 1);
                float3 col = lerp(zen, hor, horizonBand);
                float below = saturate(-d.y * 6);
                col = lerp(col, _GroundColor.rgb * lerp(1, 0.6, dayK), below);
                // sun disc and halo
                float cs = dot(d, sun);
                float disc = smoothstep(_SunSize, _SunSize + 0.0004, cs);
                float halo = pow(saturate(cs), lerp(220, 40, dayK)) * lerp(0.35, 1.2, dayK) + pow(saturate(cs), 8) * 0.12 * dayK;
                col += _SunColor.rgb * (disc * 6 + halo) * (1 - below);
                float3 night = Night(d);
                col = lerp(col, night, smoothstep(0, 1, nightK));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

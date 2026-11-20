// Hatchling perception overlay, drawn on a sphere centred on the head.
//  - Attention cone: everything outside ~45 deg of the BODY forward darkens. Head tracking is
//    never altered; only what the hatchling can attend to is restricted.
//  - Light pull: a warm halo in the direction of the dominant light attraction.
//  - Hue: a faint violet/UV tint layered on the periphery ("hues invisible to the human eye").
Shader "SameShore/TurtleVision"
{
    Properties
    {
        _BodyForward ("Body forward (world)", Vector) = (0,0,1,0)
        _InnerAngle ("Inner angle (deg)", Float) = 45
        _OuterAngle ("Outer angle (deg)", Float) = 95
        _MaxDarkness ("Max darkness", Range(0,1)) = 0.82
        _PullDir ("Pull direction (world)", Vector) = (0,0,1,0)
        _PullStrength ("Pull strength", Range(0,1)) = 0
        _PullColor ("Pull color", Color) = (1,0.85,0.55,1)
        _Tint ("Peripheral UV tint", Color) = (0.35,0.15,0.6,0.25)
        _Intensity ("Overall intensity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest Always
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BodyForward;
                float _InnerAngle, _OuterAngle, _MaxDarkness;
                float4 _PullDir;
                float _PullStrength;
                half4 _PullColor;
                half4 _Tint;
                float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dirWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dirWS = TransformObjectToWorldDir(v.positionOS.xyz, false);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d = normalize(i.dirWS);
                float3 f = normalize(_BodyForward.xyz);
                float ang = degrees(acos(clamp(dot(d, f), -1, 1)));
                float dark = smoothstep(_InnerAngle, _OuterAngle, ang) * _MaxDarkness;

                float3 p = normalize(_PullDir.xyz + 1e-5);
                float pull = pow(saturate(dot(d, p)), 24) * _PullStrength;

                float tint = smoothstep(_InnerAngle * 0.4, _InnerAngle * 1.3, ang) * _Tint.a;
                float a = saturate(dark + tint * (1 - dark)) * _Intensity;
                half3 rgb = (_Tint.rgb * tint * (1 - dark) + _PullColor.rgb * pull * 0.55) * _Intensity;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}

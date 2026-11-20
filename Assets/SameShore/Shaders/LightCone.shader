// Fake volumetric beam for the flashlight: additive, soft edges (fresnel) and length falloff.
Shader "SameShore/LightCone"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,0.85,0.65,0.18)
        _EdgePower ("Edge softness", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _EdgePower;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; float along : TEXCOORD2; float fog : TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetCameraPositionWS() - p;
                o.along = v.uv.y;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float facing = abs(dot(normalize(i.normalWS), normalize(i.viewWS)));
                float edge = pow(facing, _EdgePower);
                float fall = (1 - i.along) * (1 - i.along) * saturate(i.along * 12);
                half3 c = _Color.rgb * _Color.a * edge * fall;
                return half4(MixFogColor(c, 0, i.fog), 0);
            }
            ENDHLSL
        }
    }
}

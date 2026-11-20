// Night ocean: Gerstner swell rolling towards the shore, moon glitter from the main light,
// sky fresnel, translucent shallow edge and procedural foam. No depth/opaque textures needed
// (cheap enough for Quest).
Shader "SameShore/Ocean"
{
    Properties
    {
        _DeepColor ("Deep color", Color) = (0.01,0.03,0.06,1)
        _ShallowColor ("Shallow color", Color) = (0.03,0.09,0.11,1)
        _SkyColor ("Sky reflection", Color) = (0.07,0.1,0.18,1)
        _FoamColor ("Foam color", Color) = (0.55,0.6,0.68,1)
        _ShorePoint ("Shore point (world)", Vector) = (0,0,20,0)
        _ShoreDir ("Direction sea->land (xz)", Vector) = (0,0,-1,0)
        _WaveHeight ("Wave height", Float) = 0.18
        _WaveLength ("Wave length", Float) = 9
        _Period ("Breaker period (s)", Float) = 7
        _FoamWidth ("Shore foam width", Float) = 3
        _Glitter ("Moon glitter", Float) = 3
        _Smoothness ("Smoothness", Range(0,1)) = 0.92
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "SameShoreNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _ShallowColor, _SkyColor, _FoamColor;
                float4 _ShorePoint, _ShoreDir;
                float _WaveHeight, _WaveLength, _Period, _FoamWidth, _Glitter, _Smoothness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 shore : TEXCOORD2; // x = distance out to sea, y = crest
                float fog : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 ShoreFrame(float3 p, out float along)
            {
                float2 toLand = normalize(_ShoreDir.xz);
                float2 rel = p.xz - _ShorePoint.xz;
                along = dot(rel, float2(-toLand.y, toLand.x));
                return float2(-dot(rel, toLand), 0); // distance out to sea
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float along;
                float dist = ShoreFrame(p, along).x;
                float2 toLand = normalize(_ShoreDir.xz);

                // swell: phase travels towards the shore once per breaker period
                float k = 6.2831853 / _WaveLength;
                float t = _Time.y * 6.2831853 / _Period;
                float ph = -k * dist - t + 0.15 * sin(along * 0.07);
                float near = saturate(dist / 3.0);
                float amp = _WaveHeight * near * (0.6 + 0.4 * saturate(1 - dist / 60));
                float crest = pow(saturate(0.5 + 0.5 * sin(ph)), 3);
                float h = amp * (crest * 1.6 - 0.4);
                h += 0.03 * sin(dot(p.xz, float2(0.71, 0.33)) * 1.3 + _Time.y * 1.1) * near;
                h += 0.02 * sin(dot(p.xz, float2(-0.4, 0.9)) * 2.1 + _Time.y * 1.7) * near;
                p.y += h;
                // tuck the water edge under the sand; the swash strip draws the shoreline itself
                p.y -= 0.12 * (1 - saturate(dist / 2.5));

                // analytic-ish normal from the swell derivative
                float dh = amp * 1.6 * 3 * pow(saturate(0.5 + 0.5 * sin(ph)), 2) * 0.5 * cos(ph) * k;
                float3 n = normalize(float3(toLand.x * dh, 1, toLand.y * dh));

                o.positionWS = p;
                o.normalWS = n;
                o.shore = float2(dist, crest);
                o.positionCS = TransformWorldToHClip(p);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 V = normalize(GetCameraPositionWS() - i.positionWS);
                // small-scale ripples for glitter
                float2 uv = i.positionWS.xz;
                // sum of directional sine ripples (smooth gradients -> natural moon glitter, no grid artefacts)
                float t = _Time.y;
                float2 d1 = float2(0.8, 0.6), d2 = float2(-0.5, 0.86), d3 = float2(0.2, -0.98), d4 = float2(-0.93, -0.37);
                float2 g = d1 * cos(dot(uv, d1) * 2.1 + t * 1.3) * 0.45
                         + d2 * cos(dot(uv, d2) * 3.7 - t * 1.7) * 0.3
                         + d3 * cos(dot(uv, d3) * 6.3 + t * 2.3) * 0.2
                         + d4 * cos(dot(uv, d4) * 11.1 - t * 2.9) * 0.12;
                float3 rip = float3(g.x, 0, g.y);
                float3 N = normalize(i.normalWS + rip * 0.35);

                Light L = GetMainLight();
                float3 H = normalize(L.direction + V);
                float spec = pow(saturate(dot(N, H)), lerp(64, 900, _Smoothness)) * _Glitter;
                float fres = pow(1 - saturate(dot(N, V)), 4);

                float dist = i.shore.x;
                half3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(dist / 25));
                half3 col = lerp(water, _SkyColor.rgb, fres * 0.8);
                col += L.color * spec;

                // foam: shore band + breaking crests
                float n1 = vnoise(uv * 2.3 + float2(0, _Time.y * 0.4));
                float n2 = vnoise(uv * 6.1 - float2(_Time.y * 0.2, 0));
                float band = saturate(1 - dist / _FoamWidth);
                float lace = smoothstep(0.55, 0.8, n1 * 0.5 + n2 * 0.5) * (1 - smoothstep(0.82, 0.95, n2));
                float shoreFoam = band * band * lace * (0.4 + 0.6 * i.shore.y);
                float crestFoam = smoothstep(0.8, 0.97, i.shore.y) * saturate(1 - dist / (_FoamWidth * 2.5)) * smoothstep(0.55, 0.8, n2);
                float foam = saturate(shoreFoam + crestFoam * 0.7);
                col = lerp(col, _FoamColor.rgb * (0.3 + 0.5 * L.color), foam * 0.6);

                float alpha = lerp(0.55, 1, saturate(dist / 4)) ;
                alpha = max(alpha, foam);
                col = MixFog(col, i.fog);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}

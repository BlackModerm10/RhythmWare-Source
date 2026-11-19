Shader "RhythmWare/FluidStage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _StageMode ("Artwork instead of fluid", Float) = 0
        _VisualTime ("Animation time", Float) = 0
        _Aspect ("Camera aspect", Float) = 1.77778
        _Beat ("Subtle beat accent", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _StageMode, _VisualTime, _Aspect, _Beat;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv; output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                if (_StageMode > .5)
                {
                    half4 art = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                    float hi = max(art.r, max(art.g, art.b));
                    float lo = min(art.r, min(art.g, art.b));
                    // Black exterior becomes transparent only on the GPU.
                    // Sampling sRGB artwork in a linear project turns the dark
                    // asphalt into ~0.007; only near-zero exterior is keyed out.
                    float coverage = smoothstep(.0001, .002, hi);
                    float neutral = (1 - smoothstep(.015, .08, hi - lo)) * (1 - smoothstep(.36, .7, hi));
                    float3 surface = float3(.075, .115, .18) + hi * .8;
                    art.rgb = lerp(art.rgb, surface, neutral);
                    // Keep blue outlines recognizable, with a brighter electric edge.
                    float blue = saturate((art.b - max(art.r, art.g)) * 2);
                    art.rgb = lerp(art.rgb, float3(.12, .52, 1), blue * .55);
                    return half4(art.rgb, art.a * coverage) * input.color;
                }
                float2 p = (input.uv - .5) * float2(_Aspect, 1) * 2.8;
                float t = _VisualTime * .075;
                // Two gentle domain warps produce broad, continuously flowing ribbons.
                float2 q = p + .65 * float2(sin(p.y * 1.7 + t), cos(p.x * 1.3 - t * .8));
                q += .32 * float2(cos(q.y * 2.5 - t * .7), sin(q.x * 2 + t * .6));
                float swirl = sin(q.x * 1.6 + q.y * 2.2 + t) * .5 + .5;
                float ribbon = sin(q.y * 3.8 - q.x * 1.2 + sin(q.x * 2 + t) + t * .6) * .5 + .5;
                float3 color = lerp(float3(.12, .31, .43), float3(.29, .20, .47), smoothstep(.15, .85, swirl));
                color = lerp(color, float3(.16, .45, .48), smoothstep(.4, .95, ribbon) * .6);
                float rings = .5 + .5 * sin((q.x + q.y + swirl) * 11);
                color += rings * .016 + _Beat * .012;
                float vignette = 1 - .18 * smoothstep(.15, .8, length(input.uv - .5));
                return half4(color * vignette, 1) * input.color;
            }
            ENDHLSL
        }
    }
}

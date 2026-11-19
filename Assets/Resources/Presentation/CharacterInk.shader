Shader "RhythmWare/CharacterInk"
{
    Properties { [PerRendererData] _MainTex("Character",2D)="white"{} _OutlineColor("Outline",Color)=(.4,1,.8,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);float4 _MainTex_TexelSize,_OutlineColor;
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            V vert(A v){V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.color=v.color;return o;}
            half4 frag(V v):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,v.uv);
                float2 d=_MainTex_TexelSize.xy*2;
                half edge=0;
                [unroll]for(int i=0;i<8;i++)
                {
                    float a=i*0.78539816;edge=max(edge,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,v.uv+float2(cos(a),sin(a))*d).a);
                }
                half alpha=max(c.a,edge);
                half3 rgb=lerp(_OutlineColor.rgb,c.rgb,c.a);
                return half4(rgb*v.color.rgb,alpha*v.color.a);
            }
            ENDHLSL
        }
    }
}

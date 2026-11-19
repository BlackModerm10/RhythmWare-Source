Shader "RhythmWare/BonziSoftKey"
{
    Properties { [PerRendererData] _MainTex("Frames",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) }
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
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);float4 _Color;
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            V vert(A v){V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.color=v.color*_Color;return o;}
            half4 frag(V v):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,v.uv);
                half cyan=min(c.g,c.b)-c.r;c.a*=1-saturate((cyan-.45)*5);
                // Despill the original cyan key after bilinear sampling.
                half spill=max(0,cyan-.04);c.g=max(0,c.g-spill);c.b=max(0,c.b-spill);
                clip(c.a-.01);return c*v.color;
            }
            ENDHLSL
        }
    }
}

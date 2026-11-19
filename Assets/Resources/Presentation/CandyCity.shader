Shader "RhythmWare/CandyCity"
{
    Properties { _MainTex("Sprite",2D)="white"{} _CityTex("City",2D)="black"{} _DesktopTex("XP wallpaper",2D)="black"{} _VisualTime("Clock",Float)=0 _Theme("Theme",Float)=0 _Custom("Custom",Float)=0 _Reduced("Reduced",Float)=0 _DesktopPhase("Desktop phase",Float)=1 }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_CityTex);SAMPLER(sampler_CityTex);
            TEXTURE2D(_DesktopTex);SAMPLER(sampler_DesktopTex);
            float _VisualTime,_Theme,_Custom,_Reduced,_DesktopPhase;
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            V vert(A v){V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
            float mirror(float x){return 1-abs(frac(x*.5)*2-1);}
            half4 frag(V v):SV_Target
            {
                float2 uv=v.uv;float t=_VisualTime;
                if(_Theme>1.5)
                {
                    float phase=t*6.28318530718/24;
                    float2 wallpaperUV=(uv-.5)*.97+.5+float2(sin(phase)*.004,sin(phase*2)*.002);
                    half3 wallpaper=SAMPLE_TEXTURE2D(_DesktopTex,sampler_DesktopTex,wallpaperUV).rgb;
                    float takeover=saturate((_DesktopPhase-1)*.11);
                    wallpaper=lerp(wallpaper,wallpaper*half3(.72,.82,1)+half3(.025,.008,.05),takeover);
                    return half4(wallpaper,1);
                }
                float phase=t*6.28318530718/(_Theme>.5?20:24);
                // A bounded orbit preserves the illustrated composition. The
                // previous full-screen scroll brought towers across the boss.
                float2 nearUV=float2(uv.x+sin(phase)*.012,uv.y+sin(phase*2)*.004);
                float2 farUV=(uv-.5)*.985+.5+float2(-sin(phase)*.006,cos(phase*2)*.003);
                half3 nearCity=SAMPLE_TEXTURE2D(_CityTex,sampler_CityTex,nearUV).rgb;
                half3 farCity=SAMPLE_TEXTURE2D(_CityTex,sampler_CityTex,farUV).rgb;
                half3 city=lerp(farCity,nearCity,.90);
                if(_Custom>.5)city=SAMPLE_TEXTURE2D(_CityTex,sampler_CityTex,nearUV).rgb;
                half3 tint=_Theme>1.5?half3(1,.95,1):_Theme>.5?half3(.90,1,1):half3(1,1,1);
                city*=tint;
                // Keep the bright comic skyline; the separate opaque highway
                // supplies gameplay contrast instead of a screen-wide veil.
                return half4(city,1);
            }
            ENDHLSL
        }
    }
}

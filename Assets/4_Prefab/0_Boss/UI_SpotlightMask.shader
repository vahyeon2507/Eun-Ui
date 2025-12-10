Shader "UI/SpotlightMask"
{
    Properties{
        _MaxAlpha ("Dark Alpha (0~1)", Range(0,1)) = 0.65
        _Spot0 ("Spot0(x,y,r,feather)", Vector) = (0.5,0.5,0.25,0.08)
        _Spot1 ("Spot1", Vector) = (-1,-1,0,0)
        _Spot2 ("Spot2", Vector) = (-1,-1,0,0)
        _Spot3 ("Spot3", Vector) = (-1,-1,0,0)
    }
    SubShader{
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        ZWrite Off Blend SrcAlpha OneMinusSrcAlpha Cull Off
        Pass{
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            float _MaxAlpha;
            float4 _Spot0,_Spot1,_Spot2,_Spot3;
            v2f vert(appdata v){ v2f o; o.pos = TransformObjectToHClip(v.vertex.xyz); o.uv = v.uv; return o; }
            float holeMask(float2 uv, float4 s){
                if(s.x<0 || s.y<0) return 0; // disabled
                float2 p = uv;       // uv is already 0..1 in UI Image
                float d = distance(p, s.xy);
                // 1 outside, 0 inside (feathered)
                return smoothstep(s.z, s.z - max(0.0001,s.w), d);
            }
            half4 frag(v2f i):SV_Target{
                float m = 1.0;
                m *= holeMask(i.uv, _Spot0);
                m *= holeMask(i.uv, _Spot1);
                m *= holeMask(i.uv, _Spot2);
                m *= holeMask(i.uv, _Spot3);
                return half4(0,0,0, saturate(m) * _MaxAlpha);
            }
            ENDHLSL
        }
    }
}

// Overlay UI layar penuh untuk simulasi foveated rendering:
// area di sekitar titik gaze tetap jernih, area pinggir diredupkan.
// _GazeCenter (UV layar 0..1), _FovealRadius, dan _FoveationEnabled diisi UnitEyeGazeBridge.
Shader "UI/FoveatedVignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GazeCenter ("Gaze Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _FovealRadius ("Foveal Radius", Range(0.05, 0.8)) = 0.3
        _Feather ("Feather", Range(0.01, 0.5)) = 0.15
        _FoveationEnabled ("Foveation Enabled", Float) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _GazeCenter;
            float _FovealRadius;
            float _Feather;
            float _FoveationEnabled;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord) * i.color;
                float2 d = i.texcoord - _GazeCenter.xy;
                d.x *= _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float k = smoothstep(_FovealRadius, _FovealRadius + _Feather, length(d));
                col.a *= lerp(1.0, k, step(0.5, _FoveationEnabled));
                return col;
            }
            ENDCG
        }
    }
}

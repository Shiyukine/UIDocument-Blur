Shader "Hidden/KawaseBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Offset ("Offset", Float) = 1.0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Offset;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Mirror UV at edges
            float2 MirrorUV(float2 uv)
            {
                if (uv.x < 0) uv.x = -uv.x;
                if (uv.x > 1) uv.x = 2.0 - uv.x;
                if (uv.y < 0) uv.y = -uv.y;
                if (uv.y > 1) uv.y = 2.0 - uv.y;
                return uv;
            }

            half4 SampleTex(float2 uv)
            {
                return tex2D(_MainTex, MirrorUV(uv));
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 texelSize = _MainTex_TexelSize.xy * _Offset;
                
                half4 color = SampleTex(i.uv);
                color += SampleTex(i.uv + float2(-texelSize.x, -texelSize.y));
                color += SampleTex(i.uv + float2(-texelSize.x, texelSize.y));
                color += SampleTex(i.uv + float2(texelSize.x, -texelSize.y));
                color += SampleTex(i.uv + float2(texelSize.x, texelSize.y));
                
                return color * 0.2;
            }
            ENDCG
        }
    }
    FallBack Off
}
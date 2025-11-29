Shader "Hidden/DualKawaseBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Offset ("Offset", Float) = 1.0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
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

        // Clamp UV
        float2 ClampUV(float2 uv)
        {
            return clamp(uv, 0.001, 0.999);
        }
        ENDCG

        // Pass 0: Downsample
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown

            half4 fragDown(v2f i) : SV_Target
            {
                float2 texelSize = _MainTex_TexelSize.xy * _Offset;
                
                half4 color = tex2D(_MainTex, ClampUV(i.uv)) * 4.0;
                color += tex2D(_MainTex, ClampUV(i.uv + float2(-texelSize.x, -texelSize.y)));
                color += tex2D(_MainTex, ClampUV(i.uv + float2(-texelSize.x, texelSize.y)));
                color += tex2D(_MainTex, ClampUV(i.uv + float2(texelSize.x, -texelSize.y)));
                color += tex2D(_MainTex, ClampUV(i.uv + float2(texelSize.x, texelSize.y)));
                
                return color / 8.0;
            }
            ENDCG
        }

        // Pass 1: Upsample
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUp

            half4 fragUp(v2f i) : SV_Target
            {
                float2 texelSize = _MainTex_TexelSize.xy * _Offset;
                
                half4 color = 0;
                
                color += tex2D(_MainTex, ClampUV(i.uv + float2(-texelSize.x * 2.0, 0)));
                color += tex2D(_MainTex, ClampUV(i.uv + float2(-texelSize.x, texelSize.y))) * 2.0;
                color += tex2D(_MainTex, ClampUV(i.uv + float2(0, texelSize.y * 2.0)));
                color += tex2D(_MainTex, ClampUV(i.uv + float2(texelSize.x, texelSize.y))) * 2.0;
                color += tex2D(_MainTex, ClampUV(i.uv + float2(texelSize.x * 2.0, 0)));
                color += tex2D(_MainTex, ClampUV(i.uv + float2(texelSize.x, -texelSize.y))) * 2.0;
                color += tex2D(_MainTex, ClampUV(i.uv + float2(0, -texelSize.y * 2.0)));
                color += tex2D(_MainTex, ClampUV(i.uv + float2(-texelSize.x, -texelSize.y))) * 2.0;
                
                return color / 12.0;
            }
            ENDCG
        }
    }
    FallBack Off
}
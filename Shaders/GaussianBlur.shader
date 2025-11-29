Shader "Hidden/GaussianBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Float) = 1.0
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
        float _BlurSize;

        v2f vert(appdata v)
        {
            v2f o;
            o.vertex = UnityObjectToClipPos(v.vertex);
            o.uv = v.uv;
            return o;
        }

        // Compute Gaussian weight
        float Gaussian(float x, float sigma)
        {
            return exp(-(x * x) / (2.0 * sigma * sigma));
        }

        // 15-tap Gaussian blur with proper weights
        half4 GaussianBlur(float2 uv, float2 direction)
        {
            half4 color = 0;
            float totalWeight = 0;
            
            // Sigma based on blur size
            float sigma = max(_BlurSize * 0.5, 0.001);
            
            // Sample radius - more samples for larger blur
            const int SAMPLES = 15;
            int radius = SAMPLES / 2;
            
            for (int i = -radius; i <= radius; i++)
            {
                float offset = float(i);
                float weight = Gaussian(offset, sigma);
                
                float2 sampleUV = uv + direction * offset * _BlurSize;
                
                // Clamp UV to prevent edge artifacts
                sampleUV = clamp(sampleUV, 0.001, 0.999);
                
                color += tex2D(_MainTex, sampleUV) * weight;
                totalWeight += weight;
            }
            
            return color / totalWeight;
        }
        ENDCG

        // Pass 0: Horizontal
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragH

            half4 fragH(v2f i) : SV_Target
            {
                float2 direction = float2(_MainTex_TexelSize.x, 0);
                return GaussianBlur(i.uv, direction);
            }
            ENDCG
        }

        // Pass 1: Vertical
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragV

            half4 fragV(v2f i) : SV_Target
            {
                float2 direction = float2(0, _MainTex_TexelSize.y);
                return GaussianBlur(i.uv, direction);
            }
            ENDCG
        }
    }
    FallBack Off
}
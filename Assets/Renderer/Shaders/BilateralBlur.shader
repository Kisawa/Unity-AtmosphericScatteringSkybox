Shader "Postprocessing/BilateralBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Spread("Spread", Range(0, 1)) = .1
        _ColorSigma("Color Sigma", Range(.01, 1)) = .1
        _SpaceSigma("Space Sigma", Range(1, 20)) = 10
    }
    SubShader
    {
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv: TEXCOORD0;
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
        float _Spread;
        float _ColorSigma;
        float _SpaceSigma;
        CBUFFER_END

        float normpdf(float x, float sigma)
        {
            return 0.39894 * exp(-0.5 * x * x / (sigma * sigma)) / sigma;
        }

        float normpdf3(float3 v, float sigma)
        {
            return 0.39894 * exp(-0.5 * dot(v, v) / (sigma * sigma)) / sigma;
        }

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            return output;
        }

        #define COUNT 3
        half4 frag(Varyings input) : SV_Target
        {
            half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            half3 col = color.rgb;

            const int total = COUNT * 2 + 1;
            float kernel[total];
            [unroll]
            for (int i = 0; i <= COUNT; i++)
            {
                kernel[COUNT + i] = kernel[COUNT - i] = normpdf(i, _SpaceSigma);
            }
            float unit = 1 / normpdf(0, _ColorSigma);
            float factor = 0;
            half3 final = 0;
            [unroll]
            for (int y = -COUNT; y <= COUNT; y++)
            {
                [unroll]
                for (int x = -COUNT; x <= COUNT; x++)
                {
                    half3 _col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(x, y) * _Spread).rgb;
                    float _factor = normpdf3(_col - col, _ColorSigma) * unit * kernel[COUNT + x] * kernel[COUNT + y];
                    factor += _factor;
                    final += _factor * _col;
                }
            }
            col = final / factor;
            return half4(col, color.a);
        }
        ENDHLSL

        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}

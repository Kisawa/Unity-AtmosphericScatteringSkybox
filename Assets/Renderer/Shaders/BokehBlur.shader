Shader "Postprocessing/BokehBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "ShaderLibrary/DepthBlurTools.hlsl"
        #pragma multi_compile _ _DEPTH_BLUR_ON
        #pragma multi_compile _ _CURLY_BLUR

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
        float4 _MainTex_TexelSize;
        float _Spread;
        int _Shape;

        int _Iteration;
        float _GoldenAngle;
        float _BokehHardness;

        float _DepthFocus;
        float _DepthSmoothness;
        float _DepthBlendPower;
        CBUFFER_END

        #define random(seed) sin(seed * 641.5467987313875 + 1.943856175)

        half3 BokehBlur(float2 uv)
        {
            float seed = random((_ScreenParams.y * uv.y + uv.x) * _ScreenParams.x + 0);
            float depthBlurFactor = GetDepthBlendFactor(uv, _DepthFocus, _DepthSmoothness, _DepthBlendPower);
            float2 spread = _MainTex_TexelSize.xy * _Spread * depthBlurFactor;
            half3 col = 0, sum = 0;
            float shape = PI / _Shape;
            float cos_shape = cos(shape);
            float count = 16 * PI, step = PI / _Iteration;
            [loop]
            for (float t = 0; t < count; t += step)
            {
                seed = random(seed);
                float _t = t + step * seed * .5;
                float r = cos_shape / cos(fmod(_t, shape * 2) - shape);
                float2 offset = float2(sin(_t), cos(_t)) * r * _t * spread;
                half3 _col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + offset).rgb;
                half3 b = pow(max(.001, _col), _BokehHardness);
                col += _col * b * t;
                sum += t * b;
            }
            return col / sum;
        }

        half3 CurlyBokehBlur(float2 uv)
        {
            float c, s;
            sincos(_GoldenAngle, s, c);
            float2x2 rotate = float2x2(c, -s, s, c);
            half3 col = 0, sum = 0;
            float depthBlurFactor = GetDepthBlendFactor(uv, _DepthFocus, _DepthSmoothness, _DepthBlendPower);
            float2 angle = _Spread / _Iteration * 20 * depthBlurFactor;
            [loop]
            for (int i = 1; i <= _Iteration; i++)
            {
                angle = mul(rotate, angle);
                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + _MainTex_TexelSize.xy * angle * i).xyz;
                half3 b = pow(max(.001, c), _BokehHardness);
                col += c * b;
                sum += b;
            }
            col /= sum;
            return col;
        }

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            return output;
        }

        half4 frag(Varyings input) : SV_Target
        {
#if defined(_CURLY_BLUR)
            half3 col = CurlyBokehBlur(input.uv);
#else
            half3 col = BokehBlur(input.uv);
#endif
            return half4(col, 1);
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

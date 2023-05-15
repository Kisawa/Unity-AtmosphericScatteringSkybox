Shader "Environment/AtmosphericScatteringSky_Simple"
{
    Properties
    {
        _ColorTint("Color Tint", Color) = (1, 1, 1, 1)
        _WaveLambdaOzone("Wave Lambda Ozone", Vector) = (0, 0, 0, 0)
        [Header(Setting)]
        _EyeHeight("Eye Height", Range(1, 50000)) = 5000
        _AtmosphericHeight("Atmospheric Height", Range(1, 100000)) = 60000
        _RayleighHeight("Rayleigh Height", Range(200, 20000)) = 8000
        _Mie_G("Mie G", Range(0, 1)) = .76
        [Header(Sun)]
        _SunDistance("Sun Distance", Range(5, 1000)) = 100
        _SunStrength("Sun Strength", Range(0, 10)) = 1
        _SunRadiance("Sun Radiance", Range(1, 50)) = 20
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox"}

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "ShaderLibrary/AtmosphericScatteringTools.hlsl"

        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv: TEXCOORD0;
            float3 positionWS : TEXCOORD1;
        };

        CBUFFER_START(UnityPerMaterial)
        half4 _ColorTint;
        float3 _WaveLambdaOzone;

        float _EyeHeight;
        float _AtmosphericHeight;
        float _RayleighHeight;
        float _Mie_G;
        
        float _SunDistance;
        float _SunStrength;
        float _SunRadiance;
        CBUFFER_END

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            output.positionWS = TransformObjectToWorld(input.positionOS);
            return output;
        }

        half4 frag(Varyings input) : SV_Target
        {
            float3 V = normalize(input.positionWS);
            float3 L = _MainLightPosition.xyz;

            ScatteringParams setting;
            setting.sunRadius = _SunDistance;
            setting.sunRadiance = _SunRadiance;
            setting.mieG = _Mie_G;
            setting.rayleighHeight = _RayleighHeight;
            setting.earthRadius = EARTH_RADIUS;
            setting.earthAtmTopRadius = setting.earthRadius + max(_AtmosphericHeight, _EyeHeight + 1);
            setting.earthCenter = float3(0, -setting.earthRadius, 0);
            setting.waveLambdaMie = 2e-6 * _SunStrength;
            setting.waveLambdaRayleigh = setting.waveLambdaMie;
            setting.waveLambdaOzone = _WaveLambdaOzone * 6e-7;

            half4 sky = ComputeSkyInscattering(setting, _EyeHeight, V, L);
            half4 col = half4(sky.rgb * _ColorTint.rgb, 1);
            return col;
        }
        ENDHLSL

        Pass
        {
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
Shader "Environment/AtmosphericScatteringSky"
{
    Properties
    {
        [Header(Setting)]
        _EyeHeight("Eye Height", Range(1, 50000)) = 5000
        _AtmosphericHeight("Atmospheric Height", Range(1, 100000)) = 60000
        _RayleighHeight("Rayleigh Height", Range(200, 20000)) = 8000
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
        #define MIE_G .76
        
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
        float _EyeHeight;
        float _AtmosphericHeight;
        float _RayleighHeight;

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
            setting.mieG = MIE_G;
            setting.rayleighHeight = _RayleighHeight;
            setting.earthRadius = EARTH_RADIUS;
            setting.earthAtmTopRadius = setting.earthRadius + max(_AtmosphericHeight, _EyeHeight + 1);
            setting.earthCenter = float3(0, -setting.earthRadius, 0);
            setting.waveLambdaMie = 2e-6 * _SunStrength;
            // wavelength with 680nm, 550nm, 450nm
            setting.waveLambdaRayleigh = ComputeWaveLambdaRayleigh(float3(680e-9, 550e-9, 450e-9));
            // see https://www.shadertoy.com/view/MllBR2
            setting.waveLambdaOzone = float3(1.36820899679147, 3.31405330400124, 0.13601728252538) * 6e-7 * 2.504;

            half4 sky = ComputeSkyInscattering(setting, _EyeHeight, V, L);
            half4 col = half4(sky.rgb, 1);
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
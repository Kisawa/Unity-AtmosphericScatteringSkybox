#ifndef ATMOSPHERIC_SCATTERING_TOOLS_INCLUDED
#define ATMOSPHERIC_SCATTERING_TOOLS_INCLUDED

#define EARTH_RADIUS 6370000
#define EPSILON 1e-5
#define SAMPLES_NUMS 16

struct ScatteringParams
{
    float sunRadius;
    float sunRadiance;

    float mieG;
    float rayleighHeight;

    float3 waveLambdaMie;
    float3 waveLambdaOzone;
    float3 waveLambdaRayleigh;

    float earthRadius;
    float earthAtmTopRadius;
    float3 earthCenter;
};

float2 ComputeRaySphereIntersection(float3 position, float3 dir, float3 center, float radius)
{
    float3 origin = position - center;
    float B = dot(origin, dir);
    float C = dot(origin, origin) - radius * radius;
    float D = B * B - C;

    float2 minimaxIntersections;
    if (D < 0.0)
    {
        minimaxIntersections = float2(-1.0, -1.0);
    }
    else
    {
        D = sqrt(D);
        minimaxIntersections = float2(-B - D, -B + D);
    }
    return minimaxIntersections;
}

float3 ComputeWaveLambdaRayleigh(float3 lambda)
{
    const float n = 1.0003;
    const float N = 2.545E25;
    const float pn = 0.035;
    const float n2 = n * n;
    const float pi3 = PI * PI * PI;
    const float rayleighConst = (8.0 * pi3 * pow(n2 - 1.0, 2.0)) / (3.0 * N) * ((6.0 + 3.0 * pn) / (6.0 - 7.0 * pn));
    return rayleighConst / (lambda * lambda * lambda * lambda);
}

float ComputePhaseMie(float theta, float g)
{
    float g2 = g * g;
    return (1.0 - g2) / pow(1.0 + g2 - 2.0 * g * saturate(theta), 1.5) / (4.0 * PI);
}

float ComputePhaseRayleigh(float theta)
{
    float theta2 = theta * theta;
    return (theta2 * 0.75 + 0.75) / (4.0 * PI);
}

float ChapmanApproximation(float X, float h, float cosZenith)
{
    float c = sqrt(X + h);
    float c_exp_h = c * exp(-h);
    if (cosZenith >= 0.0)
    {
        return c_exp_h / (c * cosZenith + 1.0);
    }
    else
    {
        float x0 = sqrt(1.0 - cosZenith * cosZenith) * (X + h);
        float c0 = sqrt(x0);
        return 2.0 * c0 * exp(X - x0) - c_exp_h / (1.0 - c * cosZenith);
    }
}

float GetOpticalDepthSchueler(float h, float H, float earthRadius, float cosZenith)
{
    return H * ChapmanApproximation(earthRadius / H, h / H, cosZenith);
}

float3 GetTransmittance(ScatteringParams setting, float3 L, float3 V)
{
    float ch = GetOpticalDepthSchueler(L.y, setting.rayleighHeight, setting.earthRadius, V.y);
    return exp(-(setting.waveLambdaMie + setting.waveLambdaRayleigh) * ch);
}

float2 ComputeOpticalDepth(ScatteringParams setting, float3 samplePoint, float3 V, float3 L, float neg)
{
    float rl = length(samplePoint);
    float h = rl - setting.earthRadius;
    float3 r = samplePoint / rl;

    float cos_chi_sun = dot(r, L);
    float cos_chi_ray = dot(r, V * neg);

    float opticalDepthSun = GetOpticalDepthSchueler(h, setting.rayleighHeight, setting.earthRadius, cos_chi_sun);
    float opticalDepthCamera = GetOpticalDepthSchueler(h, setting.rayleighHeight, setting.earthRadius, cos_chi_ray) * neg;

    return float2(opticalDepthSun, opticalDepthCamera);
}

void AerialPerspective(ScatteringParams setting, float3 start, float3 end, float3 V, float3 L, bool infinite, out float3 transmittance, out float3 insctrMie, out float3 insctrRayleigh)
{
    float inf_neg = infinite ? 1.0 : -1.0;

    float3 sampleStep = (end - start) / float(SAMPLES_NUMS);
    float3 samplePoint = end - sampleStep;
    float3 sampleLambda = setting.waveLambdaMie + setting.waveLambdaRayleigh + setting.waveLambdaOzone;

    float sampleLength = length(sampleStep);

    float3 scattering = 0.0;
    float2 lastOpticalDepth = ComputeOpticalDepth(setting, end, V, L, inf_neg);

    for (int i = 1; i < SAMPLES_NUMS; i++, samplePoint -= sampleStep)
    {
        float2 opticalDepth = ComputeOpticalDepth(setting, samplePoint, V, L, inf_neg);

        float3 segment_s = exp(-sampleLambda * (opticalDepth.x + lastOpticalDepth.x));
        float3 segment_t = exp(-sampleLambda * (opticalDepth.y - lastOpticalDepth.y));

        transmittance *= segment_t;

        scattering = scattering * segment_t;
        scattering += exp(-(length(samplePoint) - setting.earthRadius) / setting.rayleighHeight) * segment_s;

        lastOpticalDepth = opticalDepth;
    }

    insctrMie = scattering * setting.waveLambdaMie * sampleLength;
    insctrRayleigh = scattering * setting.waveLambdaRayleigh * sampleLength;
}

float ComputeSkyboxChapman(ScatteringParams setting, float3 eye, float3 V, float3 L, out float3 transmittance, out float3 insctrMie, out float3 insctrRayleigh)
{
    bool neg = true;

    float2 outerIntersections = ComputeRaySphereIntersection(eye, V, setting.earthCenter, setting.earthAtmTopRadius);
    if (outerIntersections.y < 0.0) return 0.0;

    float2 innerIntersections = ComputeRaySphereIntersection(eye, V, setting.earthCenter, setting.earthRadius);
    if (innerIntersections.x > 0.0)
    {
        neg = false;
        outerIntersections.y = innerIntersections.x;
    }

    eye -= setting.earthCenter;

    float3 start = eye + V * max(0.0, outerIntersections.x);
    float3 end = eye + V * outerIntersections.y;

    AerialPerspective(setting, start, end, V, L, neg, transmittance, insctrMie, insctrRayleigh);

    bool intersectionTest = innerIntersections.x < 0.0 && innerIntersections.y < 0.0;
    return intersectionTest ? 1.0 : 0.0;
}

half4 ComputeSkyInscattering(ScatteringParams setting, float eyeHeight, float3 V, float3 L)
{
    float3 eye = float3(0, eyeHeight, 0);
    float3 insctrMie = 0;
    float3 insctrRayleigh = 0;
    float3 insctrOpticalLength = 1;
    float intersectionTest = ComputeSkyboxChapman(setting, eye, V, L, insctrOpticalLength, insctrMie, insctrRayleigh);

    float phaseTheta = dot(V, L);
    float phaseMie = ComputePhaseMie(phaseTheta, setting.mieG);
    float phaseRayleigh = ComputePhaseRayleigh(phaseTheta);
    float phaseNight = 1.0 - saturate(insctrOpticalLength.x * EPSILON);

    float3 insctrTotalMie = insctrMie * phaseMie;
    float3 insctrTotalRayleigh = insctrRayleigh * phaseRayleigh;
    half3 sky = (insctrTotalMie + insctrTotalRayleigh) * setting.sunRadiance;

    float angle = saturate((1.0 - phaseTheta) * setting.sunRadius);
    float cosAngle = cos(angle * PI * 0.5);
    float edge = ((angle >= 0.9) ? smoothstep(0.9, 1.0, angle) : 0.0);

    float3 limbDarkening = GetTransmittance(setting, -L, V);
    limbDarkening *= pow(cosAngle, half3(0.420, 0.503, 0.652)) * lerp(1.0, half3(1.2, 0.9, 0.5), edge) * intersectionTest;

    sky += limbDarkening;
    return half4(sky, phaseNight * intersectionTest);
}

#endif
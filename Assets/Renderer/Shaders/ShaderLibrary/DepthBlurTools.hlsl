#ifndef DEPTH_BLEND_TOOLS_INCLUDED
#define DEPTH_BLEND_TOOLS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

float GetDepthBlendFactor(float2 uv, float depthFocus, float smoothness, float blendPower)
{
#if defined(_DEPTH_BLUR_ON)
	float depth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
	float factor = smoothstep(depthFocus - smoothness, depthFocus, depth);
	factor = pow(factor, blendPower);
	return factor;
#else
	return 1;
#endif
}

#endif
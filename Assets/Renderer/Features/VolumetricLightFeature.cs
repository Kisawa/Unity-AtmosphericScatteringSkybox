using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VolumetricLightFeature : ScriptableRendererFeature
{
    public Setting setting = new Setting();

    class VolumetricLightPass : ScriptableRenderPass
    {
        static readonly int _VolumetricLightBufferId = Shader.PropertyToID("_VolumetricLightBuffer");
        static readonly int _LightTempBufferId = Shader.PropertyToID("_LightTempBuffer");
        static readonly int _TempBufferId = Shader.PropertyToID("_TempBuffer");
        static int _InverseVP = Shader.PropertyToID("_InverseVP");
        static int _ColorTint = Shader.PropertyToID("_ColorTint");
        static int _RandomSeed = Shader.PropertyToID("_RandomSeed");
        static int _MieG = Shader.PropertyToID("_MieG");
        static int _ExtictionFactor = Shader.PropertyToID("_ExtictionFactor");
        static int _ShadowStrength = Shader.PropertyToID("_ShadowStrength");

        Setting setting;
        Material mat;
        Material bilateralBlurMat;
        RenderTargetIdentifier volumetricLightBuffer;
        int volumetricLightId = -1;
        RenderTargetIdentifier lightTempBuffer;
        int lightTempBufferId = -1;
        RenderTargetIdentifier tempBuffer;
        int tempBufferId = -1;

        public VolumetricLightPass(Setting setting)
        {
            this.setting = setting;
            Shader shader = Shader.Find("Postprocessing/VolumetricLight");
            if (shader == null)
                Debug.LogError("VolumetricLightPost: shader not found.");
            else
                mat = CoreUtils.CreateEngineMaterial(shader);
            Shader bilateralBlurShader = Shader.Find("Postprocessing/BilateralBlur");
            if (bilateralBlurShader == null)
                Debug.LogError("VolumetricLightPost: BilateralBlur shader not found.");
            else
                bilateralBlurMat = CoreUtils.CreateEngineMaterial(bilateralBlurShader);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (mat == null)
            {
                volumetricLightId = -1;
                lightTempBufferId = -1;
                tempBufferId = -1;
                return;
            }
            RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            blitTargetDescriptor.depthBufferBits = 0;
            blitTargetDescriptor.msaaSamples = 1;

            int lightTexWidth = Mathf.RoundToInt(blitTargetDescriptor.width * setting.Resolution);
            int lightTexHeight = Mathf.RoundToInt(blitTargetDescriptor.height * setting.Resolution);
            volumetricLightId = _VolumetricLightBufferId;
            cmd.GetTemporaryRT(volumetricLightId, lightTexWidth, lightTexHeight, 0, FilterMode.Bilinear, RenderTextureFormat.RG16);
            volumetricLightBuffer = new RenderTargetIdentifier(volumetricLightId);

            lightTempBufferId = _LightTempBufferId;
            cmd.GetTemporaryRT(lightTempBufferId, lightTexWidth, lightTexHeight, 0, FilterMode.Bilinear, RenderTextureFormat.RG16);
            lightTempBuffer = new RenderTargetIdentifier(lightTempBufferId);

            tempBufferId = _TempBufferId;
            cmd.GetTemporaryRT(tempBufferId, blitTargetDescriptor);
            tempBuffer = new RenderTargetIdentifier(tempBufferId);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (mat == null)
                return;
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, new ProfilingSampler("Volumetric Light")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                RenderTargetIdentifier cameraColorTarget = renderingData.cameraData.renderer.cameraColorTarget;
                Matrix4x4 p = GL.GetGPUProjectionMatrix(renderingData.cameraData.GetProjectionMatrix(), false);
                Matrix4x4 v = renderingData.cameraData.GetViewMatrix();
                mat.SetMatrix(_InverseVP, Matrix4x4.Inverse(p * v));
                mat.SetFloat(_RandomSeed, setting.RandomSeed);
                mat.SetColor(_ColorTint, setting.ColorTint);
                mat.SetFloat(_MieG, setting.MieG);
                mat.SetFloat(_ExtictionFactor, setting.ExtictionFactor);
                mat.SetFloat(_ShadowStrength, setting.ShadowStrength);
                Blit(cmd, cameraColorTarget, lightTempBuffer, mat, 0);

                if (bilateralBlurMat == null)
                    Blit(cmd, lightTempBuffer, volumetricLightBuffer);
                else
                {
                    bilateralBlurMat.SetFloat(BilateralBlurPass._Spread, setting.BlurSetting.Spread);
                    bilateralBlurMat.SetFloat(BilateralBlurPass._ColorSigma, setting.BlurSetting.ColorSigma);
                    bilateralBlurMat.SetFloat(BilateralBlurPass._SpaceSigma, setting.BlurSetting.SpaceSigma);
                    Blit(cmd, lightTempBuffer, volumetricLightBuffer, bilateralBlurMat, 0);
                }

                cmd.SetGlobalTexture("_BaseTex", cameraColorTarget);
                Blit(cmd, volumetricLightBuffer, tempBufferId, mat, 1);
                Blit(cmd, tempBuffer, cameraColorTarget);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (volumetricLightId != -1)
                cmd.ReleaseTemporaryRT(volumetricLightId);
            if (lightTempBufferId != -1)
                cmd.ReleaseTemporaryRT(lightTempBufferId);
            if (tempBufferId != -1)
                cmd.ReleaseTemporaryRT(tempBufferId);
        }
    }

    VolumetricLightPass pullBlurPass;

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera || renderingData.cameraData.isPreviewCamera)
            return;
        renderer.EnqueuePass(pullBlurPass);
    }

    public override void Create()
    {
        name = "Volumetric Light Feature";
        pullBlurPass = new VolumetricLightPass(setting);
        pullBlurPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
    }

    [System.Serializable]
    public class Setting
    {
        [Range(.1f, 1)]
        public float Resolution = 1;
        public float RandomSeed;
        [ColorUsage(true, true)]
        public Color ColorTint;
        [Range(0, 1)]
        public float MieG = .76f;
        [Range(0, .2f)]
        public float ExtictionFactor = .01f;
        [Range(0, 1)]
        public float ShadowStrength = .5f;

        public BilateralBlurPass.Setting BlurSetting = new BilateralBlurPass.Setting();
    }
}
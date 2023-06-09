using CustomVolumeComponent;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SunShaftFeature : ScriptableRendererFeature
{
    static int _ColorTint = Shader.PropertyToID("_ColorTint");
    static int _RandomSeed = Shader.PropertyToID("_RandomSeed");
    static int _MieG = Shader.PropertyToID("_MieG");
    static int _ExtictionFactor = Shader.PropertyToID("_ExtictionFactor");
    static int _ShadowStrength = Shader.PropertyToID("_ShadowStrength");

    class SunShaftPass : ScriptableRenderPass, IPass
    {
        static readonly int _SunShaftBufferId = Shader.PropertyToID("_SunShaftBuffer");
        static readonly int _LightTempBufferId = Shader.PropertyToID("_LightTempBuffer");
        static readonly int _TempBufferId = Shader.PropertyToID("_TempBuffer");
        static int _InverseVP = Shader.PropertyToID("_InverseVP");

        public SettingVolume settingVolume { get; set; }

        Material mat;
        Material bilateralBlurMat;
        RenderTargetIdentifier volumetricLightBuffer;
        int volumetricLightId = -1;
        RenderTargetIdentifier lightTempBuffer;
        int lightTempBufferId = -1;
        RenderTargetIdentifier tempBuffer;
        int tempBufferId = -1;

        public SunShaftPass()
        {
            Shader shader = Shader.Find("Postprocessing/SunShaft");
            if (shader == null)
                Debug.LogError("SunShaftPass: shader not found.");
            else
                mat = CoreUtils.CreateEngineMaterial(shader);
            Shader bilateralBlurShader = Shader.Find("Postprocessing/BilateralBlur");
            if (bilateralBlurShader == null)
                Debug.LogError("SunShaftPass: BilateralBlur shader not found.");
            else
                bilateralBlurMat = CoreUtils.CreateEngineMaterial(bilateralBlurShader);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (mat == null || settingVolume == null)
            {
                volumetricLightId = -1;
                lightTempBufferId = -1;
                tempBufferId = -1;
                return;
            }
            RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            blitTargetDescriptor.depthBufferBits = 0;
            blitTargetDescriptor.msaaSamples = 1;

            int lightTexWidth = Mathf.RoundToInt(blitTargetDescriptor.width * settingVolume.Resolution.value);
            int lightTexHeight = Mathf.RoundToInt(blitTargetDescriptor.height * settingVolume.Resolution.value);
            volumetricLightId = _SunShaftBufferId;
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
            if (mat == null || settingVolume == null)
                return;
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, new ProfilingSampler("Sun Shaft")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                RenderTargetIdentifier cameraColorTarget = renderingData.cameraData.renderer.cameraColorTarget;
                Matrix4x4 p = GL.GetGPUProjectionMatrix(renderingData.cameraData.GetProjectionMatrix(), false);
                Matrix4x4 v = renderingData.cameraData.GetViewMatrix();
                mat.SetMatrix(_InverseVP, Matrix4x4.Inverse(p * v));
                InjectMaterial(settingVolume);
                Blit(cmd, cameraColorTarget, lightTempBuffer, mat, 0);

                if (bilateralBlurMat == null || settingVolume.BlurSetting.Spread.value == 0)
                    Blit(cmd, lightTempBuffer, volumetricLightBuffer);
                else
                {
                    settingVolume.BlurSetting.InjectMaterial(bilateralBlurMat);
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

        public void InjectMaterial(PassSettingBase setting)
        {
            if (setting == null)
                return;
            setting.InjectMaterial(mat);
        }
    }

    SunShaftPass sunShaftPass;

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera || renderingData.cameraData.isPreviewCamera)
            return;
        var stack = VolumeManager.instance.stack;
        SunShaft sunShaft = stack.GetComponent<SunShaft>();
        if (!sunShaft.IsActive())
            return;
        sunShaftPass.settingVolume = sunShaft.Setting;
        renderer.EnqueuePass(sunShaftPass);
    }

    public override void Create()
    {
        name = "Sun Shaft Feature";
        sunShaftPass = new SunShaftPass();
        sunShaftPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
    }

    [System.Serializable]
    public class Setting : PassSettingBase
    {
        [Range(.1f, 1)]
        public float Resolution = 1;
        public float RandomSeed;
        [ColorUsage(true, true)]
        public Color ColorTint = new Color(.9f, .6f, .4f, .5f);
        [Range(0, 1)]
        public float MieG = .76f;
        [Range(0, .1f)]
        public float ExtictionFactor = .01f;
        [Range(0, 1)]
        public float ShadowStrength = .5f;

        public BilateralBlurPass.Setting BlurSetting = new BilateralBlurPass.Setting();

        public override void InjectMaterial(Material mat)
        {
            if (mat == null)
                return;
            mat.SetFloat(_RandomSeed, RandomSeed);
            mat.SetColor(_ColorTint, ColorTint);
            mat.SetFloat(_MieG, MieG);
            mat.SetFloat(_ExtictionFactor, ExtictionFactor);
            mat.SetFloat(_ShadowStrength, ShadowStrength);
        }
    }

    [System.Serializable]
    public class SettingVolume : PassSettingBase
    {
        public ClampedFloatParameter Resolution = new ClampedFloatParameter(0, 0, 1);
        public FloatParameter RandomSeed = new FloatParameter(0);
        public ColorParameter ColorTint = new ColorParameter(new Color(.9f, .6f, .4f, .5f), true, true, true);
        public ClampedFloatParameter MieG = new ClampedFloatParameter(.76f, 0, 1);
        public ClampedFloatParameter ExtictionFactor = new ClampedFloatParameter(.01f, 0, .1f);
        public ClampedFloatParameter ShadowStrength = new ClampedFloatParameter(.5f, 0, 1);

        public BilateralBlurPass.SettingVolume BlurSetting = new BilateralBlurPass.SettingVolume();

        public override void InjectMaterial(Material mat)
        {
            if (mat == null)
                return;
            mat.SetFloat(_RandomSeed, RandomSeed.value);
            mat.SetColor(_ColorTint, ColorTint.value);
            mat.SetFloat(_MieG, MieG.value);
            mat.SetFloat(_ExtictionFactor, ExtictionFactor.value);
            mat.SetFloat(_ShadowStrength, ShadowStrength.value);
        }
    }
}
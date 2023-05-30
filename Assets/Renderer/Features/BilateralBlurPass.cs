using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BilateralBlurPass : ScriptableRenderPass
{
    public static int _Spread = Shader.PropertyToID("_Spread");
    public static int _ColorSigma = Shader.PropertyToID("_ColorSigma");
    public static int _SpaceSigma = Shader.PropertyToID("_SpaceSigma");
    static readonly int _TempBufferId = Shader.PropertyToID("_TempBuffer");
    
    Setting setting;
    Material mat;
    RenderTargetIdentifier tempBuffer;
    int tempBufferId = -1;

    public BilateralBlurPass(Setting setting)
    {
        this.setting = setting;
        Shader shader = Shader.Find("Postprocessing/BilateralBlur");
        if (shader == null)
        {
            Debug.LogError("BilateralBlurPass: shader not found.");
            return;
        }
        mat = CoreUtils.CreateEngineMaterial(shader);
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        if (mat == null)
        {
            tempBufferId = -1;
            return;
        }
        RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
        blitTargetDescriptor.depthBufferBits = 0;
        blitTargetDescriptor.msaaSamples = 1;

        tempBufferId = _TempBufferId;
        cmd.GetTemporaryRT(tempBufferId, blitTargetDescriptor);
        tempBuffer = new RenderTargetIdentifier(tempBufferId);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (mat == null)
            return;
        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, new ProfilingSampler("Bilateral Blur")))
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            mat.SetFloat(_Spread, setting.Spread);
            mat.SetFloat(_ColorSigma, setting.ColorSigma);
            mat.SetFloat(_SpaceSigma, setting.SpaceSigma);

            RenderTargetIdentifier cameraColorTarget = renderingData.cameraData.renderer.cameraColorTarget;
            Blit(cmd, cameraColorTarget, tempBuffer, mat);
            Blit(cmd, tempBuffer, cameraColorTarget);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        if (tempBufferId != -1)
            cmd.ReleaseTemporaryRT(tempBufferId);
    }

    [System.Serializable]
    public class Setting
    {
        [Range(0, .1f)]
        public float Spread = .01f;
        [Range(.01f, 1)]
        public float ColorSigma = .1f;
        [Range(1, 20)]
        public float SpaceSigma = 10;
    }
}
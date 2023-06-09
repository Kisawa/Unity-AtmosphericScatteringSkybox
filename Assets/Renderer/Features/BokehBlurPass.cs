using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BokehBlurPass : ScriptableRenderPass, IPass
{
    public static int _Spread = Shader.PropertyToID("_Spread");
    public static int _Shape = Shader.PropertyToID("_Shape");
    public static int _Iteration = Shader.PropertyToID("_Iteration");
    public static int _GoldenAngle = Shader.PropertyToID("_GoldenAngle");
    public static int _BokehHardness = Shader.PropertyToID("_BokehHardness");
    public static string _Keyword_CurlyTrick = "_CURLY_BLUR";
    static readonly int _TempBufferId = Shader.PropertyToID("_TempBuffer");

    Setting setting;
    Material mat;
    RenderTargetIdentifier tempBuffer;
    int tempBufferId = -1;

    public BokehBlurPass()
    {
        CreateMaterial();
    }

    public BokehBlurPass(Setting setting)
    {
        this.setting = setting;
        CreateMaterial();
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
        using (new ProfilingScope(cmd, new ProfilingSampler("Bokeh Blur")))
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            InjectMaterial(setting);
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

    public void InjectMaterial(PassSettingBase setting)
    {
        if (setting == null)
            return;
        setting.InjectMaterial(mat);
    }

    void CreateMaterial()
    {
        Shader shader = Shader.Find("Postprocessing/BokehBlur");
        if (shader == null)
        {
            Debug.LogError("BokehBlurPass: shader not found.");
            return;
        }
        mat = CoreUtils.CreateEngineMaterial(shader);
    }

    [System.Serializable]
    public class Setting : PassSettingBase
    {
        [Range(1, 64)]
        public int Iteration = 16;
        [Range(.01f, 3)]
        public float Spread = .75f;
        [Range(3, 10)]
        public int Shape = 6;
        [Range(.1f, 5)]
        public float BokehHardness = 1;
        [Header("Curly Trick")]
        public bool TrickOn = false;
        [Range(0, 3.1415f)]
        public float GoldenAngle = 2.3398f;

        public override void InjectMaterial(Material mat)
        {
            if (mat == null)
                return;
            mat.SetFloat(_Spread, Spread);
            mat.SetInt(_Shape, Shape);
            mat.SetInt(_Iteration, Iteration);
            mat.SetFloat(_GoldenAngle, GoldenAngle);
            mat.SetFloat(_BokehHardness, BokehHardness);
            if (TrickOn)
                mat.EnableKeyword(_Keyword_CurlyTrick);
            else
                mat.DisableKeyword(_Keyword_CurlyTrick);
        }
    }

    [System.Serializable]
    public class SettingVolume : PassSettingBase
    {
        [Space(10)]
        [Header("Bokeh Blur")]
        public ClampedIntParameter Iteration = new ClampedIntParameter(0, 0, 64);
        public ClampedFloatParameter Spread = new ClampedFloatParameter(.75f, 0, 3);
        public ClampedIntParameter Shape = new ClampedIntParameter(6, 3, 10);
        public ClampedFloatParameter BokehHardness = new ClampedFloatParameter(1, .1f, 5);
        [Header("Curly Trick")]
        public BoolParameter TrickOn = new BoolParameter(false);
        [Range(0, 3.1415f)]
        public ClampedFloatParameter GoldenAngle = new ClampedFloatParameter(2.3398f, 0, 3.1415f);

        public override void InjectMaterial(Material mat)
        {
            if (mat == null)
                return;
            mat.SetFloat(_Spread, Spread.value);
            mat.SetInt(_Shape, Shape.value);
            mat.SetInt(_Iteration, Iteration.value);
            mat.SetFloat(_GoldenAngle, GoldenAngle.value);
            mat.SetFloat(_BokehHardness, BokehHardness.value);
            if (TrickOn.value)
                mat.EnableKeyword(_Keyword_CurlyTrick);
            else
                mat.DisableKeyword(_Keyword_CurlyTrick);
        }
    }
}
using CustomVolumeComponent;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DepthBlurFeature : ScriptableRendererFeature
{
    public static readonly int _DepthFocus = Shader.PropertyToID("_DepthFocus");
    public static readonly int _DepthSmoothness = Shader.PropertyToID("_DepthSmoothness");
    public static readonly int _DepthBlendPower = Shader.PropertyToID("_DepthBlendPower");

    public RenderPassEvent Event = RenderPassEvent.BeforeRenderingPostProcessing;

    BokehBlurPass bokehBlurPass;
    BilateralBlurPass bilateralBlurPass;

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera || renderingData.cameraData.isPreviewCamera)
            return;
        var stack = VolumeManager.instance.stack;
        DepthBlur depthBlur = stack.GetComponent<DepthBlur>();
        if (!depthBlur.IsActive())
            return;
        if (depthBlur.IsActiveBokehBlur())
        {
            renderer.EnqueuePass(bokehBlurPass);
            bokehBlurPass.InjectMaterial(depthBlur.DepthSetting);
            bokehBlurPass.InjectMaterial(depthBlur.BokehBlurSetting);
        }
        if (depthBlur.IsActiveBilateralBlur())
        {
            renderer.EnqueuePass(bilateralBlurPass);
            bilateralBlurPass.InjectMaterial(depthBlur.DepthSetting);
            bilateralBlurPass.InjectMaterial(depthBlur.BilateralBlurSetting);
        }
    }

    public override void Create()
    {
        name = "Depth Blur";
        bokehBlurPass = new BokehBlurPass();
        bokehBlurPass.renderPassEvent = Event;
        bilateralBlurPass = new BilateralBlurPass();
        bilateralBlurPass.renderPassEvent = Event;
    }

    [System.Serializable]
    public class Setting
    {
        public float DepthFocus = 20;
        public float Smoothness = 10;
        [Range(.1f, 5)]
        public float BlendPower = 1;

        public void InjectMaterial(Material mat)
        {
            if (mat == null)
                return;
            mat.EnableKeyword("_DEPTH_BLUR_ON");
            mat.SetFloat(_DepthFocus, DepthFocus);
            mat.SetFloat(_DepthSmoothness, Smoothness);
            mat.SetFloat(_DepthBlendPower, BlendPower);
        }
    }

    [System.Serializable]
    public class SettingVolume : PassSettingBase
    {
        public FloatParameter DepthFocus = new FloatParameter(0);
        public FloatParameter Smoothness = new FloatParameter(10);
        public ClampedFloatParameter BlendPower = new ClampedFloatParameter(1, .1f, 5);

        public override void InjectMaterial(Material mat)
        {
            if (mat == null)
                return;
            mat.EnableKeyword("_DEPTH_BLUR_ON");
            mat.SetFloat(_DepthFocus, DepthFocus.value);
            mat.SetFloat(_DepthSmoothness, Smoothness.value);
            mat.SetFloat(_DepthBlendPower, BlendPower.value);
        }
    }
}
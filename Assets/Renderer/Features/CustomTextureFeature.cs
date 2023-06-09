using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CustomTextureFeature : ScriptableRendererFeature
{
    public string TextureName;
    public List<string> ShaderTags = new List<string>();

    [Header("Draw Setting")]
    public LayerMask layer = -1;
    public DrawType drawType;
    public OverrideSetting overrideSetting = new OverrideSetting();

    [Header("TextureSetting")]
    public TextureSetting textureSetting = new TextureSetting();

    class CustomTexturePass : ScriptableRenderPass
    {
        string texProp;
        RenderTargetHandle _Texture;
        List<ShaderTagId> ShaderTags;
        
        LayerMask layer;
        DrawType drawType;
        OverrideSetting overrideSetting;
        
        TextureSetting textureSetting;

        public CustomTexturePass(string textureName, List<ShaderTagId> shaderTags, LayerMask layer, DrawType drawType, OverrideSetting overrideSetting, TextureSetting textureSetting)
        {
            texProp = textureName;
            _Texture.Init(textureName);
            ShaderTags = shaderTags;
            this.layer = layer;
            this.drawType = drawType;
            this.overrideSetting = overrideSetting;
            this.textureSetting = textureSetting;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            switch (textureSetting.downSample)
            {
                case DownSample.x2:
                    cameraTextureDescriptor.width /= 2;
                    cameraTextureDescriptor.height /= 2;
                    break;
                case DownSample.x4:
                    cameraTextureDescriptor.width /= 4;
                    cameraTextureDescriptor.height /= 4;
                    break;
            }
            cmd.GetTemporaryRT(_Texture.id, cameraTextureDescriptor.width, cameraTextureDescriptor.height, (int)textureSetting.depthBuffer, textureSetting.filterMode, textureSetting.textureFormat);
            ConfigureTarget(_Texture.Identifier());
            ConfigureClear(ClearFlag.All, textureSetting.BackgroundColor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, new ProfilingSampler($"Custom Texture Pass:  {texProp}")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                if (drawType == DrawType.Opaque || drawType == DrawType.Both)
                {
                    DrawingSettings drawingSettings = CreateDrawingSettings(ShaderTags, ref renderingData, SortingCriteria.CommonOpaque);
                    if (overrideSetting.overrideMaterial != null)
                    {
                        drawingSettings.overrideMaterial = overrideSetting.overrideMaterial;
                        drawingSettings.overrideMaterialPassIndex = overrideSetting.overridePass;
                        drawingSettings.perObjectData = overrideSetting.perObjectData;
                    }
                    FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layer);
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
                }
                if (drawType == DrawType.Transparent || drawType == DrawType.Both)
                {
                    DrawingSettings drawingSettings = CreateDrawingSettings(ShaderTags, ref renderingData, SortingCriteria.CommonTransparent);
                    if (overrideSetting.overrideMaterial != null)
                    {
                        drawingSettings.overrideMaterial = overrideSetting.overrideMaterial;
                        drawingSettings.overrideMaterialPassIndex = overrideSetting.overridePass;
                        drawingSettings.perObjectData = overrideSetting.perObjectData;
                    }
                    FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.transparent, layer);
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_Texture.id);
        }
    }

    CustomTexturePass pass;

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null || renderingData.cameraData.isSceneViewCamera || renderingData.cameraData.isPreviewCamera)
            return;
        renderer.EnqueuePass(pass);
    }

    public override void Create()
    {
        List<ShaderTagId> shaderTags = new List<ShaderTagId>();
        for (int i = 0; i < ShaderTags.Count; i++)
        {
            string tag = ShaderTags[i];
            if (string.IsNullOrEmpty(tag) || shaderTags.Any(x => x.name == tag))
                continue;
            shaderTags.Add(new ShaderTagId(tag));
        }
        if (string.IsNullOrEmpty(TextureName) || shaderTags.Count == 0)
        {
            pass = null;
            return;
        }
        pass = new CustomTexturePass(TextureName, shaderTags, layer, drawType, overrideSetting, textureSetting);
        pass.renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
    }

    [System.Serializable]
    public class OverrideSetting
    {
        public Material overrideMaterial;
        public int overridePass = -1;
        public PerObjectData perObjectData;
    }

    [System.Serializable]
    public class TextureSetting
    {
        public Color BackgroundColor = Color.black;
        public DownSample downSample = DownSample.None;
        public DepthBuffer depthBuffer = DepthBuffer.None;
        public FilterMode filterMode = FilterMode.Bilinear;
        public RenderTextureFormat textureFormat = RenderTextureFormat.ARGB32;
    }

    public enum DownSample { None, x2, x4 }

    public enum DepthBuffer { None = 0, Depth = 16, Stencil = 24 }

    public enum DrawType { Both, Opaque, Transparent }
}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace CustomVolumeComponent
{
    [Serializable, VolumeComponentMenuForRenderPipeline("Custom/Depth Blur", typeof(UniversalRenderPipeline))]
    public class DepthBlur : VolumeComponent, IPostProcessComponent
    {
        public DepthBlurFeature.SettingVolume DepthSetting = new DepthBlurFeature.SettingVolume();

        public BokehBlurPass.SettingVolume BokehBlurSetting = new BokehBlurPass.SettingVolume();

        public BilateralBlurPass.SettingVolume BilateralBlurSetting = new BilateralBlurPass.SettingVolume();

        public bool IsActiveBokehBlur() => BokehBlurSetting.Iteration.value > 0 && BokehBlurSetting.Spread.value > 0;

        public bool IsActiveBilateralBlur() => BilateralBlurSetting.Spread.value > 0;

        public bool IsActive() => DepthSetting.DepthFocus.value > 0;

        public bool IsTileCompatible() => true;
    }
}
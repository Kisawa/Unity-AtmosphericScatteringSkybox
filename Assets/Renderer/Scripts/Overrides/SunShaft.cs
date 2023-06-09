using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace CustomVolumeComponent
{
    [Serializable, VolumeComponentMenuForRenderPipeline("Custom/Sun Shaft", typeof(UniversalRenderPipeline))]
    public class SunShaft : VolumeComponent, IPostProcessComponent
    {
        public SunShaftFeature.SettingVolume Setting = new SunShaftFeature.SettingVolume();

        public bool IsActive() => Setting.Resolution.value > 0;

        public bool IsTileCompatible() => true;
    }
}
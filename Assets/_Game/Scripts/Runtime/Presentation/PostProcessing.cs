using ShadowRPG.Core.Tuning;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>화면 후처리: 빛 번짐(블룸)·비네트·색 보정. 어두움은 에셋이 만들고, 여기서는 대비·빛만 살립니다.</summary>
    public static class PostProcessing
    {
        public static void Ensure(Camera camera, PresentationTuning t)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            if (Object.FindAnyObjectByType<Volume>() != null) return;

            var go = new GameObject("PostProcessing");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(t.BloomIntensity);
            bloom.threshold.Override(0.85f);
            bloom.scatter.Override(0.65f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(t.VignetteIntensity);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.02f, 0.01f, 0.04f));

            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(14f);
            color.saturation.Override(6f);
            color.postExposure.Override(0.1f);

            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.Neutral);
        }
    }
}

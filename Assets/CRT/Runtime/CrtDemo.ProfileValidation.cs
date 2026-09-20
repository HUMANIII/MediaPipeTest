using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MediaPipeTest.CRT
{
    public sealed partial class CrtDemo
    {
        IEnumerator ValidateProfiles(string output, Report report)
        {
            string[] originalProfiles = new string[presets.Length];
            for (int i = 0; i < presets.Length; i++) originalProfiles[i] = JsonUtility.ToJson(presets[i].settings);
            ApplyPreset(2);
            settings.monochrome = .17f;
            ApplyPreset(2);
            bool reapplied = presets.Length > 2 && JsonUtility.ToJson(settings) == originalProfiles[2];
            // Regions loaded from an asset must survive Update and the temporary Partial UV toggle.
            var regionProfile = ScriptableObject.CreateInstance<CrtProfile>();
            regionProfile.settings.screenRect = new Vector4(.2f,.1f,.6f,.8f);
            regionProfile.settings.effectMask = halfMask;
            var savedPresets = presets;
            presets = new[] { regionProfile };
            ApplyPreset(0); partial = false;
            yield return null;
            reapplied &= settings.screenRect == regionProfile.settings.screenRect && fullscreenSettings.effectMask == halfMask;
            partial = true; yield return null;
            reapplied &= fullscreenSettings.screenRect == regionProfile.settings.screenRect;
            partial = false; yield return null;
            reapplied &= settings.screenRect == regionProfile.settings.screenRect;
            presets = savedPresets; selectedPreset = 0;
            Destroy(regionProfile);
            new CrtSettings().CopyTo(settings);
            mode = 0; original = false; useVideo = false; partial = false; includeUI = false;
            var shared = ScriptableObject.CreateInstance<CrtProfile>();
            foreach (var surface in Surfaces) surface.profile = shared;
            Fullscreen.profile = shared;
            bool bindings = reapplied;
            yield return Capture(output,"10-profile-color");
            shared.settings.monochrome = .5f;
            yield return Capture(output,"11-profile-half-mono");
            shared.settings.monochrome = 1;
            yield return Capture(output,"12-profile-mono");
            foreach (var surface in Surfaces)
                bindings &= ReferenceEquals(surface.ActiveSettings, shared.settings) && surface.Instance.GetFloat("_Monochrome") == 1;

            // The profile also reaches uGUI's derived stencil material.
            var ui = (RectTransform)Surfaces[2].transform;
            Vector2 anchor = ui.anchorMin;
            var clip = Rect("Profile stencil validation", display.transform, anchor, new Vector2(520,270));
            clip.gameObject.AddComponent<Image>().sprite = screenShape;
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            clip.gameObject.AddComponent<CanvasGroup>().alpha = .5f;
            ui.SetParent(clip,false); ui.anchorMin = ui.anchorMax = new Vector2(.5f,.5f); ui.anchoredPosition = Vector2.zero;
            yield return Capture(output,"32-profile-ui-mask");
            bindings &= ui.GetComponent<RawImage>().materialForRendering.GetFloat("_Monochrome") == 1;
            ui.SetParent(display.transform,false); ui.anchorMin = ui.anchorMax = anchor; ui.anchoredPosition = Vector2.zero;
            Destroy(clip.gameObject);

            shared.settings.monoTint = new Color(.2f,1,.3f);
            yield return Capture(output,"13-profile-green");
            shared.settings.monoTint = new Color(1,.65f,.15f);
            yield return Capture(output,"14-profile-amber");
            new CrtSettings().CopyTo(shared.settings);
            shared.settings.vignette = 0;
            yield return Capture(output,"15-vignette-off");
            shared.settings.vignette = 1;
            yield return Capture(output,"16-vignette-strong");
            shared.settings.vignetteRadius = .55f;
            yield return Capture(output,"17-vignette-radius");
            shared.settings.vignetteSoftness = .3f;
            yield return Capture(output,"18-vignette-hard");
            shared.settings.vignetteSoftness = 1.7f;
            yield return Capture(output,"19-vignette-soft");

            new CrtSettings().CopyTo(shared.settings);
            shared.settings.monochrome = 1; shared.settings.monoTint = new Color(.2f,1,.3f);
            shared.settings.screenRect = new Vector4(.15f,.12f,.7f,.76f);
            yield return Capture(output,"20-profile-partial");
            shared.settings.screenRect = new Vector4(0,0,1,1); shared.settings.effectMask = halfMask;
            yield return Capture(output,"21-profile-mask");
            shared.settings.effectMask = null;
            mode = 1; includeUI = true;
            shared.settings.monoTint = Color.white;
            yield return Capture(output,"22-fullscreen-mono");
            bindings &= ReferenceEquals(Fullscreen.ActiveSettings,shared.settings) && Fullscreen.Instance.GetFloat("_Monochrome") == 1;
            shared.settings.monochrome = .5f;
            yield return Capture(output,"23-fullscreen-half-mono");
            shared.settings.monochrome = 1; shared.settings.monoTint = new Color(.2f,1,.3f);
            yield return Capture(output,"24-fullscreen-green");
            shared.settings.monoTint = new Color(1,.65f,.15f);
            yield return Capture(output,"25-fullscreen-amber");
            new CrtSettings().CopyTo(shared.settings);
            shared.settings.vignette = 0;
            yield return Capture(output,"26-fullscreen-vignette-off");
            shared.settings.vignette = 1;
            yield return Capture(output,"27-fullscreen-vignette-strong");
            shared.settings.vignetteRadius = .55f;
            yield return Capture(output,"28-fullscreen-vignette-radius");
            shared.settings.vignetteSoftness = 1.7f;
            yield return Capture(output,"29-fullscreen-vignette-soft");
            original = true;
            yield return Capture(output,"34-fullscreen-disabled");
            bindings &= !fullscreenFeature.isActive && !Fullscreen.Instance.IsKeywordEnabled("_CRT_EFFECT_ON");
            original = false;
            yield return Capture(output,"35-fullscreen-reenabled");
            bindings &= fullscreenFeature.isActive && Fullscreen.Instance.IsKeywordEnabled("_CRT_EFFECT_ON");
            shared.settings.monochrome = 1; original = true;
            mode = 0; includeUI = false;
            yield return Capture(output,"30-profile-disabled");
            foreach (var surface in Surfaces) { bindings &= !surface.Instance.IsKeywordEnabled("_CRT_EFFECT_ON"); surface.profile = null; }
            Fullscreen.profile = null;
            original = false;
            yield return Capture(output,"31-profile-fallback");
            foreach (var surface in Surfaces) bindings &= ReferenceEquals(surface.ActiveSettings,settings) && surface.Instance.GetFloat("_Monochrome") == 0;
            var replacement = ScriptableObject.CreateInstance<CrtProfile>();
            replacement.settings.monochrome = 1;
            Surfaces[0].profile = replacement;
            shared.settings.monoTint = new Color(.2f,1,.3f);
            Surfaces[1].profile = shared;
            yield return Capture(output,"33-profile-independent");
            Surfaces[0].profile = null; Surfaces[1].profile = null;
            Destroy(shared); Destroy(replacement);
            report.profilesUnchanged = true;
            for (int i = 0; i < presets.Length; i++) report.profilesUnchanged &= originalProfiles[i] == JsonUtility.ToJson(presets[i].settings);
            report.profileBindingsWorking = bindings;
            new CrtSettings().CopyTo(settings);
            foreach (var sync in syncControls) sync();
        }
    }
}

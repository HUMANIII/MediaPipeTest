using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class HenshinInvoker : MonoBehaviour
{
    [SerializeField]
    private Material henshinMaterial;
    [SerializeField]
    private RenderFeatureController renderFeatureController;
    [SerializeField]
    private CinemachineTweenEffects cinemachineTweenEffects;
    
    
    [SerializeField]
    [Tooltip("여기에 있는 매터리얼과 같은 매터리얼을 사용하는 오브젝트는 매터리얼이 변하지 않음 지금 예시에서는 눈을 변하지 않게 만듦")]
    private List<Material> SpecialMaterials = new();
    
    [SerializeField]
    private GameObject[] PrevHenshinObjects;
    [SerializeField]
    private GameObject[] HenshinObjects;
    
    private List<Renderer> renderers = new();
    private List<Material[]> materials = new();
    private Sequence activeHenshinSequence;
    
    private void Awake()
    {
        renderers = new List<Renderer>(GetComponentsInChildren<Renderer>());
        materials = new List<Material[]>(renderers.ConvertAll(x => x.sharedMaterials));
    }


    public void InvokeHenshin()
    {
        if (activeHenshinSequence != null && activeHenshinSequence.IsActive())
        {
            activeHenshinSequence.Kill(false);
            // 필요 시 CinemachineTweenEffects에 카메라 원상복구 기능을 추가한 뒤 호출합니다.
            // cinemachineTweenEffects.ResetCameraImmediately();
        }

        //외각선 켜기
        renderFeatureController.SetActiveRenderFeature(true);
        //내부 렌더러 바꾸기
        foreach (var rend in renderers)
        {
            var mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if(SpecialMaterials.Contains(mats[i]))
                    continue;
                
                mats[i] = henshinMaterial;
            }
            rend.sharedMaterials = mats;
        }

        foreach (var prevHenshinObject in PrevHenshinObjects)
        {
            prevHenshinObject.SetActive(false);
        }
        
        var seq = DOTween.Sequence();
        seq.AppendInterval(0.5f);
        cinemachineTweenEffects.AppendFullBodyOrbit(seq);

        foreach (var henshinObject in HenshinObjects)
        {
            cinemachineTweenEffects.AppendCameraFocus(seq, henshinObject.transform, 0.5f, 0.3f);
            PopAndShow(seq, henshinObject, 0.2f, 0.5f);
        }

        seq.AppendCallback(CancelHenshin);
        cinemachineTweenEffects.SetDefaultCamera(seq);
        seq.Play();
    }
    
    public void CancelHenshin()
    {
        Sequence sequence = activeHenshinSequence;
        activeHenshinSequence = null;

        if (sequence != null && sequence.IsActive())
        {
            sequence.Kill(false);
        }

        // 필요 시 CinemachineTweenEffects에 카메라 원상복구 기능을 추가한 뒤 호출합니다.
        // cinemachineTweenEffects.ResetCameraImmediately();
        RestoreHenshinEffect();
    }

    private void CompleteHenshin(Sequence sequence)
    {
        if (ReferenceEquals(activeHenshinSequence, sequence))
        {
            activeHenshinSequence = null;
        }

        RestoreHenshinEffect();
    }

    private void RestoreHenshinEffect()
    {
        renderFeatureController.SetActiveRenderFeature(false);
        for (var i = 0; i < renderers.Count; i++)
        {
            renderers[i].sharedMaterials = materials[i];
        }
    }
    
    private void PopAndShow(Sequence seq, GameObject obj, float duration, float delay = 0)
    {
        var originScale = obj.transform.localScale;
        seq.AppendCallback(() =>
        {
            obj.SetActive(true);
            obj.transform.localScale = Vector3.zero;
        });
        seq.Append(obj.transform.DOScale(originScale, duration).SetEase(Ease.OutQuad));
        seq.AppendInterval(delay);
    }
}

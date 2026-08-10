using System;
using System.Collections.Generic;
using DG.Tweening;
using MediaPipeTest.SequenceEffects;
using UnityEngine;

public class HenshinInvoker : MonoBehaviour
{
    [Tooltip("변신할때 몸체가 사용할 매터리얼")]
    [SerializeField]
    private Material henshinMaterial;
    [SerializeField]
    private RenderFeatureController renderFeatureController;
    [SerializeField]
    private SequenceEffectFactory sequenceEffectFactory;
    
    
    [SerializeField]
    [Tooltip("여기에 있는 매터리얼과 같은 매터리얼을 사용하는 오브젝트는 매터리얼이 변하지 않음 지금 예시에서는 눈을 변하지 않게 만듦")]
    private List<Material> SpecialMaterials = new();
    
    [Tooltip("변신 전 오브젝트 변신 시작시 사라짐")]
    [SerializeField]
    private GameObject[] PrevHenshinObjects;
    
    private List<Renderer> renderers = new();
    private List<Material[]> materials = new();
    
    private void Awake()
    {
        renderers = new List<Renderer>(GetComponentsInChildren<Renderer>());
        materials = new List<Material[]>(renderers.ConvertAll(x => x.sharedMaterials));
        sequenceEffectFactory.OnInitialize.AddListener(InitHenshin);
        sequenceEffectFactory.OnCompleted.AddListener(RestoreHenshinSettings);
    }
    
    public void InitHenshin()
    {
        foreach (var prevHenshinObject in PrevHenshinObjects)
        {
            prevHenshinObject.SetActive(false);
        }
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
    }

    public void RestoreHenshinSettings()
    {
        for (var i = 0; i < renderers.Count; i++)
        {
            renderers[i].sharedMaterials = materials[i];
        }
        renderFeatureController.SetActiveRenderFeature(false);
    }

    public void InvokeHenshinSp()
    {
        sequenceEffectFactory.TryPlay();
    }
}

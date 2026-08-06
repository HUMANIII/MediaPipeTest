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
    [Tooltip("여기에 있는 매터리얼과 같은 매터리얼을 사용하는 오브젝트는 매터리얼이 변하지 않음 지금 예시에서는 눈을 변하지 않게 만듦")]
    private List<Material> SpecialMaterials = new();
    
    [SerializeField]
    private GameObject[] PrevHenshinObjects;
    [SerializeField]
    private GameObject[] HenshinObjects;
    
    private List<Renderer> renderers = new();
    private List<Material[]> materials = new();
    
    private void Awake()
    {
        renderers = new List<Renderer>(GetComponentsInChildren<Renderer>());
        materials = new List<Material[]>(renderers.ConvertAll(x => x.sharedMaterials));
    }


    public void InvokeHenshin()
    {
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

        foreach (var henshinObject in HenshinObjects)
        {
            PopAndShow(seq, henshinObject, 0.2f, 0.5f);
        }

        seq.AppendCallback(CancelHenshin);
        seq.Play();
    }
    
    public void CancelHenshin()
    {
        renderFeatureController.SetActiveRenderFeature(false);
        for (var i = 0; i < renderers.Count; i++)
        {
            renderers[i].sharedMaterials = materials[i];
        }
    }
    
    private void PopAndShow(Sequence seq, GameObject obj, float duration, float delay = 0)
    {
        obj.SetActive(true);
        var originScale = obj.transform.localScale;
        obj.transform.localScale = Vector3.zero;
        seq.Append(obj.transform.DOScale(originScale, duration).SetEase(Ease.OutQuad));
        seq.AppendInterval(delay);
    }
}

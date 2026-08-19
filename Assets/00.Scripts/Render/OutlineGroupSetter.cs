using System;
using UnityEngine;

public class OutlineGroupSetter : MonoBehaviour
{
    public const string OutlineGroupIndexTag = "_OutlineGroupIndex";
    
    [SerializeField] 
    private int groupIndex;

    private static readonly int GroupIndexId = Shader.PropertyToID(OutlineGroupIndexTag);

    private void Awake()
    {
        ApplyGroupIndex();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyGroupIndex();
    }
#endif

    private void ApplyGroupIndex()
    {
        Renderer[] renderers =
            GetComponentsInChildren<Renderer>(true);

        foreach (Renderer targetRenderer in renderers)
        {
            var propertyBlock = new MaterialPropertyBlock();

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetInteger(GroupIndexId, groupIndex);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}

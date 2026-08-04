using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class RendererInjector : MonoBehaviour
{
    //[SerializeField]
    //private
    
    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginContextRendering += OnBeginFrameRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginContextRendering -= OnBeginFrameRendering;
    }
    
    private void OnBeginFrameRendering(ScriptableRenderContext ctx, List<Camera> cameras)
    {
        foreach (Camera camObj in cameras)
        {
            if(camObj != cam)
                continue;
            
            
            
        }
    }


}

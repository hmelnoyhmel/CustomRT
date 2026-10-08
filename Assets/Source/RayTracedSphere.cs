using System;
using UnityEngine;

public class RayTracedSphere : MonoBehaviour
{
    public RayTracingMaterial material;

    [SerializeField, HideInInspector] bool materialInitFlag;

    void OnValidate()
    {
        if (!materialInitFlag)
        {
            materialInitFlag = true;
            material.SetDefaultValues();
        }

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null) 
            renderer.sharedMaterial.color = material.color;
    }

}

using UnityEngine;

[System.Serializable]
public struct RayTracingMaterial
{
    public Color color;
    public Color emissionColor;
    public float emissionStrength;
    [Range(0, 1)] public float smoothness;
    [Range(0, 1)] public float specularProbability;
    public Color specularColor;
    
    public void SetDefaultValues()
    {
        color = Color.white;
        emissionColor = Color.white;
        emissionStrength = 0f;
        smoothness = 0.5f;
        specularProbability = 0.5f;
        specularColor = Color.white;
    }
}
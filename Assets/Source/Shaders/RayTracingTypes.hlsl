#ifndef RAY_TRACING_TYPES_INCLUDED
#define RAY_TRACING_TYPES_INCLUDED

struct Ray
{
    float3 origin;
    float3 dir;
};

struct RayTracingMaterial
{
    float4 color;
    float4 emissionColor;
    float emissionStrength;
    float smoothness;
    float specularProbability;
    float4 specularColor;
};

struct Triangle
{
    float3 posA, posB, posC;
    float3 normalA, normalB, normalC;
};

struct HitInfo
{
    bool didHit;
    float distance;
    float3 hitPoint;
    float3 normal;
    RayTracingMaterial material;
};

struct MeshInfo
{
    uint firstTriangleIndex;
    uint numTriangles;
    uint firstNodeIndex;
    float3 boundsMin;
    float3 boundsMax;
    RayTracingMaterial material;
    
    float4x4 localToWorld;
    float4x4 worldToLocal;
};

struct BVHNode
{
    float3 boundsMin;
    float3 boundsMax;
    uint startIndex;
    uint triangleCount;
};

#endif
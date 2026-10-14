#ifndef RAY_TRACING_GEOMETRY_INCLUDED
#define RAY_TRACING_GEOMETRY_INCLUDED

#include "RayTracingTypes.hlsl"

StructuredBuffer<Triangle> Triangles;
StructuredBuffer<MeshInfo> AllMeshInfo;
StructuredBuffer<BVHNode> BVHNodes;
int NumMeshes;

// Möller–Trumbore algorithm
HitInfo RayTriangle(Ray ray, Triangle tri, float maxDistance)
{
    HitInfo hitInfo = (HitInfo)0;
    
    float3 edgeAB = tri.posB - tri.posA;
    float3 edgeAC = tri.posC - tri.posA;
    float3 edgeCross = cross(edgeAB, edgeAC);
    float determinant = -dot(ray.dir, edgeCross);
    if (determinant < 1E-6) return hitInfo;
    
    float3 originOffset = ray.origin - tri.posA;
    float invDeterminant = 1 / determinant;
    float distance = dot(originOffset, edgeCross) * invDeterminant;
    if (distance < 0) return hitInfo;
    if (distance >= maxDistance) return hitInfo;
    
    float3 originOffsetCrossDirection = cross(originOffset, ray.dir);
    float u = dot(edgeAC, originOffsetCrossDirection) * invDeterminant;
    if (u < 0) return hitInfo;
    
    float v = -dot(edgeAB, originOffsetCrossDirection) * invDeterminant;
    if (v < 0) return hitInfo;
    
    float w = 1 - u - v;
    if (w < 0) return hitInfo;
    
    hitInfo.didHit = true;
    hitInfo.hitPoint = ray.origin + ray.dir * distance;
    hitInfo.normal = normalize(tri.normalA * w + tri.normalB * u + tri.normalC * v);
    hitInfo.distance = distance;
    return hitInfo;
}

bool RayBoundingBox(Ray ray, float3 boxMin, float3 boxMax, float3 inverseRayDirection, float maxDistance)
{
    float3 tMin = (boxMin - ray.origin) * inverseRayDirection;
    float3 tMax = (boxMax - ray.origin) * inverseRayDirection;
    float3 t1 = min(tMin, tMax);
    float3 t2 = max(tMin, tMax);
    float tNear = max(max(t1.x, t1.y), t1.z);
    float tFar = min(min(t2.x, t2.y), t2.z);
    
    // check for tFar >= 0 so ray doesn't consider objects behind camera
    return tNear < maxDistance && tNear <= tFar && tFar >= 0; 
};

HitInfo CalculateRayCollisionFullTraversal(Ray ray)
{
    HitInfo closestHit = (HitInfo)0;
    closestHit.distance = 1.#INF; // if ray didn't hit, the distance is inf

    // calculate once before loop, because each individual ray is the same for all meshes 
    // (but ray bounces will require recalculation)
    float3 inverseRayDirection = 1.0 / ray.dir; 
    
    for (int meshIndex = 0; meshIndex < NumMeshes; meshIndex++)
    {
        MeshInfo meshInfo = AllMeshInfo[meshIndex];
        if (!RayBoundingBox(ray, meshInfo.boundsMin, meshInfo.boundsMax, inverseRayDirection, closestHit.distance))
            continue;

        for (int i = 0; i < meshInfo.numTriangles; i++)
        {
            int triIndex = meshInfo.firstTriangleIndex + i;
            Triangle tri = Triangles[triIndex];
            HitInfo hitInfo = RayTriangle(ray, tri, closestHit.distance);

            if (hitInfo.didHit && hitInfo.distance < closestHit.distance)
            {
                closestHit = hitInfo;
                closestHit.material = meshInfo.material;
            }
        }
    }
    return closestHit;
}

HitInfo CalculateRayCollision(Ray ray)
{
    HitInfo closestHit = (HitInfo)0;
    closestHit.distance = 1.#INF;

    float3 inverseRayDirection = 1.0 / ray.dir;

    for (int meshIndex = 0; meshIndex < NumMeshes; meshIndex++)
    {
        MeshInfo meshInfo = AllMeshInfo[meshIndex];
        if (meshInfo.numTriangles == 0) continue;

        // shortcut: supports maxDepth <= 31;
        // increase stack capacity if the depth limit grows.
        uint stack[32];
        uint stackCount = 1;
        stack[0] = meshInfo.firstNodeIndex;

        [loop]
        while (stackCount > 0)
        {
            uint nodeIndex = stack[--stackCount];
            BVHNode node = BVHNodes[nodeIndex];

            if (!RayBoundingBox(ray,node.boundsMin,node.boundsMax,inverseRayDirection,closestHit.distance))
                continue;
            

            if (node.triangleCount == 0)
            {
                uint firstChildIndex = meshInfo.firstNodeIndex + node.startIndex;

                stack[stackCount++] = firstChildIndex + 1;
                stack[stackCount++] = firstChildIndex;
                continue;
            }

            uint firstTriangleIndex = meshInfo.firstTriangleIndex + node.startIndex;

            [loop]
            for (uint i = 0; i < node.triangleCount; i++)
            {
                Triangle tri = Triangles[firstTriangleIndex + i];
                HitInfo hitInfo = RayTriangle(ray, tri, closestHit.distance);

                if (hitInfo.didHit && hitInfo.distance < closestHit.distance)
                {
                    closestHit = hitInfo;
                    closestHit.material = meshInfo.material;
                }
            }
        }
    }

    return closestHit;
}

#endif

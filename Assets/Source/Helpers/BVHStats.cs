using UnityEngine;

#if UNITY_EDITOR
public struct BVHStats 
{
    public double timeMs;
    public int triangleCount;
    public int nodeCount;
    public int leafCount;

    public static BVHStats Collect(BVH bvh, double time)
    {
        var stats = new BVHStats
        {
            timeMs = time,
            nodeCount = bvh.Tree.Count
        };
        
        for (int i = 0; i < bvh.Tree.Count; i++)
        {
            var bvhNode = bvh.Tree[i];
            var bvhNodeTriangleCount = bvhNode.triangleCount;
            if (bvhNodeTriangleCount <= 0) continue;

            stats.leafCount++;
            stats.triangleCount += bvhNodeTriangleCount;
        }

        return stats;
    }
}
#endif

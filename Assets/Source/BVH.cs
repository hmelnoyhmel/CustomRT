using System.Collections.Generic;
using UnityEngine;

public class BVH
{
    public struct Node
    {
        public Vector3 boundsMin, boundsMax;
        public int startIndex;
        public int triangleCount;
    }

    private List<Node> tree = new List<Node>();
    public IReadOnlyList<Node> Tree => tree;
    
    public BVH(RayTracedMesh.Triangle[] triangles, int maxDepth = 12)
    {
        if (triangles == null || triangles.Length == 0) return;
        
        tree.Add(CreateLeaf(triangles, 0, triangles.Length));
        SplitNode(triangles, 0, 0, maxDepth);
    }

    private static Node CreateLeaf(RayTracedMesh.Triangle[] triangles, int startIndex, int triangleCount)
    {
        var leaf = new Node()
        {
            startIndex = startIndex,
            triangleCount = triangleCount,
            boundsMin = triangles[startIndex].posA,
            boundsMax = triangles[startIndex].posA
        };
        
        var endIndex = startIndex + triangleCount;

        for (int i = startIndex; i < endIndex; i++)
        {
            leaf.boundsMin = Vector3.Min(leaf.boundsMin, GetTriangleMin(triangles[i]));
            leaf.boundsMax = Vector3.Max(leaf.boundsMax, GetTriangleMax(triangles[i]));
        }

        return leaf;
    }
    
    // request description from codex
    private void SplitNode(RayTracedMesh.Triangle[] triangles, int nodeIndex, int depth, int maxDepth)
    {
        var node = tree[nodeIndex];
        if (node.triangleCount <= 1) return;
        if (depth >= maxDepth) return;
        
        var size = node.boundsMax - node.boundsMin;

        int axis = 0;
        if (size.y > size[axis]) axis = 1;
        if (size.z > size[axis]) axis = 2;

        float splitPosition = (node.boundsMin[axis] + node.boundsMax[axis]) * 0.5f;

        int leftCount = 0;
        int endIndex = node.startIndex + node.triangleCount;

        for (int i = node.startIndex; i < endIndex; i++)
        {
            var center = GetTriangleCenter(triangles[i]);

            if (center[axis] >= splitPosition) continue;

            int swapIndex = node.startIndex + leftCount;
            (triangles[swapIndex], triangles[i]) = (triangles[i], triangles[swapIndex]);
            leftCount++;
        }

        int rightCount = node.triangleCount - leftCount;

        if (leftCount == 0 || rightCount == 0) return;

        int firstChildIndex = tree.Count;

        tree.Add(CreateLeaf(triangles, node.startIndex, leftCount));
        tree.Add(CreateLeaf(triangles, node.startIndex + leftCount, rightCount));

        node.startIndex = firstChildIndex;
        node.triangleCount = 0;

        tree[nodeIndex] = node;
        SplitNode(triangles, firstChildIndex, depth + 1, maxDepth);
        SplitNode(triangles, firstChildIndex + 1, depth + 1, maxDepth);
    }

    private static Vector3 GetTriangleMin(RayTracedMesh.Triangle triangle)
    {
        var min = Vector3.Min(triangle.posA, triangle.posB);
        min = Vector3.Min(min, triangle.posC);
        return min;
    }
    
    private static Vector3 GetTriangleMax(RayTracedMesh.Triangle triangle)
    {
        var max = Vector3.Max(triangle.posA, triangle.posB);
        max = Vector3.Max(max, triangle.posC);
        return max;
    }
    
    private static Vector3 GetTriangleCenter(RayTracedMesh.Triangle triangle)
    {
        return (triangle.posA + triangle.posB + triangle.posC) / 3f;
    }
}

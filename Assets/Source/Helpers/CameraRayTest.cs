using System;
using UnityEngine;
using static UnityEngine.Mathf;

public class CameraTest : MonoBehaviour
{
    [SerializeField] private Vector2 debugPointCount = new Vector2(16f, 9f);
    [SerializeField] private float debugPointRadius = 0.01f;
    
    private void OnDrawGizmos()
    {
        Camera cam = Camera.main;
        Transform camT = cam.transform;
        
        float planeHeight = cam.nearClipPlane * Tan(cam.fieldOfView * 0.5f * Deg2Rad) * 2;
        float planeWidth = planeHeight * cam.aspect;

        Vector3 bottomLeftLocal = new Vector3(-planeWidth / 2, -planeHeight / 2, cam.nearClipPlane);

        for (int x = 0; x < debugPointCount.x; x++)
        {
            for (int y = 0; y < debugPointCount.y; y++)
            {
                float tx = x / (debugPointCount.x - 1f); // 0 = left edge, 1 = right edge
                float ty = y / (debugPointCount.y - 1f); // 0 = bottom edge, 1 = top edge

                // calculate point position in local space
                Vector3 pointLocal = bottomLeftLocal + new Vector3(planeWidth * tx, planeHeight * ty);
                
                // place the point on the plane
                Vector3 point = camT.position + 
                                camT.right * pointLocal.x + 
                                camT.up * pointLocal.y +
                                camT.forward * pointLocal.z;

                Vector3 dir = (point - camT.position); //.normalized;
                
                Gizmos.DrawSphere(point, debugPointRadius);
                Gizmos.color = Color.darkGreen;
                Gizmos.DrawRay(camT.position, dir);
                Gizmos.color = Color.white;
            }
        }
    }
}

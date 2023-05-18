using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoundsViewer : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        var bound = GetComponent<MeshFilter>().mesh.bounds;

        Gizmos.DrawWireCube(bound.center + transform.position, bound.extents * 2);
    }
}

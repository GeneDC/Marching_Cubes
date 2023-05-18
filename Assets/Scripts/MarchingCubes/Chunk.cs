using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public MeshData meshData;
    public ChunkData chunkData;

    private Mesh mesh = null;
    private MeshFilter meshFilter = null;

    private MeshCollider meshCollider = null;

    private int size = 17;
    public int Size
    {
        set { size = value; chunkData = new ChunkData(size); }
        get { return size; }
    }

    Chunk(int size)
    {
        meshData = new MeshData();
    }


    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        mesh = new Mesh();
    }

    public void ApplyMeshData()
    {
        UnityEngine.Rendering.IndexFormat meshIndexFormat = UnityEngine.Rendering.IndexFormat.UInt16;

        // UInt16 format can only support meshes with ~65,535 verts
        if (meshData.verts.Length > 60000)
        {
            meshIndexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh = new()
        {
            indexFormat = meshIndexFormat,

            vertices = meshData.verts,
            triangles = meshData.tris,
            normals = meshData.normals,
            colors32 = meshData.colours,

            name = chunkData.pos.ToString(),
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = meshFilter.sharedMesh;
    }

    public void ClearMeshFilter()
    {
        meshFilter.mesh = null;
    }
}

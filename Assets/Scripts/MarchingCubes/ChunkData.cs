using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ChunkData
{
    public Vector3Int pos;

    public int size;

    public Vector4[] data;

    public ChunkData(int a_size)
    {
        pos = Vector3Int.zero;
        size = a_size;
        data = new Vector4[a_size * a_size * a_size];
    }

}

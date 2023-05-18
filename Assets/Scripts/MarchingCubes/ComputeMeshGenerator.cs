using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

using System.Linq;
using System.Runtime.ExceptionServices;
using UnityEngine.Profiling;

public class ComputeMeshGenerator
{
    public const int THREAD_GROUP_SIZE = 8;

    static private ComputeBuffer pointsBuffer;

    struct Triangle
    {
#pragma warning disable 649 // unassigned variable warning
        public Vector3 pointA;
        public Vector3 pointB;
        public Vector3 pointC;
        public Vector3Int colour;
    }
    static readonly int vector3Size = sizeof(float) * 3;
    static readonly int vector3IntSize = sizeof(int) * 3;
    static readonly int triangleSize = vector3Size * 3 + vector3IntSize * 1;

    static private ComputeBuffer trianglesBuffer;
    static private ComputeBuffer countBuffer;

    static public MeshData GenerateMeshDataForChunkData(ChunkData chunkData, ComputeShader computeShader)
    {
        Profiler.BeginSample("GenerateMeshDataForChunkData");

        MeshData meshData = new();

        int numPointsPerAxis = chunkData.size;

        int marchHandle = computeShader.FindKernel("March");

        int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
        int numVoxelsPerAxis = numPointsPerAxis - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        int maxTriangleCount = numVoxels * 5;

        int chunkDensity = TerrainGenerator.CHUNK_DENSITY;

        {
            pointsBuffer = new(numPoints, sizeof(float));

            float[] points = chunkData.data.Select(points => points.w).ToArray();
            Profiler.BeginSample("PointsBufferSetData");
            pointsBuffer.SetData(points);
            Profiler.EndSample();
            computeShader.SetBuffer(marchHandle, "points", pointsBuffer);
        }

        trianglesBuffer = new(maxTriangleCount, triangleSize, ComputeBufferType.Append);
        countBuffer = new(1, sizeof(int), ComputeBufferType.IndirectArguments);

        computeShader.SetBuffer(marchHandle, "triangles", trianglesBuffer);
        trianglesBuffer.SetCounterValue(0);
        computeShader.SetInt("numPointsPerAxis", numPointsPerAxis);
        computeShader.SetInt("chunkDensity", chunkDensity);

        Vector4 chunkPos = new(chunkData.pos.x, chunkData.pos.y, chunkData.pos.z, 0.0f);
        computeShader.SetVector("chunkPos", chunkPos);

        int numThreadsPerAxis = Mathf.CeilToInt(numVoxelsPerAxis / (float)THREAD_GROUP_SIZE);
        computeShader.Dispatch(marchHandle, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);

        ComputeBuffer.CopyCount(trianglesBuffer, countBuffer, 0);
        int[] countArray = { 0 };

        Profiler.BeginSample("CountBufferGetData");
        countBuffer.GetData(countArray);
        Profiler.EndSample();

        int numTris = countArray[0];
        Triangle[] triangles = new Triangle[numTris];

        Profiler.BeginSample("TrianglesBufferGetData");
        trianglesBuffer.GetData(triangles, 0, 0, numTris);
        Profiler.EndSample();

        trianglesBuffer?.Release();
        countBuffer?.Release();
        pointsBuffer?.Release();

        int vertCount = numTris * 3;
        Vector3[] verts = new Vector3[vertCount];
        Color32[] colours = new Color32[vertCount];
        int[] tris = new int[vertCount];

        for (int i = 0; i < triangles.Length; i++)
        {
            int vertIdx = i * 3;

            tris[vertIdx] = vertIdx;
            tris[vertIdx + 1] = vertIdx + 1;
            tris[vertIdx + 2] = vertIdx + 2;

            var tri = triangles[i];
            verts[vertIdx] = tri.pointA;
            verts[vertIdx + 1] = tri.pointB;
            verts[vertIdx + 2] = tri.pointC;

            var triColour = tri.colour;
            Color32 colour = new((byte)triColour.x, (byte)triColour.y, (byte)triColour.z, 255);
            colours[vertIdx] = colour;
            colours[vertIdx + 1] = colour;
            colours[vertIdx + 2] = colour;
        }

        meshData.verts = verts;
        //meshData.normals = normals;
        meshData.tris = tris;
        meshData.colours = colours;

        Profiler.EndSample();
        return meshData;
    }
}

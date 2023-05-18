using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Runtime.CompilerServices;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;

public class TerrainGenerator : MonoBehaviour
{
    public static readonly int THREAD_GROUP_SIZE = 8;
    public static readonly int CHUNK_WIDTH = 16; // Physical width in meters
    public static readonly int CHUNK_DENSITY = 4; // How many points per meter
    public static readonly int CHUNK_POINTS = CHUNK_WIDTH * CHUNK_DENSITY + 1; // Actual points in each axis
    public static readonly int CHUNK_TOTAL_POINTS = CHUNK_POINTS * CHUNK_POINTS * CHUNK_POINTS;

    [Header("Voxel Settings")]
    [SerializeField, Range(-1f, 1f)]
    private float isoLevel;
    public float boundsSize = 1;
    public Vector3 offset = Vector3.zero;

    public ComputeShader computeShader;

    [SerializeField]
    private float min = 100f;
    [SerializeField]
    private float max = -100f;

    [SerializeField]
    private float rainbowLength = 32f;

    [SerializeField, Range(0f, 1f)]
    private float saturation = 1f;

    [SerializeField, Range(0f, 1f)]
    private float value = 1f;

    [SerializeField]
    private float noiseScale = 0.9f;

    private NoiseTest.OpenSimplexNoise noise = null;

    private Queue<Chunk> createMeshQueue = null;

    private Queue<Chunk> setMeshQueue = null;


    ComputeBuffer triangleBuffer;
    ComputeBuffer pointsBuffer;
    ComputeBuffer triCountBuffer;

    struct Triangle
    {
#pragma warning disable 649 // disable unassigned variable warning
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;

        public readonly Vector3 this[int i]
        {
            get
            {
                return i switch
                {
                    0 => a,
                    1 => b,
                    _ => c,
                };
            }
        }
    }

    readonly struct EdgeVertTable
    {
        private readonly int size;
        private readonly int[] x;
        private readonly int[] y;
        private readonly int[] z;

        public EdgeVertTable(int a_size)
        {
            size = a_size;
            x = new int[(size - 1) * size * size];
            y = new int[size * size * (size - 1)];
            z = new int[(size - 1) * size * size];

            int i = 0;
            for (; i < x.Length; i++)
                x[i] = y[i] = z[i] = -1;
            for (; i < y.Length; i++)
                y[i] = -1;
        }

        public readonly int Get(int a_x, int a_y, int a_z, int index)
        {
            int i = -1;

            if (index < 8)
            {
                if (index > 4)
                {
                    index %= 4;
                    ++a_y;
                }

                if (index == 0) // x array
                {
                    i = x[a_x + a_z * (size - 1) + a_y * (size - 1) * size];
                }
                else if (index == 2) // x array
                {
                    ++a_z;
                    i = x[a_x + a_z * (size - 1) + a_y * (size - 1) * size];
                }
                else if (index == 1) // z array
                {
                    ++a_x;
                    i = z[a_x + a_z * size + a_y * size * (size - 1)];
                }
                else if (index == 3) // z array
                {
                    i = z[a_x + a_z * size + a_y * size * (size - 1)];
                }
            }
            else
            {
                // y array
                if (index == 8)
                {
                    i = y[a_x + a_z * size + a_y * size * size];
                }
                else if (index == 9)
                {
                    ++a_x;
                    i = y[a_x + a_z * size + a_y * size * size];
                }
                else if (index == 10)
                {
                    ++a_x;
                    ++a_z;
                    i = y[a_x + a_z * size + a_y * size * size];
                }
                else if (index == 11)
                {
                    ++a_z;
                    i = y[a_x + a_z * size + a_y * size * size];
                }
            }

            return i;
        }

        public readonly void Set(int a_x, int a_y, int a_z, int index, int value)
        {
            if (index < 8)
            {
                if (index > 4)
                {
                    index %= 4;
                    ++a_y;
                }

                if (index == 0) // x array
                {
                    x[a_x + a_z * (size - 1) + a_y * (size - 1) * size] = value;
                }
                else if (index == 2) // x array
                {
                    ++a_z;
                    x[a_x + a_z * (size - 1) + a_y * (size - 1) * size] = value;
                }
                else if (index == 1) // z array
                {
                    ++a_x;
                    z[a_x + a_z * size + a_y * size * (size - 1)] = value;
                }
                else if (index == 3) // z array
                {
                    z[a_x + a_z * size + a_y * size * (size - 1)] = value;
                }
            }
            else
            {
                // y array
                if (index == 8)
                {
                    y[a_x + a_z * size + a_y * size * size] = value;
                }
                else if (index == 9)
                {
                    ++a_x;
                    y[a_x + a_z * size + a_y * size * size] = value;
                }
                else if (index == 10)
                {
                    ++a_x;
                    ++a_z;
                    y[a_x + a_z * size + a_y * size * size] = value;
                }
                else if (index == 11)
                {
                    ++a_z;
                    y[a_x + a_z * size + a_y * size * size] = value;
                }
            }
        }
    }

#if UNITY_EDITOR

    private void OnDrawGizmos()
    {
        Vector3[] edgePoints = {new Vector3(0.5f, 0, 0), new Vector3(1, 0, 0.5f), new Vector3(0.5f, 0, 1), new Vector3(0, 0, 0.5f),
                                new Vector3(0.5f, 1, 0), new Vector3(1, 1, 0.5f), new Vector3(0.5f, 1, 1), new Vector3(0, 1, 0.5f),
                                new Vector3(0, 0.5f, 0), new Vector3(1, 0.5f, 0), new Vector3(1, 0.5f, 1), new Vector3(0, 0.5f, 1)};

        Handles.color = Color.red;
        for (int i = 0; i < 12; i++)
        {
            Handles.Label(edgePoints[i], i.ToString());
        }

        Gizmos.color = Color.green;

        Gizmos.DrawLine(new Vector3(0, 0, 0), new Vector3(1, 0, 0));
        Gizmos.DrawLine(new Vector3(1, 0, 0), new Vector3(1, 0, 1));
        Gizmos.DrawLine(new Vector3(1, 0, 1), new Vector3(0, 0, 1));
        Gizmos.DrawLine(new Vector3(0, 0, 1), new Vector3(0, 0, 0));

        Gizmos.DrawLine(new Vector3(0, 0, 0), new Vector3(0, 1, 0));
        Gizmos.DrawLine(new Vector3(1, 0, 0), new Vector3(1, 1, 0));
        Gizmos.DrawLine(new Vector3(1, 0, 1), new Vector3(1, 1, 1));
        Gizmos.DrawLine(new Vector3(0, 0, 1), new Vector3(0, 1, 1));

        Gizmos.DrawLine(new Vector3(0, 1, 0), new Vector3(1, 1, 0));
        Gizmos.DrawLine(new Vector3(1, 1, 0), new Vector3(1, 1, 1));
        Gizmos.DrawLine(new Vector3(1, 1, 1), new Vector3(0, 1, 1));
        Gizmos.DrawLine(new Vector3(0, 1, 1), new Vector3(0, 1, 0));
    }

    private void OnDrawGizmosSelected()
    {
        //if (mesh == null)
        //    return;


        for (int x = 0; x < CHUNK_POINTS; x++)
        {
            for (int y = 0; y < CHUNK_POINTS; y++)
            {
                for (int z = 0; z < CHUNK_POINTS; z++)
                {
                    Gizmos.color = Color.cyan;

                    //Gizmos.DrawLine(new Vector3(x, y, z), new Vector3(x + 1, y, z));
                    ////Gizmos.DrawLine(new Vector3(x + 1, y, z), new Vector3(x + 1, y, z + 1));
                    ////Gizmos.DrawLine(new Vector3(x + 1, y, z + 1), new Vector3(x, y, z + 1));
                    //Gizmos.DrawLine(new Vector3(x, y, z + 1), new Vector3(x, y, z));

                    //Gizmos.DrawLine(new Vector3(x, y, z), new Vector3(x, y + 1, z));
                    //Gizmos.DrawLine(new Vector3(x + 1, y, z), new Vector3(x + 1, y + 1, z));
                    //Gizmos.DrawLine(new Vector3(x + 1, y, z + 1), new Vector3(x + 1, y + 1, z + 1));
                    //Gizmos.DrawLine(new Vector3(x, y, z + 1), new Vector3(x, y + 1, z + 1));

                    //Gizmos.color = chunk[PosToIndex(x, y, z)] > 0 ? Color.cyan : Color.yellow;
                    //Gizmos.DrawSphere(new Vector3(x, y, z), (chunk[PosToIndex(x, y, z)] / 255f) * 0.5f);
                }
            }
        }
    }

#endif

    private void Awake()
    {
        noise = new NoiseTest.OpenSimplexNoise(123);

        createMeshQueue = new Queue<Chunk>();
        setMeshQueue = new Queue<Chunk>();
    }

    private void Start()
    {
        CreateBuffers();
        //thread.Start();
    }

    private void Update()
    {
        CreateMeshesThread();

        while (setMeshQueue.Count > 0)
        {
            if (setMeshQueue.TryDequeue(out Chunk chunk))
            {
                chunk.ApplyMeshData();
            }
        }
    }

    private void OnDestroy()
    {
        //thread.Abort();

        ReleaseBuffers();
    }

    public void QueueChunk(Chunk chunk)
    {
        CreateMesh(chunk);
    }

    private void GenerateChunk(Chunk chunk)
    {
        Profiler.BeginSample("GenerateChunk");
        // need to compute-ify this function for speeeeeed
        Vector3Int chunkWorldPos = chunk.chunkData.pos * CHUNK_WIDTH;

        for (int x = 0; x < CHUNK_POINTS; x++)
        {
            for (int y = 0; y < CHUNK_POINTS; y++)
            {
                for (int z = 0; z < CHUNK_POINTS; z++)
                {
                    float surfaceLevel = 0f;

                    Vector3 pointPos = new Vector3(x , y, z) / CHUNK_DENSITY;
                    Vector3 pointWorldPos = pointPos + chunkWorldPos;
                    if (pointWorldPos.y < 0f)
                    {
                        Vector3 scaledPointWorldPos = pointWorldPos * noiseScale;
                        surfaceLevel = (float)noise.Evaluate(scaledPointWorldPos.x, scaledPointWorldPos.y, scaledPointWorldPos.z);

                        surfaceLevel = MathUtils.Remap(surfaceLevel, -1f, 1f, -10f, 10f);

                        if (surfaceLevel < 0f)
                        {
                            surfaceLevel = 0f;
                        }
                        else if (surfaceLevel > 1f)
                        {
                            surfaceLevel = 1f;
                        }

                        min = Mathf.Min(min, surfaceLevel);
                        max = Mathf.Max(max, surfaceLevel);
                    }

                    int index = PosToIndex(x, y, z);
                    chunk.chunkData.data[index] = new(pointPos.x, pointPos.y, pointPos.z, surfaceLevel);
                }
            }
        }

        Profiler.EndSample();
    }

    private void CreateMesh(Chunk chunk)
    {
        createMeshQueue.Enqueue(chunk);
    }

    private void CreateMeshesThread()
    {
        Profiler.BeginSample("CreateMeshesThread");


        Stopwatch stopwatch = new();
        stopwatch.Start();

        int milliseconds = 5;
        int count = 20;
        while (count > 0 
            && stopwatch.ElapsedMilliseconds < milliseconds
            && createMeshQueue.TryDequeue(out Chunk chunk))
        {
            --count;
            GenerateChunk(chunk);

            //UpdateChunkMesh(chunk);

            //Profiler.BeginSample("GenerateMeshDataForChunkData");
            //var meshData = MeshGenerator.GenerateMeshDataForChunkData(chunk.chunkData, isoLevel, rainbowLength, saturation, value);
            //Profiler.EndSample();
            var meshData = ComputeMeshGenerator.GenerateMeshDataForChunkData(chunk.chunkData, computeShader);
            chunk.meshData = meshData;

            chunk.ApplyMeshData();
            //setMeshQueue.Enqueue(chunk);
        }

        Profiler.EndSample();
    }

    private void UpdateChunkMesh(Chunk chunk)
    {
        int numVoxelsPerAxis = CHUNK_POINTS - 1;
        int numThreadsPerAxis = Mathf.CeilToInt(numVoxelsPerAxis / (float)THREAD_GROUP_SIZE);
        //float pointSpacing = boundsSize / (CHUNK_WIDTH - 1);

        //Vector3Int coord = chunk.chunkData.pos;
        //Vector3 centre = CentreFromCoord(coord);

        //Vector3 worldBounds = new Vector3(numChunks.x, numChunks.y, numChunks.z) * boundsSize;

        //densityGenerator.Generate(pointsBuffer, CHUNK_WIDTH, boundsSize, worldBounds, centre, offset, pointSpacing);

        if (pointsBuffer == null || triangleBuffer == null || triCountBuffer == null)
        {
            return;
        }

        pointsBuffer.SetData(chunk.chunkData.data);

        triangleBuffer.SetCounterValue(0);
        computeShader.SetBuffer(0, "points", pointsBuffer);
        computeShader.SetBuffer(0, "triangles", triangleBuffer);
        computeShader.SetInt("chunkWidth", CHUNK_POINTS);
        computeShader.SetInt("numThreadsPerAxis", numThreadsPerAxis);
        computeShader.SetFloat("isoLevel", isoLevel);

        computeShader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);

        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int numTris = triCountArray[0];

        // Get triangle data from shader
        Triangle[] tris = new Triangle[numTris];
        triangleBuffer.GetData(tris, 0, 0, numTris);

        var vertices = new Vector3[numTris * 3];
        var meshTriangles = new int[numTris * 3];

        for (int i = 0; i < numTris; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                meshTriangles[i * 3 + j] = i * 3 + j;
                vertices[i * 3 + j] = tris[i][j];
            }
        }

        chunk.meshData.verts = vertices;
        chunk.meshData.tris = meshTriangles;

        chunk.ApplyMeshData();
    }

    void CreateBuffers()
    {
        int numPoints = CHUNK_TOTAL_POINTS;
        int numVoxelsPerAxis = CHUNK_POINTS - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        int maxTriangleCount = numVoxels * 5;

        // only create if null or if size has changed
        if (pointsBuffer == null || numPoints != pointsBuffer.count)
        {
            ReleaseBuffers();

            triangleBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append);
            pointsBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
            triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);
        }
    }

    void ReleaseBuffers()
    {
        triangleBuffer?.Release();
        pointsBuffer?.Release();
        triCountBuffer?.Release();
    }

    public static float Perlin3D(float x, float y, float z)
    {
        //float ab = Mathf.PerlinNoise(x, y);
        //float bc = Mathf.PerlinNoise(y, z);
        //float ac = Mathf.PerlinNoise(x, z);

        //float ba = Mathf.PerlinNoise(y, x);
        //float cb = Mathf.PerlinNoise(z, y);
        //float ca = Mathf.PerlinNoise(z, x);

        //float abc = ab + bc + ac + ba + cb + ca;
        //return abc;

        return Mathf.PerlinNoise(x, y) + Mathf.PerlinNoise(y, z) + Mathf.PerlinNoise(x, z) + Mathf.PerlinNoise(y, x) + Mathf.PerlinNoise(z, y) + Mathf.PerlinNoise(z, x);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PosToIndex(int x, int y, int z)
    {
        return x + (y + z * CHUNK_POINTS) * CHUNK_POINTS;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PosToIndex(Vector3 pos)
    {
        return (int)pos.x + ((int)pos.y + (int)pos.z * CHUNK_POINTS) * CHUNK_POINTS;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3Int IndexToPos(int index)
    {
        return new Vector3Int(index % CHUNK_POINTS, index / CHUNK_POINTS % CHUNK_POINTS, index / (CHUNK_POINTS * CHUNK_POINTS));
    }

}


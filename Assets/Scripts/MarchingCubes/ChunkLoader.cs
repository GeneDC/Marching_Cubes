using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ChunkLoader : MonoBehaviour
{
    [SerializeField]
    private Material mat = null;

    [SerializeField]
    private TerrainGenerator terrainGenerator = null;

    [SerializeField]
    private int radius = 3;

    private Queue<Chunk> chunkPool = null;

    private Queue<Vector3Int> chunksToLoad = null;

    //private List<Chunk> loadedChunks = null;

    private Dictionary<Vector3Int, Chunk> loadedChunks = null;

    private Vector3Int centre = Vector3Int.zero;

    private int currentLoadRadius = 0;

    private Vector3 cameraPos = Vector3.zero;

    private void Start()
    {
        chunkPool = new Queue<Chunk>();
        loadedChunks = new Dictionary<Vector3Int, Chunk>();
        chunksToLoad = new Queue<Vector3Int>();

    }

    private void Update()
    {
        cameraPos = Camera.main.transform.position;
        var cameraChunkPos = new Vector3Int(
            (int)cameraPos.x / TerrainGenerator.CHUNK_WIDTH,
            (int)cameraPos.y / TerrainGenerator.CHUNK_WIDTH,
            (int)cameraPos.z / TerrainGenerator.CHUNK_WIDTH);
        UpdateLoadedChunks(cameraChunkPos);

        // Check if we should be looking for more chunks to load
        if (chunksToLoad.Count == 0 && currentLoadRadius <= radius)
        {
            List<Vector3Int> positions;
            do
            {
                currentLoadRadius++;
                positions = GetPositions(centre, currentLoadRadius);

                // Remove the already loaded chunks
                for (int i = 0; i < positions.Count;)
                {
                    if (loadedChunks.ContainsKey(positions[i]))
                    {
                        positions.RemoveAt(i);
                    }
                    else
                    {
                        ++i;
                    }
                }
            } while (positions.Count == 0 && currentLoadRadius < radius - 1);

            positions.Sort(Comparer<Vector3Int>.Create(
                (p1, p2) => (p1 * TerrainGenerator.CHUNK_WIDTH - cameraPos).sqrMagnitude.CompareTo((p2 * TerrainGenerator.CHUNK_WIDTH - cameraPos).sqrMagnitude)));

            chunksToLoad = new Queue<Vector3Int>(positions);
        }

        while (chunksToLoad.Count > 0)
        {
            if (chunksToLoad.TryDequeue(out Vector3Int chunkPos))
            {
                if (loadedChunks.ContainsKey(chunkPos) == false)
                {
                    Load(chunkPos);
                }
            }
        }
    }

    private void UpdateLoadedChunks(Vector3Int newCentre)
    {
        if (newCentre != centre)
        {
            List<Vector3Int> toUnload = new();

            foreach (var c in loadedChunks)
            {
                if (IsInBox(c.Key, newCentre, radius + 1) == false)
                {
                    toUnload.Add(c.Key);
                }
            }

            foreach (Vector3Int pos in toUnload)
            {
                Unload(pos);
            }

            currentLoadRadius = 0;

            chunksToLoad = new Queue<Vector3Int>();

            centre = newCentre;
        }
    }

    private void Unload(Vector3Int a_chunkPos)
    {
        Chunk chunk = loadedChunks[a_chunkPos];
        chunk.ClearMesh();
        chunk.gameObject.SetActive(false);

        chunkPool.Enqueue(chunk);

        loadedChunks[a_chunkPos] = null;
        loadedChunks.Remove(a_chunkPos);
    }

    private void Load(Vector3Int a_chunkPosition)
    {
        Chunk chunk;
        if (chunkPool.Count > 0)
        {
            chunk = chunkPool.Dequeue();
            chunk.gameObject.SetActive(true);
        }
        else
        {
            chunk = CreateChunkGameObject();
        }

        chunk.ClearMesh();

        chunk.chunkData.pos = a_chunkPosition;

        terrainGenerator.QueueChunk(chunk);

        chunk.gameObject.transform.position = a_chunkPosition * TerrainGenerator.CHUNK_WIDTH;
        chunk.gameObject.name = "Chunk" + a_chunkPosition;
        loadedChunks.Add(a_chunkPosition, chunk);
    }

    private Chunk CreateChunkGameObject()
    {
        // Could use a prefab to save some code here
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Chunk";
        go.transform.parent = transform;

        Destroy(go.GetComponent<Collider>()); // Box collider
        MeshCollider meshCollider = go.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = null;

        go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        Chunk chunk = go.AddComponent<Chunk>();
        chunk.Size = TerrainGenerator.CHUNK_POINTS;
        return chunk;
    }

    public static List<Vector3Int> GetPositions(Vector3Int a_pos, int a_radius)
    {
        List<Vector3Int> positions = new(a_radius * a_radius * a_radius);

        Vector3Int currentPos = Vector3Int.zero;
        for (currentPos.x = -a_radius; currentPos.x < a_radius; ++currentPos.x)
            for (currentPos.y = -a_radius; currentPos.y < a_radius; ++currentPos.y)
                for (currentPos.z = -a_radius; currentPos.z < a_radius; ++currentPos.z)
                    if (a_radius > 0 && IsInBox(currentPos, Vector3Int.zero, a_radius - 1) == false)
                        positions.Add(currentPos + a_pos);

        return positions;
    }

    public static bool IsInBox(Vector3Int a_pos, Vector3Int a_centre, int a_halfSize)
    {
        Vector3Int diff = a_pos - a_centre;

        if (diff.x >= a_halfSize || diff.x < -a_halfSize)
        {
            return false;
        }

        if (diff.y >= a_halfSize || diff.y < -a_halfSize)
        {
            return false;
        }

        if (diff.z >= a_halfSize || diff.z < -a_halfSize)
        {
            return false;
        }

        return true;

        //return Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y), Mathf.Abs(diff.z)) <= a_halfSize;
    }

}

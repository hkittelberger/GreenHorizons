using System.Collections.Generic;
using UnityEngine;

public class HexTileGenerator : MonoBehaviour
{
    [System.Serializable]
    public class TileType
    {
        public string name;
        public GameObject prefab;
        public int count;
    }

    public int radius = 4;
    public List<TileType> tileTypes;

    public GameObject waterTile; // Center tile

    public float hexWidth = 3f;      // Width of a flat-top hex
    public float hexHeight = 2.598f; // height = width * sqrt(3)/2

    void Start()
    {
        GenerateHexMap();
    }

    void GenerateHexMap()
    {
        Dictionary<Vector3Int, GameObject> hexMap = new Dictionary<Vector3Int, GameObject>();
        List<Vector3Int> hexPositions = new List<Vector3Int>();

        // Build list of cube coords within radius
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = Mathf.Max(-radius, -x - radius); y <= Mathf.Min(radius, -x + radius); y++)
            {
                int z = -x - y;
                Vector3Int cubeCoord = new Vector3Int(x, y, z);
                hexPositions.Add(cubeCoord);
            }
        }

        // Shuffle tile pool
        List<GameObject> tilePool = BuildShuffledTilePool();

        // Place tiles
        foreach (Vector3Int cube in hexPositions)
        {
            Vector3 pos = CubeToWorld(cube);
            GameObject tile;

            if (cube == Vector3Int.zero)
            {
                tile = Instantiate(waterTile, pos, Quaternion.identity, this.transform);
            }
            else
            {
                tile = Instantiate(tilePool[0], pos, Quaternion.identity, this.transform);
                tilePool.RemoveAt(0);
            }

            tile.name = $"Hex_{cube.x}_{cube.y}_{cube.z}";
        }
    }

    List<GameObject> BuildShuffledTilePool()
    {
        List<GameObject> pool = new List<GameObject>();

        foreach (var type in tileTypes)
        {
            for (int i = 0; i < type.count; i++)
            {
                pool.Add(type.prefab);
            }
        }

        Shuffle(pool);
        return pool;
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }

    Vector3 CubeToWorld(Vector3Int cube)
    {
        float x = hexWidth * 0.75f * cube.x;
        float y = hexHeight * (cube.z + 0.5f * cube.x);
        return new Vector3(x, y, 0f);
    }

}
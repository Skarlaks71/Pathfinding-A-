using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridPF : MonoBehaviour
{
    public bool displayGridGizmos;
    public Transform playerTransform;
    public LayerMask unwalkableMask;
    public Vector2 gridWorldSize;
    public float nodeRadius;
    public TerrainType[] walkableRegions;
    LayerMask walkableMask;
    Dictionary<int, int> walkableMaskDictionary = new Dictionary<int, int>();
    Node[,] grid;

    float nodeDiameter;
    int gridSizeX, gridSizeY;

    private void Awake()
    {
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);

        foreach( TerrainType region in walkableRegions)
        {
            walkableMask.value |= region.terrainLayer.value;
            walkableMaskDictionary.Add((int)Mathf.Log(region.terrainLayer.value, 2), region.terrainPenalty);
        }

        CreateGrid();
    }

    public int MaxSize
    {
        get { return gridSizeX * gridSizeY; }
    }

    void CreateGrid()
    {
        grid = new Node[gridSizeX, gridSizeY];
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x / 2 - Vector3.forward * gridWorldSize.y / 2;

        for( int x = 0; x< gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) + Vector3.forward * (y * nodeDiameter + nodeRadius);
                bool walkable = !(Physics.CheckSphere(worldPoint, nodeRadius, unwalkableMask));

                int movementPenalty = 0;

                if (walkable)
                {
                    Ray ray = new Ray(worldPoint + Vector3.up * 50, Vector3.down);
                    RaycastHit hit;
                    if(Physics.Raycast(ray, out hit, 100, walkableMask))
                    {
                        walkableMaskDictionary.TryGetValue(hit.collider.gameObject.layer, out movementPenalty);

                    }
                }

                grid[x, y] = new Node(walkable, worldPoint, x, y, movementPenalty);
            }
        }
        BlurPenaltyMap(4);
    }

    void BlurPenaltyMap(int blurSize)
    {
        int kernelSize = blurSize * 2 + 1;
        int kernelExtends = (kernelSize - 1) / 2;

        int[,] penaltiesHorizontalPass = new int[gridSizeX, gridSizeY];
        int[,] penaltiesVerticalPass = new int[gridSizeX, gridSizeY];

        for(int y = 0; y<gridSizeY; y++)
        {
            for (int x = -kernelExtends; x<=kernelExtends; x++)
            {
                int sampleX = Mathf.Clamp(x, 0, kernelExtends);
                penaltiesHorizontalPass[0, y] += grid[sampleX, y].movePenalty;
            }

            for(int x = 1; x < gridSizeX; x++)
            {
                int removeIndex = Mathf.Clamp(x - kernelExtends - 1, 0, gridSizeX);
                int addIndex = Mathf.Clamp(x + kernelExtends, 0, gridSizeX - 1);

                penaltiesHorizontalPass[x, y] = penaltiesHorizontalPass[x - 1, y] - grid[removeIndex, y].movePenalty + grid[addIndex, y].movePenalty;
            }

        }

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = -kernelExtends; y <= kernelExtends; y++)
            {
                int sampleY = Mathf.Clamp(y, 0, kernelExtends);
                penaltiesVerticalPass[x, 0] += penaltiesHorizontalPass[x,sampleY];
            }

            for (int y = 1; y < gridSizeY; y++)
            {
                int removeIndex = Mathf.Clamp(y - kernelExtends - 1, 0, gridSizeY);
                int addIndex = Mathf.Clamp(y + kernelExtends, 0, gridSizeY - 1);

                penaltiesVerticalPass[x, y] = penaltiesVerticalPass[x, y -1] - penaltiesHorizontalPass[x,removeIndex] + penaltiesHorizontalPass[x,addIndex];
                int blurredPenalty = Mathf.RoundToInt((float)penaltiesVerticalPass[x, y] / (kernelSize * kernelSize));
                grid[x, y].movePenalty = blurredPenalty;
            }

        }
    }

    public List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();

        for(int x = -1; x<= 1; x++)
        {
            for(int y = -1; y<=1; y++)
            {
                if(x == 0 && y == 0)
                {
                    continue;
                }

                int checkX = node.gridX + x;
                int checkY = node.gridY + y;

                if(checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY)
                {
                    neighbours.Add(grid[checkX, checkY]);
                }
            }
        }

        return neighbours;
    }

    public Node NodeFromWorldPoint(Vector3 worldPos)
    {
        float percentX = (worldPos.x + gridWorldSize.x / 2) / gridWorldSize.x;
        float percentY = (worldPos.z + gridWorldSize.y / 2) / gridWorldSize.y;
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
        return grid[x, y];
    }

    public List<Node> path;
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, 1, gridWorldSize.y));

        
        if (grid != null && displayGridGizmos)
        {
            Node playerNode = NodeFromWorldPoint(playerTransform.position);
            foreach (Node n in grid)
            {
                Gizmos.color = (n.walkable) ? Color.white : Color.red;
                
                if(n.movePenalty > 0)
                {
                    Gizmos.color = Color.green;
                }
                
                if (playerNode == n)
                {
                    Gizmos.color = Color.cyan;
                }
                Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeDiameter - .1f));

            }
        }
        
    }

    [System.Serializable]
    public class TerrainType
    {
        public LayerMask terrainLayer;
        public int terrainPenalty;
    }
}

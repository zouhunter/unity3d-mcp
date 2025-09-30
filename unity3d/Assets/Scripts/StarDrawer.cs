using UnityEngine;

public class StarDrawer : MonoBehaviour
{
    [Header("五角星设置")]
    public float radius = 1f;           // 外圆半径
    public float innerRadius = 0.4f;   // 内圆半径
    public Color starColor = Color.yellow;  // 五角星颜色
    public Material starMaterial;      // 五角星材质
    public bool drawInEditor = true;   // 是否在编辑器中绘制

    private Mesh starMesh;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;

    void Start()
    {
        CreateStarMesh();
        SetupComponents();
    }

    void CreateStarMesh()
    {
        starMesh = new Mesh();
        starMesh.name = "StarMesh";

        // 计算五角星的顶点
        Vector3[] vertices = CalculateStarVertices();
        int[] triangles = CalculateStarTriangles();
        Vector2[] uvs = CalculateStarUVs();

        starMesh.vertices = vertices;
        starMesh.triangles = triangles;
        starMesh.uv = uvs;
        starMesh.RecalculateNormals();
        starMesh.RecalculateBounds();
    }

    Vector3[] CalculateStarVertices()
    {
        Vector3[] vertices = new Vector3[10];
        float angleStep = 72f * Mathf.Deg2Rad; // 五角星每个角的角度
        float startAngle = -90f * Mathf.Deg2Rad; // 从顶部开始

        for (int i = 0; i < 10; i++)
        {
            float angle = startAngle + i * angleStep;
            float currentRadius = (i % 2 == 0) ? radius : innerRadius;
            vertices[i] = new Vector3(
                Mathf.Cos(angle) * currentRadius,
                Mathf.Sin(angle) * currentRadius,
                0f
            );
        }

        return vertices;
    }

    int[] CalculateStarTriangles()
    {
        // 创建五角星的三角形索引
        int[] triangles = new int[15];
        int triangleIndex = 0;

        for (int i = 0; i < 5; i++)
        {
            int outerVertex = i * 2;
            int innerVertex = i * 2 + 1;
            int nextOuterVertex = ((i + 1) * 2) % 10;
            int nextInnerVertex = ((i + 1) * 2 + 1) % 10;

            // 第一个三角形
            triangles[triangleIndex++] = outerVertex;
            triangles[triangleIndex++] = innerVertex;
            triangles[triangleIndex++] = nextOuterVertex;

            // 第二个三角形
            triangles[triangleIndex++] = innerVertex;
            triangles[triangleIndex++] = nextInnerVertex;
            triangles[triangleIndex++] = nextOuterVertex;
        }

        return triangles;
    }

    Vector2[] CalculateStarUVs()
    {
        Vector2[] uvs = new Vector2[10];
        float angleStep = 72f * Mathf.Deg2Rad;
        float startAngle = -90f * Mathf.Deg2Rad;

        for (int i = 0; i < 10; i++)
        {
            float angle = startAngle + i * angleStep;
            float currentRadius = (i % 2 == 0) ? 1f : 0.4f;
            uvs[i] = new Vector2(
                Mathf.Cos(angle) * currentRadius * 0.5f + 0.5f,
                Mathf.Sin(angle) * currentRadius * 0.5f + 0.5f
            );
        }

        return uvs;
    }

    void SetupComponents()
    {
        // 获取或添加MeshFilter组件
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        // 获取或添加MeshRenderer组件
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // 设置网格
        meshFilter.mesh = starMesh;

        // 设置材质
        if (starMaterial != null)
        {
            meshRenderer.material = starMaterial;
        }
        else
        {
            // 创建默认材质
            Material defaultMaterial = new Material(Shader.Find("Standard"));
            defaultMaterial.color = starColor;
            meshRenderer.material = defaultMaterial;
        }
    }

    // 在编辑器中绘制Gizmos
    void OnDrawGizmos()
    {
        if (!drawInEditor) return;

        Gizmos.color = starColor;
        Gizmos.matrix = transform.localToWorldMatrix;

        // 绘制五角星轮廓
        Vector3[] vertices = CalculateStarVertices();
        for (int i = 0; i < vertices.Length; i++)
        {
            int nextIndex = (i + 1) % vertices.Length;
            Gizmos.DrawLine(vertices[i], vertices[nextIndex]);
        }
    }

    // 公共方法：更新五角星参数
    public void UpdateStar(float newRadius, float newInnerRadius, Color newColor)
    {
        radius = newRadius;
        innerRadius = newInnerRadius;
        starColor = newColor;

        if (starMesh != null)
        {
            CreateStarMesh();
            if (meshFilter != null)
                meshFilter.mesh = starMesh;

            if (meshRenderer != null && meshRenderer.material != null)
                meshRenderer.material.color = starColor;
        }
    }

    // 公共方法：设置材质
    public void SetMaterial(Material material)
    {
        starMaterial = material;
        if (meshRenderer != null)
            meshRenderer.material = material;
    }
}
using UnityEngine;
using UnityEditor;

public class PyramidMeshGenerator : MonoBehaviour
{
    [MenuItem("Tools/Generate Pyramid Mesh")]
    public static void GeneratePyramidMesh()
    {
        // 创建新的mesh
        Mesh pyramidMesh = new Mesh();
        pyramidMesh.name = "PyramidMesh";

        // 定义顶点 - 四个底面顶点 + 一个顶点
        Vector3[] vertices = new Vector3[5]
        {
            new Vector3(-1f, 0f, -1f),  // 底面左后
            new Vector3( 1f, 0f, -1f),  // 底面右后
            new Vector3( 1f, 0f,  1f),  // 底面右前
            new Vector3(-1f, 0f,  1f),  // 底面左前
            new Vector3( 0f, 2f,  0f)   // 顶点
        };

        // 定义三角形索引
        int[] triangles = new int[18]
        {
            // 底面（逆时针，从下方看）
            0, 2, 1,  // 第一个三角形
            0, 3, 2,  // 第二个三角形
            
            // 侧面
            0, 1, 4,  // 后面
            1, 2, 4,  // 右面
            2, 3, 4,  // 前面
            3, 0, 4   // 左面
        };

        // 定义UV坐标
        Vector2[] uvs = new Vector2[5]
        {
            new Vector2(0f, 0f),    // 底面左后
            new Vector2(1f, 0f),    // 底面右后
            new Vector2(1f, 1f),    // 底面右前
            new Vector2(0f, 1f),    // 底面左前
            new Vector2(0.5f, 0.5f) // 顶点
        };

        // 定义法线
        Vector3[] normals = new Vector3[5];
        normals[0] = Vector3.up;
        normals[1] = Vector3.up;
        normals[2] = Vector3.up;
        normals[3] = Vector3.up;
        normals[4] = Vector3.up;

        // 应用到mesh
        pyramidMesh.vertices = vertices;
        pyramidMesh.triangles = triangles;
        pyramidMesh.uv = uvs;
        pyramidMesh.normals = normals;

        // 重新计算边界和法线
        pyramidMesh.RecalculateBounds();
        pyramidMesh.RecalculateNormals();

        // 保存为asset
        string meshPath = "Assets/Meshes/PyramidMesh.asset";
        AssetDatabase.CreateAsset(pyramidMesh, meshPath);
        AssetDatabase.SaveAssets();

        // 查找名为"Pyramid"的GameObject并应用mesh
        GameObject pyramidGO = GameObject.Find("Pyramid");
        if (pyramidGO != null)
        {
            MeshFilter meshFilter = pyramidGO.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.mesh = pyramidMesh;
                Debug.Log("金字塔mesh已应用到Pyramid GameObject");
            }
            else
            {
                Debug.LogWarning("Pyramid GameObject上未找到MeshFilter组件");
            }
        }
        else
        {
            Debug.LogWarning("未找到名为'Pyramid'的GameObject");
        }

        Debug.Log($"金字塔mesh已创建并保存到: {meshPath}");
    }

    // 用于运行时生成的方法
    public void GenerateMesh()
    {
        GeneratePyramidMesh();
    }
}




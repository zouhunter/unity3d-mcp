using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models; // For Response class
using UnityMcp.Tools; // 添加这个引用

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles Unity mesh asset operations including generation, modification, optimization, etc.
    /// 对应方法名: asset_mesh
    /// </summary>
    [ToolName("edit_mesh", "资源管理")]
    public class EditMesh : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：create, modify, optimize, generate_primitive, subdivide, smooth, export, import等", false),
                new MethodKey("path", "网格资产路径，Unity标准格式：Assets/Meshes/MeshName.asset", false),
                new MethodKey("mesh_type", "网格类型：cube, sphere, cylinder, plane, custom等", true),
                new MethodKey("properties", "网格属性字典，包含顶点、面、UV等数据", true),
                new MethodKey("source_path", "源网格路径（修改时使用）", true),
                new MethodKey("destination", "目标路径（导出时使用）", true),
                new MethodKey("subdivision_level", "细分级别（细分时使用）", true),
                new MethodKey("smooth_factor", "平滑因子（平滑时使用）", true),
                new MethodKey("optimization_level", "优化级别：low, medium, high", true),
                new MethodKey("export_format", "导出格式：obj, fbx, stl等", true),
                new MethodKey("force", "是否强制执行操作（覆盖现有文件等）", true)
            };
        }

        /// <summary>
        /// 创建状态树
        /// </summary>
        /// <returns>状态树</returns>
        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("create", CreateMesh)
                    .Leaf("modify", ModifyMesh)
                    .Leaf("optimize", OptimizeMesh)
                    .Leaf("generate_primitive", GeneratePrimitiveMesh)
                    .Leaf("subdivide", SubdivideMesh)
                    .Leaf("smooth", SmoothMesh)
                    .Node("export", "export_format")
                        .Leaf("obj", ExportMeshToOBJ)
                        .Leaf("asset", ExportMeshToAsset)
                    .Up()
                    .Leaf("import", ImportMesh)
                    .Leaf("get_info", GetMeshInfo)
                    .Leaf("duplicate", DuplicateMesh)
                .Build();
        }

        private object CreateMesh(JObject args)
        {
            string path = args["path"]?.ToString();
            JObject properties = args["properties"] as JObject;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for create.");
            if (properties == null || !properties.HasValues)
                return Response.Error("'properties' are required for create.");

            string fullPath = SanitizeAssetPath(path);
            string directory = Path.GetDirectoryName(fullPath);

            // Ensure directory exists
            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), directory)))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), directory));
                AssetDatabase.Refresh();
            }

            if (AssetExists(fullPath))
                return Response.Error($"Mesh already exists at path: {fullPath}");

            try
            {
                Mesh mesh = new Mesh();

                // Apply properties from JObject
                bool meshModified = ApplyMeshProperties(mesh, properties);
                if (meshModified)
                {
                    EditorUtility.SetDirty(mesh);
                }

                AssetDatabase.CreateAsset(mesh, fullPath);
                AssetDatabase.SaveAssets();

                return Response.Success(
                    $"Mesh '{fullPath}' created successfully.",
                    GetMeshData(fullPath)
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to create mesh at '{fullPath}': {e.Message}");
            }
        }

        private object ModifyMesh(JObject args)
        {
            string path = args["path"]?.ToString();
            JObject properties = args["properties"] as JObject;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for modify.");
            if (properties == null || !properties.HasValues)
                return Response.Error("'properties' are required for modify.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Mesh not found at path: {fullPath}");

            try
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(fullPath);
                if (mesh == null)
                    return Response.Error($"Failed to load mesh at path: {fullPath}");

                // Record the asset state for Undo before making any modifications
                Undo.RecordObject(mesh, $"Modify Mesh '{Path.GetFileName(fullPath)}'");

                bool modified = ApplyMeshProperties(mesh, properties);

                if (modified)
                {
                    EditorUtility.SetDirty(mesh);
                    AssetDatabase.SaveAssets();

                    return Response.Success(
                        $"Mesh '{fullPath}' modified successfully.",
                        GetMeshData(fullPath)
                    );
                }
                else
                {
                    return Response.Success(
                        $"No applicable properties found to modify for mesh '{fullPath}'.",
                        GetMeshData(fullPath)
                    );
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AssetMesh] Action 'modify' failed for path '{path}': {e}");
                return Response.Error($"Failed to modify mesh '{fullPath}': {e.Message}");
            }
        }

        private object OptimizeMesh(JObject args)
        {
            string path = args["path"]?.ToString();
            string optimizationLevel = args["optimization_level"]?.ToString() ?? "medium";

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for optimize.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Mesh not found at path: {fullPath}");

            try
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(fullPath);
                if (mesh == null)
                    return Response.Error($"Failed to load mesh at path: {fullPath}");

                Undo.RecordObject(mesh, $"Optimize Mesh '{Path.GetFileName(fullPath)}'");

                // Apply optimization based on level
                switch (optimizationLevel.ToLowerInvariant())
                {
                    case "low":
                        mesh.Optimize();
                        break;
                    case "medium":
                        mesh.Optimize();
                        mesh.RecalculateBounds();
                        mesh.RecalculateNormals();
                        break;
                    case "high":
                        mesh.Optimize();
                        mesh.RecalculateBounds();
                        mesh.RecalculateNormals();
                        mesh.RecalculateTangents();
                        break;
                    default:
                        return Response.Error($"Unknown optimization level: {optimizationLevel}. Use: low, medium, high");
                }

                EditorUtility.SetDirty(mesh);
                AssetDatabase.SaveAssets();

                return Response.Success(
                    $"Mesh '{fullPath}' optimized with level '{optimizationLevel}' successfully.",
                    GetMeshData(fullPath)
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to optimize mesh '{fullPath}': {e.Message}");
            }
        }

        private object GeneratePrimitiveMesh(JObject args)
        {
            string path = args["path"]?.ToString();
            string meshType = args["mesh_type"]?.ToString() ?? "cube";
            JObject properties = args["properties"] as JObject;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for generate_primitive.");

            string fullPath = SanitizeAssetPath(path);
            string directory = Path.GetDirectoryName(fullPath);

            // Ensure directory exists
            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), directory)))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), directory));
                AssetDatabase.Refresh();
            }

            if (AssetExists(fullPath))
                return Response.Error($"Mesh already exists at path: {fullPath}");

            try
            {
                Mesh mesh = new Mesh();

                // Generate primitive based on type
                switch (meshType.ToLowerInvariant())
                {
                    case "cube":
                        mesh = CreateCubeMesh();
                        break;
                    case "sphere":
                        mesh = CreateSphereMesh();
                        break;
                    case "cylinder":
                        mesh = CreateCylinderMesh();
                        break;
                    case "plane":
                        mesh = CreatePlaneMesh();
                        break;
                    default:
                        return Response.Error($"Unknown mesh type: {meshType}. Use: cube, sphere, cylinder, plane");
                }

                // Apply additional properties if provided
                if (properties != null && properties.HasValues)
                {
                    ApplyMeshProperties(mesh, properties);
                }

                AssetDatabase.CreateAsset(mesh, fullPath);
                AssetDatabase.SaveAssets();

                return Response.Success(
                    $"Primitive mesh '{meshType}' created at '{fullPath}' successfully.",
                    GetMeshData(fullPath)
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to generate primitive mesh '{meshType}' at '{fullPath}': {e.Message}");
            }
        }

        private object SubdivideMesh(JObject args)
        {
            string path = args["path"]?.ToString();
            int subdivisionLevel = args["subdivision_level"]?.ToObject<int>() ?? 1;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for subdivide.");
            if (subdivisionLevel < 1 || subdivisionLevel > 5)
                return Response.Error("'subdivision_level' must be between 1 and 5.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Mesh not found at path: {fullPath}");

            try
            {
                Mesh originalMesh = AssetDatabase.LoadAssetAtPath<Mesh>(fullPath);
                if (originalMesh == null)
                    return Response.Error($"Failed to load mesh at path: {fullPath}");

                Undo.RecordObject(originalMesh, $"Subdivide Mesh '{Path.GetFileName(fullPath)}'");

                // Create subdivided mesh
                Mesh subdividedMesh = SubdivideMeshInternal(originalMesh, subdivisionLevel);

                // Copy subdivided data back to original mesh
                originalMesh.vertices = subdividedMesh.vertices;
                originalMesh.triangles = subdividedMesh.triangles;
                originalMesh.normals = subdividedMesh.normals;
                originalMesh.uv = subdividedMesh.uv;
                originalMesh.RecalculateBounds();

                EditorUtility.SetDirty(originalMesh);
                AssetDatabase.SaveAssets();

                return Response.Success(
                    $"Mesh '{fullPath}' subdivided {subdivisionLevel} times successfully.",
                    GetMeshData(fullPath)
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to subdivide mesh '{fullPath}': {e.Message}");
            }
        }

        private object SmoothMesh(JObject args)
        {
            string path = args["path"]?.ToString();
            float smoothFactor = args["smooth_factor"]?.ToObject<float>() ?? 0.5f;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for smooth.");
            if (smoothFactor < 0f || smoothFactor > 1f)
                return Response.Error("'smooth_factor' must be between 0.0 and 1.0.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Mesh not found at path: {fullPath}");

            try
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(fullPath);
                if (mesh == null)
                    return Response.Error($"Failed to load mesh at path: {fullPath}");

                Undo.RecordObject(mesh, $"Smooth Mesh '{Path.GetFileName(fullPath)}'");

                // Apply smoothing
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;

                // Simple smoothing algorithm
                for (int i = 0; i < vertices.Length; i++)
                {
                    // Find connected vertices and average their positions
                    Vector3 smoothedPosition = Vector3.zero;
                    int connectedCount = 0;

                    for (int j = 0; j < mesh.triangles.Length; j += 3)
                    {
                        if (mesh.triangles[j] == i || mesh.triangles[j + 1] == i || mesh.triangles[j + 2] == i)
                        {
                            smoothedPosition += vertices[mesh.triangles[j]];
                            smoothedPosition += vertices[mesh.triangles[j + 1]];
                            smoothedPosition += vertices[mesh.triangles[j + 2]];
                            connectedCount += 3;
                        }
                    }

                    if (connectedCount > 0)
                    {
                        smoothedPosition /= connectedCount;
                        vertices[i] = Vector3.Lerp(vertices[i], smoothedPosition, smoothFactor);
                    }
                }

                mesh.vertices = vertices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                EditorUtility.SetDirty(mesh);
                AssetDatabase.SaveAssets();

                return Response.Success(
                    $"Mesh '{fullPath}' smoothed with factor {smoothFactor} successfully.",
                    GetMeshData(fullPath)
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to smooth mesh '{fullPath}': {e.Message}");
            }
        }

        private object ExportMeshToOBJ(JObject args)
        {
            string path = args["path"]?.ToString();
            string destination = args["destination"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for export.");
            if (string.IsNullOrEmpty(destination))
                return Response.Error("'destination' is required for export.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Mesh not found at path: {fullPath}");

            try
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(fullPath);
                if (mesh == null)
                    return Response.Error($"Failed to load mesh at path: {fullPath}");

                string exportPath = destination;
                if (!Path.IsPathRooted(exportPath))
                {
                    exportPath = Path.Combine(Directory.GetCurrentDirectory(), exportPath);
                }

                // Ensure export directory exists
                string exportDir = Path.GetDirectoryName(exportPath);
                if (!Directory.Exists(exportDir))
                {
                    Directory.CreateDirectory(exportDir);
                }

                bool success = ExportToOBJ(mesh, exportPath);

                if (success)
                {
                    return Response.Success(
                        $"Mesh '{fullPath}' exported to OBJ file '{exportPath}' successfully."
                    );
                }
                else
                {
                    return Response.Error($"Failed to export mesh to OBJ file '{exportPath}'.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to export mesh '{fullPath}' to OBJ: {e.Message}");
            }
        }

        private object ExportMeshToAsset(JObject args)
        {
            string path = args["path"]?.ToString();
            string destination = args["destination"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for export.");
            if (string.IsNullOrEmpty(destination))
                return Response.Error("'destination' is required for export.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Mesh not found at path: {fullPath}");

            try
            {
                Mesh sourceMesh = AssetDatabase.LoadAssetAtPath<Mesh>(fullPath);
                if (sourceMesh == null)
                    return Response.Error($"Failed to load mesh at path: {fullPath}");

                string destPath = SanitizeAssetPath(destination);
                string directory = Path.GetDirectoryName(destPath);

                // Ensure destination directory exists
                if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), directory)))
                {
                    Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), directory));
                    AssetDatabase.Refresh();
                }

                if (AssetExists(destPath))
                    return Response.Error($"Mesh already exists at destination path: {destPath}");

                // Create a copy of the mesh
                Mesh newMesh = new Mesh();
                newMesh.vertices = sourceMesh.vertices;
                newMesh.triangles = sourceMesh.triangles;
                newMesh.normals = sourceMesh.normals;
                newMesh.uv = sourceMesh.uv;
                newMesh.tangents = sourceMesh.tangents;
                newMesh.bounds = sourceMesh.bounds;

                AssetDatabase.CreateAsset(newMesh, destPath);
                AssetDatabase.SaveAssets();

                return Response.Success(
                    $"Mesh '{fullPath}' exported to asset '{destPath}' successfully.",
                    GetMeshData(destPath)
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to export mesh '{fullPath}' to asset: {e.Message}");
            }
        }

        private object ImportMesh(JObject args)
        {
            string path = args["path"]?.ToString();
            string sourcePath = args["source_path"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for import.");
            if (string.IsNullOrEmpty(sourcePath))
                return Response.Error("'source_path' is required for import.");

            string fullPath = SanitizeAssetPath(path);
            string directory = Path.GetDirectoryName(fullPath);

            // Ensure directory exists
            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), directory)))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), directory));
                AssetDatabase.Refresh();
            }

            if (AssetExists(fullPath))
                return Response.Error($"Mesh already exists at path: {fullPath}");

            try
            {
                string importPath = sourcePath;
                if (!Path.IsPathRooted(importPath))
                {
                    importPath = Path.Combine(Directory.GetCurrentDirectory(), importPath);
                }

                if (!File.Exists(importPath))
                    return Response.Error($"Source file not found at path: {importPath}");

                // Import based on file extension
                string extension = Path.GetExtension(importPath).ToLowerInvariant();
                bool success = false;

                switch (extension)
                {
                    case ".obj":
                        success = ImportFromOBJ(importPath, fullPath);
                        break;
                    default:
                        return Response.Error($"Unsupported import format: {extension}. Currently supported: .obj");
                }

                if (success)
                {
                    AssetDatabase.Refresh();
                    return Response.Success(
                        $"Mesh imported from '{importPath}' to '{fullPath}' successfully.",
                        GetMeshData(fullPath)
                    );
                }
                else
                {
                    return Response.Error($"Failed to import mesh from '{importPath}'.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to import mesh to '{fullPath}': {e.Message}");
            }
        }

        private object GetMeshInfo(JObject args)
        {
            string path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for get_info.");
            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Mesh not found at path: {fullPath}");

            try
            {
                return Response.Success(
                    "Mesh info retrieved.",
                    GetMeshData(fullPath)
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting info for mesh '{fullPath}': {e.Message}");
            }
        }

        private object DuplicateMesh(JObject args)
        {
            string path = args["path"]?.ToString();
            string destinationPath = args["destination"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for duplicate.");

            string sourcePath = SanitizeAssetPath(path);
            if (!AssetExists(sourcePath))
                return Response.Error($"Source mesh not found at path: {sourcePath}");

            string destPath;
            if (string.IsNullOrEmpty(destinationPath))
            {
                // Generate a unique path if destination is not provided
                destPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            }
            else
            {
                destPath = SanitizeAssetPath(destinationPath);
                if (AssetExists(destPath))
                    return Response.Error($"Mesh already exists at destination path: {destPath}");
                // Ensure destination directory exists
                EnsureDirectoryExists(Path.GetDirectoryName(destPath));
            }

            try
            {
                bool success = AssetDatabase.CopyAsset(sourcePath, destPath);
                if (success)
                {
                    return Response.Success(
                        $"Mesh '{sourcePath}' duplicated to '{destPath}'.",
                        GetMeshData(destPath)
                    );
                }
                else
                {
                    return Response.Error(
                        $"Failed to duplicate mesh from '{sourcePath}' to '{destPath}'."
                    );
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error duplicating mesh '{sourcePath}': {e.Message}");
            }
        }

        // --- Internal Helpers ---

        /// <summary>
        /// Ensures the asset path starts with "Assets/".
        /// </summary>
        private string SanitizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            path = path.Replace('\\', '/'); // Normalize separators
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets/" + path.TrimStart('/');
            }
            return path;
        }

        /// <summary>
        /// Checks if an asset exists at the given path.
        /// </summary>
        private bool AssetExists(string sanitizedPath)
        {
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath)))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Ensures the directory for a given asset path exists, creating it if necessary.
        /// </summary>
        private void EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return;
            string fullDirPath = Path.Combine(Directory.GetCurrentDirectory(), directoryPath);
            if (!Directory.Exists(fullDirPath))
            {
                Directory.CreateDirectory(fullDirPath);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Applies properties from JObject to a Mesh.
        /// </summary>
        private bool ApplyMeshProperties(Mesh mesh, JObject properties)
        {
            if (mesh == null || properties == null)
                return false;
            bool modified = false;

            // Handle vertices
            if (properties["vertices"] is JArray verticesArray)
            {
                Vector3[] vertices = new Vector3[verticesArray.Count];
                for (int i = 0; i < verticesArray.Count; i++)
                {
                    if (verticesArray[i] is JArray vertexData && vertexData.Count >= 3)
                    {
                        vertices[i] = new Vector3(
                            vertexData[0].ToObject<float>(),
                            vertexData[1].ToObject<float>(),
                            vertexData[2].ToObject<float>()
                        );
                    }
                }
                mesh.vertices = vertices;
                modified = true;
            }

            // Handle triangles
            if (properties["triangles"] is JArray trianglesArray)
            {
                int[] triangles = new int[trianglesArray.Count];
                for (int i = 0; i < trianglesArray.Count; i++)
                {
                    triangles[i] = trianglesArray[i].ToObject<int>();
                }
                mesh.triangles = triangles;
                modified = true;
            }

            // Handle normals
            if (properties["normals"] is JArray normalsArray)
            {
                Vector3[] normals = new Vector3[normalsArray.Count];
                for (int i = 0; i < normalsArray.Count; i++)
                {
                    if (normalsArray[i] is JArray normalData && normalData.Count >= 3)
                    {
                        normals[i] = new Vector3(
                            normalData[0].ToObject<float>(),
                            normalData[1].ToObject<float>(),
                            normalData[2].ToObject<float>()
                        );
                    }
                }
                mesh.normals = normals;
                modified = true;
            }

            // Handle UVs
            if (properties["uv"] is JArray uvArray)
            {
                Vector2[] uvs = new Vector2[uvArray.Count];
                for (int i = 0; i < uvArray.Count; i++)
                {
                    if (uvArray[i] is JArray uvData && uvData.Count >= 2)
                    {
                        uvs[i] = new Vector2(
                            uvData[0].ToObject<float>(),
                            uvData[1].ToObject<float>()
                        );
                    }
                }
                mesh.uv = uvs;
                modified = true;
            }

            // Handle tangents
            if (properties["tangents"] is JArray tangentsArray)
            {
                Vector4[] tangents = new Vector4[tangentsArray.Count];
                for (int i = 0; i < tangentsArray.Count; i++)
                {
                    if (tangentsArray[i] is JArray tangentData && tangentData.Count >= 4)
                    {
                        tangents[i] = new Vector4(
                            tangentData[0].ToObject<float>(),
                            tangentData[1].ToObject<float>(),
                            tangentData[2].ToObject<float>(),
                            tangentData[3].ToObject<float>()
                        );
                    }
                }
                mesh.tangents = tangents;
                modified = true;
            }

            if (modified)
            {
                mesh.RecalculateBounds();
            }

            return modified;
        }

        /// <summary>
        /// Creates a cube mesh.
        /// </summary>
        private Mesh CreateCubeMesh()
        {
            Mesh mesh = new Mesh();

            Vector3[] vertices = {
                // Front face
                new Vector3(-0.5f, -0.5f,  0.5f),
                new Vector3( 0.5f, -0.5f,  0.5f),
                new Vector3( 0.5f,  0.5f,  0.5f),
                new Vector3(-0.5f,  0.5f,  0.5f),
                // Back face
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f,  0.5f, -0.5f)
            };

            int[] triangles = {
                // Front face
                0, 2, 1, 0, 3, 2,
                // Back face
                5, 6, 4, 4, 6, 7,
                // Left face
                4, 7, 0, 0, 7, 3,
                // Right face
                1, 2, 5, 5, 2, 6,
                // Top face
                3, 6, 2, 3, 7, 6,
                // Bottom face
                4, 0, 5, 5, 0, 1
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Creates a sphere mesh.
        /// </summary>
        private Mesh CreateSphereMesh()
        {
            Mesh mesh = new Mesh();

            int segments = 16;
            int rings = 16;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            // Generate vertices
            for (int ring = 0; ring <= rings; ring++)
            {
                float phi = Mathf.PI * ring / rings;
                float y = Mathf.Cos(phi);
                float radius = Mathf.Sin(phi);

                for (int segment = 0; segment <= segments; segment++)
                {
                    float theta = 2 * Mathf.PI * segment / segments;
                    float x = radius * Mathf.Cos(theta);
                    float z = radius * Mathf.Sin(theta);

                    vertices.Add(new Vector3(x, y, z) * 0.5f);
                }
            }

            // Generate triangles
            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int current = ring * (segments + 1) + segment;
                    int next = current + segments + 1;

                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(current + 1);

                    triangles.Add(current + 1);
                    triangles.Add(next);
                    triangles.Add(next + 1);
                }
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Creates a cylinder mesh.
        /// </summary>
        private Mesh CreateCylinderMesh()
        {
            Mesh mesh = new Mesh();

            int segments = 16;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            // Generate vertices for top and bottom circles
            for (int segment = 0; segment <= segments; segment++)
            {
                float theta = 2 * Mathf.PI * segment / segments;
                float x = Mathf.Cos(theta) * 0.5f;
                float z = Mathf.Sin(theta) * 0.5f;

                // Top circle
                vertices.Add(new Vector3(x, 0.5f, z));
                // Bottom circle
                vertices.Add(new Vector3(x, -0.5f, z));
            }

            // Generate triangles for side faces
            for (int segment = 0; segment < segments; segment++)
            {
                int current = segment * 2;
                int next = (segment + 1) * 2;

                // First triangle
                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(current + 1);

                // Second triangle
                triangles.Add(current + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Creates a plane mesh.
        /// </summary>
        private Mesh CreatePlaneMesh()
        {
            Mesh mesh = new Mesh();

            Vector3[] vertices = {
                new Vector3(-0.5f, 0, -0.5f),
                new Vector3( 0.5f, 0, -0.5f),
                new Vector3( 0.5f, 0,  0.5f),
                new Vector3(-0.5f, 0,  0.5f)
            };

            int[] triangles = {
                0, 2, 1, 0, 3, 2
            };

            Vector2[] uvs = {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Subdivides a mesh using simple subdivision.
        /// </summary>
        private Mesh SubdivideMeshInternal(Mesh originalMesh, int levels)
        {
            Mesh result = originalMesh;

            for (int level = 0; level < levels; level++)
            {
                Vector3[] vertices = result.vertices;
                int[] triangles = result.triangles;

                List<Vector3> newVertices = new List<Vector3>(vertices);
                List<int> newTriangles = new List<int>();

                // For each triangle, create 4 new triangles
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int v1 = triangles[i];
                    int v2 = triangles[i + 1];
                    int v3 = triangles[i + 2];

                    // Calculate midpoints
                    Vector3 mid1 = (vertices[v1] + vertices[v2]) * 0.5f;
                    Vector3 mid2 = (vertices[v2] + vertices[v3]) * 0.5f;
                    Vector3 mid3 = (vertices[v3] + vertices[v1]) * 0.5f;

                    // Add new vertices
                    int mid1Index = newVertices.Count;
                    int mid2Index = newVertices.Count + 1;
                    int mid3Index = newVertices.Count + 2;

                    newVertices.Add(mid1);
                    newVertices.Add(mid2);
                    newVertices.Add(mid3);

                    // Create 4 new triangles
                    newTriangles.Add(v1);
                    newTriangles.Add(mid1Index);
                    newTriangles.Add(mid3Index);

                    newTriangles.Add(mid1Index);
                    newTriangles.Add(v2);
                    newTriangles.Add(mid2Index);

                    newTriangles.Add(mid3Index);
                    newTriangles.Add(mid2Index);
                    newTriangles.Add(v3);

                    newTriangles.Add(mid1Index);
                    newTriangles.Add(mid2Index);
                    newTriangles.Add(mid3Index);
                }

                result = new Mesh();
                result.vertices = newVertices.ToArray();
                result.triangles = newTriangles.ToArray();
                result.RecalculateNormals();
                result.RecalculateBounds();
            }

            return result;
        }

        /// <summary>
        /// Exports mesh to OBJ format.
        /// </summary>
        private bool ExportToOBJ(Mesh mesh, string filePath)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("# Exported by Unity MCP AssetMesh");
                    writer.WriteLine($"# Vertices: {mesh.vertices.Length}");
                    writer.WriteLine($"# Triangles: {mesh.triangles.Length / 3}");

                    // Write vertices
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        writer.WriteLine($"v {vertex.x} {vertex.y} {vertex.z}");
                    }

                    // Write normals
                    if (mesh.normals != null && mesh.normals.Length > 0)
                    {
                        foreach (Vector3 normal in mesh.normals)
                        {
                            writer.WriteLine($"vn {normal.x} {normal.y} {normal.z}");
                        }
                    }

                    // Write UVs
                    if (mesh.uv != null && mesh.uv.Length > 0)
                    {
                        foreach (Vector2 uv in mesh.uv)
                        {
                            writer.WriteLine($"vt {uv.x} {uv.y}");
                        }
                    }

                    // Write faces (OBJ uses 1-based indexing)
                    for (int i = 0; i < mesh.triangles.Length; i += 3)
                    {
                        int v1 = mesh.triangles[i] + 1;
                        int v2 = mesh.triangles[i + 1] + 1;
                        int v3 = mesh.triangles[i + 2] + 1;
                        writer.WriteLine($"f {v1} {v2} {v3}");
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to export OBJ: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Imports mesh from OBJ format.
        /// </summary>
        private bool ImportFromOBJ(string filePath, string assetPath)
        {
            try
            {
                List<Vector3> vertices = new List<Vector3>();
                List<Vector3> normals = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> triangles = new List<int>();

                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                            continue;

                        string[] parts = line.Split(' ');
                        if (parts.Length < 2)
                            continue;

                        switch (parts[0])
                        {
                            case "v": // Vertex
                                if (parts.Length >= 4)
                                {
                                    vertices.Add(new Vector3(
                                        float.Parse(parts[1]),
                                        float.Parse(parts[2]),
                                        float.Parse(parts[3])
                                    ));
                                }
                                break;

                            case "vn": // Normal
                                if (parts.Length >= 4)
                                {
                                    normals.Add(new Vector3(
                                        float.Parse(parts[1]),
                                        float.Parse(parts[2]),
                                        float.Parse(parts[3])
                                    ));
                                }
                                break;

                            case "vt": // UV
                                if (parts.Length >= 3)
                                {
                                    uvs.Add(new Vector2(
                                        float.Parse(parts[1]),
                                        float.Parse(parts[2])
                                    ));
                                }
                                break;

                            case "f": // Face
                                if (parts.Length >= 4)
                                {
                                    // Simple face parsing (assumes triangles)
                                    for (int i = 1; i <= 3; i++)
                                    {
                                        string[] indices = parts[i].Split('/');
                                        int vertexIndex = int.Parse(indices[0]) - 1; // Convert to 0-based
                                        triangles.Add(vertexIndex);
                                    }
                                }
                                break;
                        }
                    }
                }

                // Create mesh
                Mesh mesh = new Mesh();
                mesh.vertices = vertices.ToArray();
                mesh.triangles = triangles.ToArray();

                if (normals.Count > 0)
                    mesh.normals = normals.ToArray();
                if (uvs.Count > 0)
                    mesh.uv = uvs.ToArray();

                mesh.RecalculateBounds();
                if (normals.Count == 0)
                    mesh.RecalculateNormals();

                // Save mesh asset
                AssetDatabase.CreateAsset(mesh, assetPath);
                AssetDatabase.SaveAssets();

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to import OBJ: {e.Message}");
                return false;
            }
        }

        // --- Data Serialization ---

        /// <summary>
        /// 创建mesh资产的紧凑YAML表示 - 避免返回大量顶点数据
        /// </summary>
        private object GetMeshData(string path)
        {
            if (string.IsNullOrEmpty(path) || !AssetExists(path))
                return null;

            string guid = AssetDatabase.AssetPathToGUID(path);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (mesh == null)
                return null;

            // 计算边界框
            var bounds = mesh.bounds;
            var boundsMin = new float[] { bounds.min.x, bounds.min.y, bounds.min.z };
            var boundsMax = new float[] { bounds.max.x, bounds.max.y, bounds.max.z };

            // 使用YAML格式大幅减少token使用量
            var yaml = $@"name: {Path.GetFileNameWithoutExtension(path)}
path: {path}
guid: {guid}
vertices: {mesh.vertexCount}
triangles: {mesh.triangles.Length / 3}
submeshes: {mesh.subMeshCount}
hasNormals: {(mesh.normals != null && mesh.normals.Length > 0).ToString().ToLower()}
hasUVs: {(mesh.uv != null && mesh.uv.Length > 0).ToString().ToLower()}
hasTangents: {(mesh.tangents != null && mesh.tangents.Length > 0).ToString().ToLower()}
hasColors: {(mesh.colors != null && mesh.colors.Length > 0).ToString().ToLower()}
boundsMin: [{boundsMin[0]:F2}, {boundsMin[1]:F2}, {boundsMin[2]:F2}]
boundsMax: [{boundsMax[0]:F2}, {boundsMax[1]:F2}, {boundsMax[2]:F2}]
readable: {mesh.isReadable.ToString().ToLower()}
lastModified: {File.GetLastWriteTimeUtc(Path.Combine(Directory.GetCurrentDirectory(), path)):yyyy-MM-dd}";

            return new { yaml = yaml };
        }
    }
}
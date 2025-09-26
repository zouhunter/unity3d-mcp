using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// 专门的模型管理工具，提供模型的导入、修改、复制、删除等操作
    /// 对应方法名: manage_model
    /// </summary>
    [ToolName("edit_model", "资源管理")]
    public class EditModel : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：import, modify, duplicate, delete, get_info, search, set_import_settings", false),
                new MethodKey("path", "模型资源路径，Unity标准格式：Assets/Models/ModelName.fbx", false),
                new MethodKey("source_file", "源文件路径（导入时使用）", true),
                new MethodKey("destination", "目标路径（复制/移动时使用）", true),
                new MethodKey("query", "搜索模式，如*.fbx, *.obj", true),
                new MethodKey("recursive", "是否递归搜索子文件夹", true),
                new MethodKey("force", "是否强制执行操作（覆盖现有文件等）", true),
                new MethodKey("import_settings", "导入设置", true),
                new MethodKey("scale_factor", "缩放因子", true),
                new MethodKey("use_file_scale", "是否使用文件缩放", true),
                new MethodKey("use_file_units", "是否使用文件单位", true),
                new MethodKey("import_blend_shapes", "是否导入混合形状", true),
                new MethodKey("import_visibility", "是否导入可见性", true),
                new MethodKey("import_cameras", "是否导入相机", true),
                new MethodKey("import_lights", "是否导入灯光", true),
                new MethodKey("preserve_hierarchy", "是否保持层级", true),
                new MethodKey("animation_type", "动画类型：None, Legacy, Generic, Humanoid", true),
                new MethodKey("optimize_mesh", "是否优化网格", true),
                new MethodKey("generate_secondary_uv", "是否生成次要UV", true),
                new MethodKey("secondary_uv_hard_angle", "次要UV硬角度", true),
                new MethodKey("secondary_uv_pack_margin", "次要UV打包边距", true),
                new MethodKey("secondary_uv_angle_distortion", "次要UV角度扭曲", true),
                new MethodKey("secondary_uv_area_distortion", "次要UV面积扭曲", true),
                new MethodKey("secondary_uv_edge_distortion", "次要UV边缘扭曲", true),
                new MethodKey("read_write_enabled", "是否启用读写", true),
                new MethodKey("optimize_game_objects", "是否优化游戏对象", true),
                new MethodKey("import_materials", "是否导入材质", true),
                new MethodKey("material_naming", "材质命名模式：ByBaseTextureName, ByModelName, ByTextureName", true),
                new MethodKey("material_search", "材质搜索模式：Local, RecursiveUp, Everywhere", true),
                new MethodKey("extract_materials", "是否提取材质", true),
                new MethodKey("extract_materials_path", "提取材质路径", true),
                new MethodKey("mesh_compression", "网格压缩：Off, Low, Medium, High", true),
                new MethodKey("add_collider", "是否添加碰撞器", true),
                new MethodKey("keep_quads", "是否保持四边形", true),
                new MethodKey("weld_vertices", "是否焊接顶点", true),
                new MethodKey("index_format", "索引格式：Auto, UInt16, UInt32", true),
                new MethodKey("legacy_blend_shape_normals", "是否使用传统混合形状法线", true),
                new MethodKey("blend_shape_normals", "混合形状法线模式：Default, None, Calculate, Import", true),
                new MethodKey("tangents", "切线模式：Default, None, Calculate, Import", true),
                new MethodKey("smoothness_source", "平滑度来源：None, DiffuseAlpha, SpecularAlpha", true),
                new MethodKey("smoothness", "平滑度", true),
                new MethodKey("normal_import_mode", "法线导入模式：Default, None, Calculate, Import", true),
                new MethodKey("normal_map_mode", "法线贴图模式：Default, OpenGL, DirectX", true),
                new MethodKey("height_map_mode", "高度贴图模式：Default, OpenGL, DirectX", true),
                new MethodKey("generate_secondary_uv", "是否生成次要UV", true),
                new MethodKey("secondary_uv_hard_angle", "次要UV硬角度", true),
                new MethodKey("secondary_uv_pack_margin", "次要UV打包边距", true),
                new MethodKey("secondary_uv_angle_distortion", "次要UV角度扭曲", true),
                new MethodKey("secondary_uv_area_distortion", "次要UV面积扭曲", true),
                new MethodKey("secondary_uv_edge_distortion", "次要UV边缘扭曲", true),
                new MethodKey("read_write_enabled", "是否启用读写", true),
                new MethodKey("optimize_game_objects", "是否优化游戏对象", true),
                new MethodKey("import_materials", "是否导入材质", true),
                new MethodKey("material_naming", "材质命名模式：ByBaseTextureName, ByModelName, ByTextureName", true),
                new MethodKey("material_search", "材质搜索模式：Local, RecursiveUp, Everywhere", true),
                new MethodKey("extract_materials", "是否提取材质", true),
                new MethodKey("extract_materials_path", "提取材质路径", true),
                new MethodKey("mesh_compression", "网格压缩：Off, Low, Medium, High", true),
                new MethodKey("add_collider", "是否添加碰撞器", true),
                new MethodKey("keep_quads", "是否保持四边形", true),
                new MethodKey("weld_vertices", "是否焊接顶点", true),
                new MethodKey("index_format", "索引格式：Auto, UInt16, UInt32", true),
                new MethodKey("legacy_blend_shape_normals", "是否使用传统混合形状法线", true),
                new MethodKey("blend_shape_normals", "混合形状法线模式：Default, None, Calculate, Import", true),
                new MethodKey("tangents", "切线模式：Default, None, Calculate, Import", true),
                new MethodKey("smoothness_source", "平滑度来源：None, DiffuseAlpha, SpecularAlpha", true),
                new MethodKey("smoothness", "平滑度", true),
                new MethodKey("normal_import_mode", "法线导入模式：Default, None, Calculate, Import", true),
                new MethodKey("normal_map_mode", "法线贴图模式：Default, OpenGL, DirectX", true),
                new MethodKey("height_map_mode", "高度贴图模式：Default, OpenGL, DirectX", true)
            };
        }

        /// <summary>
        /// 创建状态树
        /// </summary>
        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("import", ImportModel)
                    .Leaf("modify", ModifyModel)
                    .Leaf("duplicate", DuplicateModel)
                    .Leaf("delete", DeleteModel)
                    .Leaf("get_info", GetModelInfo)
                    .Leaf("search", SearchModels)
                    .Leaf("set_import_settings", SetModelImportSettings)
                    .Leaf("extract_materials", ExtractModelMaterials)
                    .Leaf("optimize", OptimizeModel)
                .Build();
        }

        // --- 状态树操作方法 ---

        private object ImportModel(JObject args)
        {
            string sourceFile = args["source_file"]?.ToString();
            string path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(sourceFile))
                return Response.Error("'source_file' is required for import.");
            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for import.");

            string fullPath = SanitizeAssetPath(path);
            string directory = Path.GetDirectoryName(fullPath);

            // 确保目录存在
            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), directory)))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), directory));
                AssetDatabase.Refresh();
            }

            if (AssetExists(fullPath))
                return Response.Error($"Model already exists at path: {fullPath}");

            try
            {
                // 检查源文件是否存在
                if (!File.Exists(sourceFile))
                    return Response.Error($"Source file not found: {sourceFile}");

                // 复制文件到目标路径
                string targetFilePath = Path.Combine(Directory.GetCurrentDirectory(), fullPath);
                File.Copy(sourceFile, targetFilePath);

                // 导入设置
                JObject importSettings = args["import_settings"] as JObject;
                if (importSettings != null && importSettings.HasValues)
                {
                    AssetDatabase.ImportAsset(fullPath);
                    ModelImporter importer = AssetImporter.GetAtPath(fullPath) as ModelImporter;
                    if (importer != null)
                    {
                        ApplyModelImportSettings(importer, importSettings);
                        importer.SaveAndReimport();
                    }
                }
                else
                {
                    AssetDatabase.ImportAsset(fullPath);
                }

                LogInfo($"[ManageModel] Imported model from '{sourceFile}' to '{fullPath}'");
                return Response.Success($"Model imported successfully to '{fullPath}'.", GetModelData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to import model to '{fullPath}': {e.Message}");
            }
        }

        private object ModifyModel(JObject args)
        {
            string path = args["path"]?.ToString();
            JObject importSettings = args["import_settings"] as JObject;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for modify.");
            if (importSettings == null || !importSettings.HasValues)
                return Response.Error("'import_settings' are required for modify.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Model not found at path: {fullPath}");

            try
            {
                ModelImporter importer = AssetImporter.GetAtPath(fullPath) as ModelImporter;
                if (importer == null)
                    return Response.Error($"Failed to get ModelImporter for '{fullPath}'");

                bool modified = ApplyModelImportSettings(importer, importSettings);

                if (modified)
                {
                    importer.SaveAndReimport();
                    LogInfo($"[ManageModel] Modified model import settings at '{fullPath}'");
                    return Response.Success($"Model '{fullPath}' modified successfully.", GetModelData(fullPath));
                }
                else
                {
                    return Response.Success($"No applicable settings found to modify for model '{fullPath}'.", GetModelData(fullPath));
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to modify model '{fullPath}': {e.Message}");
            }
        }

        private object DuplicateModel(JObject args)
        {
            string path = args["path"]?.ToString();
            string destinationPath = args["destination"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for duplicate.");

            string sourcePath = SanitizeAssetPath(path);
            if (!AssetExists(sourcePath))
                return Response.Error($"Source model not found at path: {sourcePath}");

            string destPath;
            if (string.IsNullOrEmpty(destinationPath))
            {
                destPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            }
            else
            {
                destPath = SanitizeAssetPath(destinationPath);
                if (AssetExists(destPath))
                    return Response.Error($"Model already exists at destination path: {destPath}");
                EnsureDirectoryExists(Path.GetDirectoryName(destPath));
            }

            try
            {
                bool success = AssetDatabase.CopyAsset(sourcePath, destPath);
                if (success)
                {
                    LogInfo($"[ManageModel] Duplicated model from '{sourcePath}' to '{destPath}'");
                    return Response.Success($"Model '{sourcePath}' duplicated to '{destPath}'.", GetModelData(destPath));
                }
                else
                {
                    return Response.Error($"Failed to duplicate model from '{sourcePath}' to '{destPath}'.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error duplicating model '{sourcePath}': {e.Message}");
            }
        }

        private object DeleteModel(JObject args)
        {
            string path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for delete.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Model not found at path: {fullPath}");

            try
            {
                bool success = AssetDatabase.DeleteAsset(fullPath);
                if (success)
                {
                    LogInfo($"[ManageModel] Deleted model at '{fullPath}'");
                    return Response.Success($"Model '{fullPath}' deleted successfully.");
                }
                else
                {
                    return Response.Error($"Failed to delete model '{fullPath}'. Check logs or if the file is locked.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error deleting model '{fullPath}': {e.Message}");
            }
        }

        private object GetModelInfo(JObject args)
        {
            string path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for get_info.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Model not found at path: {fullPath}");

            try
            {
                return Response.Success("Model info retrieved.", GetModelData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting info for model '{fullPath}': {e.Message}");
            }
        }

        private object SearchModels(JObject args)
        {
            string searchPattern = args["query"]?.ToString();
            string pathScope = args["path"]?.ToString();
            bool recursive = args["recursive"]?.ToObject<bool>() ?? true;

            List<string> searchFilters = new List<string>();
            if (!string.IsNullOrEmpty(searchPattern))
                searchFilters.Add(searchPattern);
            searchFilters.Add("t:GameObject");

            string[] folderScope = null;
            if (!string.IsNullOrEmpty(pathScope))
            {
                folderScope = new string[] { SanitizeAssetPath(pathScope) };
                if (!AssetDatabase.IsValidFolder(folderScope[0]))
                {
                    LogWarning($"Search path '{folderScope[0]}' is not a valid folder. Searching entire project.");
                    folderScope = null;
                }
            }

            try
            {
                string[] guids = AssetDatabase.FindAssets(string.Join(" ", searchFilters), folderScope);
                List<object> results = new List<object>();

                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath))
                        continue;

                    // 检查是否是模型文件
                    if (IsModelFile(assetPath))
                    {
                        results.Add(GetModelData(assetPath));
                    }
                }

                LogInfo($"[ManageModel] Found {results.Count} model(s)");
                return Response.Success($"Found {results.Count} model(s).", results);
            }
            catch (Exception e)
            {
                return Response.Error($"Error searching models: {e.Message}");
            }
        }

        private object SetModelImportSettings(JObject args)
        {
            string path = args["path"]?.ToString();
            JObject importSettings = args["import_settings"] as JObject;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for set_import_settings.");
            if (importSettings == null || !importSettings.HasValues)
                return Response.Error("'import_settings' are required for set_import_settings.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Model not found at path: {fullPath}");

            try
            {
                ModelImporter importer = AssetImporter.GetAtPath(fullPath) as ModelImporter;
                if (importer == null)
                    return Response.Error($"Failed to get ModelImporter for '{fullPath}'");

                bool modified = ApplyModelImportSettings(importer, importSettings);

                if (modified)
                {
                    importer.SaveAndReimport();
                    LogInfo($"[ManageModel] Set import settings on model '{fullPath}'");
                    return Response.Success($"Import settings set on model '{fullPath}'.", GetModelData(fullPath));
                }
                else
                {
                    return Response.Success($"No valid import settings found to set on model '{fullPath}'.", GetModelData(fullPath));
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error setting import settings on model '{fullPath}': {e.Message}");
            }
        }

        private object ExtractModelMaterials(JObject args)
        {
            string path = args["path"]?.ToString();
            string extractPath = args["extract_path"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for extract_materials.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Model not found at path: {fullPath}");

            try
            {
                ModelImporter importer = AssetImporter.GetAtPath(fullPath) as ModelImporter;
                if (importer == null)
                    return Response.Error($"Failed to get ModelImporter for '{fullPath}'");

                // 设置提取材质
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

                if (!string.IsNullOrEmpty(extractPath))
                {
                    string extractFullPath = SanitizeAssetPath(extractPath);
                    EnsureDirectoryExists(extractFullPath);
                    importer.materialLocation = ModelImporterMaterialLocation.External;
                    importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                    importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
                }

                importer.SaveAndReimport();

                LogInfo($"[ManageModel] Extracted materials from model '{fullPath}'");
                return Response.Success($"Materials extracted from model '{fullPath}'.", GetModelData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Error extracting materials from model '{fullPath}': {e.Message}");
            }
        }

        private object OptimizeModel(JObject args)
        {
            string path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for optimize.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Model not found at path: {fullPath}");

            try
            {
                ModelImporter importer = AssetImporter.GetAtPath(fullPath) as ModelImporter;
                if (importer == null)
                    return Response.Error($"Failed to get ModelImporter for '{fullPath}'");

                // 应用优化设置
                importer.optimizeMesh = true;
                importer.optimizeGameObjects = true;
                importer.weldVertices = true;
                importer.indexFormat = ModelImporterIndexFormat.Auto;
                importer.meshCompression = ModelImporterMeshCompression.Medium;

                importer.SaveAndReimport();

                LogInfo($"[ManageModel] Optimized model '{fullPath}'");
                return Response.Success($"Model '{fullPath}' optimized successfully.", GetModelData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Error optimizing model '{fullPath}': {e.Message}");
            }
        }

        // --- 内部辅助方法 ---

        /// <summary>
        /// 确保资产路径以"Assets/"开头
        /// </summary>
        private string SanitizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets/" + path.TrimStart('/');
            }
            return path;
        }

        /// <summary>
        /// 检查资产是否存在
        /// </summary>
        private bool AssetExists(string sanitizedPath)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sanitizedPath)))
            {
                return true;
            }
            if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath)))
            {
                return AssetDatabase.IsValidFolder(sanitizedPath);
            }
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath)))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 确保目录存在
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
        /// 检查是否是模型文件
        /// </summary>
        private bool IsModelFile(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".fbx" || extension == ".obj" || extension == ".dae" ||
                   extension == ".3ds" || extension == ".dxf" || extension == ".skp" ||
                   extension == ".max" || extension == ".c4d" || extension == ".blend";
        }

        /// <summary>
        /// 应用模型导入设置
        /// </summary>
        private bool ApplyModelImportSettings(ModelImporter importer, JObject settings)
        {
            if (importer == null || settings == null)
                return false;
            bool modified = false;

            foreach (var setting in settings.Properties())
            {
                string settingName = setting.Name;
                JToken settingValue = setting.Value;

                try
                {
                    switch (settingName.ToLowerInvariant())
                    {
                        case "scale_factor":
                            if (settingValue.Type == JTokenType.Float || settingValue.Type == JTokenType.Integer)
                            {
                                float scaleFactor = settingValue.ToObject<float>();
                                if (Math.Abs(importer.globalScale - scaleFactor) > 0.001f)
                                {
                                    importer.globalScale = scaleFactor;
                                    modified = true;
                                }
                            }
                            break;
                        case "use_file_scale":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool useFileScale = settingValue.ToObject<bool>();
                                if (importer.useFileScale != useFileScale)
                                {
                                    importer.useFileScale = useFileScale;
                                    modified = true;
                                }
                            }
                            break;
                        case "use_file_units":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool useFileUnits = settingValue.ToObject<bool>();
                                if (importer.useFileUnits != useFileUnits)
                                {
                                    importer.useFileUnits = useFileUnits;
                                    modified = true;
                                }
                            }
                            break;
                        case "import_blend_shapes":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool importBlendShapes = settingValue.ToObject<bool>();
                                if (importer.importBlendShapes != importBlendShapes)
                                {
                                    importer.importBlendShapes = importBlendShapes;
                                    modified = true;
                                }
                            }
                            break;
                        case "import_visibility":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool importVisibility = settingValue.ToObject<bool>();
                                if (importer.importVisibility != importVisibility)
                                {
                                    importer.importVisibility = importVisibility;
                                    modified = true;
                                }
                            }
                            break;
                        case "import_cameras":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool importCameras = settingValue.ToObject<bool>();
                                if (importer.importCameras != importCameras)
                                {
                                    importer.importCameras = importCameras;
                                    modified = true;
                                }
                            }
                            break;
                        case "import_lights":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool importLights = settingValue.ToObject<bool>();
                                if (importer.importLights != importLights)
                                {
                                    importer.importLights = importLights;
                                    modified = true;
                                }
                            }
                            break;
                        case "preserve_hierarchy":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool preserveHierarchy = settingValue.ToObject<bool>();
                                if (importer.preserveHierarchy != preserveHierarchy)
                                {
                                    importer.preserveHierarchy = preserveHierarchy;
                                    modified = true;
                                }
                            }
                            break;
                        case "animation_type":
                            if (settingValue.Type == JTokenType.String)
                            {
                                string animationType = settingValue.ToString();
                                ModelImporterAnimationType animType = ModelImporterAnimationType.None;

                                switch (animationType.ToLowerInvariant())
                                {
                                    case "legacy":
                                        animType = ModelImporterAnimationType.Legacy;
                                        break;
                                    case "generic":
                                        animType = ModelImporterAnimationType.Generic;
                                        break;
                                    case "humanoid":
                                        // 注意：ModelImporterAnimationType.Humanoid 在某些Unity版本中可能不可用
                                        // animType = ModelImporterAnimationType.Humanoid;
                                        LogWarning($"[ApplyModelImportSettings] ModelImporterAnimationType.Humanoid not supported in current Unity version");
                                        break;
                                    case "none":
                                    default:
                                        animType = ModelImporterAnimationType.None;
                                        break;
                                }

                                if (importer.animationType != animType)
                                {
                                    importer.animationType = animType;
                                    modified = true;
                                }
                            }
                            break;
                        case "optimize_mesh":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool optimizeMesh = settingValue.ToObject<bool>();
                                if (importer.optimizeMesh != optimizeMesh)
                                {
                                    importer.optimizeMesh = optimizeMesh;
                                    modified = true;
                                }
                            }
                            break;
                        case "generate_secondary_uv":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool generateSecondaryUV = settingValue.ToObject<bool>();
                                if (importer.generateSecondaryUV != generateSecondaryUV)
                                {
                                    importer.generateSecondaryUV = generateSecondaryUV;
                                    modified = true;
                                }
                            }
                            break;
                        case "read_write_enabled":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool readWriteEnabled = settingValue.ToObject<bool>();
                                if (importer.isReadable != readWriteEnabled)
                                {
                                    importer.isReadable = readWriteEnabled;
                                    modified = true;
                                }
                            }
                            break;
                        case "optimize_game_objects":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool optimizeGameObjects = settingValue.ToObject<bool>();
                                if (importer.optimizeGameObjects != optimizeGameObjects)
                                {
                                    importer.optimizeGameObjects = optimizeGameObjects;
                                    modified = true;
                                }
                            }
                            break;
                        case "import_materials":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool importMaterials = settingValue.ToObject<bool>();
                                ModelImporterMaterialImportMode materialMode = importMaterials ?
                                    ModelImporterMaterialImportMode.ImportStandard :
                                    ModelImporterMaterialImportMode.None;

                                if (importer.materialImportMode != materialMode)
                                {
                                    importer.materialImportMode = materialMode;
                                    modified = true;
                                }
                            }
                            break;
                        case "mesh_compression":
                            if (settingValue.Type == JTokenType.String)
                            {
                                string compression = settingValue.ToString();
                                ModelImporterMeshCompression meshCompression = ModelImporterMeshCompression.Off;

                                switch (compression.ToLowerInvariant())
                                {
                                    case "low":
                                        meshCompression = ModelImporterMeshCompression.Low;
                                        break;
                                    case "medium":
                                        meshCompression = ModelImporterMeshCompression.Medium;
                                        break;
                                    case "high":
                                        meshCompression = ModelImporterMeshCompression.High;
                                        break;
                                    case "off":
                                    default:
                                        meshCompression = ModelImporterMeshCompression.Off;
                                        break;
                                }

                                if (importer.meshCompression != meshCompression)
                                {
                                    importer.meshCompression = meshCompression;
                                    modified = true;
                                }
                            }
                            break;
                        case "add_collider":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool addCollider = settingValue.ToObject<bool>();
                                if (importer.addCollider != addCollider)
                                {
                                    importer.addCollider = addCollider;
                                    modified = true;
                                }
                            }
                            break;
                        case "weld_vertices":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool weldVertices = settingValue.ToObject<bool>();
                                if (importer.weldVertices != weldVertices)
                                {
                                    importer.weldVertices = weldVertices;
                                    modified = true;
                                }
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"[ApplyModelImportSettings] Error setting '{settingName}': {ex.Message}");
                }
            }

            return modified;
        }

        /// <summary>
        /// 获取模型数据
        /// </summary>
        private object GetModelData(string path)
        {
            if (string.IsNullOrEmpty(path) || !AssetExists(path))
                return null;

            string guid = AssetDatabase.AssetPathToGUID(path);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (model == null)
                return null;

            // 获取模型导入器信息
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            object importSettings = null;

            if (importer != null)
            {
                importSettings = new
                {
                    scale_factor = importer.globalScale,
                    use_file_scale = importer.useFileScale,
                    use_file_units = importer.useFileUnits,
                    import_blend_shapes = importer.importBlendShapes,
                    import_visibility = importer.importVisibility,
                    import_cameras = importer.importCameras,
                    import_lights = importer.importLights,
                    preserve_hierarchy = importer.preserveHierarchy,
                    animation_type = importer.animationType.ToString(),
                    optimize_mesh = importer.optimizeMesh,
                    generate_secondary_uv = importer.generateSecondaryUV,
                    read_write_enabled = importer.isReadable,
                    optimize_game_objects = importer.optimizeGameObjects,
                    import_materials = importer.materialImportMode != ModelImporterMaterialImportMode.None,
                    mesh_compression = importer.meshCompression.ToString(),
                    add_collider = importer.addCollider,
                    weld_vertices = importer.weldVertices
                };
            }

            return new
            {
                path = path,
                guid = guid,
                name = Path.GetFileNameWithoutExtension(path),
                fileName = Path.GetFileName(path),
                file_extension = Path.GetExtension(path),
                is_model_file = IsModelFile(path),
                import_settings = importSettings,
                lastWriteTimeUtc = File.GetLastWriteTimeUtc(Path.Combine(Directory.GetCurrentDirectory(), path)).ToString("o")
            };
        }
    }
}
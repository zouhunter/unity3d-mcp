using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Handles Unity asset management operations including import, modify, move, duplicate, etc.
    /// 对应方法名: manage_asset
    /// </summary>
    [ToolName("project_operate", "项目管理")]
    public class ProjectOperate : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：import, modify, move, duplicate, rename, get_info, create_folder, reload, select, ping, select_depends, select_usage等", false),
                new MethodKey("path", "资产路径，Unity标准格式：Assets/Folder/File.extension", false),
                new MethodKey("properties", "资产属性字典，用于设置资产的各种属性", true),
                new MethodKey("destination", "目标路径（移动/复制时使用）", true),
                new MethodKey("force", "是否强制执行操作（覆盖现有文件等）", true),
                new MethodKey("refresh_type", "刷新类型：all(全部), assets(仅资产), scripts(仅脚本)，默认all", true),
                new MethodKey("save_before_refresh", "刷新前是否保存所有资产，默认true", true),
                new MethodKey("include_indirect", "是否包含间接依赖/引用，默认false", true),
                new MethodKey("max_results", "最大结果数量，默认100", true)
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
                    .Leaf("import", ReimportAsset)
                    .Leaf("refresh", RefreshProject)
                    .Leaf("modify", ModifyAsset)
                    .Leaf("duplicate", DuplicateAsset)
                    .Leaf("move", MoveOrRenameAsset)
                    .Leaf("rename", MoveOrRenameAsset)
                    .Leaf("get_info", GetAssetInfo)
                    .Leaf("create_folder", CreateFolder)
                    .Leaf("select", SelectAsset)
                    .Leaf("ping", PingAsset)
                    .Leaf("select_depends", SelectDependencies)
                    .Leaf("select_usage", SelectUsages)
                .Build();
        }

        private object ReimportAsset(JObject args)
        {
            string path = args["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for reimport.");
            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Asset not found at path: {fullPath}");

            try
            {
                // TODO: Apply importer properties before reimporting?
                // This is complex as it requires getting the AssetImporter, casting it,
                // applying properties via reflection or specific methods, saving, then reimporting.
                JObject properties = args["properties"] as JObject;
                if (properties != null && properties.HasValues)
                {
                    Debug.LogWarning(
                        "[ManageAsset.Reimport] Modifying importer properties before reimport is not fully implemented yet."
                    );
                    // AssetImporter importer = AssetImporter.GetAtPath(fullPath);
                    // if (importer != null) { /* Apply properties */ AssetDatabase.WriteImportSettingsIfDirty(fullPath); }
                }

                AssetDatabase.ImportAsset(fullPath, ImportAssetOptions.ForceUpdate);
                // AssetDatabase.Refresh(); // Usually ImportAsset handles refresh
                return Response.Success($"Asset '{fullPath}' reimported.", GetAssetData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to reimport asset '{fullPath}': {e.Message}");
            }
        }



        private object CreateFolder(JObject args)
        {
            string path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for create_folder.");
            string fullPath = SanitizeAssetPath(path);
            string parentDir = Path.GetDirectoryName(fullPath);
            string folderName = Path.GetFileName(fullPath);

            if (AssetExists(fullPath))
            {
                // Check if it's actually a folder already
                if (AssetDatabase.IsValidFolder(fullPath))
                {
                    return Response.Success(
                        $"Folder already exists at path: {fullPath}",
                        GetAssetData(fullPath)
                    );
                }
                else
                {
                    return Response.Error(
                        $"An asset (not a folder) already exists at path: {fullPath}"
                    );
                }
            }

            try
            {
                // Ensure parent exists
                if (!string.IsNullOrEmpty(parentDir) && !AssetDatabase.IsValidFolder(parentDir))
                {
                    // Recursively create parent folders if needed (AssetDatabase handles this internally)
                    // Or we can do it manually: Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), parentDir)); AssetDatabase.Refresh();
                }

                string guid = AssetDatabase.CreateFolder(parentDir, folderName);
                if (string.IsNullOrEmpty(guid))
                {
                    return Response.Error(
                        $"Failed to create folder '{fullPath}'. Check logs and permissions."
                    );
                }

                // AssetDatabase.Refresh(); // CreateFolder usually handles refresh
                return Response.Success(
                    $"Folder '{fullPath}' created successfully.",
                    GetAssetData(fullPath)
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to create folder '{fullPath}': {e.Message}");
            }
        }

        private object ModifyAsset(JObject args)
        {
            string path = args["path"]?.ToString();
            JObject properties = args["properties"] as JObject;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for modify.");
            if (properties == null || !properties.HasValues)
                return Response.Error("'properties' are required for modify.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Asset not found at path: {fullPath}");

            try
            {
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    fullPath
                );
                if (asset == null)
                    return Response.Error($"Failed to load asset at path: {fullPath}");

                // Record the asset state for Undo before making any modifications
                Undo.RecordObject(asset, $"Modify Asset '{Path.GetFileName(fullPath)}'");

                bool modified = false; // Flag to track if any changes were made

                // --- NEW: Handle GameObject / Prefab Component Modification ---
                if (asset is GameObject gameObject)
                {
                    // Iterate through the properties JSON: keys are component names, values are properties objects for that component
                    foreach (var prop in properties.Properties())
                    {
                        string component_type = prop.Name; // e.g., "Collectible"
                        // Check if the value associated with the component name is actually an object containing properties
                        if (
                            prop.Value is JObject component_properties
                            && component_properties.HasValues
                        ) // e.g., {"bobSpeed": 2.0}
                        {
                            // Find the component on the GameObject using the name from the JSON key
                            // Using GetComponent(string) is convenient but might require exact type name or be ambiguous.
                            // Consider using FindType helper if needed for more complex scenarios.
                            Component targetComponent = gameObject.GetComponent(component_type);

                            if (targetComponent != null)
                            {
                                // Record the component state for Undo before modification
                                Undo.RecordObject(targetComponent, $"Modify Component '{component_type}' on '{gameObject.name}'");

                                // Apply the nested properties (e.g., bobSpeed) to the found component instance
                                // Use |= to ensure 'modified' becomes true if any component is successfully modified
                                modified |= ApplyObjectProperties(
                                    targetComponent,
                                    component_properties
                                );
                            }
                            else
                            {
                                // Log a warning if a specified component couldn't be found
                                Debug.LogWarning(
                                    $"[ManageAsset.ModifyAsset] Component '{component_type}' not found on GameObject '{gameObject.name}' in asset '{fullPath}'. Skipping modification for this component."
                                );
                            }
                        }
                        else
                        {
                            // Log a warning if the structure isn't {"component_type": {"prop": value}}
                            // We could potentially try to apply this property directly to the GameObject here if needed,
                            // but the primary goal is component modification.
                            Debug.LogWarning(
                                $"[ManageAsset.ModifyAsset] Property '{prop.Name}' for GameObject modification should have a JSON object value containing component properties. Value was: {prop.Value.Type}. Skipping."
                            );
                        }
                    }
                    // Note: 'modified' is now true if ANY component property was successfully changed.
                }
                // --- End NEW ---

                // --- Existing logic for other asset types (now as else-if) ---
                // Example: Modifying a Material
                else if (asset is Material material)
                {
                    // Material already recorded by the main Undo.RecordObject call above
                    // Apply properties directly to the material. If this modifies, it sets modified=true.
                    // Use |= in case the asset was already marked modified by previous logic (though unlikely here)
                    modified |= ApplyMaterialProperties(material, properties);
                }
                // Example: Modifying a ScriptableObject
                else if (asset is ScriptableObject so)
                {
                    // ScriptableObject already recorded by the main Undo.RecordObject call above
                    // Apply properties directly to the ScriptableObject.
                    modified |= ApplyObjectProperties(so, properties); // General helper
                }
                // Example: Modifying TextureImporter settings
                else if (asset is Texture)
                {
                    AssetImporter importer = AssetImporter.GetAtPath(fullPath);
                    if (importer is TextureImporter textureImporter)
                    {
                        // Record the importer state for Undo before modification
                        Undo.RecordObject(textureImporter, $"Modify Texture Import Settings '{Path.GetFileName(fullPath)}'");

                        bool importerModified = ApplyObjectProperties(textureImporter, properties);
                        if (importerModified)
                        {
                            // Importer settings need saving and reimporting
                            AssetDatabase.WriteImportSettingsIfDirty(fullPath);
                            AssetDatabase.ImportAsset(fullPath, ImportAssetOptions.ForceUpdate); // Reimport to apply changes
                            modified = true; // Mark overall operation as modified
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Could not get TextureImporter for {fullPath}.");
                    }
                }
                // TODO: Add modification logic for other common asset types (Models, AudioClips importers, etc.)
                else // Fallback for other asset types OR direct properties on non-GameObject assets
                {
                    // This block handles non-GameObject/Material/ScriptableObject/Texture assets.
                    // Asset already recorded by the main Undo.RecordObject call above
                    // Attempts to apply properties directly to the asset itself.
                    Debug.LogWarning(
                        $"[ManageAsset.ModifyAsset] Asset type '{asset.GetType().Name}' at '{fullPath}' is not explicitly handled for component modification. Attempting generic property setting on the asset itself."
                    );
                    modified |= ApplyObjectProperties(asset, properties);
                }
                // --- End Existing Logic ---

                // Check if any modification happened (either component or direct asset modification)
                if (modified)
                {
                    // Mark the asset as dirty (important for prefabs/SOs) so Unity knows to save it.
                    EditorUtility.SetDirty(asset);
                    // Save all modified assets to disk.
                    AssetDatabase.SaveAssets();
                    // Refresh might be needed in some edge cases, but SaveAssets usually covers it.
                    // AssetDatabase.Refresh();
                    return Response.Success(
                        $"Asset '{fullPath}' modified successfully.",
                        GetAssetData(fullPath)
                    );
                }
                else
                {
                    // If no changes were made (e.g., component not found, property names incorrect, value unchanged), return a success message indicating nothing changed.
                    return Response.Success(
                        $"No applicable or modifiable properties found for asset '{fullPath}'. Check component names, property names, and values.",
                        GetAssetData(fullPath)
                    );
                    // Previous message: return Response.Success($"No applicable properties found to modify for asset '{fullPath}'.", GetAssetData(fullPath));
                }
            }
            catch (Exception e)
            {
                // Log the detailed error internally
                Debug.LogError($"[ManageAsset] Action 'modify' failed for path '{path}': {e}");
                // Return a user-friendly error message
                return Response.Error($"Failed to modify asset '{fullPath}': {e.Message}");
            }
        }



        private object DuplicateAsset(JObject args)
        {
            string path = args["path"]?.ToString();
            string destinationPath = args["destination"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for duplicate.");

            string sourcePath = SanitizeAssetPath(path);
            if (!AssetExists(sourcePath))
                return Response.Error($"Source asset not found at path: {sourcePath}");

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
                    return Response.Error($"Asset already exists at destination path: {destPath}");
                // Ensure destination directory exists
                EnsureDirectoryExists(Path.GetDirectoryName(destPath));
            }

            try
            {
                bool success = AssetDatabase.CopyAsset(sourcePath, destPath);
                if (success)
                {
                    // AssetDatabase.Refresh();
                    return Response.Success(
                        $"Asset '{sourcePath}' duplicated to '{destPath}'.",
                        GetAssetData(destPath)
                    );
                }
                else
                {
                    return Response.Error(
                        $"Failed to duplicate asset from '{sourcePath}' to '{destPath}'."
                    );
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error duplicating asset '{sourcePath}': {e.Message}");
            }
        }

        private object MoveOrRenameAsset(JObject args)
        {
            string path = args["path"]?.ToString();
            string destinationPath = args["destination"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for move/rename.");
            if (string.IsNullOrEmpty(destinationPath))
                return Response.Error("'destination' path is required for move/rename.");

            string sourcePath = SanitizeAssetPath(path);
            string destPath = SanitizeAssetPath(destinationPath);

            if (!AssetExists(sourcePath))
                return Response.Error($"Source asset not found at path: {sourcePath}");
            if (AssetExists(destPath))
                return Response.Error(
                    $"An asset already exists at the destination path: {destPath}"
                );

            // Ensure destination directory exists
            EnsureDirectoryExists(Path.GetDirectoryName(destPath));

            try
            {
                // Validate will return an error string if failed, null if successful
                string error = AssetDatabase.ValidateMoveAsset(sourcePath, destPath);
                if (!string.IsNullOrEmpty(error))
                {
                    return Response.Error(
                        $"Failed to move/rename asset from '{sourcePath}' to '{destPath}': {error}"
                    );
                }

                string guid = AssetDatabase.MoveAsset(sourcePath, destPath);
                if (!string.IsNullOrEmpty(guid)) // MoveAsset returns the new GUID on success
                {
                    // AssetDatabase.Refresh(); // MoveAsset usually handles refresh
                    return Response.Success(
                        $"Asset moved/renamed from '{sourcePath}' to '{destPath}'.",
                        GetAssetData(destPath)
                    );
                }
                else
                {
                    // This case might not be reachable if ValidateMoveAsset passes, but good to have
                    return Response.Error(
                        $"MoveAsset call failed unexpectedly for '{sourcePath}' to '{destPath}'."
                    );
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error moving/renaming asset '{sourcePath}': {e.Message}");
            }
        }

        /// <summary>
        /// 选中指定路径的资源
        /// </summary>
        private object SelectAsset(JObject args)
        {
            string path = args["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for select.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Asset not found at path: {fullPath}");

            try
            {
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fullPath);
                if (asset == null)
                    return Response.Error($"Failed to load asset at path: {fullPath}");

                Selection.activeObject = asset;
                return Response.Success(
                    $"Asset '{fullPath}' selected successfully.",
                    GetAssetData(fullPath)
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Error selecting asset '{fullPath}': {e.Message}");
            }
        }

        /// <summary>
        /// 定位（ping）指定路径的资源，在Project窗口中高亮显示
        /// </summary>
        private object PingAsset(JObject args)
        {
            string path = args["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for ping.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Asset not found at path: {fullPath}");

            try
            {
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fullPath);
                if (asset == null)
                    return Response.Error($"Failed to load asset at path: {fullPath}");

                EditorGUIUtility.PingObject(asset);
                return Response.Success(
                    $"Asset '{fullPath}' pinged successfully.",
                    GetAssetData(fullPath)
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Error pinging asset '{fullPath}': {e.Message}");
            }
        }

        /// <summary>
        /// 选择指定资源的所有依赖项
        /// </summary>
        private object SelectDependencies(JObject args)
        {
            string path = args["path"]?.ToString();
            bool includeIndirect = args["include_indirect"]?.ToObject<bool?>() ?? false;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for select_depends.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Asset not found at path: {fullPath}");

            try
            {
                string[] dependencies = AssetDatabase.GetDependencies(fullPath, includeIndirect);
                List<UnityEngine.Object> dependencyObjects = new List<UnityEngine.Object>();
                List<object> dependencyData = new List<object>();

                foreach (string depPath in dependencies)
                {
                    if (depPath == fullPath) continue; // 排除自身

                    UnityEngine.Object depAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(depPath);
                    if (depAsset != null)
                    {
                        dependencyObjects.Add(depAsset);
                        dependencyData.Add(GetAssetData(depPath));
                    }
                }

                // 选中所有依赖项
                Selection.objects = dependencyObjects.ToArray();

                return Response.Success(
                    $"Selected {dependencyObjects.Count} dependencies for asset '{fullPath}' (indirect: {includeIndirect}).",
                    new
                    {
                        sourceAsset = GetAssetData(fullPath),
                        dependencyCount = dependencyObjects.Count,
                        includeIndirect = includeIndirect,
                        dependencies = dependencyData
                    }
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Error selecting dependencies for asset '{fullPath}': {e.Message}");
            }
        }

        /// <summary>
        /// 查询并选择引用指定资源的所有资源（优化版本）
        /// </summary>
        private object SelectUsages(JObject args)
        {
            string path = args["path"]?.ToString();
            bool includeIndirect = args["include_indirect"]?.ToObject<bool?>() ?? false;
            int maxResults = args["max_results"]?.ToObject<int?>() ?? 100; // 限制结果数量

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for select_usage.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Asset not found at path: {fullPath}");

            try
            {
                var startTime = System.DateTime.Now;

                // 获取资源的GUID
                string targetGuid = AssetDatabase.AssetPathToGUID(fullPath);
                if (string.IsNullOrEmpty(targetGuid))
                    return Response.Error($"Could not get GUID for asset: {fullPath}");

                List<string> referencingPaths = new List<string>();

                // 方法1: 使用Unity内置的引用查找（更高效）
                if (TryFindReferencesUsingBuiltinAPI(fullPath, targetGuid, referencingPaths, maxResults))
                {
                    LogInfo($"[SelectUsages] Used builtin API to find references");
                }
                else
                {
                    // 方法2: 优化的手动查找（仅查找常见资源类型）
                    FindReferencesOptimized(fullPath, targetGuid, referencingPaths, includeIndirect, maxResults);
                }

                List<UnityEngine.Object> referencingObjects = new List<UnityEngine.Object>();
                List<object> referencingData = new List<object>();

                foreach (string refPath in referencingPaths.Take(maxResults))
                {
                    UnityEngine.Object refAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(refPath);
                    if (refAsset != null)
                    {
                        referencingObjects.Add(refAsset);
                        referencingData.Add(GetAssetData(refPath));
                    }
                }

                // 选中所有引用此资源的资源
                Selection.objects = referencingObjects.ToArray();

                var duration = System.DateTime.Now - startTime;
                string message = referencingPaths.Count >= maxResults
                    ? $"Found {referencingPaths.Count}+ references (showing first {referencingObjects.Count}) for '{fullPath}' in {duration.TotalMilliseconds:F0}ms"
                    : $"Selected {referencingObjects.Count} assets that reference '{fullPath}' in {duration.TotalMilliseconds:F0}ms";

                return Response.Success(message, new
                {
                    targetAsset = GetAssetData(fullPath),
                    referencingCount = referencingObjects.Count,
                    totalFound = referencingPaths.Count,
                    maxResults = maxResults,
                    includeIndirect = includeIndirect,
                    searchDurationMs = duration.TotalMilliseconds,
                    referencingAssets = referencingData
                });
            }
            catch (Exception e)
            {
                return Response.Error($"Error finding usages for asset '{fullPath}': {e.Message}");
            }
        }

        /// <summary>
        /// 尝试使用Unity内置API查找引用（如果可用）
        /// </summary>
        private bool TryFindReferencesUsingBuiltinAPI(string targetPath, string targetGuid, List<string> referencingPaths, int maxResults)
        {
            try
            {
                // 尝试使用Unity 2020+的内置引用查找API
                var searchFilter = $"ref:{targetGuid}";
                string[] foundGuids = AssetDatabase.FindAssets(searchFilter);

                if (foundGuids != null && foundGuids.Length > 0)
                {
                    foreach (string guid in foundGuids.Take(maxResults))
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(assetPath) && assetPath != targetPath)
                        {
                            referencingPaths.Add(assetPath);
                        }
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogInfo($"[SelectUsages] Builtin API not available or failed: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 优化的手动引用查找（仅查找常见资源类型）
        /// </summary>
        private void FindReferencesOptimized(string targetPath, string targetGuid, List<string> referencingPaths, bool includeIndirect, int maxResults)
        {
            // 只查找常见的可能包含引用的资源类型，而不是所有资源
            string[] searchFilters = {
                "t:Scene",           // 场景文件
                "t:Prefab",          // 预制体
                "t:Material",        // 材质
                "t:AnimationClip",   // 动画片段
                "t:AnimatorController", // 动画控制器
                "t:ScriptableObject", // ScriptableObject
                "t:Shader"           // 着色器
            };

            var processedGuids = new HashSet<string>();

            foreach (string filter in searchFilters)
            {
                if (referencingPaths.Count >= maxResults) break;

                string[] guids = AssetDatabase.FindAssets(filter);

                foreach (string guid in guids)
                {
                    if (referencingPaths.Count >= maxResults) break;
                    if (processedGuids.Contains(guid)) continue;

                    processedGuids.Add(guid);
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                    if (string.IsNullOrEmpty(assetPath) || assetPath == targetPath) continue;

                    // 检查依赖关系
                    string[] dependencies = AssetDatabase.GetDependencies(assetPath, includeIndirect);
                    if (dependencies.Contains(targetPath))
                    {
                        referencingPaths.Add(assetPath);
                    }
                }
            }
        }

        private object GetAssetInfo(JObject args)
        {
            string path = args["path"]?.ToString();
            bool generatePreview = args["generate_preview"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for get_info.");
            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Asset not found at path: {fullPath}");

            try
            {
                return Response.Success(
                    "Asset info retrieved.",
                    GetAssetData(fullPath, generatePreview)
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting info for asset '{fullPath}': {e.Message}");
            }
        }

        /// <summary>
        /// 重载（刷新）Unity工程资产数据库
        /// </summary>
        private object RefreshProject(JObject args)
        {
            try
            {
                string refreshType = args["refresh_type"]?.ToString()?.ToLower() ?? "all";
                bool saveBeforeRefresh = args["save_before_refresh"]?.ToObject<bool?>() ?? true;
                string specificPath = args["path"]?.ToString();

                LogInfo($"[ProjectOperate] Starting project reload with type: {refreshType}");

                // 记录开始时间用于性能监控
                var startTime = System.DateTime.Now;

                // 保存所有待保存的资产
                if (saveBeforeRefresh)
                {
                    LogInfo("[ProjectOperate] Saving all modified assets before refresh...");
                    AssetDatabase.SaveAssets();
                }

                // 根据刷新类型执行不同的刷新操作
                switch (refreshType)
                {
                    case "all":
                        LogInfo("[ProjectOperate] Performing full project refresh...");
                        // 全面刷新：包括资产导入、脚本编译等
                        if (!string.IsNullOrEmpty(specificPath))
                        {
                            string sanitizedPath = SanitizeAssetPath(specificPath);
                            if (AssetExists(sanitizedPath))
                            {
                                AssetDatabase.ImportAsset(sanitizedPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
                                LogInfo($"[ProjectOperate] Refreshed specific path: {sanitizedPath}");
                            }
                            else
                            {
                                LogInfo($"[ProjectOperate] Specified path '{specificPath}' not found, performing full refresh");
                            }
                        }
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        break;

                    case "assets":
                        LogInfo("[ProjectOperate] Performing assets-only refresh...");
                        // 仅刷新资产，不触发脚本重新编译
                        if (!string.IsNullOrEmpty(specificPath))
                        {
                            string sanitizedPath = SanitizeAssetPath(specificPath);
                            if (AssetExists(sanitizedPath))
                            {
                                AssetDatabase.ImportAsset(sanitizedPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
                            }
                        }
                        AssetDatabase.Refresh(ImportAssetOptions.Default);
                        break;

                    case "scripts":
                        LogInfo("[ProjectOperate] Performing scripts-only refresh...");
                        // 主要针对脚本文件的重新编译
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                        // 强制请求脚本重新编译
                        EditorUtility.RequestScriptReload();
                        break;

                    default:
                        return Response.Error($"Unknown refresh_type: '{refreshType}'. Valid options are 'all', 'assets', or 'scripts'.");
                }

                // 计算耗时
                var duration = System.DateTime.Now - startTime;

                // 获取项目统计信息
                var projectStats = GetProjectStatistics();

                LogInfo($"[ProjectOperate] Project reload completed in {duration.TotalSeconds:F2} seconds");

                return Response.Success(
                    $"Project reloaded successfully with type '{refreshType}' in {duration.TotalSeconds:F2} seconds.",
                    new
                    {
                        refreshType = refreshType,
                        durationSeconds = duration.TotalSeconds,
                        savedBeforeRefresh = saveBeforeRefresh,
                        specificPath = specificPath,
                        timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        projectStatistics = projectStats
                    }
                );
            }
            catch (Exception e)
            {
                LogError($"[ProjectOperate] Failed to reload project: {e.Message}");
                return Response.Error($"Failed to reload project: {e.Message}");
            }
        }



        /// <summary>
        /// 获取项目统计信息
        /// </summary>
        private object GetProjectStatistics()
        {
            try
            {
                // 统计不同类型的资产数量
                string[] allAssetGUIDs = AssetDatabase.FindAssets("");

                int totalAssets = allAssetGUIDs.Length;
                int scriptCount = AssetDatabase.FindAssets("t:MonoScript").Length;
                int prefabCount = AssetDatabase.FindAssets("t:Prefab").Length;
                int materialCount = AssetDatabase.FindAssets("t:Material").Length;
                int textureCount = AssetDatabase.FindAssets("t:Texture2D").Length;
                int sceneCount = AssetDatabase.FindAssets("t:Scene").Length;
                int audioCount = AssetDatabase.FindAssets("t:AudioClip").Length;
                int modelCount = AssetDatabase.FindAssets("t:Model").Length;

                // 统计文件夹数量
                int folderCount = 0;
                foreach (string guid in allAssetGUIDs)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        folderCount++;
                    }
                }

                return new
                {
                    totalAssets = totalAssets,
                    breakdown = new
                    {
                        scripts = scriptCount,
                        prefabs = prefabCount,
                        materials = materialCount,
                        textures = textureCount,
                        scenes = sceneCount,
                        audioClips = audioCount,
                        models = modelCount,
                        folders = folderCount,
                        others = totalAssets - scriptCount - prefabCount - materialCount - textureCount - sceneCount - audioCount - modelCount - folderCount
                    },
                    lastRefreshTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ProjectOperate] Failed to get project statistics: {e.Message}");
                return new
                {
                    error = "Failed to gather project statistics",
                    message = e.Message
                };
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
        /// Checks if an asset exists at the given path (file or folder).
        /// </summary>
        private bool AssetExists(string sanitizedPath)
        {
            // AssetDatabase APIs are generally preferred over raw File/Directory checks for assets.
            // Check if it's a known asset GUID.
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sanitizedPath)))
            {
                return true;
            }
            // AssetPathToGUID might not work for newly created folders not yet refreshed.
            // Check directory explicitly for folders.
            if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath)))
            {
                // Check if it's considered a *valid* folder by Unity
                return AssetDatabase.IsValidFolder(sanitizedPath);
            }
            // Check file existence for non-folder assets.
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath)))
            {
                return true; // Assume if file exists, it's an asset or will be imported
            }

            return false;
            // Alternative: return !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sanitizedPath));
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
                AssetDatabase.Refresh(); // Let Unity know about the new folder
            }
        }

        /// <summary>
        /// Applies properties from JObject to a Material.
        /// </summary>
        private bool ApplyMaterialProperties(Material mat, JObject properties)
        {
            if (mat == null || properties == null)
                return false;
            bool modified = false;

            // Example: Set shader
            if (properties["shader"]?.Type == JTokenType.String)
            {
                Shader newShader = Shader.Find(properties["shader"].ToString());
                if (newShader != null && mat.shader != newShader)
                {
                    mat.shader = newShader;
                    modified = true;
                }
            }
            // Example: Set color property
            if (properties["color"] is JObject colorProps)
            {
                string propName = colorProps["name"]?.ToString() ?? "_Color"; // Default main color
                if (colorProps["value"] is JArray colArr && colArr.Count >= 3)
                {
                    try
                    {
                        Color newColor = new Color(
                            colArr[0].ToObject<float>(),
                            colArr[1].ToObject<float>(),
                            colArr[2].ToObject<float>(),
                            colArr.Count > 3 ? colArr[3].ToObject<float>() : 1.0f
                        );
                        if (mat.HasProperty(propName) && mat.GetColor(propName) != newColor)
                        {
                            mat.SetColor(propName, newColor);
                            modified = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"Error parsing color property '{propName}': {ex.Message}"
                        );
                    }
                }
            }
            // Example: Set float property
            if (properties["float"] is JObject floatProps)
            {
                string propName = floatProps["name"]?.ToString();
                if (
                    !string.IsNullOrEmpty(propName) && floatProps["value"]?.Type == JTokenType.Float
                    || floatProps["value"]?.Type == JTokenType.Integer
                )
                {
                    try
                    {
                        float newVal = floatProps["value"].ToObject<float>();
                        if (mat.HasProperty(propName) && mat.GetFloat(propName) != newVal)
                        {
                            mat.SetFloat(propName, newVal);
                            modified = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"Error parsing float property '{propName}': {ex.Message}"
                        );
                    }
                }
            }
            // Example: Set texture property
            if (properties["texture"] is JObject texProps)
            {
                string propName = texProps["name"]?.ToString() ?? "_MainTex"; // Default main texture
                string texPath = texProps["path"]?.ToString();
                if (!string.IsNullOrEmpty(texPath))
                {
                    Texture newTex = AssetDatabase.LoadAssetAtPath<Texture>(
                        SanitizeAssetPath(texPath)
                    );
                    if (
                        newTex != null
                        && mat.HasProperty(propName)
                        && mat.GetTexture(propName) != newTex
                    )
                    {
                        mat.SetTexture(propName, newTex);
                        modified = true;
                    }
                    else if (newTex == null)
                    {
                        Debug.LogWarning($"Texture not found at path: {texPath}");
                    }
                }
            }

            // Handle direct material properties (e.g., "_Color", "_MainTex", etc.)
            foreach (var prop in properties.Properties())
            {
                string propName = prop.Name;
                JToken propValue = prop.Value;

                // Skip properties already handled above
                if (propName == "shader" || propName == "color" || propName == "float" || propName == "texture")
                    continue;

                try
                {
                    if (mat.HasProperty(propName))
                    {
                        // Handle Color properties (like "_Color")
                        if (propValue is JObject colorObj &&
                            colorObj["r"] != null && colorObj["g"] != null && colorObj["b"] != null)
                        {
                            Color newColor = new Color(
                                colorObj["r"].ToObject<float>(),
                                colorObj["g"].ToObject<float>(),
                                colorObj["b"].ToObject<float>(),
                                colorObj["a"]?.ToObject<float>() ?? 1.0f
                            );
                            if (mat.GetColor(propName) != newColor)
                            {
                                mat.SetColor(propName, newColor);
                                modified = true;
                                Debug.Log($"[ApplyMaterialProperties] Set {propName} to {newColor}");
                            }
                        }
                        // Handle Vector4 properties
                        else if (propValue is JArray vecArray && vecArray.Count >= 4)
                        {
                            Vector4 newVector = new Vector4(
                                vecArray[0].ToObject<float>(),
                                vecArray[1].ToObject<float>(),
                                vecArray[2].ToObject<float>(),
                                vecArray[3].ToObject<float>()
                            );
                            if (mat.GetVector(propName) != newVector)
                            {
                                mat.SetVector(propName, newVector);
                                modified = true;
                            }
                        }
                        // Handle Float properties
                        else if (propValue.Type == JTokenType.Float || propValue.Type == JTokenType.Integer)
                        {
                            float newVal = propValue.ToObject<float>();
                            if (Math.Abs(mat.GetFloat(propName) - newVal) > 0.001f)
                            {
                                mat.SetFloat(propName, newVal);
                                modified = true;
                            }
                        }
                        // Handle Texture properties (string paths)
                        else if (propValue.Type == JTokenType.String)
                        {
                            string texPath = propValue.ToString();
                            if (!string.IsNullOrEmpty(texPath))
                            {
                                Texture newTex = AssetDatabase.LoadAssetAtPath<Texture>(SanitizeAssetPath(texPath));
                                if (newTex != null && mat.GetTexture(propName) != newTex)
                                {
                                    mat.SetTexture(propName, newTex);
                                    modified = true;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ApplyMaterialProperties] Error setting property '{propName}': {ex.Message}");
                }
            }

            // TODO: Add handlers for other property types (Keywords, RenderQueue, etc.)
            return modified;
        }

        /// <summary>
        /// Generic helper to set properties on any UnityEngine.Object using reflection.
        /// </summary>
        private bool ApplyObjectProperties(UnityEngine.Object target, JObject properties)
        {
            if (target == null || properties == null)
                return false;
            bool modified = false;
            Type type = target.GetType();

            foreach (var prop in properties.Properties())
            {
                string propName = prop.Name;
                JToken propValue = prop.Value;
                if (SetPropertyOrField(target, propName, propValue, type))
                {
                    modified = true;
                }
            }
            return modified;
        }

        /// <summary>
        /// Helper to set a property or field via reflection, handling basic types and Unity objects.
        /// </summary>
        private bool SetPropertyOrField(
            object target,
            string memberName,
            JToken value,
            Type type = null
        )
        {
            type = type ?? target.GetType();
            System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.IgnoreCase;

            try
            {
                System.Reflection.PropertyInfo propInfo = type.GetProperty(memberName, flags);
                if (propInfo != null && propInfo.CanWrite)
                {
                    object convertedValue = ConvertJTokenToType(value, propInfo.PropertyType);
                    if (
                        convertedValue != null
                        && !object.Equals(propInfo.GetValue(target), convertedValue)
                    )
                    {
                        propInfo.SetValue(target, convertedValue);
                        return true;
                    }
                }
                else
                {
                    System.Reflection.FieldInfo fieldInfo = type.GetField(memberName, flags);
                    if (fieldInfo != null)
                    {
                        object convertedValue = ConvertJTokenToType(value, fieldInfo.FieldType);
                        if (
                            convertedValue != null
                            && !object.Equals(fieldInfo.GetValue(target), convertedValue)
                        )
                        {
                            fieldInfo.SetValue(target, convertedValue);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[SetPropertyOrField] Failed to set '{memberName}' on {type.Name}: {ex.Message}"
                );
            }
            return false;
        }

        /// <summary>
        /// Simple JToken to Type conversion for common Unity types and primitives.
        /// </summary>
        private object ConvertJTokenToType(JToken token, Type targetType)
        {
            try
            {
                if (token == null || token.Type == JTokenType.Null)
                    return null;

                if (targetType == typeof(string))
                    return token.ToObject<string>();
                if (targetType == typeof(int))
                    return token.ToObject<int>();
                if (targetType == typeof(float))
                    return token.ToObject<float>();
                if (targetType == typeof(bool))
                    return token.ToObject<bool>();
                if (targetType == typeof(Vector2) && token is JArray arrV2 && arrV2.Count == 2)
                    return new Vector2(arrV2[0].ToObject<float>(), arrV2[1].ToObject<float>());
                if (targetType == typeof(Vector3) && token is JArray arrV3 && arrV3.Count == 3)
                    return new Vector3(
                        arrV3[0].ToObject<float>(),
                        arrV3[1].ToObject<float>(),
                        arrV3[2].ToObject<float>()
                    );
                if (targetType == typeof(Vector4) && token is JArray arrV4 && arrV4.Count == 4)
                    return new Vector4(
                        arrV4[0].ToObject<float>(),
                        arrV4[1].ToObject<float>(),
                        arrV4[2].ToObject<float>(),
                        arrV4[3].ToObject<float>()
                    );
                if (targetType == typeof(Quaternion) && token is JArray arrQ && arrQ.Count == 4)
                    return new Quaternion(
                        arrQ[0].ToObject<float>(),
                        arrQ[1].ToObject<float>(),
                        arrQ[2].ToObject<float>(),
                        arrQ[3].ToObject<float>()
                    );
                if (targetType == typeof(Color) && token is JArray arrC && arrC.Count >= 3) // Allow RGB or RGBA
                    return new Color(
                        arrC[0].ToObject<float>(),
                        arrC[1].ToObject<float>(),
                        arrC[2].ToObject<float>(),
                        arrC.Count > 3 ? arrC[3].ToObject<float>() : 1.0f
                    );
                if (targetType.IsEnum)
                    return Enum.Parse(targetType, token.ToString(), true); // Case-insensitive enum parsing

                // Handle loading Unity Objects (Materials, Textures, etc.) by path
                if (
                    typeof(UnityEngine.Object).IsAssignableFrom(targetType)
                    && token.Type == JTokenType.String
                )
                {
                    string assetPath = SanitizeAssetPath(token.ToString());
                    UnityEngine.Object loadedAsset = AssetDatabase.LoadAssetAtPath(
                        assetPath,
                        targetType
                    );
                    if (loadedAsset == null)
                    {
                        Debug.LogWarning(
                            $"[ConvertJTokenToType] Could not load asset of type {targetType.Name} from path: {assetPath}"
                        );
                    }
                    return loadedAsset;
                }

                // Fallback: Try direct conversion (might work for other simple value types)
                return token.ToObject(targetType);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[ConvertJTokenToType] Could not convert JToken '{token}' (type {token.Type}) to type '{targetType.Name}': {ex.Message}"
                );
                return null;
            }
        }

        /// <summary>
        /// Helper to find a Type by name, searching relevant assemblies.
        /// Needed for creating ScriptableObjects or finding component types by name.
        /// </summary>
        private Type FindType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            // Try direct lookup first (common Unity types often don't need assembly qualified name)
            var type =
                Type.GetType(typeName)
                ?? Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule")
                ?? Type.GetType($"UnityEngine.UI.{typeName}, UnityEngine.UI")
                ?? Type.GetType($"UnityEditor.{typeName}, UnityEditor.CoreModule");

            if (type != null)
                return type;

            // If not found, search loaded assemblies (slower but more robust for user scripts)
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Look for non-namespaced first
                type = assembly.GetType(typeName, false, true); // throwOnError=false, ignoreCase=true
                if (type != null)
                    return type;

                // Check common namespaces if simple name given
                type = assembly.GetType("UnityEngine." + typeName, false, true);
                if (type != null)
                    return type;
                type = assembly.GetType("UnityEditor." + typeName, false, true);
                if (type != null)
                    return type;
                // Add other likely namespaces if needed (e.g., specific plugins)
            }

            Debug.LogWarning($"[FindType] Type '{typeName}' not found in any loaded assembly.");
            return null; // Not found
        }

        // --- Data Serialization ---

        /// <summary>
        /// Creates a serializable representation of an asset.
        /// </summary>
        private object GetAssetData(string path, bool generatePreview = false)
        {
            if (string.IsNullOrEmpty(path) || !AssetExists(path))
                return null;

            string guid = AssetDatabase.AssetPathToGUID(path);
            Type assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            string previewBase64 = null;
            int previewWidth = 0;
            int previewHeight = 0;

            if (generatePreview && asset != null)
            {
                Texture2D preview = AssetPreview.GetAssetPreview(asset);

                if (preview != null)
                {
                    try
                    {
                        // Ensure texture is readable for EncodeToPNG
                        // Creating a temporary readable copy is safer
                        RenderTexture rt = RenderTexture.GetTemporary(
                            preview.width,
                            preview.height
                        );
                        Graphics.Blit(preview, rt);
                        RenderTexture previous = RenderTexture.active;
                        RenderTexture.active = rt;
                        Texture2D readablePreview = new Texture2D(preview.width, preview.height);
                        readablePreview.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                        readablePreview.Apply();
                        RenderTexture.active = previous;
                        RenderTexture.ReleaseTemporary(rt);

                        byte[] pngData = readablePreview.EncodeToPNG();
                        previewBase64 = Convert.ToBase64String(pngData);
                        previewWidth = readablePreview.width;
                        previewHeight = readablePreview.height;
                        UnityEngine.Object.DestroyImmediate(readablePreview); // Clean up temp texture
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"Failed to generate readable preview for '{path}': {ex.Message}. Preview might not be readable."
                        );
                        // Fallback: Try getting static preview if available?
                        // Texture2D staticPreview = AssetPreview.GetMiniThumbnail(asset);
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"Could not get asset preview for {path} (Type: {assetType?.Name}). Is it supported?"
                    );
                }
            }

            return new
            {
                path = path,
                guid = guid,
                assetType = assetType?.FullName ?? "Unknown",
                name = Path.GetFileNameWithoutExtension(path),
                fileName = Path.GetFileName(path),
                isFolder = AssetDatabase.IsValidFolder(path),
                instanceID = asset?.GetInstanceID() ?? 0,
                lastWriteTimeUtc = File.GetLastWriteTimeUtc(
                        Path.Combine(Directory.GetCurrentDirectory(), path)
                    )
                    .ToString("o"), // ISO 8601
                // --- Preview Data ---
                previewBase64 = previewBase64, // PNG data as Base64 string
                previewWidth = previewWidth,
                previewHeight = previewHeight,
                // TODO: Add more metadata? Importer settings? Dependencies?
            };
        }

    }
}


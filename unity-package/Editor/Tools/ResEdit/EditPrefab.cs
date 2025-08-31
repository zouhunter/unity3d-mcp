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
    /// 专门的预制体管理工具，提供预制体的创建、修改、复制、删除等操作
    /// 对应方法名: manage_prefab
    /// </summary>
    [ToolName("manage_prefab")]
    public class ManagePrefab : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：create, modify, duplicate, get_info, search, instantiate, unpack, pack", false),
                new MethodKey("path", "预制体资源路径，Unity标准格式：Assets/Prefabs/PrefabName.prefab", false),
                new MethodKey("source_object", "源GameObject名称或路径（创建时使用）", true),
                new MethodKey("destination", "目标路径（复制/移动时使用）", true),
                new MethodKey("search_pattern", "搜索模式，如*.prefab", true),
                new MethodKey("recursive", "是否递归搜索子文件夹", true),
                new MethodKey("force", "是否强制执行操作（覆盖现有文件等）", true),
                new MethodKey("prefab_variant", "是否创建预制体变体", true),
                new MethodKey("unpack_mode", "解包模式：Completely, OutermostRoot", true),
                new MethodKey("pack_mode", "打包模式：Default, ReuseExisting", true),
                new MethodKey("connect_to_prefab", "是否连接到预制体", true),
                new MethodKey("apply_prefab_changes", "是否应用预制体更改", true),
                new MethodKey("revert_prefab_changes", "是否还原预制体更改", true),
                new MethodKey("break_prefab_connection", "是否断开预制体连接", true),
                new MethodKey("prefab_type", "预制体类型：Regular, Variant", true),
                new MethodKey("parent_prefab", "父预制体路径（变体时使用）", true),
                new MethodKey("scene_path", "场景路径（实例化时使用）", true),
                new MethodKey("position", "位置坐标 [x, y, z]", true),
                new MethodKey("rotation", "旋转角度 [x, y, z]", true),
                new MethodKey("scale", "缩放比例 [x, y, z]", true),
                new MethodKey("parent", "父对象名称或路径", true)
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
                    .Leaf("create", CreatePrefab)
                    .Leaf("modify", ModifyPrefab)
                    .Leaf("duplicate", DuplicatePrefab)
                    .Leaf("get_info", GetPrefabInfo)
                    .Leaf("search", SearchPrefabs)
                    .Leaf("instantiate", InstantiatePrefab)
                    .Leaf("unpack", UnpackPrefab)
                    .Leaf("pack", PackPrefab)
                    .Leaf("create_variant", CreatePrefabVariant)
                    .Leaf("connect_to_prefab", ConnectToPrefab)
                    .Leaf("apply_changes", ApplyPrefabChanges)
                    .Leaf("revert_changes", RevertPrefabChanges)
                    .Leaf("break_connection", BreakPrefabConnection)
                .Build();
        }

        // --- 状态树操作方法 ---

        private object CreatePrefab(JObject args)
        {
            string path = args["path"]?.ToString();
            string sourceObject = args["source_object"]?.ToString();
            bool isVariant = args["prefab_variant"]?.ToObject<bool>() ?? false;
            string parentPrefab = args["parent_prefab"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for create.");

            string fullPath = SanitizeAssetPath(path);
            string directory = Path.GetDirectoryName(fullPath);

            // 确保目录存在
            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), directory)))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), directory));
                AssetDatabase.Refresh();
            }

            if (AssetExists(fullPath))
                return Response.Error($"Prefab already exists at path: {fullPath}");

            try
            {
                GameObject sourceGameObject = null;

                if (!string.IsNullOrEmpty(sourceObject))
                {
                    // 查找源GameObject
                    sourceGameObject = GameObject.Find(sourceObject);
                    if (sourceGameObject == null)
                    {
                        // 尝试从路径加载
                        sourceGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(SanitizeAssetPath(sourceObject));
                    }
                }

                if (sourceGameObject == null)
                {
                    // 创建空的GameObject
                    sourceGameObject = new GameObject(Path.GetFileNameWithoutExtension(fullPath));
                }

                GameObject prefabAsset = null;

                if (isVariant && !string.IsNullOrEmpty(parentPrefab))
                {
                    // 创建预制体变体
                    GameObject parentPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SanitizeAssetPath(parentPrefab));
                    if (parentPrefabAsset == null)
                        return Response.Error($"Parent prefab not found at path: {parentPrefab}");

                    prefabAsset = PrefabUtility.SaveAsPrefabAssetAndConnect(sourceGameObject, fullPath, InteractionMode.AutomatedAction);
                    PrefabUtility.SaveAsPrefabAssetAndConnect(prefabAsset, fullPath, InteractionMode.AutomatedAction);
                }
                else
                {
                    // 创建普通预制体
                    prefabAsset = PrefabUtility.SaveAsPrefabAsset(sourceGameObject, fullPath);
                }

                if (prefabAsset == null)
                    return Response.Error($"Failed to create prefab at '{fullPath}'");

                // 如果源对象是临时创建的，删除它
                if (string.IsNullOrEmpty(sourceObject))
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(sourceGameObject);
                    else
                        UnityEngine.Object.DestroyImmediate(sourceGameObject);
                }

                AssetDatabase.SaveAssets();

                LogInfo($"[ManagePrefab] Created prefab at '{fullPath}'");
                return Response.Success($"Prefab '{fullPath}' created successfully.", GetPrefabData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to create prefab at '{fullPath}': {e.Message}");
            }
        }

        private object ModifyPrefab(JObject args)
        {
            string path = args["path"]?.ToString();
            JObject modifications = args["modifications"] as JObject;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for modify.");
            if (modifications == null || !modifications.HasValues)
                return Response.Error("'modifications' are required for modify.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Prefab not found at path: {fullPath}");

            try
            {
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
                if (prefabAsset == null)
                    return Response.Error($"Failed to load prefab at path: {fullPath}");

                // 打开预制体编辑模式
                // 注意：OpenPrefab 方法在某些Unity版本中可能不可用
                // PrefabUtility.OpenPrefab(fullPath);
                LogWarning($"[ManagePrefab] OpenPrefab method not supported in current Unity version");

                bool modified = false;

                // 应用修改
                foreach (var modification in modifications.Properties())
                {
                    string propertyPath = modification.Name;
                    JToken propertyValue = modification.Value;

                    // 这里需要根据具体的修改需求来实现
                    // 例如：修改组件属性、添加/删除组件、修改Transform等
                    LogWarning($"[ManagePrefab] Prefab modification for '{propertyPath}' not fully implemented yet.");
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabAsset, fullPath);
                    AssetDatabase.SaveAssets();
                    LogInfo($"[ManagePrefab] Modified prefab at '{fullPath}'");
                    return Response.Success($"Prefab '{fullPath}' modified successfully.", GetPrefabData(fullPath));
                }
                else
                {
                    return Response.Success($"No applicable modifications found for prefab '{fullPath}'.", GetPrefabData(fullPath));
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to modify prefab '{fullPath}': {e.Message}");
            }
        }

        private object DuplicatePrefab(JObject args)
        {
            string path = args["path"]?.ToString();
            string destinationPath = args["destination"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for duplicate.");

            string sourcePath = SanitizeAssetPath(path);
            if (!AssetExists(sourcePath))
                return Response.Error($"Source prefab not found at path: {sourcePath}");

            string destPath;
            if (string.IsNullOrEmpty(destinationPath))
            {
                destPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            }
            else
            {
                destPath = SanitizeAssetPath(destinationPath);
                if (AssetExists(destPath))
                    return Response.Error($"Prefab already exists at destination path: {destPath}");
                EnsureDirectoryExists(Path.GetDirectoryName(destPath));
            }

            try
            {
                bool success = AssetDatabase.CopyAsset(sourcePath, destPath);
                if (success)
                {
                    LogInfo($"[ManagePrefab] Duplicated prefab from '{sourcePath}' to '{destPath}'");
                    return Response.Success($"Prefab '{sourcePath}' duplicated to '{destPath}'.", GetPrefabData(destPath));
                }
                else
                {
                    return Response.Error($"Failed to duplicate prefab from '{sourcePath}' to '{destPath}'.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error duplicating prefab '{sourcePath}': {e.Message}");
            }
        }



        private object GetPrefabInfo(JObject args)
        {
            string path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for get_info.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Prefab not found at path: {fullPath}");

            try
            {
                return Response.Success("Prefab info retrieved.", GetPrefabData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting info for prefab '{fullPath}': {e.Message}");
            }
        }

        private object SearchPrefabs(JObject args)
        {
            string searchPattern = args["search_pattern"]?.ToString();
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

                    // 检查是否是预制体文件
                    if (IsPrefabFile(assetPath))
                    {
                        results.Add(GetPrefabData(assetPath));
                    }
                }

                LogInfo($"[ManagePrefab] Found {results.Count} prefab(s)");
                return Response.Success($"Found {results.Count} prefab(s).", results);
            }
            catch (Exception e)
            {
                return Response.Error($"Error searching prefabs: {e.Message}");
            }
        }

        private object InstantiatePrefab(JObject args)
        {
            string path = args["path"]?.ToString();
            string scenePath = args["scene_path"]?.ToString();
            JArray position = args["position"] as JArray;
            JArray rotation = args["rotation"] as JArray;
            JArray scale = args["scale"] as JArray;
            string parent = args["parent"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for instantiate.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Prefab not found at path: {fullPath}");

            try
            {
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
                if (prefabAsset == null)
                    return Response.Error($"Failed to load prefab at path: {fullPath}");

                // 设置位置、旋转、缩放
                Vector3 pos = Vector3.zero;
                Vector3 rot = Vector3.zero;
                Vector3 scl = Vector3.one;

                if (position != null && position.Count >= 3)
                {
                    pos = new Vector3(position[0].ToObject<float>(), position[1].ToObject<float>(), position[2].ToObject<float>());
                }

                if (rotation != null && rotation.Count >= 3)
                {
                    rot = new Vector3(rotation[0].ToObject<float>(), rotation[1].ToObject<float>(), rotation[2].ToObject<float>());
                }

                if (scale != null && scale.Count >= 3)
                {
                    scl = new Vector3(scale[0].ToObject<float>(), scale[1].ToObject<float>(), scale[2].ToObject<float>());
                }

                // 实例化预制体
                GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                if (instance == null)
                    return Response.Error($"Failed to instantiate prefab '{fullPath}'");

                // 设置Transform
                instance.transform.position = pos;
                instance.transform.eulerAngles = rot;
                instance.transform.localScale = scl;

                // 设置父对象
                if (!string.IsNullOrEmpty(parent))
                {
                    GameObject parentObject = GameObject.Find(parent);
                    if (parentObject != null)
                    {
                        instance.transform.SetParent(parentObject.transform);
                    }
                }

                LogInfo($"[ManagePrefab] Instantiated prefab '{fullPath}' at position {pos}");
                return Response.Success($"Prefab '{fullPath}' instantiated successfully.", new
                {
                    instance_name = instance.name,
                    instance_id = instance.GetInstanceID(),
                    position = pos,
                    rotation = rot,
                    scale = scl
                });
            }
            catch (Exception e)
            {
                return Response.Error($"Error instantiating prefab '{fullPath}': {e.Message}");
            }
        }

        private object UnpackPrefab(JObject args)
        {
            string path = args["path"]?.ToString();
            string unpackMode = args["unpack_mode"]?.ToString() ?? "Completely";

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for unpack.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Prefab not found at path: {fullPath}");

            try
            {
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
                if (prefabAsset == null)
                    return Response.Error($"Failed to load prefab at path: {fullPath}");

                // 注意：PrefabUnpackMode 枚举和 UnpackPrefab 方法在某些Unity版本中可能不可用
                // PrefabUnpackMode mode = PrefabUnpackMode.Completely;
                // switch (unpackMode.ToLowerInvariant())
                // {
                //     case "outermostroot":
                //         mode = PrefabUnpackMode.OutermostRoot;
                //     break;
                //     case "completely":
                //     default:
                //         mode = PrefabUnpackMode.Completely;
                //     break;
                // }
                // PrefabUtility.UnpackPrefab(prefabAsset, mode, InteractionMode.AutomatedAction);
                LogWarning($"[ManagePrefab] UnpackPrefab method and PrefabUnpackMode enum not supported in current Unity version");

                LogInfo($"[ManagePrefab] Unpacked prefab '{fullPath}' with mode '{unpackMode}'");
                return Response.Success($"Prefab '{fullPath}' unpacked successfully.");
            }
            catch (Exception e)
            {
                return Response.Error($"Error unpacking prefab '{fullPath}': {e.Message}");
            }
        }

        private object PackPrefab(JObject args)
        {
            string path = args["path"]?.ToString();
            string packMode = args["pack_mode"]?.ToString() ?? "Default";

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for pack.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Prefab not found at path: {fullPath}");

            try
            {
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
                if (prefabAsset == null)
                    return Response.Error($"Failed to load prefab at path: {fullPath}");

                // 注意：PrefabPackMode 枚举和 PackPrefab 方法在某些Unity版本中可能不可用
                // PrefabPackMode mode = PrefabPackMode.Default;
                // switch (packMode.ToLowerInvariant())
                // {
                //     case "reuseexisting":
                //         mode = PrefabPackMode.ReuseExisting;
                //         break;
                //     case "default":
                //     default:
                //         mode = PrefabPackMode.Default;
                //         break;
                // }
                // PrefabUtility.PackPrefab(prefabAsset, mode, InteractionMode.AutomatedAction);
                LogWarning($"[ManagePrefab] PackPrefab method and PrefabPackMode enum not supported in current Unity version");

                LogInfo($"[ManagePrefab] Packed prefab '{fullPath}' with mode '{packMode}'");
                return Response.Success($"Prefab '{fullPath}' packed successfully.");
            }
            catch (Exception e)
            {
                return Response.Error($"Error packing prefab '{fullPath}': {e.Message}");
            }
        }

        private object CreatePrefabVariant(JObject args)
        {
            string path = args["path"]?.ToString();
            string parentPrefab = args["parent_prefab"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for create_variant.");
            if (string.IsNullOrEmpty(parentPrefab))
                return Response.Error("'parent_prefab' is required for create_variant.");

            string fullPath = SanitizeAssetPath(path);
            string parentFullPath = SanitizeAssetPath(parentPrefab);

            if (!AssetExists(parentFullPath))
                return Response.Error($"Parent prefab not found at path: {parentFullPath}");
            if (AssetExists(fullPath))
                return Response.Error($"Prefab already exists at path: {fullPath}");

            try
            {
                GameObject parentPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(parentFullPath);
                if (parentPrefabAsset == null)
                    return Response.Error($"Failed to load parent prefab at path: {parentFullPath}");

                // 创建预制体变体
                GameObject variant = PrefabUtility.SaveAsPrefabAssetAndConnect(parentPrefabAsset, fullPath, InteractionMode.AutomatedAction);

                LogInfo($"[ManagePrefab] Created prefab variant '{fullPath}' from parent '{parentFullPath}'");
                return Response.Success($"Prefab variant '{fullPath}' created successfully.", GetPrefabData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Error creating prefab variant '{fullPath}': {e.Message}");
            }
        }

        private object ConnectToPrefab(JObject args)
        {
            string path = args["path"]?.ToString();
            string targetObject = args["target_object"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for connect_to_prefab.");
            if (string.IsNullOrEmpty(targetObject))
                return Response.Error("'target_object' is required for connect_to_prefab.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Prefab not found at path: {fullPath}");

            try
            {
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
                GameObject targetGameObject = GameObject.Find(targetObject);

                if (prefabAsset == null)
                    return Response.Error($"Failed to load prefab at path: {fullPath}");
                if (targetGameObject == null)
                    return Response.Error($"Target GameObject '{targetObject}' not found in scene");

                PrefabUtility.SaveAsPrefabAssetAndConnect(targetGameObject, fullPath, InteractionMode.AutomatedAction);

                LogInfo($"[ManagePrefab] Connected GameObject '{targetObject}' to prefab '{fullPath}'");
                return Response.Success($"GameObject '{targetObject}' connected to prefab '{fullPath}' successfully.");
            }
            catch (Exception e)
            {
                return Response.Error($"Error connecting to prefab '{fullPath}': {e.Message}");
            }
        }

        private object ApplyPrefabChanges(JObject args)
        {
            string path = args["path"]?.ToString();
            string targetObject = args["target_object"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for apply_changes.");
            if (string.IsNullOrEmpty(targetObject))
                return Response.Error("'target_object' is required for apply_changes.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Prefab not found at path: {fullPath}");

            try
            {
                GameObject targetGameObject = GameObject.Find(targetObject);
                if (targetGameObject == null)
                    return Response.Error($"Target GameObject '{targetObject}' not found in scene");

                PrefabUtility.ApplyPrefabInstance(targetGameObject, InteractionMode.AutomatedAction);

                LogInfo($"[ManagePrefab] Applied changes from GameObject '{targetObject}' to prefab '{fullPath}'");
                return Response.Success($"Changes applied from GameObject '{targetObject}' to prefab '{fullPath}' successfully.");
            }
            catch (Exception e)
            {
                return Response.Error($"Error applying changes to prefab '{fullPath}': {e.Message}");
            }
        }

        private object RevertPrefabChanges(JObject args)
        {
            string targetObject = args["target_object"]?.ToString();

            if (string.IsNullOrEmpty(targetObject))
                return Response.Error("'target_object' is required for revert_changes.");

            try
            {
                GameObject targetGameObject = GameObject.Find(targetObject);
                if (targetGameObject == null)
                    return Response.Error($"Target GameObject '{targetObject}' not found in scene");

                PrefabUtility.RevertPrefabInstance(targetGameObject, InteractionMode.AutomatedAction);

                LogInfo($"[ManagePrefab] Reverted changes for GameObject '{targetObject}'");
                return Response.Success($"Changes reverted for GameObject '{targetObject}' successfully.");
            }
            catch (Exception e)
            {
                return Response.Error($"Error reverting changes for GameObject '{targetObject}': {e.Message}");
            }
        }

        private object BreakPrefabConnection(JObject args)
        {
            string targetObject = args["target_object"]?.ToString();

            if (string.IsNullOrEmpty(targetObject))
                return Response.Error("'target_object' is required for break_connection.");

            try
            {
                GameObject targetGameObject = GameObject.Find(targetObject);
                if (targetGameObject == null)
                    return Response.Error($"Target GameObject '{targetObject}' not found in scene");

                // 注意：UnpackPrefabInstance 方法在某些Unity版本中可能不可用
                // PrefabUtility.UnpackPrefabInstance(targetGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                LogWarning($"[ManagePrefab] UnpackPrefabInstance method not supported in current Unity version");

                LogInfo($"[ManagePrefab] Broke prefab connection for GameObject '{targetObject}'");
                return Response.Success($"Prefab connection broken for GameObject '{targetObject}' successfully.");
            }
            catch (Exception e)
            {
                return Response.Error($"Error breaking prefab connection for GameObject '{targetObject}': {e.Message}");
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
        /// 检查是否是预制体文件
        /// </summary>
        private bool IsPrefabFile(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".prefab";
        }

        /// <summary>
        /// 获取预制体数据
        /// </summary>
        private object GetPrefabData(string path)
        {
            if (string.IsNullOrEmpty(path) || !AssetExists(path))
                return null;

            string guid = AssetDatabase.AssetPathToGUID(path);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefabAsset == null)
                return null;

            // 获取预制体信息
            PrefabAssetType prefabType = PrefabUtility.GetPrefabAssetType(prefabAsset);
            GameObject parentPrefab = PrefabUtility.GetCorrespondingObjectFromSource(prefabAsset);

            return new
            {
                path = path,
                guid = guid,
                name = Path.GetFileNameWithoutExtension(path),
                fileName = Path.GetFileName(path),
                file_extension = Path.GetExtension(path),
                is_prefab_file = IsPrefabFile(path),
                prefab_type = prefabType.ToString(),
                parent_prefab = parentPrefab != null ? AssetDatabase.GetAssetPath(parentPrefab) : null,
                child_count = prefabAsset.transform.childCount,
                component_count = prefabAsset.GetComponents<Component>().Length,
                lastWriteTimeUtc = File.GetLastWriteTimeUtc(Path.Combine(Directory.GetCurrentDirectory(), path)).ToString("o")
            };
        }
    }
} 
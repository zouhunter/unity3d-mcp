using System;
using System.Collections.Generic;
using System.Linq; // Added for .Take()
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models; // For Response class

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles GameObject deletion operations.
    /// Scene hierarchy and creation operations are handled by ManageHierarchy.
    /// Component operations are handled by ManageComponent.
    /// 对应方法名: object_delete
    /// </summary>
    [ToolName("object_delete")]
    public class ObjectDelete : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：delete(删除单个), deletes(删除多个)", false),
                new MethodKey("target", "目标GameObject标识符（名称、ID或路径）", false),
                new MethodKey("search_method", "搜索方法：by_name, by_id, by_tag, by_layer, by_component, by_path, by_guid等", false),
                new MethodKey("targets", "多个目标GameObject标识符列表", true),
                new MethodKey("find_all", "是否查找所有匹配项", true)
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Key("delete")
                        .Key("search_method")
                            .Leaf("by_name", HandleDeleteByName)
                            .Leaf("by_id", HandleDeleteById)
                            .Leaf("by_tag", HandleDeleteByTag)
                            .Leaf("by_layer", HandleDeleteByLayer)
                            .Leaf("by_component", HandleDeleteByComponent)
                            .Leaf("by_path", HandleDeleteByPath)
                            .Leaf("by_guid", HandleDeleteByGuid)
                            .Leaf("default", HandleDeleteDefault)
                    .Key("deletes")
                        .Key("search_method")
                            .Leaf("by_name", HandleDeletesByName)
                            .Leaf("by_id", HandleDeletesById)
                            .Leaf("by_tag", HandleDeletesByTag)
                            .Leaf("by_layer", HandleDeletesByLayer)
                            .Leaf("by_component", HandleDeletesByComponent)
                            .Leaf("by_path", HandleDeletesByPath)
                            .Leaf("by_guid", HandleDeletesByGuid)
                            .Leaf("default", HandleDeletesDefault)
                .Build();
        }

        // --- Delete Single Object Handlers ---

        /// <summary>
        /// 按名称删除单个GameObject
        /// </summary>
        private object HandleDeleteByName(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required for by_name search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteSingleGameObject(targetToken, "by_name");
        }

        /// <summary>
        /// 按ID删除单个GameObject
        /// </summary>
        private object HandleDeleteById(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required for by_id search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteSingleGameObject(targetToken, "by_id");
        }

        /// <summary>
        /// 按标签删除单个GameObject
        /// </summary>
        private object HandleDeleteByTag(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required for by_tag search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteSingleGameObject(targetToken, "by_tag");
        }

        /// <summary>
        /// 按层级删除单个GameObject
        /// </summary>
        private object HandleDeleteByLayer(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required for by_layer search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteSingleGameObject(targetToken, "by_layer");
        }

        /// <summary>
        /// 按组件删除单个GameObject
        /// </summary>
        private object HandleDeleteByComponent(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required for by_component search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteSingleGameObject(targetToken, "by_component");
        }

        /// <summary>
        /// 按路径删除单个GameObject
        /// </summary>
        private object HandleDeleteByPath(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required for by_path search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteSingleGameObject(targetToken, "by_path");
        }

        /// <summary>
        /// 按GUID删除单个GameObject
        /// </summary>
        private object HandleDeleteByGuid(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required for by_guid search method.");
            }

            string guid = targetToken.ToString();
            if (string.IsNullOrEmpty(guid))
            {
                return Response.Error("GUID cannot be empty for by_guid search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteSingleGameObjectByGuid(guid);
        }

        /// <summary>
        /// 默认删除单个GameObject
        /// </summary>
        private object HandleDeleteDefault(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteSingleGameObject(targetToken, "by_id_or_name_or_path");
        }

        // --- Delete Multiple Objects Handlers ---

        /// <summary>
        /// 按名称删除多个GameObject
        /// </summary>
        private object HandleDeletesByName(JObject args)
        {
            JToken targetsToken = args["targets"];
            JToken targetToken = args["target"];
            bool findAll = args["find_all"]?.ToObject<bool>() ?? false;

            if (targetsToken == null && targetToken == null)
            {
                return Response.Error("Either targets or target parameter is required for by_name search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteMultipleGameObjects(targetsToken ?? targetToken, "by_name", findAll);
        }

        /// <summary>
        /// 按ID删除多个GameObject
        /// </summary>
        private object HandleDeletesById(JObject args)
        {
            JToken targetsToken = args["targets"];
            JToken targetToken = args["target"];
            bool findAll = args["find_all"]?.ToObject<bool>() ?? false;

            if (targetsToken == null && targetToken == null)
            {
                return Response.Error("Either targets or target parameter is required for by_id search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteMultipleGameObjects(targetsToken ?? targetToken, "by_id", findAll);
        }

        /// <summary>
        /// 按标签删除多个GameObject
        /// </summary>
        private object HandleDeletesByTag(JObject args)
        {
            JToken targetsToken = args["targets"];
            JToken targetToken = args["target"];
            bool findAll = args["find_all"]?.ToObject<bool>() ?? false;

            if (targetsToken == null && targetToken == null)
            {
                return Response.Error("Either targets or target parameter is required for by_tag search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteMultipleGameObjects(targetsToken ?? targetToken, "by_tag", findAll);
        }

        /// <summary>
        /// 按层级删除多个GameObject
        /// </summary>
        private object HandleDeletesByLayer(JObject args)
        {
            JToken targetsToken = args["targets"];
            JToken targetToken = args["target"];
            bool findAll = args["find_all"]?.ToObject<bool>() ?? false;

            if (targetsToken == null && targetToken == null)
            {
                return Response.Error("Either targets or target parameter is required for by_layer search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteMultipleGameObjects(targetsToken ?? targetToken, "by_layer", findAll);
        }

        /// <summary>
        /// 按组件删除多个GameObject
        /// </summary>
        private object HandleDeletesByComponent(JObject args)
        {
            JToken targetsToken = args["targets"];
            JToken targetToken = args["target"];
            bool findAll = args["find_all"]?.ToObject<bool>() ?? false;

            if (targetsToken == null && targetToken == null)
            {
                return Response.Error("Either targets or target parameter is required for by_component search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteMultipleGameObjects(targetsToken ?? targetToken, "by_component", findAll);
        }

        /// <summary>
        /// 按路径删除多个GameObject
        /// </summary>
        private object HandleDeletesByPath(JObject args)
        {
            JToken targetsToken = args["targets"];
            JToken targetToken = args["target"];
            bool findAll = args["find_all"]?.ToObject<bool>() ?? false;

            if (targetsToken == null && targetToken == null)
            {
                return Response.Error("Either targets or target parameter is required for by_path search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteMultipleGameObjects(targetsToken ?? targetToken, "by_path", findAll);
        }

        /// <summary>
        /// 按GUID删除多个GameObject
        /// </summary>
        private object HandleDeletesByGuid(JObject args)
        {
            JToken targetsToken = args["targets"];
            JToken targetToken = args["target"];
            bool findAll = args["find_all"]?.ToObject<bool>() ?? false;

            if (targetsToken == null && targetToken == null)
            {
                return Response.Error("Either targets or target parameter is required for by_guid search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteMultipleGameObjectsByGuid(targetsToken ?? targetToken, findAll);
        }

        /// <summary>
        /// 默认删除多个GameObject
        /// </summary>
        private object HandleDeletesDefault(JObject args)
        {
            JToken targetsToken = args["targets"];
            JToken targetToken = args["target"];
            bool findAll = args["find_all"]?.ToObject<bool>() ?? false;

            if (targetsToken == null && targetToken == null)
            {
                return Response.Error("Either targets or target parameter is required.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            return DeleteMultipleGameObjects(targetsToken ?? targetToken, "by_id_or_name_or_path", findAll);
        }

        /// <summary>
        /// 检查预制体重定向逻辑
        /// </summary>
        private object CheckPrefabRedirection(JObject args)
        {
            JToken targetToken = args["target"];
            string targetPath = targetToken?.Type == JTokenType.String ? targetToken.ToString() : null;

            if (string.IsNullOrEmpty(targetPath) || !targetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return null; // 不是预制体，继续正常处理

            return Response.Error($"Action 'delete' on a prefab asset ('{targetPath}') should be performed using the 'manage_asset' command.");
        }

        /// <summary>
        /// 删除单个GameObject
        /// </summary>
        private object DeleteSingleGameObject(JToken targetToken, string searchMethod)
        {
            List<GameObject> targets = GameObjectUtils.FindObjectsInternal(targetToken, searchMethod, false); // find_all=false for single delete

            if (targets.Count == 0)
            {
                return Response.Error(
                    $"Target GameObject ('{targetToken}') not found using method '{searchMethod ?? "default"}'."
                );
            }

            if (targets.Count > 1)
            {
                return Response.Error(
                    $"Multiple GameObjects found ('{targets.Count}'). Use 'deletes' action or set find_all=false for single deletion."
                );
            }

            GameObject targetGo = targets[0];
            if (targetGo != null)
            {
                string goName = targetGo.name;
                int goId = targetGo.GetInstanceID();
                Undo.DestroyObjectImmediate(targetGo);
                var deletedObject = new { name = goName, instanceID = goId };
                return Response.Success($"GameObject '{goName}' deleted successfully.", deletedObject);
            }

            return Response.Error("Failed to delete target GameObject.");
        }

        /// <summary>
        /// 通过GUID删除单个GameObject
        /// </summary>
        private object DeleteSingleGameObjectByGuid(string guid)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                return Response.Error($"No asset found with GUID '{guid}'.");
            }

            GameObject assetObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (assetObject == null)
            {
                return Response.Error($"Asset at path '{assetPath}' is not a GameObject.");
            }

            List<GameObject> instances = new List<GameObject>();
            GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            
            foreach (GameObject go in allObjects)
            {
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == assetPath)
                {
                    instances.Add(go);
                }
            }

            if (instances.Count == 0)
            {
                return Response.Error($"No instances of GameObject '{assetObject.name}' found in scene.");
            }

            if (instances.Count > 1)
            {
                return Response.Error(
                    $"Multiple instances found ('{instances.Count}'). Use 'deletes' action for multiple deletion."
                );
            }

            GameObject instance = instances[0];
            string goName = instance.name;
            int goId = instance.GetInstanceID();
            Undo.DestroyObjectImmediate(instance);
            var deletedObject = new { name = goName, instanceID = goId, guid = guid };
            return Response.Success($"GameObject instance '{goName}' deleted successfully.", deletedObject);
        }

        /// <summary>
        /// 删除多个GameObject
        /// </summary>
        private object DeleteMultipleGameObjects(JToken targetToken, string searchMethod, bool findAll)
        {
            List<GameObject> targets = GameObjectUtils.FindObjectsInternal(targetToken, searchMethod, findAll);

            if (targets.Count == 0)
            {
                return Response.Error(
                    $"No GameObjects found using method '{searchMethod ?? "default"}'."
                );
            }

            List<object> deletedObjects = new List<object>();
            foreach (var targetGo in targets)
            {
                if (targetGo != null)
                {
                    string goName = targetGo.name;
                    int goId = targetGo.GetInstanceID();
                    Undo.DestroyObjectImmediate(targetGo);
                    deletedObjects.Add(new { name = goName, instanceID = goId });
                }
            }

            if (deletedObjects.Count > 0)
            {
                string message = deletedObjects.Count == 1
                    ? $"GameObject '{deletedObjects[0].GetType().GetProperty("name").GetValue(deletedObjects[0])}' deleted successfully."
                    : $"{deletedObjects.Count} GameObjects deleted successfully.";
                return Response.Success(message, deletedObjects);
            }
            else
            {
                return Response.Error("Failed to delete target GameObjects.");
            }
        }

        /// <summary>
        /// 通过GUID删除多个GameObject
        /// </summary>
        private object DeleteMultipleGameObjectsByGuid(JToken targetToken, bool findAll)
        {
            List<string> guids = new List<string>();
            
            if (targetToken.Type == JTokenType.Array)
            {
                foreach (var guid in targetToken)
                {
                    guids.Add(guid.ToString());
                }
            }
            else
            {
                guids.Add(targetToken.ToString());
            }

            List<object> allDeletedObjects = new List<object>();
            int totalDeleted = 0;

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue; // 跳过无效GUID
                }

                GameObject assetObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (assetObject == null)
                {
                    continue; // 跳过非GameObject资产
                }

                List<GameObject> instances = new List<GameObject>();
                GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                
                foreach (GameObject go in allObjects)
                {
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == assetPath)
                    {
                        instances.Add(go);
                    }
                }

                if (!findAll && instances.Count > 1)
                {
                    instances = instances.Take(1).ToList(); // 只删除第一个实例
                }

                foreach (var instance in instances)
                {
                    if (instance != null)
                    {
                        string goName = instance.name;
                        int goId = instance.GetInstanceID();
                        Undo.DestroyObjectImmediate(instance);
                        allDeletedObjects.Add(new { name = goName, instanceID = goId, guid = guid });
                        totalDeleted++;
                    }
                }
            }

            if (totalDeleted > 0)
            {
                string message = totalDeleted == 1
                    ? $"GameObject instance deleted successfully."
                    : $"{totalDeleted} GameObject instances deleted successfully.";
                return Response.Success(message, allDeletedObjects);
            }
            else
            {
                return Response.Error("No valid GameObject instances found to delete.");
            }
        }
    }
} 
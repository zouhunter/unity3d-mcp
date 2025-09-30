using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models; // For Response class

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles UnityEngine.Object deletion operations using dual state tree architecture with interactive confirmation.
    /// Supports GameObjects, assets, and other Unity objects.
    /// Target tree: IObjectSelector handles target location
    /// Action tree: 'confirm' parameter determines confirmation behavior:
    ///   - confirm=true: Always shows confirmation dialog before deletion
    ///   - confirm=false/unset: Asset deletion requires confirmation, scene object deletion is direct
    /// Uses coroutines with EditorUtility.DisplayDialog for interactive user confirmation.
    /// 对应方法名: object_delete
    /// </summary>
    [ToolName("object_delete", "对象编辑")]
    public class ObjectDelete : DualStateMethodBase
    {
        private IObjectSelector objectSelector;

        public ObjectDelete()
        {
            objectSelector = new ObjectSelector<UnityEngine.Object>();
        }

        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                // 目标查找参数（交给IObjectSelector处理）
                new MethodKey("path", "Object Hierarchy path", false),
                new MethodKey("instance_id", "Object InstanceID", true),
                new MethodKey("confirm", "Force confirmation dialog: true=always confirm, false/unset=smart confirmation (auto ≤3, dialog >3)", true),
            };
        }

        /// <summary>
        /// 创建目标定位状态树（使用IObjectSelector）
        /// </summary>
        protected override StateTree CreateTargetTree()
        {
            return objectSelector.BuildStateTree();
        }

        /// <summary>
        /// 创建操作执行状态树
        /// </summary>
        protected override StateTree CreateActionTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("confirm")
                    .Leaf("true", (Func<StateTreeContext, object>)HandleConfirmedDeleteAction) // 确认删除
                    .Leaf("false", (Func<StateTreeContext, object>)HandleUnconfirmedDeleteAction) // 未确认删除
                    .DefaultLeaf((Func<StateTreeContext, object>)HandleUnconfirmedDeleteAction) // 默认为未确认删除
                .Build();
        }

        /// <summary>
        /// 异步处理需要用户确认的删除操作（仅用于资源文件删除）
        /// </summary>
        private IEnumerator HandleConfirmedDeleteActionAsync(StateTreeContext ctx)
        {
            UnityEngine.Object target = ExtractTargetFromContext(ctx);
            if (target == null)
            {
                yield return Response.Error("No target Object found for deletion.");
                yield break;
            }

            // 检查是否是资源文件删除
            bool isAssetDeletion = IsAssetDeletion(ctx);
            if (!isAssetDeletion)
            {
                // 不是资源删除，直接删除Object
                var result = DeleteSingleObject(target);
                yield return result;
                yield break;
            }

            // 资源删除需要确认对话框
            string confirmationMessage = $"Are you sure you want to delete the asset '{target.name}' ({target.GetType().Name})?\n\nThis action cannot be undone.";

            bool confirmed = EditorUtility.DisplayDialog(
                "Confirm Asset Deletion",
                confirmationMessage,
                "Delete Asset",
                "Cancel"
            );

            if (!confirmed)
            {
                LogInfo($"[ObjectDelete] User cancelled asset deletion for Object '{target.name}'");
                yield return Response.Success($"Asset deletion cancelled by user. Object '{target.name}' was not deleted.", new { cancelled = true, target_name = target.name });
                yield break;
            }

            LogInfo($"[ObjectDelete] User confirmed asset deletion for Object '{target.name}'");

            // 用户确认后执行删除
            var deleteResult = DeleteSingleObject(target);
            yield return deleteResult;
        }

        /// <summary>
        /// 处理需要用户确认的删除操作
        /// </summary>
        private object HandleConfirmedDeleteAction(StateTreeContext ctx)
        {
            return ctx.AsyncReturn(HandleConfirmedDeleteActionAsync(ctx));
        }

        /// <summary>
        /// 异步处理未明确确认的删除操作，检查是否是资源删除来决定是否需要确认
        /// </summary>
        private IEnumerator HandleUnconfirmedDeleteActionAsync(StateTreeContext ctx)
        {
            UnityEngine.Object target = ExtractTargetFromContext(ctx);
            if (target == null)
            {
                yield return Response.Error("No target Object found for deletion.");
                yield break;
            }

            // 检查预制体重定向（仅对GameObject适用）
            if (target is GameObject gameObject)
            {
                object redirectResult = CheckPrefabRedirection(gameObject);
                if (redirectResult != null)
                {
                    yield return redirectResult;
                    yield break;
                }
            }

            // 检查是否是资源删除
            bool isAssetDeletion = IsAssetDeletion(ctx);
            if (!isAssetDeletion)
            {
                // 场景Object删除，直接删除无需确认
                LogInfo($"[ObjectDelete] Direct deletion of {target.GetType().Name} '{target.name}' without confirmation");
                var result = DeleteSingleObject(target);
                yield return result;
                yield break;
            }

            // 资源删除需要用户确认
            LogInfo($"[ObjectDelete] Asset deletion detected for '{target.name}', showing confirmation dialog");

            string confirmationMessage = $"You are about to delete the asset '{target.name}' ({target.GetType().Name}).\n\nThis action cannot be undone. Continue?";

            bool confirmed = EditorUtility.DisplayDialog(
                "Confirm Asset Deletion",
                confirmationMessage,
                "Delete Asset",
                "Cancel"
            );

            if (!confirmed)
            {
                LogInfo($"[ObjectDelete] User cancelled asset deletion for '{target.name}'");
                yield return Response.Success($"Asset deletion cancelled by user. Object '{target.name}' was not deleted.", new { cancelled = true, target_name = target.name });
                yield break;
            }

            LogInfo($"[ObjectDelete] User confirmed asset deletion for '{target.name}'");

            // 用户确认后执行删除
            var deleteResult = DeleteSingleObject(target);
            yield return deleteResult;
        }

        /// <summary>
        /// 处理未确认的删除操作
        /// </summary>
        private object HandleUnconfirmedDeleteAction(StateTreeContext ctx)
        {
            return ctx.AsyncReturn(HandleUnconfirmedDeleteActionAsync(ctx));
        }

        /// <summary>
        /// 从执行上下文中提取唯一目标UnityEngine.Object
        /// </summary>
        private UnityEngine.Object ExtractTargetFromContext(StateTreeContext context)
        {
            // 先尝试从ObjectReferences获取（避免序列化问题）
            if (context.TryGetObjectReference("_resolved_targets", out object targetsObj))
            {
                if (targetsObj is UnityEngine.Object singleObject)
                {
                    return singleObject;
                }
                else if (targetsObj is UnityEngine.Object[] objectArray && objectArray.Length > 0)
                {
                    return objectArray[0]; // 只取第一个
                }
                else if (targetsObj is System.Collections.IList list && list.Count > 0)
                {
                    foreach (var item in list)
                    {
                        if (item is UnityEngine.Object obj)
                            return obj; // 返回第一个找到的UnityEngine.Object
                    }
                }
            }

            // 如果ObjectReferences中没有，尝试从JsonData获取（向后兼容）
            if (context.TryGetJsonValue("_resolved_targets", out JToken targetToken))
            {
                if (targetToken is JArray targetArray && targetArray.Count > 0)
                {
                    return targetArray[0].ToObject<UnityEngine.Object>(); // 只取第一个
                }
                else
                {
                    // 单个对象的情况
                    return targetToken.ToObject<UnityEngine.Object>();
                }
            }

            return null;
        }

        /// <summary>
        /// 检查是否是资源删除操作
        /// </summary>
        private bool IsAssetDeletion(StateTreeContext context)
        {
            // 通过path参数判断是否是资源路径
            if (context.TryGetValue("path", out object pathObj) && pathObj != null)
            {
                string path = pathObj.ToString();
                // 如果路径以Assets/开头，则认为是资源删除
                if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检查预制体重定向逻辑
        /// </summary>
        private object CheckPrefabRedirection(GameObject target)
        {
            if (target == null)
                return null;

            // 检查是否是预制体实例，如果是预制体资产本身，应该使用manage_asset命令
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                // 这是预制体实例，可以正常删除
                return null;
            }

            return null; // 继续正常处理
        }

        /// <summary>
        /// 删除单个UnityEngine.Object
        /// </summary>
        private object DeleteSingleObject(UnityEngine.Object targetObject)
        {
            if (targetObject == null)
            {
                return Response.Error("Target Object is null.");
            }

            string objectName = targetObject.name;
            int objectId = targetObject.GetInstanceID();
            string objectType = targetObject.GetType().Name;

            try
            {
                // 判断是否为资源文件
                string assetPath = AssetDatabase.GetAssetPath(targetObject);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    // 资源文件删除
                    bool success = AssetDatabase.DeleteAsset(assetPath);
                    if (success)
                    {
                        var deletedObject = new { name = objectName, instanceID = objectId, type = objectType, assetPath = assetPath };
                        return Response.Success($"{objectType} asset '{objectName}' deleted successfully.", deletedObject);
                    }
                    else
                    {
                        return Response.Error($"Failed to delete {objectType} asset '{objectName}' at path: {assetPath}");
                    }
                }
                else
                {
                    // 场景对象删除
                    Undo.DestroyObjectImmediate(targetObject);
                    var deletedObject = new { name = objectName, instanceID = objectId, type = objectType };
                    return Response.Success($"{objectType} '{objectName}' deleted successfully.", deletedObject);
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to delete {objectType} '{objectName}': {e.Message}");
            }
        }



    }
}
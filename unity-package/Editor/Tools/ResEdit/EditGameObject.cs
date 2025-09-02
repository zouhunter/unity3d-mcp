using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.SearchService;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMcp.Models; // For Response class

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles GameObject modification operations using dual state tree architecture.
    /// First tree: Target location (using GameObjectSelector)
    /// Second tree: Property modification operations
    /// 对应方法名: gameobject_modify
    /// </summary>
    [ToolName("edit_gameobject")]
    public class EditGameObject : DualStateMethodBase
    {
        private HierarchyCreate hierarchyCreate;
        private IObjectSelector objectSelector;

        public EditGameObject()
        {
            hierarchyCreate = new HierarchyCreate();
            objectSelector = objectSelector ?? new ObjectSelector<GameObject>();
        }

        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                   // 目标查找参数
                new MethodKey("instance_id", "对象的InstanceID", true),
                new MethodKey("path", "对象的Hierachy路径", false),
                // 操作参数
                new MethodKey("action", "操作类型：create,modify, get_components, add_component, remove_component,", false),
                // 基本修改参数
                new MethodKey("name", "GameObject名称", true),
                new MethodKey("tag", "GameObject标签", true),
                new MethodKey("layer", "GameObject所在层", true),
                new MethodKey("parent_id", "父对象的InstanceID", true),
                new MethodKey("position", "位置坐标 [x, y, z]", true),
                new MethodKey("rotation", "旋转角度 [x, y, z]", true),
                new MethodKey("scale", "缩放比例 [x, y, z]", true),
                new MethodKey("active", "设置激活状态", true),
                // 组件操作参数
                new MethodKey("component_name", "组件名称", true),
                new MethodKey("component_properties", "组件属性字典", true),
                // 属性操作参数（兜底功能）
                new MethodKey("property_name", "属性名称（用于属性设置/获取）", true),
                new MethodKey("value", "要设置的属性值", true),
            };
        }

        /// <summary>
        /// 创建目标定位状态树（使用GameObjectDynamicSelector）
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
                .Key("action")
                .Leaf("create", (Func<StateTreeContext, object>)HandleCreateAction)
                .Leaf("modify", (Func<StateTreeContext, object>)HandleModifyAction)
                .Leaf("get_components", (Func<StateTreeContext, object>)HandleGetComponentsAction)
                .Leaf("add_component", (Func<StateTreeContext, object>)HandleAddComponentAction)
                .Leaf("remove_component", (Func<StateTreeContext, object>)HandleRemoveComponentAction)
                .Leaf("set_property", (Func<StateTreeContext, object>)HandleSetPropertyAction)
                .Leaf("get_property", (Func<StateTreeContext, object>)HandleGetPropertyAction)
                .DefaultLeaf((Func<StateTreeContext, object>)HandleDefaultAction)
                .Build();
        }

        /// <summary>
        /// 处理创建操作
        /// </summary>
        private object HandleCreateAction(StateTreeContext args)
        {
            hierarchyCreate.ExecuteMethod(args);
            return args;
        }

        /// <summary>
        /// 处理修改操作
        /// </summary>
        private object HandleModifyAction(StateTreeContext args)
        {
            GameObject[] targets = GetTargetsBasedOnSelectMany(args);
            if (targets.Length == 0)
            {
                return Response.Error("No target GameObjects found in execution context.");
            }

            if (targets.Length == 1)
            {
                // 单个对象修改
                return ApplyModifications(targets[0], args);
            }
            else
            {
                // 批量修改
                return ApplyModificationsToMultiple(targets, args);
            }
        }

        /// <summary>
        /// 默认操作处理（兼容性，不指定action时使用modify）
        /// </summary>
        private object HandleDefaultAction(StateTreeContext args)
        {
            LogInfo("[GameObjectModify] No action specified, using default modify action");
            return HandleModifyAction(args);
        }

        /// <summary>
        /// 从执行上下文中提取目标GameObject数组
        /// </summary>
        private GameObject[] ExtractTargetsFromContext(StateTreeContext context)
        {
            // 先尝试从ObjectReferences获取（避免序列化问题）
            if (context.TryGetObjectReference("_resolved_targets", out object targetsObj))
            {
                if (targetsObj is GameObject[] gameObjectArray)
                {
                    return gameObjectArray;
                }
                else if (targetsObj is GameObject singleGameObject)
                {
                    return new GameObject[] { singleGameObject };
                }
                else if (targetsObj is System.Collections.IList list)
                {
                    var gameObjects = new List<GameObject>();
                    foreach (var item in list)
                    {
                        if (item is GameObject go)
                            gameObjects.Add(go);
                    }
                    return gameObjects.ToArray();
                }
            }

            // 如果ObjectReferences中没有，尝试从JsonData获取（向后兼容）
            if (context.TryGetJsonValue("_resolved_targets", out JToken targetToken))
            {
                if (targetToken is JArray targetArray)
                {
                    return targetArray.ToObject<GameObject[]>();
                }
                else
                {
                    // 单个对象的情况
                    GameObject single = targetToken.ToObject<GameObject>();
                    return single != null ? new GameObject[] { single } : new GameObject[0];
                }
            }

            return new GameObject[0];
        }

        /// <summary>
        /// 从目标数组中提取第一个GameObject（用于需要单个目标的操作）
        /// </summary>
        private GameObject ExtractFirstTargetFromContext(StateTreeContext context)
        {
            GameObject[] targets = ExtractTargetsFromContext(context);
            return targets.Length > 0 ? targets[0] : null;
        }

        /// <summary>
        /// 检查是否应该进行批量操作
        /// </summary>
        private bool ShouldSelectMany(StateTreeContext context)
        {
            if (context.TryGetValue("select_many", out object selectManyObj))
            {
                if (selectManyObj is bool selectMany)
                    return selectMany;
                if (bool.TryParse(selectManyObj?.ToString(), out bool parsedSelectMany))
                    return parsedSelectMany;
            }
            return false; // 默认为false
        }

        /// <summary>
        /// 根据select_many参数获取目标对象（单个或多个）
        /// </summary>
        private GameObject[] GetTargetsBasedOnSelectMany(StateTreeContext context)
        {
            GameObject[] targets = ExtractTargetsFromContext(context);

            if (ShouldSelectMany(context))
            {
                return targets; // 返回所有匹配的对象
            }
            else
            {
                // 只返回第一个对象（如果存在）
                return targets.Length > 0 ? new GameObject[] { targets[0] } : new GameObject[0];
            }
        }

        /// <summary>
        /// 处理属性设置操作（兜底功能）
        /// </summary>
        private object HandleSetPropertyAction(StateTreeContext args)
        {
            GameObject[] targets = GetTargetsBasedOnSelectMany(args);
            if (targets.Length == 0)
            {
                return Response.Error("No target GameObjects found in execution context.");
            }

            if (!args.TryGetValue("property_name", out object propertyNameObj) || propertyNameObj == null)
            {
                return Response.Error("Property name is required for set_property action.");
            }
            string propertyName = propertyNameObj.ToString();

            if (!args.TryGetValue("value", out object valueObj))
            {
                return Response.Error("Value is required for set_property action.");
            }

            if (targets.Length == 1)
            {
                return SetPropertyOnSingleTarget(targets[0], propertyName, valueObj);
            }
            else
            {
                return SetPropertyOnMultipleTargets(targets, propertyName, valueObj);
            }
        }

        /// <summary>
        /// 处理属性获取操作（兜底功能）
        /// </summary>
        private object HandleGetPropertyAction(StateTreeContext args)
        {
            GameObject[] targets = GetTargetsBasedOnSelectMany(args);
            if (targets.Length == 0)
            {
                return Response.Error("No target GameObjects found in execution context.");
            }

            if (!args.TryGetValue("property_name", out object propertyNameObj) || propertyNameObj == null)
            {
                return Response.Error("Property name is required for get_property action.");
            }
            string propertyName = propertyNameObj.ToString();

            if (targets.Length == 1)
            {
                return GetPropertyFromSingleTarget(targets[0], propertyName);
            }
            else
            {
                return GetPropertyFromMultipleTargets(targets, propertyName);
            }
        }

        /// <summary>
        /// 在单个目标上设置属性
        /// </summary>
        private object SetPropertyOnSingleTarget(GameObject target, string propertyName, object valueObj)
        {
            try
            {
                // 只操作GameObject本身的属性
                Undo.RecordObject(target, $"Set Property {propertyName}");

                // 如果值是JToken，使用原有逻辑；否则直接使用值
                if (valueObj is JToken valueToken)
                {
                    SetPropertyValue(target, propertyName, valueToken);
                }
                else
                {
                    // 将值转换为JToken再设置
                    JToken convertedToken = JToken.FromObject(valueObj);
                    SetPropertyValue(target, propertyName, convertedToken);
                }

                EditorUtility.SetDirty(target);

                LogInfo($"[EditGameObject] Set property '{propertyName}' on {target.name}");

                return Response.Success(
                    $"Property '{propertyName}' set successfully on {target.name}.",
                    new Dictionary<string, object>
                    {
                        { "target", target.name },
                        { "property", propertyName },
                        { "value", valueObj?.ToString() }
                    }
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to set property '{propertyName}': {e.Message}");
            }
        }

        /// <summary>
        /// 在多个目标上设置属性
        /// </summary>
        private object SetPropertyOnMultipleTargets(GameObject[] targets, string propertyName, object valueObj)
        {
            var results = new List<Dictionary<string, object>>();
            var errors = new List<string>();
            int successCount = 0;

            foreach (GameObject target in targets)
            {
                if (target == null) continue;

                try
                {
                    var result = SetPropertyOnSingleTarget(target, propertyName, valueObj);

                    if (IsSuccessResponse(result, out object data, out string message))
                    {
                        successCount++;
                        if (data is Dictionary<string, object> dictData)
                        {
                            results.Add(dictData);
                        }
                        else if (data != null)
                        {
                            // 如果data不是Dictionary，包装它
                            results.Add(new Dictionary<string, object> { { "result", data } });
                        }
                    }
                    else
                    {
                        errors.Add($"[{target.name}] {message ?? "Unknown error"}");
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"[{target.name}] Error: {e.Message}");
                }
            }

            return CreateBatchOperationResponse($"set property '{propertyName}'", successCount, targets.Length, results, errors);
        }

        /// <summary>
        /// 从单个目标获取属性
        /// </summary>
        private object GetPropertyFromSingleTarget(GameObject target, string propertyName)
        {
            try
            {
                // 只获取GameObject本身的属性
                var value = GetPropertyValue(target, propertyName);
                LogInfo($"[EditGameObject] Got property '{propertyName}' from {target.name}: {value}");

                return Response.Success(
                    $"Property '{propertyName}' retrieved successfully from {target.name}.",
                    new Dictionary<string, object>
                    {
                        { "target", target.name },
                        { "property", propertyName },
                        { "value", value }
                    }
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to get property '{propertyName}': {e.Message}");
            }
        }

        /// <summary>
        /// 从多个目标获取属性
        /// </summary>
        private object GetPropertyFromMultipleTargets(GameObject[] targets, string propertyName)
        {
            var results = new List<Dictionary<string, object>>();
            var errors = new List<string>();
            int successCount = 0;

            foreach (GameObject target in targets)
            {
                if (target == null) continue;

                try
                {
                    var result = GetPropertyFromSingleTarget(target, propertyName);

                    if (IsSuccessResponse(result, out object data, out string message))
                    {
                        successCount++;
                        if (data is Dictionary<string, object> dictData)
                        {
                            results.Add(dictData);
                        }
                        else if (data != null)
                        {
                            results.Add(new Dictionary<string, object> { { "result", data } });
                        }
                    }
                    else
                    {
                        errors.Add($"[{target.name}] {message ?? "Unknown error"}");
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"[{target.name}] Error: {e.Message}");
                }
            }

            return CreateBatchOperationResponse($"get property '{propertyName}'", successCount, targets.Length, results, errors);
        }



        /// <summary>
        /// 应用修改到GameObject
        /// </summary>
        private object ApplyModifications(GameObject targetGo, StateTreeContext args)
        {
            // Record state for Undo *before* modifications
            Undo.RecordObject(targetGo.transform, "Modify GameObject Transform");
            Undo.RecordObject(targetGo, "Modify GameObject Properties");

            bool modified = false;

            // 应用名称修改
            modified |= ApplyNameModification(targetGo, args);

            // 应用父对象修改
            object parentResult = ApplyParentModification(targetGo, args);
            if (parentResult != null)
                return parentResult;
            modified |= parentResult != null;

            // 应用激活状态修改
            modified |= ApplyActiveStateModification(targetGo, args);

            // 应用标签修改
            object tagResult = ApplyTagModification(targetGo, args);
            if (tagResult != null)
                return tagResult;
            modified |= tagResult != null;

            // 应用层级修改
            modified |= ApplyLayerModification(targetGo, args);

            // 应用变换修改
            modified |= ApplyTransformModifications(targetGo, args);

            if (!modified)
            {
                return Response.Success(
                    $"No modifications applied to GameObject '{targetGo.name}'.",
                    GetGameObjectData(targetGo)
                );
            }

            EditorUtility.SetDirty(targetGo); // Mark scene as dirty
            return Response.Success(
                $"GameObject '{targetGo.name}' modified successfully.",
                GetGameObjectData(targetGo)
            );
        }

        /// <summary>
        /// 应用修改到多个GameObject
        /// </summary>
        private object ApplyModificationsToMultiple(GameObject[] targets, StateTreeContext args)
        {
            var results = new List<Dictionary<string, object>>();
            var errors = new List<string>();
            int successCount = 0;

            foreach (GameObject targetGo in targets)
            {
                if (targetGo == null) continue;

                try
                {
                    var result = ApplyModifications(targetGo, args);

                    if (IsSuccessResponse(result, out object data, out string responseMessage))
                    {
                        successCount++;
                        if (data is Dictionary<string, object> dictData)
                        {
                            results.Add(dictData);
                        }
                        else
                        {
                            results.Add(GetGameObjectData(targetGo));
                        }
                    }
                    else
                    {
                        errors.Add($"[{targetGo.name}] {responseMessage ?? "Unknown error"}");
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"[{targetGo.name}] Error: {e.Message}");
                }
            }

            // 构建响应消息
            string message;
            if (successCount == targets.Length)
            {
                message = $"Successfully modified {successCount} GameObject(s).";
            }
            else if (successCount > 0)
            {
                message = $"Modified {successCount} of {targets.Length} GameObject(s). {errors.Count} failed.";
            }
            else
            {
                message = $"Failed to modify any of the {targets.Length} GameObject(s).";
            }

            var responseData = new Dictionary<string, object>
            {
                { "modified_count", successCount },
                { "total_count", targets.Length },
                { "success_rate", (double)successCount / targets.Length },
                { "modified_objects", results }
            };

            if (errors.Count > 0)
            {
                responseData["errors"] = errors;
            }

            // 如果有成功的修改，返回成功响应
            if (successCount > 0)
            {
                return Response.Success(message, responseData);
            }
            else
            {
                return Response.Error(message, responseData);
            }
        }

        /// <summary>
        /// 应用名称修改
        /// </summary>
        private bool ApplyNameModification(GameObject targetGo, StateTreeContext args)
        {
            if (args.TryGetValue("name", out object nameObj) && nameObj != null)
            {
                string name = nameObj.ToString();
                if (!string.IsNullOrEmpty(name) && targetGo.name != name)
                {
                    targetGo.name = name;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 应用父对象修改
        /// </summary>
        private object ApplyParentModification(GameObject targetGo, StateTreeContext args)
        {
            if (args.TryGetValue("parent", out object parentObj))
            {
                JToken parentToken = null;
                GameObject newParentGo = null;

                // 处理不同类型的父对象标识符
                if (parentObj is JToken token)
                {
                    parentToken = token;
                    newParentGo = FindParentGameObject(parentToken);
                }
                else if (parentObj is GameObject parentGameObject)
                {
                    newParentGo = parentGameObject;
                }
                else if (parentObj != null)
                {
                    // 转换为JToken进行处理
                    parentToken = JToken.FromObject(parentObj);
                    newParentGo = FindParentGameObject(parentToken);
                }

                if (
                    newParentGo == null
                    && parentObj != null
                    && !(parentObj.ToString() == "null" || string.IsNullOrEmpty(parentObj.ToString()))
                )
                {
                    return Response.Error($"New parent ('{parentObj}') not found.");
                }

                // Check for hierarchy loops
                if (newParentGo != null && newParentGo.transform.IsChildOf(targetGo.transform))
                {
                    return Response.Error(
                        $"Cannot parent '{targetGo.name}' to '{newParentGo.name}', as it would create a hierarchy loop."
                    );
                }

                if (targetGo.transform.parent != (newParentGo?.transform))
                {
                    targetGo.transform.SetParent(newParentGo?.transform, true); // worldPositionStays = true
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 应用激活状态修改
        /// </summary>
        private bool ApplyActiveStateModification(GameObject targetGo, StateTreeContext args)
        {
            if (args.TryGetValue("setActive", out object setActiveObj) ||
                args.TryGetValue("active", out setActiveObj))
            {
                if (setActiveObj is bool setActive)
                {
                    if (targetGo.activeSelf != setActive)
                    {
                        targetGo.SetActive(setActive);
                        return true;
                    }
                }
                else if (bool.TryParse(setActiveObj?.ToString(), out bool parsedActive))
                {
                    if (targetGo.activeSelf != parsedActive)
                    {
                        targetGo.SetActive(parsedActive);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 应用标签修改
        /// </summary>
        private object ApplyTagModification(GameObject targetGo, StateTreeContext args)
        {
            if (args.TryGetValue("tag", out object tagObj) && tagObj != null)
            {
                string tag = tagObj.ToString();
                // Only attempt to change tag if a non-null tag is provided and it's different from the current one.
                // Allow setting an empty string to remove the tag (Unity uses "Untagged").
                if (targetGo.tag != tag)
                {
                    // Ensure the tag is not empty, if empty, it means "Untagged" implicitly
                    string tagToSet = string.IsNullOrEmpty(tag) ? "Untagged" : tag;

                    try
                    {
                        // First attempt to set the tag
                        targetGo.tag = tagToSet;
                        return true;
                    }
                    catch (UnityException ex)
                    {
                        // Check if the error is specifically because the tag doesn't exist
                        if (ex.Message.Contains("is not defined"))
                        {
                            LogInfo($"[GameObjectModify] Tag '{tagToSet}' not found. Attempting to create it.");
                            try
                            {
                                // Attempt to create the tag using internal utility
                                InternalEditorUtility.AddTag(tagToSet);
                                // Wait a frame maybe? Not strictly necessary but sometimes helps editor updates.
                                // yield return null; // Cannot yield here, editor script limitation

                                // Retry setting the tag immediately after creation
                                targetGo.tag = tagToSet;
                                LogInfo($"[GameObjectModify] Tag '{tagToSet}' created and assigned successfully.");
                                return true;
                            }
                            catch (Exception innerEx)
                            {
                                // Handle failure during tag creation or the second assignment attempt
                                Debug.LogError(
                                    $"[GameObjectModify] Failed to create or assign tag '{tagToSet}' after attempting creation: {innerEx.Message}"
                                );
                                return Response.Error(
                                    $"Failed to create or assign tag '{tagToSet}': {innerEx.Message}. Check Tag Manager and permissions."
                                );
                            }
                        }
                        else
                        {
                            // If the exception was for a different reason, return the original error
                            return Response.Error($"Failed to set tag to '{tagToSet}': {ex.Message}.");
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 应用层级修改
        /// </summary>
        private bool ApplyLayerModification(GameObject targetGo, StateTreeContext args)
        {
            if (args.TryGetValue("layer", out object layerObj) && layerObj != null)
            {
                string layerName = layerObj.ToString();
                if (!string.IsNullOrEmpty(layerName))
                {
                    int layerId = LayerMask.NameToLayer(layerName);
                    if (layerId == -1 && layerName != "Default")
                    {
                        Debug.LogWarning($"Invalid layer specified: '{layerName}'. Use a valid layer name.");
                        return false;
                    }
                    if (layerId != -1 && targetGo.layer != layerId)
                    {
                        targetGo.layer = layerId;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 应用变换修改
        /// </summary>
        private bool ApplyTransformModifications(GameObject targetGo, StateTreeContext args)
        {
            bool modified = false;

            // 获取transform参数并转换为JArray（如果需要）
            JArray positionArray = null;
            JArray rotationArray = null;
            JArray scaleArray = null;

            if (args.TryGetValue("position", out object positionObj))
            {
                positionArray = positionObj as JArray ?? (positionObj != null ? JArray.FromObject(positionObj) : null);
            }

            if (args.TryGetValue("rotation", out object rotationObj))
            {
                rotationArray = rotationObj as JArray ?? (rotationObj != null ? JArray.FromObject(rotationObj) : null);
            }

            if (args.TryGetValue("scale", out object scaleObj))
            {
                scaleArray = scaleObj as JArray ?? (scaleObj != null ? JArray.FromObject(scaleObj) : null);
            }

            Vector3? position = GameObjectUtils.ParseVector3(positionArray);
            Vector3? rotation = GameObjectUtils.ParseVector3(rotationArray);
            Vector3? scale = GameObjectUtils.ParseVector3(scaleArray);

            if (position.HasValue && targetGo.transform.localPosition != position.Value)
            {
                targetGo.transform.localPosition = position.Value;
                modified = true;
            }
            if (rotation.HasValue && targetGo.transform.localEulerAngles != rotation.Value)
            {
                targetGo.transform.localEulerAngles = rotation.Value;
                modified = true;
            }
            if (scale.HasValue && targetGo.transform.localScale != scale.Value)
            {
                targetGo.transform.localScale = scale.Value;
                modified = true;
            }

            return modified;
        }

        #region 属性操作辅助方法

        /// <summary>
        /// 获取属性值
        /// </summary>
        private object GetPropertyValue(object target, string propertyName)
        {
            Type type = target.GetType();
            PropertyInfo propInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (propInfo != null && propInfo.CanRead)
            {
                return propInfo.GetValue(target);
            }

            FieldInfo fieldInfo = type.GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo != null)
            {
                return fieldInfo.GetValue(target);
            }

            throw new ArgumentException($"Property or field '{propertyName}' not found on type '{type.Name}'");
        }

        /// <summary>
        /// 设置属性值
        /// </summary>
        private void SetPropertyValue(object target, string propertyName, JToken value)
        {
            Type type = target.GetType();
            PropertyInfo propInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (propInfo != null && propInfo.CanWrite)
            {
                object convertedValue = ConvertValue(value, propInfo.PropertyType);
                propInfo.SetValue(target, convertedValue);
                return;
            }

            FieldInfo fieldInfo = type.GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo != null)
            {
                object convertedValue = ConvertValue(value, fieldInfo.FieldType);
                fieldInfo.SetValue(target, convertedValue);
                return;
            }

            throw new ArgumentException($"Property or field '{propertyName}' not found or is read-only on type '{type.Name}'");
        }

        /// <summary>
        /// 转换JToken值到指定类型
        /// </summary>
        private object ConvertValue(JToken token, Type targetType)
        {
            if (targetType == typeof(string))
                return token.ToObject<string>();
            if (targetType == typeof(int))
                return token.ToObject<int>();
            if (targetType == typeof(float))
                return token.ToObject<float>();
            if (targetType == typeof(bool))
                return token.ToObject<bool>();
            if (targetType == typeof(Vector3) && token is JArray arr && arr.Count == 3)
                return new Vector3(arr[0].ToObject<float>(), arr[1].ToObject<float>(), arr[2].ToObject<float>());

            // 尝试直接转换
            return token.ToObject(targetType);
        }

        #endregion

        #region 组件操作方法

        /// <summary>
        /// 处理获取组件的操作
        /// </summary>
        private object HandleGetComponentsAction(StateTreeContext args)
        {
            GameObject[] targets = GetTargetsBasedOnSelectMany(args);
            if (targets.Length == 0)
            {
                return Response.Error("No target GameObjects found in execution context.");
            }

            if (targets.Length == 1)
            {
                return GetComponentsFromTarget(targets[0]);
            }
            else
            {
                return GetComponentsFromMultipleTargets(targets);
            }
        }

        /// <summary>
        /// 处理添加组件的操作
        /// </summary>
        private object HandleAddComponentAction(StateTreeContext args)
        {
            GameObject[] targets = GetTargetsBasedOnSelectMany(args);
            if (targets.Length == 0)
            {
                return Response.Error("No target GameObjects found in execution context.");
            }

            if (targets.Length == 1)
            {
                return AddComponentToTarget(args, targets[0]);
            }
            else
            {
                return AddComponentToMultipleTargets(args, targets);
            }
        }

        /// <summary>
        /// 处理移除组件的操作
        /// </summary>
        private object HandleRemoveComponentAction(StateTreeContext args)
        {
            GameObject[] targets = GetTargetsBasedOnSelectMany(args);
            if (targets.Length == 0)
            {
                return Response.Error("No target GameObjects found in execution context.");
            }

            if (targets.Length == 1)
            {
                return RemoveComponentFromTarget(args, targets[0]);
            }
            else
            {
                return RemoveComponentFromMultipleTargets(args, targets);
            }
        }

        /// <summary>
        /// 处理设置组件属性的操作
        /// </summary>
        private object HandleSetComponentPropertyAction(StateTreeContext args)
        {
            GameObject[] targets = GetTargetsBasedOnSelectMany(args);
            if (targets.Length == 0)
            {
                return Response.Error("No target GameObjects found in execution context.");
            }

            if (targets.Length == 1)
            {
                return SetComponentPropertyOnTarget(args, targets[0]);
            }
            else
            {
                return SetComponentPropertyOnMultipleTargets(args, targets);
            }
        }

        #endregion

        #region 组件操作核心方法

        private object GetComponentsFromTarget(GameObject targetGo)
        {
            try
            {
                Component[] components = targetGo.GetComponents<Component>();
                var componentData = components.Select(c => GetComponentData(c)).ToList();
                return Response.Success(
                    $"Retrieved {componentData.Count} components from '{targetGo.name}'.",
                    componentData
                );
            }
            catch (Exception e)
            {
                return Response.Error(
                    $"Error getting components from '{targetGo.name}': {e.Message}"
                );
            }
        }

        /// <summary>
        /// 从多个GameObject获取组件
        /// </summary>
        private object GetComponentsFromMultipleTargets(GameObject[] targets)
        {
            var results = new List<Dictionary<string, object>>();
            var errors = new List<string>();
            int successCount = 0;

            foreach (GameObject targetGo in targets)
            {
                if (targetGo == null) continue;

                try
                {
                    var result = GetComponentsFromTarget(targetGo);

                    if (IsSuccessResponse(result, out object data, out string message))
                    {
                        successCount++;
                        var targetData = new Dictionary<string, object>
                        {
                            { "target", targetGo.name },
                            { "instanceID", targetGo.GetInstanceID() },
                            { "components", data }
                        };
                        results.Add(targetData);
                    }
                    else
                    {
                        errors.Add($"[{targetGo.name}] {message ?? "Unknown error"}");
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"[{targetGo.name}] Error: {e.Message}");
                }
            }

            return CreateBatchOperationResponse("get components", successCount, targets.Length, results, errors);
        }

        private object AddComponentToTarget(StateTreeContext cmd, GameObject targetGo)
        {
            string typeName = null;
            JObject properties = null;

            // Allow adding component specified directly or via components array (take first)
            if (cmd.TryGetValue("component_name", out object componentNameObj))
            {
                typeName = componentNameObj?.ToString();

                // Check if props are nested under name
                if (cmd.TryGetValue("component_properties", out object componentPropsObj))
                {
                    if (componentPropsObj is JObject allProps && !string.IsNullOrEmpty(typeName))
                    {
                        properties = allProps[typeName] as JObject ?? allProps;
                    }
                    else if (componentPropsObj is JObject directProps)
                    {
                        properties = directProps;
                    }
                }
            }
            else if (cmd.TryGetValue("components", out object componentsObj) && componentsObj is JArray componentsToAddArray && componentsToAddArray.Count > 0)
            {
                var compToken = componentsToAddArray.First;
                if (compToken.Type == JTokenType.String)
                    typeName = compToken.ToString();
                else if (compToken is JObject compObj)
                {
                    typeName = compObj["typeName"]?.ToString();
                    properties = compObj["properties"] as JObject;
                }
            }

            if (string.IsNullOrEmpty(typeName))
            {
                return Response.Error(
                    "Component type name ('component_name' or first element in 'components') is required."
                );
            }

            var addResult = AddComponentInternal(targetGo, typeName, properties);
            if (addResult != null)
                return addResult; // Return error

            // Set properties if provided (after successful component addition)
            if (properties != null)
            {
                var setResult = SetComponentPropertiesInternal(
                    targetGo,
                    typeName,
                    properties
                );
                if (setResult != null)
                {
                    // If setting properties failed, consider removing the component or log warning
                    Debug.LogWarning($"[EditGameObject] Failed to set properties for component '{typeName}': {setResult}");
                }
            }

            EditorUtility.SetDirty(targetGo);
            return Response.Success(
                $"Component '{typeName}' added to '{targetGo.name}'.",
                GetGameObjectData(targetGo)
            ); // Return updated GO data
        }

        private object RemoveComponentFromTarget(StateTreeContext cmd, GameObject targetGo)
        {
            string typeName = null;
            // Allow removing component specified directly or via components_to_remove array (take first)
            if (cmd.TryGetValue("component_name", out object componentNameObj))
            {
                typeName = componentNameObj?.ToString();
            }
            else if (cmd.TryGetValue("components_to_remove", out object componentsToRemoveObj) &&
                     componentsToRemoveObj is JArray componentsToRemoveArray &&
                     componentsToRemoveArray.Count > 0)
            {
                typeName = componentsToRemoveArray.First?.ToString();
            }

            if (string.IsNullOrEmpty(typeName))
            {
                return Response.Error(
                    "Component type name ('component_name' or first element in 'components_to_remove') is required."
                );
            }

            var removeResult = RemoveComponentInternal(targetGo, typeName);
            if (removeResult != null)
                return removeResult; // Return error

            EditorUtility.SetDirty(targetGo);
            return Response.Success(
                $"Component '{typeName}' removed from '{targetGo.name}'.",
                GetGameObjectData(targetGo)
            );
        }

        private object SetComponentPropertyOnTarget(StateTreeContext cmd, GameObject targetGo)
        {
            if (!cmd.TryGetValue("component_name", out object compNameObj) || compNameObj == null)
            {
                return Response.Error("'component_name' parameter is required.");
            }

            string compName = compNameObj.ToString();
            JObject propertiesToSet = null;

            // Properties might be directly under component_properties or nested under the component name
            if (cmd.TryGetValue("component_properties", out object compPropsObj) && compPropsObj is JObject compProps)
            {
                propertiesToSet = compProps[compName] as JObject ?? compProps; // Allow flat or nested structure
            }

            if (propertiesToSet == null || !propertiesToSet.HasValues)
            {
                return Response.Error(
                    "'component_properties' dictionary for the specified component is required and cannot be empty."
                );
            }

            var setResult = SetComponentPropertiesInternal(targetGo, compName, propertiesToSet);
            if (setResult != null)
                return setResult; // Return error

            EditorUtility.SetDirty(targetGo);
            return Response.Success(
                $"Properties set for component '{compName}' on '{targetGo.name}'.",
                GetGameObjectData(targetGo)
            );
        }

        /// <summary>
        /// 批量添加组件到多个GameObject
        /// </summary>
        private object AddComponentToMultipleTargets(StateTreeContext cmd, GameObject[] targets)
        {
            var results = new List<Dictionary<string, object>>();
            var errors = new List<string>();
            int successCount = 0;

            foreach (GameObject targetGo in targets)
            {
                if (targetGo == null) continue;

                try
                {
                    var result = AddComponentToTarget(cmd, targetGo);

                    if (IsSuccessResponse(result, out object data, out string message))
                    {
                        successCount++;
                        if (data is Dictionary<string, object> dictData)
                        {
                            results.Add(dictData);
                        }
                        else
                        {
                            results.Add(GetGameObjectData(targetGo));
                        }
                    }
                    else
                    {
                        errors.Add($"[{targetGo.name}] {message ?? "Unknown error"}");
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"[{targetGo.name}] Error: {e.Message}");
                }
            }

            return CreateBatchOperationResponse("add component", successCount, targets.Length, results, errors);
        }

        /// <summary>
        /// 批量移除组件从多个GameObject
        /// </summary>
        private object RemoveComponentFromMultipleTargets(StateTreeContext cmd, GameObject[] targets)
        {
            var results = new List<Dictionary<string, object>>();
            var errors = new List<string>();
            int successCount = 0;

            foreach (GameObject targetGo in targets)
            {
                if (targetGo == null) continue;

                try
                {
                    var result = RemoveComponentFromTarget(cmd, targetGo);

                    if (IsSuccessResponse(result, out object data, out string message))
                    {
                        successCount++;
                        if (data is Dictionary<string, object> dictData)
                        {
                            results.Add(dictData);
                        }
                        else
                        {
                            results.Add(GetGameObjectData(targetGo));
                        }
                    }
                    else
                    {
                        errors.Add($"[{targetGo.name}] {message ?? "Unknown error"}");
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"[{targetGo.name}] Error: {e.Message}");
                }
            }

            return CreateBatchOperationResponse("remove component", successCount, targets.Length, results, errors);
        }

        /// <summary>
        /// 批量设置组件属性在多个GameObject上
        /// </summary>
        private object SetComponentPropertyOnMultipleTargets(StateTreeContext cmd, GameObject[] targets)
        {
            var results = new List<Dictionary<string, object>>();
            var errors = new List<string>();
            int successCount = 0;

            foreach (GameObject targetGo in targets)
            {
                if (targetGo == null) continue;

                try
                {
                    var result = SetComponentPropertyOnTarget(cmd, targetGo);

                    if (IsSuccessResponse(result, out object data, out string message))
                    {
                        successCount++;
                        if (data is Dictionary<string, object> dictData)
                        {
                            results.Add(dictData);
                        }
                        else
                        {
                            results.Add(GetGameObjectData(targetGo));
                        }
                    }
                    else
                    {
                        errors.Add($"[{targetGo.name}] {message ?? "Unknown error"}");
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"[{targetGo.name}] Error: {e.Message}");
                }
            }

            return CreateBatchOperationResponse("set component properties", successCount, targets.Length, results, errors);
        }

        /// <summary>
        /// 检查Response对象是否表示成功
        /// </summary>
        private bool IsSuccessResponse(object response, out object data, out string message)
        {
            data = null;
            message = null;

            var resultType = response.GetType();
            var successProperty = resultType.GetProperty("success");
            var dataProperty = resultType.GetProperty("data");
            var messageProperty = resultType.GetProperty("message");
            var errorProperty = resultType.GetProperty("error");

            bool isSuccess = successProperty != null && (bool)successProperty.GetValue(response);
            data = dataProperty?.GetValue(response);
            message = isSuccess ?
                messageProperty?.GetValue(response)?.ToString() :
                (errorProperty?.GetValue(response)?.ToString() ?? messageProperty?.GetValue(response)?.ToString());

            return isSuccess;
        }

        /// <summary>
        /// 创建批量操作响应
        /// </summary>
        private object CreateBatchOperationResponse(string operation, int successCount, int totalCount,
            List<Dictionary<string, object>> results, List<string> errors)
        {
            string message;
            if (successCount == totalCount)
            {
                message = $"Successfully completed {operation} on {successCount} GameObject(s).";
            }
            else if (successCount > 0)
            {
                message = $"Completed {operation} on {successCount} of {totalCount} GameObject(s). {errors.Count} failed.";
            }
            else
            {
                message = $"Failed to complete {operation} on any of the {totalCount} GameObject(s).";
            }

            var responseData = new Dictionary<string, object>
            {
                { "operation", operation },
                { "success_count", successCount },
                { "total_count", totalCount },
                { "success_rate", (double)successCount / totalCount },
                { "affected_objects", results }
            };

            if (errors.Count > 0)
            {
                responseData["errors"] = errors;
            }

            if (successCount > 0)
            {
                return Response.Success(message, responseData);
            }
            else
            {
                return Response.Error(message, responseData);
            }
        }

        #endregion

        #region 组件辅助方法

        /// <summary>
        /// Removes a component by type name.
        /// Returns null on success, or an error response object on failure.
        /// </summary>
        private object RemoveComponentInternal(GameObject targetGo, string typeName)
        {
            Type componentType = FindComponentType(typeName);
            if (componentType == null)
            {
                return Response.Error($"Component type '{typeName}' not found for removal.");
            }

            // Prevent removing essential components
            if (componentType == typeof(Transform))
            {
                return Response.Error("Cannot remove the Transform component.");
            }

            Component componentToRemove = targetGo.GetComponent(componentType);
            if (componentToRemove == null)
            {
                return Response.Error(
                    $"Component '{typeName}' not found on '{targetGo.name}' to remove."
                );
            }

            try
            {
                // Use Undo.DestroyObjectImmediate for undo support
                Undo.DestroyObjectImmediate(componentToRemove);
                return null; // Success
            }
            catch (Exception e)
            {
                return Response.Error(
                    $"Error removing component '{typeName}' from '{targetGo.name}': {e.Message}"
                );
            }
        }

        /// <summary>
        /// Sets properties on a component.
        /// Returns null on success, or an error response object on failure.
        /// </summary>
        private object SetComponentPropertiesInternal(
            GameObject targetGo,
            string compName,
            JObject propertiesToSet,
            Component targetComponentInstance = null
        )
        {
            Component targetComponent = targetComponentInstance;

            // If no specific component instance is provided, find it by type name
            if (targetComponent == null)
            {
                // Use FindType helper to locate the correct component type
                Type componentType = FindComponentType(compName);
                if (componentType != null && typeof(Component).IsAssignableFrom(componentType))
                {
                    targetComponent = targetGo.GetComponent(componentType);
                }
                else
                {
                    // Fallback: try common Unity component namespaces
                    string[] commonNamespaces = { "UnityEngine", "UnityEngine.UI" };
                    foreach (string ns in commonNamespaces)
                    {
                        string fullTypeName = $"{ns}.{compName}";
                        componentType = FindComponentType(fullTypeName);
                        if (componentType != null && typeof(Component).IsAssignableFrom(componentType))
                        {
                            targetComponent = targetGo.GetComponent(componentType);
                            break;
                        }
                    }
                }
            }

            if (targetComponent == null)
            {
                return Response.Error(
                    $"Component '{compName}' not found on '{targetGo.name}' to set properties."
                );
            }

            Undo.RecordObject(targetComponent, "Set Component Properties");

            foreach (var prop in propertiesToSet.Properties())
            {
                string propName = prop.Name;
                JToken propValue = prop.Value;

                try
                {
                    if (!SetComponentProperty(targetComponent, propName, propValue))
                    {
                        // Log warning if property could not be set
                        Debug.LogWarning(
                            $"[EditGameObject] Could not set property '{propName}' on component '{compName}' ('{targetComponent.GetType().Name}'). Property might not exist, be read-only, or type mismatch."
                        );
                        // Optionally return an error here instead of just logging
                        // return Response.Error($"Could not set property '{propName}' on component '{compName}'.");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[EditGameObject] Error setting property '{propName}' on '{compName}': {e.Message}"
                    );
                    // Optionally return an error here
                    // return Response.Error($"Error setting property '{propName}' on '{compName}': {e.Message}");
                }
            }
            EditorUtility.SetDirty(targetComponent);
            return null; // Success (or partial success if warnings were logged)
        }

        /// <summary>
        /// Creates a serializable representation of a Component.
        /// </summary>
        private object GetComponentData(Component c)
        {
            if (c == null)
                return null;
            var data = new Dictionary<string, object>
            {
                { "typeName", c.GetType().FullName },
                { "instanceID", c.GetInstanceID() },
            };

            return data;
        }

        /// <summary>
        /// Helper to set a property or field via reflection, handling basic types.
        /// </summary>
        private bool SetComponentProperty(object target, string memberName, JToken value)
        {
            Type type = target.GetType();
            BindingFlags flags =
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

            try
            {
                // Handle special case for materials with dot notation (material.property)
                // Examples: material.color, sharedMaterial.color, materials[0].color
                if (memberName.Contains('.') || memberName.Contains('['))
                {
                    return SetNestedProperty(target, memberName, value);
                }

                PropertyInfo propInfo = type.GetProperty(memberName, flags);
                if (propInfo != null && propInfo.CanWrite)
                {
                    object convertedValue = ConvertJTokenToType(value, propInfo.PropertyType);
                    if (convertedValue != null)
                    {
                        propInfo.SetValue(target, convertedValue);
                        return true;
                    }
                }
                else
                {
                    FieldInfo fieldInfo = type.GetField(memberName, flags);
                    if (fieldInfo != null)
                    {
                        object convertedValue = ConvertJTokenToType(value, fieldInfo.FieldType);
                        if (convertedValue != null)
                        {
                            fieldInfo.SetValue(target, convertedValue);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[SetComponentProperty] Failed to set '{memberName}' on {type.Name}: {ex.Message}"
                );
            }
            return false;
        }

        /// <summary>
        /// Sets a nested property using dot notation (e.g., "material.color") or array access (e.g., "materials[0]")
        /// </summary>
        private bool SetNestedProperty(object target, string path, JToken value)
        {
            try
            {
                // Split the path into parts (handling both dot notation and array indexing)
                string[] pathParts = SplitPropertyPath(path);
                if (pathParts.Length == 0)
                    return false;

                object currentObject = target;
                Type currentType = currentObject.GetType();
                BindingFlags flags =
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

                // Traverse the path until we reach the final property
                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    string part = pathParts[i];
                    bool isArray = false;
                    int arrayIndex = -1;

                    // Check if this part contains array indexing
                    if (part.Contains("["))
                    {
                        int startBracket = part.IndexOf('[');
                        int endBracket = part.IndexOf(']');
                        if (startBracket > 0 && endBracket > startBracket)
                        {
                            string indexStr = part.Substring(
                                startBracket + 1,
                                endBracket - startBracket - 1
                            );
                            if (int.TryParse(indexStr, out arrayIndex))
                            {
                                isArray = true;
                                part = part.Substring(0, startBracket);
                            }
                        }
                    }

                    // Get the property/field
                    PropertyInfo propInfo = currentType.GetProperty(part, flags);
                    FieldInfo fieldInfo = null;
                    if (propInfo == null)
                    {
                        fieldInfo = currentType.GetField(part, flags);
                        if (fieldInfo == null)
                        {
                            Debug.LogWarning(
                                $"[SetNestedProperty] Could not find property or field '{part}' on type '{currentType.Name}'"
                            );
                            return false;
                        }
                    }

                    // Get the value
                    currentObject =
                        propInfo != null
                            ? propInfo.GetValue(currentObject)
                            : fieldInfo.GetValue(currentObject);

                    // If the current property is null, we need to stop
                    if (currentObject == null)
                    {
                        Debug.LogWarning(
                            $"[SetNestedProperty] Property '{part}' is null, cannot access nested properties."
                        );
                        return false;
                    }

                    // If this is an array/list access, get the element at the index
                    if (isArray)
                    {
                        if (currentObject is Material[])
                        {
                            var materials = currentObject as Material[];
                            if (arrayIndex < 0 || arrayIndex >= materials.Length)
                            {
                                Debug.LogWarning(
                                    $"[SetNestedProperty] Material index {arrayIndex} out of range (0-{materials.Length - 1})"
                                );
                                return false;
                            }
                            currentObject = materials[arrayIndex];
                        }
                        else if (currentObject is System.Collections.IList)
                        {
                            var list = currentObject as System.Collections.IList;
                            if (arrayIndex < 0 || arrayIndex >= list.Count)
                            {
                                Debug.LogWarning(
                                    $"[SetNestedProperty] Index {arrayIndex} out of range (0-{list.Count - 1})"
                                );
                                return false;
                            }
                            currentObject = list[arrayIndex];
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"[SetNestedProperty] Property '{part}' is not an array or list, cannot access by index."
                            );
                            return false;
                        }
                    }

                    // Update type for next iteration
                    currentType = currentObject.GetType();
                }

                // Set the final property
                string finalPart = pathParts[pathParts.Length - 1];

                // Special handling for Material properties (shader properties)
                if (currentObject is Material material && finalPart.StartsWith("_"))
                {
                    // Handle various material property types
                    if (value is JArray jArray)
                    {
                        if (jArray.Count == 4) // Color with alpha
                        {
                            Color color = new Color(
                                jArray[0].ToObject<float>(),
                                jArray[1].ToObject<float>(),
                                jArray[2].ToObject<float>(),
                                jArray[3].ToObject<float>()
                            );
                            material.SetColor(finalPart, color);
                            return true;
                        }
                        else if (jArray.Count == 3) // Color without alpha
                        {
                            Color color = new Color(
                                jArray[0].ToObject<float>(),
                                jArray[1].ToObject<float>(),
                                jArray[2].ToObject<float>(),
                                1.0f
                            );
                            material.SetColor(finalPart, color);
                            return true;
                        }
                        else if (jArray.Count == 2) // Vector2
                        {
                            Vector2 vec = new Vector2(
                                jArray[0].ToObject<float>(),
                                jArray[1].ToObject<float>()
                            );
                            material.SetVector(finalPart, vec);
                            return true;
                        }
                        else if (jArray.Count == 4) // Vector4
                        {
                            Vector4 vec = new Vector4(
                                jArray[0].ToObject<float>(),
                                jArray[1].ToObject<float>(),
                                jArray[2].ToObject<float>(),
                                jArray[3].ToObject<float>()
                            );
                            material.SetVector(finalPart, vec);
                            return true;
                        }
                    }
                    else if (value.Type == JTokenType.Float || value.Type == JTokenType.Integer)
                    {
                        material.SetFloat(finalPart, value.ToObject<float>());
                        return true;
                    }
                    else if (value.Type == JTokenType.Boolean)
                    {
                        material.SetFloat(finalPart, value.ToObject<bool>() ? 1f : 0f);
                        return true;
                    }
                    else if (value.Type == JTokenType.String)
                    {
                        // Might be a texture path
                        string texturePath = value.ToString();
                        if (
                            texturePath.EndsWith(".png")
                            || texturePath.EndsWith(".jpg")
                            || texturePath.EndsWith(".tga")
                        )
                        {
                            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                                texturePath
                            );
                            if (texture != null)
                            {
                                material.SetTexture(finalPart, texture);
                                return true;
                            }
                        }
                        else
                        {
                            // Materials don't have SetString, use SetTextureOffset as workaround or skip
                            Debug.LogWarning(
                                $"[SetNestedProperty] String values not directly supported for material property {finalPart}"
                            );
                            return false;
                        }
                    }

                    Debug.LogWarning(
                        $"[SetNestedProperty] Unsupported material property value type: {value.Type} for {finalPart}"
                    );
                    return false;
                }

                // For standard properties (not shader specific)
                PropertyInfo finalPropInfo = currentType.GetProperty(finalPart, flags);
                if (finalPropInfo != null && finalPropInfo.CanWrite)
                {
                    object convertedValue = ConvertJTokenToType(value, finalPropInfo.PropertyType);
                    if (convertedValue != null)
                    {
                        finalPropInfo.SetValue(currentObject, convertedValue);
                        return true;
                    }
                }
                else
                {
                    FieldInfo finalFieldInfo = currentType.GetField(finalPart, flags);
                    if (finalFieldInfo != null)
                    {
                        object convertedValue = ConvertJTokenToType(
                            value,
                            finalFieldInfo.FieldType
                        );
                        if (convertedValue != null)
                        {
                            finalFieldInfo.SetValue(currentObject, convertedValue);
                            return true;
                        }
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[SetNestedProperty] Could not find final property or field '{finalPart}' on type '{currentType.Name}'"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[SetNestedProperty] Error setting nested property '{path}': {ex.Message}"
                );
            }

            return false;
        }

        /// <summary>
        /// Split a property path into parts, handling both dot notation and array indexers
        /// </summary>
        private string[] SplitPropertyPath(string path)
        {
            // Handle complex paths with both dots and array indexers
            List<string> parts = new List<string>();
            int startIndex = 0;
            bool inBrackets = false;

            for (int i = 0; i < path.Length; i++)
            {
                char c = path[i];

                if (c == '[')
                {
                    inBrackets = true;
                }
                else if (c == ']')
                {
                    inBrackets = false;
                }
                else if (c == '.' && !inBrackets)
                {
                    // Found a dot separator outside of brackets
                    parts.Add(path.Substring(startIndex, i - startIndex));
                    startIndex = i + 1;
                }
            }

            // Add the final part
            if (startIndex < path.Length)
            {
                parts.Add(path.Substring(startIndex));
            }

            return parts.ToArray();
        }

        /// <summary>
        /// Simple JToken to Type conversion for common Unity types.
        /// </summary>
        private object ConvertJTokenToType(JToken token, Type targetType)
        {
            try
            {
                // Unwrap nested material properties if we're assigning to a Material
                if (typeof(Material).IsAssignableFrom(targetType) && token is JObject materialProps)
                {
                    // Handle case where we're passing shader properties directly in a nested object
                    string materialPath = token["path"]?.ToString();
                    if (!string.IsNullOrEmpty(materialPath))
                    {
                        // Load the material by path
                        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if (material != null)
                        {
                            // If there are additional properties, set them
                            foreach (var prop in materialProps.Properties())
                            {
                                if (prop.Name != "path")
                                {
                                    SetComponentProperty(material, prop.Name, prop.Value);
                                }
                            }
                            return material;
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"[ConvertJTokenToType] Could not load material at path: '{materialPath}'"
                            );
                            return null;
                        }
                    }

                    // If no path is specified, could be a dynamic material or instance set by reference
                    return null;
                }

                // Basic types first
                if (targetType == typeof(string))
                    return token.ToObject<string>();
                if (targetType == typeof(int))
                    return token.ToObject<int>();
                if (targetType == typeof(float))
                    return token.ToObject<float>();
                if (targetType == typeof(bool))
                    return token.ToObject<bool>();

                // Vector/Quaternion/Color types
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

                // Enum types
                if (targetType.IsEnum)
                    return Enum.Parse(targetType, token.ToString(), true); // Case-insensitive enum parsing

                // Handle assigning Unity Objects (Assets, Scene Objects, Components)
                if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
                {
                    // CASE 1: Reference is a JSON Object specifying a scene object/component find criteria
                    if (token is JObject refObject)
                    {
                        JToken findToken = refObject["find"];
                        string findMethod =
                            refObject["method"]?.ToString() ?? "by_id_or_name_or_path"; // Default search
                        string componentTypeName = refObject["component"]?.ToString();

                        if (findToken == null)
                        {
                            Debug.LogWarning(
                                $"[ConvertJTokenToType] Reference object missing 'find' property: {token}"
                            );
                            return null;
                        }

                        // Find the target GameObject
                        // Pass 'searchInactive: true' for internal lookups to be more robust
                        JObject findParams = new JObject();
                        findParams["searchInactive"] = true;
                        GameObject foundGo = GameObjectUtils.FindObjectInternal(findToken, findMethod, findParams);

                        if (foundGo == null)
                        {
                            Debug.LogWarning(
                                $"[ConvertJTokenToType] Could not find GameObject specified by reference object: {token}"
                            );
                            return null;
                        }

                        // If a component type is specified, try to get it
                        if (!string.IsNullOrEmpty(componentTypeName))
                        {
                            Type compType = FindComponentType(componentTypeName);
                            if (compType == null)
                            {
                                Debug.LogWarning(
                                    $"[ConvertJTokenToType] Could not find component type '{componentTypeName}' specified in reference object: {token}"
                                );
                                return null;
                            }

                            // Ensure the targetType is assignable from the found component type
                            if (!targetType.IsAssignableFrom(compType))
                            {
                                Debug.LogWarning(
                                    $"[ConvertJTokenToType] Found component '{componentTypeName}' but it is not assignable to the target property type '{targetType.Name}'. Reference: {token}"
                                );
                                return null;
                            }

                            Component foundComp = foundGo.GetComponent(compType);
                            if (foundComp == null)
                            {
                                Debug.LogWarning(
                                    $"[ConvertJTokenToType] Found GameObject '{foundGo.name}' but could not find component '{componentTypeName}' on it. Reference: {token}"
                                );
                                return null;
                            }
                            return foundComp; // Return the found component
                        }
                        else
                        {
                            // Otherwise, return the GameObject itself, ensuring it's assignable
                            if (!targetType.IsAssignableFrom(typeof(GameObject)))
                            {
                                Debug.LogWarning(
                                    $"[ConvertJTokenToType] Found GameObject '{foundGo.name}' but it is not assignable to the target property type '{targetType.Name}' (component name was not specified). Reference: {token}"
                                );
                                return null;
                            }
                            return foundGo; // Return the found GameObject
                        }
                    }
                    // CASE 2: Reference is a string, assume it's an asset path
                    else if (token.Type == JTokenType.String)
                    {
                        string assetPath = token.ToString();
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            // Attempt to load the asset from the provided path using the target type
                            UnityEngine.Object loadedAsset = AssetDatabase.LoadAssetAtPath(
                                assetPath,
                                targetType
                            );
                            if (loadedAsset != null)
                            {
                                return loadedAsset; // Return the loaded asset if successful
                            }
                            else
                            {
                                // Log a warning if the asset could not be found at the path
                                Debug.LogWarning(
                                    $"[ConvertJTokenToType] Could not load asset of type '{targetType.Name}' from path: '{assetPath}'. Make sure the path is correct and the asset exists."
                                );
                                return null;
                            }
                        }
                        else
                        {
                            // Handle cases where an empty string might be intended to clear the reference
                            return null; // Assign null if the path is empty
                        }
                    }
                    // CASE 3: Reference is null or empty JToken, assign null
                    else if (
                        token.Type == JTokenType.Null
                        || string.IsNullOrEmpty(token.ToString())
                    )
                    {
                        return null;
                    }
                    // CASE 4: Invalid format for Unity Object reference
                    else
                    {
                        Debug.LogWarning(
                            $"[ConvertJTokenToType] Expected a string asset path or a reference object to assign Unity Object of type '{targetType.Name}', but received token type '{token.Type}'. Value: {token}"
                        );
                        return null;
                    }
                }

                // Fallback: Try direct conversion (might work for other simple value types)
                // Be cautious here, this might throw errors for complex types not handled above
                try
                {
                    return token.ToObject(targetType);
                }
                catch (Exception directConversionEx)
                {
                    Debug.LogWarning(
                        $"[ConvertJTokenToType] Direct conversion failed for JToken '{token}' to type '{targetType.Name}': {directConversionEx.Message}. Specific handling might be needed."
                    );
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[ConvertJTokenToType] Could not convert JToken '{token}' to type '{targetType.Name}': {ex.Message}"
                );
                return null;
            }
        }

        #endregion

        #region 辅助查找方法

        /// <summary>
        /// 查找父GameObject（用于设置父级关系）
        /// </summary>
        private GameObject FindParentGameObject(JToken parentToken)
        {
            if (parentToken == null || parentToken.Type == JTokenType.Null)
                return null;

            string parentIdentifier = parentToken.ToString();
            if (string.IsNullOrEmpty(parentIdentifier))
                return null;

            // 使用GameObjectDynamicSelector来查找父对象
            var selector = new ObjectSelector<GameObject>();
            JObject findArgs = new JObject();
            findArgs["id"] = parentIdentifier;
            findArgs["path"] = "";
            var stateTree = selector.BuildStateTree();
            object result = stateTree.Run(new StateTreeContext(findArgs));

            if (result is GameObject[] gameObjects && gameObjects.Length > 0)
            {
                return gameObjects[0]; // 返回找到的第一个对象
            }

            return null;
        }

        /// <summary>
        /// 查找组件类型
        /// </summary>
        /// <summary>
        /// 查找组件类型，遍历所有已加载的程序集，名字或全名匹配且继承自Component即可
        /// </summary>
        private Type FindComponentType(string componentName)
        {
            if (string.IsNullOrEmpty(componentName))
                return null;

            // 遍历所有已加载的程序集
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                Type[] types = null;
                try
                {
                    types = assembly.GetTypes();
                }
                catch
                {
                    // 某些动态程序集可能抛异常，忽略
                    continue;
                }

                foreach (var type in types)
                {
                    if (!typeof(Component).IsAssignableFrom(type))
                        continue;

                    // 名字或全名匹配即可
                    if (type.Name == componentName || type.FullName == componentName)
                    {
                        return type;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 添加组件到GameObject的内部实现
        /// </summary>
        private object AddComponentInternal(GameObject targetGo, string typeName, JObject properties)
        {
            Type componentType = FindComponentType(typeName);
            if (componentType == null)
            {
                return Response.Error($"Component type '{typeName}' not found.");
            }

            // 检查是否已经存在该组件（对于不允许重复的组件）
            if (componentType == typeof(Transform) || componentType == typeof(RectTransform))
            {
                return Response.Error($"Cannot add component '{typeName}' because it already exists or is not allowed to be duplicated.");
            }

            try
            {
                Component addedComponent = targetGo.AddComponent(componentType);
                if (addedComponent == null)
                {
                    return Response.Error($"Failed to add component '{typeName}' to '{targetGo.name}'.");
                }

                Undo.RegisterCreatedObjectUndo(addedComponent, $"Add Component {typeName}");
                LogInfo($"[EditGameObject] Successfully added component '{typeName}' to '{targetGo.name}'");

                return null; // Success
            }
            catch (Exception e)
            {
                return Response.Error($"Error adding component '{typeName}' to '{targetGo.name}': {e.Message}");
            }
        }

        /// <summary>
        /// 获取GameObject的数据表示
        /// </summary>
        private Dictionary<string, object> GetGameObjectData(GameObject go)
        {
            if (go == null) return null;

            var data = new Dictionary<string, object>
            {
                { "name", go.name },
                { "instanceID", go.GetInstanceID() },
                { "active", go.activeSelf },
                { "tag", go.tag },
                { "layer", go.layer },
                { "position", new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z } },
                { "rotation", new { x = go.transform.rotation.x, y = go.transform.rotation.y, z = go.transform.rotation.z, w = go.transform.rotation.w } },
                { "scale", new { x = go.transform.localScale.x, y = go.transform.localScale.y, z = go.transform.localScale.z } },
                { "parent", go.transform.parent?.name },
                { "childCount", go.transform.childCount }
            };

            // 添加组件信息
            Component[] components = go.GetComponents<Component>();
            var componentNames = components.Where(c => c != null).Select(c => c.GetType().Name).ToArray();
            data["components"] = componentNames;

            return data;
        }

        #endregion
    }
}
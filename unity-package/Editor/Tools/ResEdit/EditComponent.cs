using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles Component-specific operations using dual state tree architecture.
    /// First tree: Target location (using GameObjectSelector)
    /// Second tree: Component operation execution
    /// 对应方法名: edit_component
    /// </summary>
    [ToolName("edit_component", "资源管理")]
    public class EditComponent : DualStateMethodBase
    {
        /// <summary>
        /// 目标查找
        /// </summary>
        private IObjectSelector objectSelector;
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                // 目标查找参数
                new MethodKey("instance_id", "Object InstanceID", false),
                new MethodKey("path", "Object Hierarchy path", false),
                new MethodKey("action", "Operation type: get_component_propertys, set_component_propertys", true),
                new MethodKey("component_type", "Component type name (type name inheriting from Component)", true),
                new MethodKey("properties", "Properties dictionary (set_component_propertys)", false),
            };
        }

        /// <summary>
        /// 创建目标定位状态树（使用GameObjectSelector）
        /// </summary>
        protected override StateTree CreateTargetTree()
        {
            objectSelector = objectSelector ?? new HierarchySelector<GameObject>();
            return objectSelector.BuildStateTree();
        }

        /// <summary>
        /// 创建组件操作执行状态树
        /// </summary>
        protected override StateTree CreateActionTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("get_component_propertys", (Func<StateTreeContext, object>)HandleGetComponentPropertysAction)
                    .Leaf("set_component_propertys", (Func<StateTreeContext, object>)HandleSetComponentPropertysAction)
                    .DefaultLeaf((Func<StateTreeContext, object>)HandleDefaultAction)
                .Build();
        }

        /// <summary>
        /// 默认操作处理（当没有指定具体action时）
        /// </summary>
        private object HandleDefaultAction(StateTreeContext args)
        {
            if (args.ContainsKey("properties"))
            {
                return HandleSetComponentPropertysAction(args);
            }
            return Response.Error("Action is required for edit_component. Valid actions are: get_component_propertys, set_component_propertys.");
        }

        /// <summary>
        /// 从执行上下文中提取目标GameObject（单个）
        /// </summary>
        private GameObject ExtractTargetFromContext(StateTreeContext context)
        {
            // 先尝试从ObjectReferences获取
            if (context.TryGetObjectReference("_resolved_targets", out object targetsObj))
            {
                if (targetsObj is GameObject singleGameObject)
                {
                    return singleGameObject;
                }
                else if (targetsObj is GameObject[] gameObjectArray && gameObjectArray.Length > 0)
                {
                    return gameObjectArray[0]; // 只取第一个
                }
                else if (targetsObj is System.Collections.IList list && list.Count > 0)
                {
                    if (list[0] is GameObject go)
                        return go;
                }
            }

            // 如果ObjectReferences中没有，尝试从JsonData获取
            if (context.TryGetJsonValue("_resolved_targets", out JToken targetToken))
            {
                if (targetToken is JArray targetArray && targetArray.Count > 0)
                {
                    return targetArray[0].ToObject<GameObject>();
                }
                else
                {
                    return targetToken.ToObject<GameObject>();
                }
            }

            return null;
        }



        #region 组件操作Action Handlers

        /// <summary>
        /// 处理获取组件属性的操作（批量）
        /// </summary>
        private object HandleGetComponentPropertysAction(StateTreeContext args)
        {
            GameObject target = ExtractTargetFromContext(args);
            if (target == null)
            {
                return Response.Error("No target GameObject found in execution context.");
            }

            return GetComponentPropertysFromTarget(args, target);
        }

        /// <summary>
        /// 处理设置组件属性的操作（批量）
        /// </summary>
        private object HandleSetComponentPropertysAction(StateTreeContext args)
        {
            GameObject target = ExtractTargetFromContext(args);
            if (target == null)
            {
                return Response.Error("No target GameObject found in execution context.");
            }

            return SetComponentPropertysOnTarget(args, target);
        }

        #endregion

        #region 组件操作核心方法

        /// <summary>
        /// 获取组件属性的具体实现（批量）
        /// </summary>
        private object GetComponentPropertysFromTarget(StateTreeContext cmd, GameObject targetGo)
        {
            if (!cmd.TryGetValue("component_type", out object compNameObj) || compNameObj == null)
            {
                return Response.Error("'component_type' parameter is required.");
            }

            string compName = compNameObj.ToString();

            // 查找组件
            Component targetComponent = FindComponentOnGameObject(targetGo, compName);
            if (targetComponent == null)
            {
                return Response.Error($"Component '{compName}' not found on '{targetGo.name}'.");
            }

            // 获取所有可读属性
            var properties = cmd["properties"] as JObject;

            // 获取所有公共属性
            foreach (var pair in properties)
            {
                try
                {
                    var success = GameObjectUtils.SetObjectPropertyDeepth(targetComponent, pair.Key, pair.Value, LogInfo);
                    if (!success)
                    {
                        return Response.Error($"Failed to set property '{pair.Key}' on '{compName}': {pair.Key}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GetComponentPropertysFromTarget] Failed to set property '{pair.Key}' on '{compName}': {ex.Message}");
                    return Response.Error($"Failed to set property '{pair.Key}' on '{compName}': {ex.Message}");
                }
            }

            return Response.Success(
                $"Retrieved {properties.Count} properties from component '{compName}' on '{targetGo.name}'.",
                new Dictionary<string, object>
                {
                    { "component_type", compName },
                    { "properties", properties }
                }
            );
        }





        /// <summary>
        /// 设置组件属性的具体实现（批量）
        /// </summary>
        private object SetComponentPropertysOnTarget(StateTreeContext cmd, GameObject targetGo)
        {
            if (!cmd.TryGetValue("component_type", out object compNameObj) || compNameObj == null)
            {
                return Response.Error("'component_type' parameter is required.");
            }

            string compName = compNameObj.ToString();

            // 查找组件
            Component targetComponent = FindComponentOnGameObject(targetGo, compName);
            if (targetComponent == null)
            {
                return Response.Error($"Component '{compName}' not found on '{targetGo.name}'.");
            }

            // 获取要设置的属性字典
            if (!cmd.TryGetValue("properties", out object propertiesObj) || propertiesObj == null)
            {
                return Response.Error("'properties' parameter is required for setting component properties.");
            }

            JObject propertiesToSet = null;
            if (propertiesObj is JObject jObj)
            {
                propertiesToSet = jObj;
            }
            else
            {
                // 尝试从其他格式转换
                try
                {
                    propertiesToSet = JObject.FromObject(propertiesObj);
                }
                catch (Exception ex)
                {
                    return Response.Error($"Failed to parse 'properties' parameter: {ex.Message}");
                }
            }

            if (propertiesToSet == null || !propertiesToSet.HasValues)
            {
                return Response.Error("'properties' dictionary cannot be empty.");
            }

            Undo.RecordObject(targetComponent, "Set Component Properties");

            var results = new Dictionary<string, object>();
            var errors = new List<string>();
            int successCount = 0;

            foreach (var prop in propertiesToSet.Properties())
            {
                string propName = prop.Name;
                JToken propValue = prop.Value;

                try
                {
                    if (SetComponentProperty(targetComponent, propName, propValue, out string error))
                    {
                        results[propName] = "Success";
                        successCount++;
                    }
                    else
                    {
                        results[propName] = "Failed";
                        Debug.LogError($"[SetComponentPropertysOnTarget] Failed to set property '{propName}': {error}");
                        errors.Add($"Property '{propName}': Failed to set {error}");
                    }
                }
                catch (Exception e)
                {
                    results[propName] = $"Error: {e.Message}";
                    errors.Add($"Property '{propName}': {e.Message}");
                }
            }

            EditorUtility.SetDirty(targetComponent);

            string message = $"Set {successCount} of {propertiesToSet.Count} properties on component '{compName}' on '{targetGo.name}'.";
            var responseData = new Dictionary<string, object>
            {
                { "component_type", compName },
                { "total_properties", propertiesToSet.Count },
                { "successful_properties", successCount },
                { "failed_properties", propertiesToSet.Count - successCount },
                { "results", results }
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
                return Response.Error($"Failed to set any properties on component '{compName}'.", responseData);
            }
        }



        #endregion

        #region 组件辅助方法

        /// <summary>
        /// 在GameObject上查找组件
        /// </summary>
        private Component FindComponentOnGameObject(GameObject gameObject, string componentName)
        {
            Type componentType = FindComponentType(componentName);
            if (componentType != null && typeof(Component).IsAssignableFrom(componentType))
            {
                return gameObject.GetComponent(componentType);
            }
            return null;
        }

        /// <summary>
        /// 将值转换为可序列化的格式
        /// </summary>
        private object ConvertToSerializableValue(object value)
        {
            if (value == null) return null;

            Type valueType = value.GetType();

            // 基本类型直接返回
            if (valueType.IsPrimitive || value is string || value is decimal)
            {
                return value;
            }

            // Unity基本类型转换
            if (value is Vector2 v2)
                return new { x = v2.x, y = v2.y };
            if (value is Vector3 v3)
                return new { x = v3.x, y = v3.y, z = v3.z };
            if (value is Vector4 v4)
                return new { x = v4.x, y = v4.y, z = v4.z, w = v4.w };
            if (value is Quaternion q)
                return new { x = q.x, y = q.y, z = q.z, w = q.w };
            if (value is Color c)
                return new { r = c.r, g = c.g, b = c.b, a = c.a };
            if (value is Rect rect)
                return new { x = rect.x, y = rect.y, width = rect.width, height = rect.height };

            // Unity Object引用
            if (value is UnityEngine.Object unityObj)
            {
                if (unityObj == null)
                    return null;
                return new
                {
                    name = unityObj.name,
                    type = unityObj.GetType().Name,
                    instanceID = unityObj.GetInstanceID()
                };
            }

            // 枚举类型
            if (valueType.IsEnum)
            {
                return value.ToString();
            }

            // 数组或列表
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(ConvertToSerializableValue(item));
                    if (list.Count > 10) // 限制数组长度避免过大
                    {
                        list.Add("...(truncated)");
                        break;
                    }
                }
                return list.ToArray();
            }

            // 复杂对象，尝试序列化为字符串
            try
            {
                return value.ToString();
            }
            catch
            {
                return $"<{valueType.Name}>";
            }
        }

        /// <summary>
        /// 获取组件属性值
        /// </summary>
        private object GetComponentProperty(Component component, string propertyName)
        {
            Type type = component.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

            try
            {
                // 处理嵌套属性（点符号分隔）
                if (propertyName.Contains('.') || propertyName.Contains('['))
                {
                    return GetNestedProperty(component, propertyName);
                }

                PropertyInfo propInfo = type.GetProperty(propertyName, flags);
                if (propInfo != null && propInfo.CanRead)
                {
                    return propInfo.GetValue(component);
                }
                else
                {
                    FieldInfo fieldInfo = type.GetField(propertyName, flags);
                    if (fieldInfo != null)
                    {
                        return fieldInfo.GetValue(component);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GetComponentProperty] Failed to get '{propertyName}' from {type.Name}: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 获取嵌套属性值
        /// </summary>
        private object GetNestedProperty(object target, string path)
        {
            try
            {
                string[] pathParts = SplitPropertyPath(path);
                if (pathParts.Length == 0) return null;

                object currentObject = target;
                Type currentType = currentObject.GetType();
                BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

                for (int i = 0; i < pathParts.Length; i++)
                {
                    string part = pathParts[i];
                    bool isArray = false;
                    int arrayIndex = -1;

                    if (part.Contains("["))
                    {
                        int startBracket = part.IndexOf('[');
                        int endBracket = part.IndexOf(']');
                        if (startBracket > 0 && endBracket > startBracket)
                        {
                            string indexStr = part.Substring(startBracket + 1, endBracket - startBracket - 1);
                            if (int.TryParse(indexStr, out arrayIndex))
                            {
                                isArray = true;
                                part = part.Substring(0, startBracket);
                            }
                        }
                    }

                    PropertyInfo propInfo = currentType.GetProperty(part, flags);
                    FieldInfo fieldInfo = null;
                    if (propInfo == null)
                    {
                        fieldInfo = currentType.GetField(part, flags);
                        if (fieldInfo == null) return null;
                    }

                    currentObject = propInfo != null ? propInfo.GetValue(currentObject) : fieldInfo.GetValue(currentObject);
                    if (currentObject == null) return null;

                    if (isArray)
                    {
                        if (currentObject is System.Collections.IList list)
                        {
                            if (arrayIndex < 0 || arrayIndex >= list.Count) return null;
                            currentObject = list[arrayIndex];
                        }
                        else return null;
                    }

                    currentType = currentObject.GetType();
                }

                return currentObject;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GetNestedProperty] Error getting nested property '{path}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Helper to set a property or field via reflection, handling basic types.
        /// </summary>
        private bool SetComponentProperty(object target, string memberName, JToken value, out string error)
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
                    return SetNestedProperty(target, memberName, value, out error);
                }

                PropertyInfo propInfo = type.GetProperty(memberName, flags);
                if (propInfo != null && propInfo.CanWrite)
                {
                    object convertedValue = ConvertJTokenToType(value, propInfo.PropertyType);
                    if (convertedValue != null)
                    {
                        propInfo.SetValue(target, convertedValue);
                        error = null;
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
                            error = null;
                            return true;
                        }
                    }
                }
                error = $"{target} ,Failed to set '{memberName}' on {type.Name}";
                Debug.LogError(error);
                return false;
            }
            catch (Exception ex)
            {
                error = $"{target} ,Failed to set '{memberName}' on {type.Name}: {ex.Message}";
                Debug.LogError(error);
            }
            return false;
        }

        /// <summary>
        /// Sets a nested property using dot notation (e.g., "material.color") or array access (e.g., "materials[0]")
        /// </summary>
        private bool SetNestedProperty(object target, string path, JToken value, out string error)
        {
            try
            {
                // Split the path into parts (handling both dot notation and array indexing)
                string[] pathParts = SplitPropertyPath(path);
                if (pathParts.Length == 0)
                {
                    error = "Path parts length is 0";
                    return false;
                }

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
                            error = $"{currentObject} ,Could not find property or field '{part}' on type '{currentType.Name}'";
                            Debug.LogWarning(error);
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
                        error = $"{currentObject} ,Property '{part}' is null, cannot access nested properties.";
                        Debug.LogWarning(error);
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
                                error = $"{currentObject} ,Material index {arrayIndex} out of range (0-{materials.Length - 1})";
                                Debug.LogWarning(error);
                                return false;
                            }
                            currentObject = materials[arrayIndex];
                        }
                        else if (currentObject is System.Collections.IList)
                        {
                            var list = currentObject as System.Collections.IList;
                            if (arrayIndex < 0 || arrayIndex >= list.Count)
                            {
                                error = $"{currentObject} ,Index {arrayIndex} out of range (0-{list.Count - 1})";
                                Debug.LogWarning(error);
                                return false;
                            }
                            currentObject = list[arrayIndex];
                        }
                        else
                        {
                            error = $"{currentObject} ,Property '{part}' is not an array or list, cannot access by index.";
                            Debug.LogWarning(error);
                            return false;
                        }
                    }

                    // Update type for next iteration
                    currentType = currentObject.GetType();
                }

                LogInfo($"{currentObject} ,Current type: {currentType}");
                if (typeof(UnityEngine.Object).IsAssignableFrom(currentType))
                {
                    var assetPath = AssetDatabase.GetAssetPath(currentObject as UnityEngine.Object);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        error = $"{currentObject} ,Asset path is null";
                        return false;
                    }
                    if (!string.IsNullOrEmpty(assetPath) && !assetPath.StartsWith(Application.dataPath) && !assetPath.StartsWith("Assets/"))
                    {
                        error = $"{currentObject} ,Asset path is not in the assets path";
                        return false;
                    }
                }

                // Set the final property
                string finalPart = pathParts[pathParts.Length - 1];

                // Special handling for Material properties (shader properties)
                if (currentObject is Material material && finalPart.StartsWith("_"))
                {
                    return SetMaterialShaderProperty(material, finalPart, value, out error);
                }

                // For standard properties (not shader specific)
                PropertyInfo finalPropInfo = currentType.GetProperty(finalPart, flags);
                if (finalPropInfo != null && finalPropInfo.CanWrite)
                {
                    object convertedValue = ConvertJTokenToType(value, finalPropInfo.PropertyType);
                    if (convertedValue != null)
                    {
                        finalPropInfo.SetValue(currentObject, convertedValue);
                        error = null;
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
                            error = null;
                            return true;
                        }
                    }
                    else
                    {
                        error = $"{currentObject} ,Could not find final property or field '{finalPart}' on type '{currentType.Name}'";
                        Debug.LogWarning(error);
                    }
                }
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Error setting nested property '{path}': {ex.Message}";
                Debug.LogError(error);
                return false;
            }
        }

        /// <summary>
        /// Sets material shader properties
        /// </summary>
        private bool SetMaterialShaderProperty(Material material, string propertyName, JToken value, out string error)
        {
            try
            {
                // Handle various material property types
                if (value is JArray jArray)
                {
                    if (jArray.Count == 4) // Color with alpha or Vector4
                    {
                        if (propertyName.ToLower().Contains("color"))
                        {
                            Color color = new Color(
                                jArray[0].ToObject<float>(),
                                jArray[1].ToObject<float>(),
                                jArray[2].ToObject<float>(),
                                jArray[3].ToObject<float>()
                            );
                            material.SetColor(propertyName, color);
                            error = null;
                            return true;
                        }
                        else
                        {
                            Vector4 vec = new Vector4(
                                jArray[0].ToObject<float>(),
                                jArray[1].ToObject<float>(),
                                jArray[2].ToObject<float>(),
                                jArray[3].ToObject<float>()
                            );
                            material.SetVector(propertyName, vec);
                            error = null;
                            return true;
                        }
                    }
                    else if (jArray.Count == 3) // Color without alpha
                    {
                        Color color = new Color(
                            jArray[0].ToObject<float>(),
                            jArray[1].ToObject<float>(),
                            jArray[2].ToObject<float>(),
                            1.0f
                        );
                        material.SetColor(propertyName, color);
                        error = null;
                        return true;
                    }
                    else if (jArray.Count == 2) // Vector2
                    {
                        Vector2 vec = new Vector2(
                            jArray[0].ToObject<float>(),
                            jArray[1].ToObject<float>()
                        );
                        material.SetVector(propertyName, vec);
                        error = null;
                        return true;
                    }
                }
                else if (value.Type == JTokenType.Float || value.Type == JTokenType.Integer)
                {
                    material.SetFloat(propertyName, value.ToObject<float>());
                    error = null;
                    return true;
                }
                else if (value.Type == JTokenType.Boolean)
                {
                    material.SetFloat(propertyName, value.ToObject<bool>() ? 1f : 0f);
                    error = null;
                    return true;
                }
                else if (value.Type == JTokenType.String)
                {
                    // Might be a texture path
                    string texturePath = value.ToString();
                    if (texturePath.EndsWith(".png") || texturePath.EndsWith(".jpg") || texturePath.EndsWith(".tga"))
                    {
                        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                        if (texture != null)
                        {
                            material.SetTexture(propertyName, texture);
                            error = null;
                            return true;
                        }
                    }
                }

                error = $"{material} ,Unsupported material property value type: {value.Type} for {propertyName}";
                Debug.LogWarning(error);
                return false;
            }
            catch (Exception ex)
            {
                error = $"{material} ,Error setting material property '{propertyName}': {ex.Message}";
                Debug.LogError(error);
                return false;
            }
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
                if (targetType == typeof(Color) && token is JArray arrC && arrC.Count >= 3)
                    return new Color(
                        arrC[0].ToObject<float>(),
                        arrC[1].ToObject<float>(),
                        arrC[2].ToObject<float>(),
                        arrC.Count > 3 ? arrC[3].ToObject<float>() : 1.0f
                    );

                // Enum types
                if (targetType.IsEnum)
                    return Enum.Parse(targetType, token.ToString(), true);

                // Handle Unity Objects (Assets)
                if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
                {
                    if (token.Type == JTokenType.String)
                    {
                        string assetPath = token.ToString();
                        if (!string.IsNullOrEmpty(assetPath) && System.IO.File.Exists(assetPath))
                        {
                            UnityEngine.Object loadedAsset = AssetDatabase.LoadAssetAtPath(assetPath, targetType);
                            if (loadedAsset != null)
                            {
                                return loadedAsset;
                            }
                            else
                            {
                                Debug.LogWarning($"[ConvertJTokenToType] Could not load asset of type '{targetType.Name}' from path: '{assetPath}'");
                            }
                        }
                        else
                        {
                            var sceneObj = GameObjectUtils.FindByHierarchyPath(assetPath, targetType);
                            if (sceneObj != null)
                            {
                                return sceneObj;
                            }
                            else
                            {
                                Debug.LogWarning($"[ConvertJTokenToType] Could not load asset of type '{targetType.Name}' from path: '{assetPath}'");
                            }
                        }
                    }
                    else if (token.Type == JTokenType.Integer)
                    {
                        var instanceId = token.ToObject<int>();
                        var objectItem = UnityEditor.EditorUtility.InstanceIDToObject(instanceId);
                        if (objectItem != null)
                        {
                            if (objectItem.GetType() == targetType)
                            {
                                return objectItem;
                            }
                            else if (objectItem is GameObject go && typeof(Component).IsAssignableFrom(targetType))
                            {
                                return go.GetComponent(targetType);
                            }
                            else
                            {
                                Debug.LogWarning($"[ConvertJTokenToType] Could not load asset of type '{targetType.Name}' from instance id: '{instanceId}'");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[ConvertJTokenToType] Could not load asset of type '{targetType.Name}' from instance id: '{instanceId}'");
                        }
                    }

                    return null;
                }

                // Fallback: Try direct conversion
                return token.ToObject(targetType);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConvertJTokenToType] Could not convert JToken '{token}' to type '{targetType.Name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 查找组件类型，遍历所有已加载的程序集
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



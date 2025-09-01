using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// GameObject操作的通用工具类
    /// </summary>
    public static class GameObjectUtils
    {
        /// <summary>
        /// 根据Token和搜索方法查找单个GameObject
        /// </summary>
        public static GameObject FindObjectInternal(
            JToken targetToken,
            string searchMethod,
            JObject findParams = null
        )
        {
            // If find_all is not explicitly false, we still want only one for most single-target operations.
            bool findAll = findParams?["find_all"]?.ToObject<bool>() ?? false;
            // If a specific target ID is given, always find just that one.
            if (
                targetToken?.Type == JTokenType.Integer
                || (searchMethod == "by_id" && int.TryParse(targetToken?.ToString(), out _))
            )
            {
                findAll = false;
            }
            List<GameObject> results = FindObjectsInternal(
                targetToken,
                searchMethod,
                findAll,
                findParams
            );
            return results.Count > 0 ? results[0] : null;
        }

        /// <summary>
        /// 根据Token和搜索方法查找多个GameObject
        /// </summary>
        public static List<GameObject> FindObjectsInternal(
            JToken targetToken,
            string searchMethod,
            bool findAll,
            JObject findParams = null
        )
        {
            List<GameObject> results = new List<GameObject>();
            string searchTerm = findParams?["search_term"]?.ToString() ?? targetToken?.ToString();
            bool searchInChildren = findParams?["search_in_children"]?.ToObject<bool>() ?? false;
            bool searchInactive = findParams?["search_in_inactive"]?.ToObject<bool>() ?? false;

            // Default search method if not specified
            if (string.IsNullOrEmpty(searchMethod))
            {
                if (targetToken?.Type == JTokenType.Integer)
                    searchMethod = "by_id";
                else if (!string.IsNullOrEmpty(searchTerm) && searchTerm.Contains('/'))
                    searchMethod = "by_path";
                else
                    searchMethod = "by_name"; // Default fallback
            }

            GameObject rootSearchObject = null;
            // If searching in children, find the initial target first
            if (searchInChildren && targetToken != null)
            {
                rootSearchObject = FindObjectInternal(targetToken, "by_id_or_name_or_path");
                if (rootSearchObject == null)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[GameObjectUtils.Find] Root object '{targetToken}' for child search not found."
                    );
                    return results;
                }
            }

            switch (searchMethod)
            {
                case "by_id":
                    if (int.TryParse(searchTerm, out int instanceId))
                    {
                        var allObjects = GetAllSceneObjects(searchInactive);
                        GameObject obj = allObjects.FirstOrDefault(go =>
                            go.GetInstanceID() == instanceId
                        );
                        if (obj != null)
                            results.Add(obj);
                    }
                    break;
                case "by_name":
                    var searchPoolName = rootSearchObject
                        ? rootSearchObject
                            .GetComponentsInChildren<Transform>(searchInactive)
                            .Select(t => t.gameObject)
                        : GetAllSceneObjects(searchInactive);
                    results.AddRange(searchPoolName.Where(go => go.name == searchTerm));
                    break;
                case "by_path":
                    Transform foundTransform = rootSearchObject
                        ? rootSearchObject.transform.Find(searchTerm)
                        : GameObject.Find(searchTerm)?.transform;
                    if (foundTransform != null)
                        results.Add(foundTransform.gameObject);
                    break;
                case "by_tag":
                    var searchPoolTag = rootSearchObject
                        ? rootSearchObject
                            .GetComponentsInChildren<Transform>(searchInactive)
                            .Select(t => t.gameObject)
                        : GetAllSceneObjects(searchInactive);
                    results.AddRange(searchPoolTag.Where(go => go.CompareTag(searchTerm)));
                    break;
                case "by_layer":
                    var searchPoolLayer = rootSearchObject
                        ? rootSearchObject
                            .GetComponentsInChildren<Transform>(searchInactive)
                            .Select(t => t.gameObject)
                        : GetAllSceneObjects(searchInactive);
                    if (int.TryParse(searchTerm, out int layerIndex))
                    {
                        results.AddRange(searchPoolLayer.Where(go => go.layer == layerIndex));
                    }
                    else
                    {
                        int namedLayer = LayerMask.NameToLayer(searchTerm);
                        if (namedLayer != -1)
                            results.AddRange(searchPoolLayer.Where(go => go.layer == namedLayer));
                    }
                    break;
                case "by_component":
                    Type componentType = FindType(searchTerm);
                    if (componentType != null)
                    {
                        FindObjectsInactive findInactive = searchInactive
                            ? FindObjectsInactive.Include
                            : FindObjectsInactive.Exclude;
                        var searchPoolComp = rootSearchObject
                            ? rootSearchObject
                                .GetComponentsInChildren(componentType, searchInactive)
                                .Select(c => (c as Component).gameObject)
                            : UnityEngine
                                .Object.FindObjectsByType(
                                    componentType,
                                    findInactive,
                                    FindObjectsSortMode.None
                                )
                                .Select(c => (c as Component).gameObject);
                        results.AddRange(searchPoolComp.Where(go => go != null));
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[GameObjectUtils.Find] Component type not found: {searchTerm}"
                        );
                    }
                    break;
                case "by_id_or_name_or_path":
                    if (int.TryParse(searchTerm, out int id))
                    {
                        var allObjectsId = GetAllSceneObjects(true);
                        GameObject objById = allObjectsId.FirstOrDefault(go =>
                            go.GetInstanceID() == id
                        );
                        if (objById != null)
                        {
                            results.Add(objById);
                            break;
                        }
                    }
                    GameObject objByPath = GameObject.Find(searchTerm);
                    if (objByPath != null)
                    {
                        results.Add(objByPath);
                        break;
                    }

                    var allObjectsName = GetAllSceneObjects(true);
                    results.AddRange(allObjectsName.Where(go => go.name == searchTerm));
                    break;
                default:
                    UnityEngine.Debug.LogWarning(
                        $"[GameObjectUtils.Find] Unknown search method: {searchMethod}"
                    );
                    break;
            }

            if (!findAll && results.Count > 1)
            {
                return new List<GameObject> { results[0] };
            }

            return results.Distinct().ToList();
        }

        /// <summary>
        /// 简单查找GameObject，用于父对象查找等场景
        /// </summary>
        public static GameObject FindObjectByIdOrNameOrPath(JToken targetToken)
        {
            string searchTerm = targetToken?.ToString();
            if (string.IsNullOrEmpty(searchTerm))
                return null;

            // 尝试按ID查找
            if (int.TryParse(searchTerm, out int id))
            {
                var allObjects = GetAllSceneObjects(true);
                GameObject objById = allObjects.FirstOrDefault(go => go.GetInstanceID() == id);
                if (objById != null)
                    return objById;
            }

            // 尝试按路径查找
            GameObject objByPath = GameObject.Find(searchTerm);
            if (objByPath != null)
                return objByPath;

            // 尝试按名称查找
            var allObjectsName = GetAllSceneObjects(true);
            return allObjectsName.FirstOrDefault(go => go.name == searchTerm);
        }

        /// <summary>
        /// 获取场景中的所有GameObject
        /// </summary>
        public static IEnumerable<GameObject> GetAllSceneObjects(bool includeInactive)
        {
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            var allObjects = new List<GameObject>();
            foreach (var root in rootObjects)
            {
                allObjects.AddRange(
                    root.GetComponentsInChildren<Transform>(includeInactive)
                        .Select(t => t.gameObject)
                );
            }
            return allObjects;
        }

        /// <summary>
        /// 根据类型名称查找Type，搜索相关程序集
        /// </summary>
        public static Type FindType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            var type =
                Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule")
                ?? Type.GetType($"UnityEngine.{typeName}, UnityEngine.PhysicsModule")
                ?? Type.GetType($"UnityEngine.UI.{typeName}, UnityEngine.UI")
                ?? Type.GetType($"UnityEditor.{typeName}, UnityEditor.CoreModule")
                ?? Type.GetType(typeName);

            if (type != null)
                return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                    return type;
                type = assembly.GetType("UnityEngine." + typeName);
                if (type != null)
                    return type;
                type = assembly.GetType("UnityEditor." + typeName);
                if (type != null)
                    return type;
                type = assembly.GetType("UnityEngine.UI." + typeName);
                if (type != null)
                    return type;
            }

            return null;
        }

        /// <summary>
        /// 解析JArray为Vector3
        /// </summary>
        public static Vector3? ParseVector3(JArray array)
        {
            if (array != null && array.Count == 3)
            {
                try
                {
                    return new Vector3(
                        array[0].ToObject<float>(),
                        array[1].ToObject<float>(),
                        array[2].ToObject<float>()
                    );
                }
                catch
                {
                }
            }
            return null;
        }

        /// <summary>
        /// 创建GameObject的可序列化表示
        /// </summary>
        public static object GetGameObjectData(GameObject go)
        {
            if (go == null)
                return null;
            return new
            {
                name = go.name,
                instance_id = go.GetInstanceID(),
                tag = go.tag,
                layer = go.layer,
                active_self = go.activeSelf,
                active_in_hierarchy = go.activeInHierarchy,
                is_static = go.isStatic,
                scene_path = go.scene.path,
                transform = new
                {
                    position = new
                    {
                        x = go.transform.position.x,
                        y = go.transform.position.y,
                        z = go.transform.position.z,
                    },
                    local_position = new
                    {
                        x = go.transform.localPosition.x,
                        y = go.transform.localPosition.y,
                        z = go.transform.localPosition.z,
                    },
                    rotation = new
                    {
                        x = go.transform.rotation.eulerAngles.x,
                        y = go.transform.rotation.eulerAngles.y,
                        z = go.transform.rotation.eulerAngles.z,
                    },
                    local_rotation = new
                    {
                        x = go.transform.localRotation.eulerAngles.x,
                        y = go.transform.localRotation.eulerAngles.y,
                        z = go.transform.localRotation.eulerAngles.z,
                    },
                    scale = new
                    {
                        x = go.transform.localScale.x,
                        y = go.transform.localScale.y,
                        z = go.transform.localScale.z,
                    },
                    forward = new
                    {
                        x = go.transform.forward.x,
                        y = go.transform.forward.y,
                        z = go.transform.forward.z,
                    },
                    up = new
                    {
                        x = go.transform.up.x,
                        y = go.transform.up.y,
                        z = go.transform.up.z,
                    },
                    right = new
                    {
                        x = go.transform.right.x,
                        y = go.transform.right.y,
                        z = go.transform.right.z,
                    },
                },
                parent_instance_id = go.transform.parent?.gameObject.GetInstanceID() ?? 0,
                component_names = go.GetComponents<Component>()
                    .Select(c => c.GetType().FullName)
                    .ToList(),
            };
        }

        // --- GameObject Configuration Methods ---

        /// <summary>
        /// 应用通用GameObject设置
        /// </summary>
        public static void ApplyCommonGameObjectSettings(JObject args, GameObject newGo, Action<string> logAction = null)
        {

            // 设置名称
            ApplyNameSetting(args, newGo, logAction);

            // 设置父对象
            ApplyParentSetting(args, newGo, logAction);

            // 设置变换
            ApplyTransformSettings(args, newGo);

            // 设置标签
            ApplyTagSetting(args, newGo, logAction);

            // 设置层
            ApplyLayerSetting(args, newGo, logAction);

            // 添加组件
            ApplyComponentsToAdd(args, newGo, logAction);

            //设置组件属性
            ApplyComponentProperties(args, newGo, logAction);

            // 设置激活状态
            bool? setActive = args["active"]?.ToObject<bool?>();
            if (setActive.HasValue)
            {
                newGo.SetActive(setActive.Value);
            }
        }
        /// <summary>
        /// 应用名称设置
        /// </summary>
        public static void ApplyNameSetting(JObject args, GameObject newGo, Action<string> logAction = null)
        {
            string name = args["name"]?.ToString();
            if (!string.IsNullOrEmpty(name))
            {
                newGo.name = name;
            }
        }
        /// <summary>
        /// 应用父对象设置
        /// </summary>
        public static void ApplyParentSetting(JObject args, GameObject newGo, Action<string> logAction = null)
        {
            JToken parentToken = args["parent"];
            if (parentToken != null)
            {
                GameObject parentGo = FindObjectByIdOrNameOrPath(parentToken);
                if (parentGo == null)
                {
                    logAction?.Invoke($"Parent specified ('{parentToken}') but not found.");
                    return;
                }
                newGo.transform.SetParent(parentGo.transform, true);
            }
        }

        /// <summary>
        /// 应用变换设置
        /// </summary>
        public static void ApplyTransformSettings(JObject args, GameObject newGo)
        {
            Vector3? position = ParseVector3(args["position"] as JArray);
            Vector3? rotation = ParseVector3(args["rotation"] as JArray);
            Vector3? scale = ParseVector3(args["scale"] as JArray);

            if (position.HasValue)
                newGo.transform.localPosition = position.Value;
            if (rotation.HasValue)
                newGo.transform.localEulerAngles = rotation.Value;
            if (scale.HasValue)
                newGo.transform.localScale = scale.Value;
        }

        /// <summary>
        /// 应用标签设置
        /// </summary>
        public static void ApplyTagSetting(JObject args, GameObject newGo, Action<string> logAction = null)
        {
            string tag = args["tag"]?.ToString();
            if (!string.IsNullOrEmpty(tag))
            {
                string tagToSet = string.IsNullOrEmpty(tag) ? "Untagged" : tag;
                try
                {
                    newGo.tag = tagToSet;
                }
                catch (UnityException ex)
                {
                    if (ex.Message.Contains("is not defined"))
                    {
                        logAction?.Invoke($"Tag '{tagToSet}' not found. Attempting to create it.");
                        try
                        {
                            InternalEditorUtility.AddTag(tagToSet);
                            newGo.tag = tagToSet;
                            logAction?.Invoke($"Tag '{tagToSet}' created and assigned successfully.");
                        }
                        catch (Exception innerEx)
                        {
                            logAction?.Invoke($"Failed to create or assign tag '{tagToSet}': {innerEx.Message}");
                        }
                    }
                    else
                    {
                        logAction?.Invoke($"Failed to set tag to '{tagToSet}': {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 应用层设置
        /// </summary>
        public static void ApplyLayerSetting(JObject args, GameObject newGo, Action<string> logAction = null)
        {
            string layerName = args["layer"]?.ToString();
            if (!string.IsNullOrEmpty(layerName))
            {
                int layerId = LayerMask.NameToLayer(layerName);
                if (layerId != -1)
                {
                    newGo.layer = layerId;
                }
                else
                {
                    logAction?.Invoke($"Layer '{layerName}' not found. Using default layer.");
                }
            }
        }

        /// <summary>
        /// 应用组件添加
        /// </summary>
        public static void ApplyComponentsToAdd(JObject args, GameObject newGo, Action<string> logAction = null)
        {
            if (args["components"] is JArray componentsToAddArray)
            {
                foreach (var compToken in componentsToAddArray)
                {
                    string typeName = null;
                    JObject properties = null;

                    if (compToken.Type == JTokenType.String)
                    {
                        typeName = compToken.ToString();
                    }
                    else if (compToken is JObject compObj)
                    {
                        typeName = compObj["type_name"]?.ToString();
                        properties = compObj["properties"] as JObject;
                    }

                    if (!string.IsNullOrEmpty(typeName))
                    {
                        var addResult = AddComponentInternal(newGo, typeName, properties);
                        if (addResult != null)
                        {
                            logAction?.Invoke($"Failed to add component '{typeName}': {addResult}");
                        }
                    }
                    else
                    {
                        logAction?.Invoke($"Invalid component format in components: {compToken}");
                    }
                }
            }
        }

        /// <summary>
        /// 应用组件属性设置
        /// </summary>
        public static void ApplyComponentProperties(JObject args, GameObject newGo, Action<string> logAction = null)
        {
            // 处理component_properties
            if (args["component_properties"] is JObject componentPropsObj)
            {
                foreach (var componentProp in componentPropsObj.Properties())
                {
                    string componentName = componentProp.Name;
                    if (componentProp.Value is JObject properties)
                    {
                        SetComponentPropertiesInternal(newGo, componentName, properties, logAction);
                    }
                }
            }

            // 处理单个component_name和component_properties的情况
            string singleComponentName = args["component_name"]?.ToString();
            if (!string.IsNullOrEmpty(singleComponentName) && args["component_properties"] is JObject singleProps)
            {
                // 检查是否是嵌套结构
                if (singleProps[singleComponentName] is JObject nestedProps)
                {
                    SetComponentPropertiesInternal(newGo, singleComponentName, nestedProps, logAction);
                }
                else
                {
                    // 直接使用属性对象
                    SetComponentPropertiesInternal(newGo, singleComponentName, singleProps, logAction);
                }
            }
        }

        /// <summary>
        /// 设置组件属性的内部方法
        /// </summary>
        private static void SetComponentPropertiesInternal(
            GameObject targetGo,
            string componentName,
            JObject properties,
            Action<string> logAction = null
        )
        {
            if (properties == null || !properties.HasValues)
                return;

            // 查找组件类型
            Type componentType = FindType(componentName);
            Component targetComponent = null;

            if (componentType != null && typeof(Component).IsAssignableFrom(componentType))
            {
                targetComponent = targetGo.GetComponent(componentType);
            }
            else
            {
                // 尝试常见的Unity组件命名空间
                string[] commonNamespaces = { "UnityEngine", "UnityEngine.UI" };
                foreach (string ns in commonNamespaces)
                {
                    string fullTypeName = $"{ns}.{componentName}";
                    componentType = FindType(fullTypeName);
                    if (componentType != null && typeof(Component).IsAssignableFrom(componentType))
                    {
                        targetComponent = targetGo.GetComponent(componentType);
                        break;
                    }
                }
            }

            if (targetComponent == null)
            {
                logAction?.Invoke($"Component '{componentName}' not found on '{targetGo.name}' to set properties.");
                return;
            }

            Undo.RecordObject(targetComponent, "Set Component Properties");

            foreach (var prop in properties.Properties())
            {
                string propName = prop.Name;
                JToken propValue = prop.Value;

                try
                {
                    if (!SetComponentProperty(targetComponent, propName, propValue, logAction))
                    {
                        logAction?.Invoke($"Could not set property '{propName}' on component '{componentName}'. Property might not exist, be read-only, or type mismatch.");
                    }
                }
                catch (Exception e)
                {
                    logAction?.Invoke($"Error setting property '{propName}' on '{componentName}': {e.Message}");
                }
            }

            EditorUtility.SetDirty(targetComponent);
        }

        /// <summary>
        /// 设置组件属性
        /// </summary>
        private static bool SetComponentProperty(object target, string memberName, JToken value, Action<string> logAction = null)
        {
            Type type = target.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

            try
            {
                // 处理材质属性的特殊情况
                if (memberName.Equals("material", StringComparison.OrdinalIgnoreCase) && value.Type == JTokenType.String)
                {
                    string materialPath = value.ToString();
                    if (!string.IsNullOrEmpty(materialPath))
                    {
                        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if (material != null)
                        {
                            PropertyInfo materialProp = type.GetProperty("material", flags);
                            if (materialProp != null && materialProp.CanWrite)
                            {
                                materialProp.SetValue(target, material);
                                logAction?.Invoke($"Set material to '{materialPath}' on {type.Name}");
                                return true;
                            }
                        }
                        else
                        {
                            logAction?.Invoke($"Could not load material at path: '{materialPath}'");
                            return false;
                        }
                    }
                }

                // 处理嵌套属性 (如 material.color)
                if (memberName.Contains('.'))
                {
                    return SetNestedProperty(target, memberName, value, logAction);
                }

                // 处理普通属性
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
                logAction?.Invoke($"Failed to set '{memberName}' on {type.Name}: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 设置嵌套属性
        /// </summary>
        private static bool SetNestedProperty(object target, string path, JToken value, Action<string> logAction = null)
        {
            string[] pathParts = path.Split('.');
            if (pathParts.Length < 2)
                return false;

            object currentObject = target;
            Type currentType = currentObject.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

            // 遍历到最后一个属性之前
            for (int i = 0; i < pathParts.Length - 1; i++)
            {
                string part = pathParts[i];
                PropertyInfo propInfo = currentType.GetProperty(part, flags);
                if (propInfo != null)
                {
                    currentObject = propInfo.GetValue(currentObject);
                    if (currentObject == null)
                    {
                        logAction?.Invoke($"Property '{part}' is null, cannot access nested properties.");
                        return false;
                    }
                    currentType = currentObject.GetType();
                }
                else
                {
                    logAction?.Invoke($"Could not find property '{part}' on type '{currentType.Name}'");
                    return false;
                }
            }

            // 设置最终属性
            string finalPart = pathParts[pathParts.Length - 1];
            return SetComponentProperty(currentObject, finalPart, value, logAction);
        }

        /// <summary>
        /// 转换JToken为指定类型
        /// </summary>
        private static object ConvertJTokenToType(JToken token, Type targetType)
        {
            try
            {
                if (targetType == typeof(string))
                    return token.ToObject<string>();
                if (targetType == typeof(int))
                    return token.ToObject<int>();
                if (targetType == typeof(float))
                    return token.ToObject<float>();
                if (targetType == typeof(bool))
                    return token.ToObject<bool>();

                // Vector类型
                if (targetType == typeof(Vector2) && token is JArray arrV2 && arrV2.Count == 2)
                    return new Vector2(arrV2[0].ToObject<float>(), arrV2[1].ToObject<float>());
                if (targetType == typeof(Vector3) && token is JArray arrV3 && arrV3.Count == 3)
                    return new Vector3(arrV3[0].ToObject<float>(), arrV3[1].ToObject<float>(), arrV3[2].ToObject<float>());
                if (targetType == typeof(Vector4) && token is JArray arrV4 && arrV4.Count == 4)
                    return new Vector4(arrV4[0].ToObject<float>(), arrV4[1].ToObject<float>(), arrV4[2].ToObject<float>(), arrV4[3].ToObject<float>());

                // Color类型
                if (targetType == typeof(Color) && token is JArray arrC && arrC.Count >= 3)
                    return new Color(arrC[0].ToObject<float>(), arrC[1].ToObject<float>(), arrC[2].ToObject<float>(), arrC.Count > 3 ? arrC[3].ToObject<float>() : 1.0f);

                // 枚举类型
                if (targetType.IsEnum)
                    return Enum.Parse(targetType, token.ToString(), true);

                // Unity Object类型（Material, Texture等）
                if (typeof(UnityEngine.Object).IsAssignableFrom(targetType) && token.Type == JTokenType.String)
                {
                    string assetPath = token.ToString();
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        return AssetDatabase.LoadAssetAtPath(assetPath, targetType);
                    }
                }

                // 尝试直接转换
                return token.ToObject(targetType);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 添加组件的内部方法（包含物理组件冲突检查）
        /// Returns null on success, or an error response object on failure.
        /// </summary>
        public static object AddComponentInternal(
            GameObject targetGo,
            string typeName,
            JObject properties = null
        )
        {
            Type componentType = FindType(typeName);
            if (componentType == null)
            {
                return Response.Error(
                    $"Component type '{typeName}' not found or is not a valid Component."
                );
            }
            if (!typeof(Component).IsAssignableFrom(componentType))
            {
                return Response.Error($"Type '{typeName}' is not a Component.");
            }

            // Prevent adding Transform again
            if (componentType == typeof(Transform))
            {
                return Response.Error("Cannot add another Transform component.");
            }

            // Check for 2D/3D physics component conflicts
            bool isAdding2DPhysics =
                typeof(Rigidbody2D).IsAssignableFrom(componentType)
                || typeof(Collider2D).IsAssignableFrom(componentType);
            bool isAdding3DPhysics =
                typeof(Rigidbody).IsAssignableFrom(componentType)
                || typeof(Collider).IsAssignableFrom(componentType);

            if (isAdding2DPhysics)
            {
                // Check if the GameObject already has any 3D Rigidbody or Collider
                if (
                    targetGo.GetComponent<Rigidbody>() != null
                    || targetGo.GetComponent<Collider>() != null
                )
                {
                    return Response.Error(
                        $"Cannot add 2D physics component '{typeName}' because the GameObject '{targetGo.name}' already has a 3D Rigidbody or Collider."
                    );
                }
            }
            else if (isAdding3DPhysics)
            {
                // Check if the GameObject already has any 2D Rigidbody or Collider
                if (
                    targetGo.GetComponent<Rigidbody2D>() != null
                    || targetGo.GetComponent<Collider2D>() != null
                )
                {
                    return Response.Error(
                        $"Cannot add 3D physics component '{typeName}' because the GameObject '{targetGo.name}' already has a 2D Rigidbody or Collider."
                    );
                }
            }

            try
            {
                // Use Undo.AddComponent for undo support
                Component newComponent = Undo.AddComponent(targetGo, componentType);
                if (newComponent == null)
                {
                    return Response.Error(
                        $"Failed to add component '{typeName}' to '{targetGo.name}'. It might be disallowed (e.g., adding script twice)."
                    );
                }

                // Set default values for specific component types
                if (newComponent is Light light)
                {
                    // Default newly added lights to directional
                    light.type = LightType.Directional;
                }

                // Note: Property setting is handled by the calling code if needed
                // This keeps the method simpler and more focused

                return null; // Success
            }
            catch (Exception e)
            {
                return Response.Error(
                    $"Error adding component '{typeName}' to '{targetGo.name}': {e.Message}"
                );
            }
        }
    }
}

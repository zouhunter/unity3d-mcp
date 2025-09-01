using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Unity对象选择器基础抽象类
    /// 提供通用的对象查找功能
    /// </summary>
    /// <typeparam name="T">要查找的Unity对象类型</typeparam>
    public abstract class BaseObjectSelector<T> : IObjectSelector where T : UnityEngine.Object
    {
        public abstract MethodKey[] CreateKeys();

        /// <summary>
        /// 构建对象查找状态树
        /// </summary>
        public virtual StateTree BuildStateTree()
        {
            return StateTreeBuilder.Create()
                .Key("search_method")
                .Leaf("by_name", (Func<StateTreeContext, object>)HandleByNameSearch)
                .Leaf("by_type", (Func<StateTreeContext, object>)HandleByTypeSearch)
                .Leaf("by_id", (Func<StateTreeContext, object>)HandleByIdSearch)
                .Leaf("by_guid", (Func<StateTreeContext, object>)HandleByGuidSearch)
                .Leaf("find_all", (Func<StateTreeContext, object>)HandleFindAllSearch)
                .DefaultLeaf((Func<StateTreeContext, object>)HandleDefaultSearch)
                .Build();
        }

        /// <summary>
        /// 按名称查找指定类型的对象数组
        /// </summary>
        public abstract T[] FindObjectsByName(string name);

        /// <summary>
        /// 查找所有指定类型的对象
        /// </summary>
        public abstract T[] FindAllObjects();

        /// <summary>
        /// 按ID查找指定类型的对象
        /// </summary>
        public abstract T[] FindObjectsById(int instanceId);

        /// <summary>
        /// 按GUID查找指定类型的对象
        /// </summary>
        public abstract T[] FindObjectsByGuid(string guid);

        #region 状态树处理方法

        /// <summary>
        /// 按名称搜索处理
        /// </summary>
        protected virtual object HandleByNameSearch(StateTreeContext args)
        {
            if (!args.TryGetValue("target", out object target) || target == null)
            {
                return Response.Error("Target parameter is required for by_name search method.");
            }

            string targetName = target.ToString();
            T[] foundObjects = FindObjectsByName(targetName);

            if (foundObjects.Length == 0)
            {
                return Response.Error($"No objects of type '{typeof(T).Name}' with name '{targetName}' found.");
            }

            return foundObjects;
        }

        /// <summary>
        /// 按类型搜索处理
        /// </summary>
        protected virtual object HandleByTypeSearch(StateTreeContext args)
        {
            // 类型已经由泛型参数确定，直接查找所有对象
            T[] foundObjects = FindAllObjects();

            if (foundObjects.Length == 0)
            {
                return Response.Error($"No objects of type '{typeof(T).Name}' found.");
            }

            return foundObjects;
        }

        /// <summary>
        /// 按ID搜索处理
        /// </summary>
        protected virtual object HandleByIdSearch(StateTreeContext args)
        {
            if (!args.TryGetValue("target", out object target) || target == null)
            {
                return Response.Error("Target parameter is required for by_id search method.");
            }

            if (!int.TryParse(target.ToString(), out int targetId))
            {
                return Response.Error($"Invalid ID format: '{target}'. ID must be an integer.");
            }

            T[] foundObjects = FindObjectsById(targetId);

            if (foundObjects.Length == 0)
            {
                return Response.Error($"No objects of type '{typeof(T).Name}' with ID '{targetId}' found.");
            }

            return foundObjects;
        }

        /// <summary>
        /// 按GUID搜索处理
        /// </summary>
        protected virtual object HandleByGuidSearch(StateTreeContext args)
        {
            if (!args.TryGetValue("target", out object target) || target == null)
            {
                return Response.Error("Target parameter is required for by_guid search method.");
            }

            string guid = target.ToString();
            T[] foundObjects = FindObjectsByGuid(guid);

            if (foundObjects.Length == 0)
            {
                return Response.Error($"No objects of type '{typeof(T).Name}' with GUID '{guid}' found.");
            }

            return foundObjects;
        }

        /// <summary>
        /// 查找所有对象处理
        /// </summary>
        protected virtual object HandleFindAllSearch(StateTreeContext args)
        {
            T[] foundObjects = FindAllObjects();

            if (foundObjects.Length == 0)
            {
                return Response.Error($"No objects of type '{typeof(T).Name}' found.");
            }

            return foundObjects;
        }

        /// <summary>
        /// 默认搜索处理
        /// </summary>
        protected virtual object HandleDefaultSearch(StateTreeContext args)
        {
            if (!args.TryGetValue("target", out object targetObj) || targetObj == null)
            {
                return Response.Error("Target parameter is required.");
            }

            string target = targetObj.ToString();
            List<T> allFound = new List<T>();

            // 尝试按名称查找
            allFound.AddRange(FindObjectsByName(target));

            // 尝试按ID查找（如果是数字）
            if (int.TryParse(target, out int id))
            {
                allFound.AddRange(FindObjectsById(id));
            }

            // 尝试按GUID查找
            allFound.AddRange(FindObjectsByGuid(target));

            // 去重
            T[] uniqueObjects = allFound.Distinct().ToArray();

            if (uniqueObjects.Length == 0)
            {
                return Response.Error($"No objects of type '{typeof(T).Name}' matching '{target}' found using default search method.");
            }

            return uniqueObjects;
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 查找Unity对象类型
        /// </summary>
        protected Type FindUnityObjectType(string typeName)
        {
            // 尝试直接获取类型
            Type objectType = Type.GetType(typeName);
            if (objectType != null && typeof(UnityEngine.Object).IsAssignableFrom(objectType))
            {
                return objectType;
            }

            // 尝试常见的Unity命名空间
            string[] commonNamespaces = {
                "UnityEngine",
                "UnityEngine.UI",
                "UnityEngine.Audio",
                "UnityEngine.Rendering",
                "UnityEditor"
            };

            foreach (string ns in commonNamespaces)
            {
                string fullTypeName = $"{ns}.{typeName}";
                objectType = Type.GetType(fullTypeName);
                if (objectType != null && typeof(UnityEngine.Object).IsAssignableFrom(objectType))
                {
                    return objectType;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取对象的完整路径
        /// </summary>
        protected string GetObjectPath(T obj)
        {
            if (obj is GameObject gameObject)
            {
                return GetGameObjectPath(gameObject);
            }
            else if (obj is Component component)
            {
                return GetGameObjectPath(component.gameObject) + $"/{component.GetType().Name}";
            }
            else
            {
                // 对于其他类型的对象，尝试获取资产路径
                string assetPath = AssetDatabase.GetAssetPath(obj);
                return !string.IsNullOrEmpty(assetPath) ? assetPath : obj.name;
            }
        }

        /// <summary>
        /// 获取GameObject的完整路径
        /// </summary>
        protected string GetGameObjectPath(GameObject obj)
        {
            List<string> pathParts = new List<string>();
            Transform current = obj.transform;

            while (current != null)
            {
                pathParts.Insert(0, current.name);
                current = current.parent;
            }

            return string.Join("/", pathParts);
        }

        /// <summary>
        /// 过滤对象数组为指定类型
        /// </summary>
        protected T[] FilterObjectsOfType(UnityEngine.Object[] objects)
        {
            List<T> filtered = new List<T>();
            foreach (var obj in objects)
            {
                if (obj is T typedObject)
                {
                    filtered.Add(typedObject);
                }
            }
            return filtered.ToArray();
        }

        /// <summary>
        /// 获取对象的详细信息
        /// </summary>
        public virtual string GetObjectInfo(T obj)
        {
            if (obj == null) return "null";

            string type = typeof(T).Name;
            string name = obj.name;
            string instanceId = obj.GetInstanceID().ToString();

            return $"Type: {type}, Name: {name}, InstanceID: {instanceId}";
        }

        #endregion
    }
}
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
    /// 项目资产对象选择器
    /// 专门处理项目资产中的对象查找
    /// 支持各种Unity资产类型的查找
    /// </summary>
    /// <typeparam name="T">要查找的Unity对象类型</typeparam>
    public class ProjectSelector<T> : IObjectSelector where T : UnityEngine.Object
    {
        public MethodKey[] CreateKeys()
        {
            return new MethodKey[]
            {
                new MethodKey("instance_id", "Object InstanceID", true),
                new MethodKey("path", "Object Project path", true)
            };
        }

        public StateTree BuildStateTree()
        {
            return StateTreeBuilder.Create()
                .OptionalLeaf("instance_id", (Func<StateTreeContext, object>)HandleByIdSearch)
                .OptionalLeaf("path", (Func<StateTreeContext, object>)HandleByPathSearch)
                .DefaultLeaf((Func<StateTreeContext, object>)HandleDefaultSearch)
                .Build();
        }

        private object HandleByIdSearch(StateTreeContext context)
        {
            // 获取ID参数
            if (!context.TryGetValue("instance_id", out object idObj) || idObj == null)
            {
                return Response.Error("Parameter 'id' is required.");
            }

            // 解析ID
            if (!int.TryParse(idObj.ToString(), out int instanceId))
            {
                return Response.Error($"Invalid ID format: '{idObj}'. ID must be an integer.");
            }

            try
            {
                // 使用EditorUtility.InstanceIDToObject查找对象
                var foundObject = UnityEditor.EditorUtility.InstanceIDToObject(instanceId);

                if (foundObject == null)
                {
                    return Response.Error($"Object with ID '{instanceId}' not found.");
                }

                // 检查对象是否是项目资产
                if (!IsProjectAsset(foundObject))
                {
                    return Response.Error($"Object with ID '{instanceId}' is not a project asset.");
                }

                // 检查对象类型是否匹配
                if (foundObject is T typedObject)
                {
                    return typedObject;
                }
                else
                {
                    return Response.Error($"Found object with ID '{instanceId}', but type mismatch. Expected type: '{typeof(T).Name}', actual type: '{foundObject.GetType().Name}'.");
                }
            }
            catch (Exception ex)
            {
                return Response.Error($"Error occurred while searching object: {ex.Message}");
            }
        }

        private object HandleByPathSearch(StateTreeContext context)
        {
            // 获取path参数
            if (!context.TryGetValue("path", out object pathObj) || pathObj == null)
            {
                return Response.Error("Parameter 'path' is required.");
            }

            string path = pathObj.ToString();

            try
            {
                // 只在项目资产中查找
                T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    return asset;
                }

                return Response.Error($"Asset of type {typeof(T).Name} not found at path '{path}'.");
            }
            catch (Exception ex)
            {
                return Response.Error($"Error occurred while searching path '{path}': {ex.Message}");
            }
        }

        private object HandleDefaultSearch(StateTreeContext context)
        {
            // 检查是否至少提供了id或path参数之一
            bool hasId = context.TryGetValue("instance_id", out object idObj) && idObj != null;
            bool hasPath = context.TryGetValue("path", out object pathObj) && pathObj != null;

            if (!hasId && !hasPath)
            {
                Debug.LogError("Either 'instance_id' or 'path' parameter must be provided.");
                return Response.Error("Either 'instance_id' or 'path' parameter must be provided.");
            }

            // 优先使用id查找
            if (hasId)
            {
                return HandleByIdSearch(context);
            }

            // 使用path查找
            if (hasPath)
            {
                return HandleByPathSearch(context);
            }

            return Response.Error("No matching asset found.");
        }

        /// <summary>
        /// 检查对象是否是项目资产
        /// </summary>
        /// <param name="obj">要检查的对象</param>
        /// <returns>如果对象是项目资产返回true，否则返回false</returns>
        private bool IsProjectAsset(UnityEngine.Object obj)
        {
            if (obj == null) return false;

            // 使用AssetDatabase.Contains检查对象是否是资产
            return UnityEditor.AssetDatabase.Contains(obj);
        }

        /// <summary>
        /// 通用的按ID查找方法，只在项目资产中查找
        /// </summary>
        /// <param name="instanceId">对象的InstanceID</param>
        /// <returns>找到的对象，如果未找到或类型不匹配则返回null</returns>
        public T FindById(int instanceId)
        {
            try
            {
                var foundObject = UnityEditor.EditorUtility.InstanceIDToObject(instanceId);
                if (foundObject != null && IsProjectAsset(foundObject) && foundObject is T typedObject)
                {
                    return typedObject;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 通用的按路径查找方法，只在项目资产中查找
        /// </summary>
        /// <param name="path">资产路径</param>
        /// <returns>找到的对象，如果未找到或类型不匹配则返回null</returns>
        public T FindByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            try
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取对象的详细信息
        /// </summary>
        /// <param name="obj">要获取信息的对象</param>
        /// <returns>对象信息字符串</returns>
        public string GetObjectInfo(T obj)
        {
            if (obj == null) return "null";

            string type = typeof(T).Name;
            string name = obj.name;
            string instanceId = obj.GetInstanceID().ToString();
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(obj);

            return $"Type: {type}, Name: {name}, InstanceID: {instanceId}, Asset Path: {assetPath}";
        }

        /// <summary>
        /// 获取指定类型的所有项目资产
        /// </summary>
        /// <returns>找到的所有T类型资产列表</returns>
        public List<T> FindAllAssets()
        {
            var assets = new List<T>();
            try
            {
                // 使用AssetDatabase.FindAssets查找所有T类型的资产
                string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(T).Name}");

                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                    if (asset != null)
                    {
                        assets.Add(asset);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error occurred while finding all {typeof(T).Name} assets: {ex.Message}");
            }

            return assets;
        }

        /// <summary>
        /// 在指定文件夹中查找资产
        /// </summary>
        /// <param name="folderPath">Folder path, e.g. "Assets/Scripts"</param>
        /// <returns>找到的T类型资产列表</returns>
        public List<T> FindAssetsInFolder(string folderPath)
        {
            var assets = new List<T>();
            if (string.IsNullOrEmpty(folderPath))
            {
                return assets;
            }

            try
            {
                // 在指定文件夹中查找资产
                string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folderPath });

                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                    if (asset != null)
                    {
                        assets.Add(asset);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error occurred while finding {typeof(T).Name} assets in folder '{folderPath}': {ex.Message}");
            }

            return assets;
        }

    }
}
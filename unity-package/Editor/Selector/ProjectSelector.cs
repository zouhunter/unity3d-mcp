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
                new MethodKey("instance_id", "对象的InstanceID", true),
                new MethodKey("path", "对象的Project路径", true)
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
                return Response.Error("参数'id'是必需的。");
            }

            // 解析ID
            if (!int.TryParse(idObj.ToString(), out int instanceId))
            {
                return Response.Error($"无效的ID格式：'{idObj}'。ID必须是整数。");
            }

            try
            {
                // 使用EditorUtility.InstanceIDToObject查找对象
                var foundObject = UnityEditor.EditorUtility.InstanceIDToObject(instanceId);

                if (foundObject == null)
                {
                    return Response.Error($"未找到ID为'{instanceId}'的对象。");
                }

                // 检查对象是否是项目资产
                if (!IsProjectAsset(foundObject))
                {
                    return Response.Error($"ID为'{instanceId}'的对象不是项目资产。");
                }

                // 检查对象类型是否匹配
                if (foundObject is T typedObject)
                {
                    return typedObject;
                }
                else
                {
                    return Response.Error($"找到ID为'{instanceId}'的对象，但类型不匹配。期望类型：'{typeof(T).Name}'，实际类型：'{foundObject.GetType().Name}'。");
                }
            }
            catch (Exception ex)
            {
                return Response.Error($"查找对象时发生错误：{ex.Message}");
            }
        }

        private object HandleByPathSearch(StateTreeContext context)
        {
            // 获取path参数
            if (!context.TryGetValue("path", out object pathObj) || pathObj == null)
            {
                return Response.Error("参数'path'是必需的。");
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

                return Response.Error($"未找到路径为'{path}'的{typeof(T).Name}类型资产。");
            }
            catch (Exception ex)
            {
                return Response.Error($"查找路径'{path}'时发生错误：{ex.Message}");
            }
        }

        private object HandleDefaultSearch(StateTreeContext context)
        {
            // 检查是否至少提供了id或path参数之一
            bool hasId = context.TryGetValue("instance_id", out object idObj) && idObj != null;
            bool hasPath = context.TryGetValue("path", out object pathObj) && pathObj != null;

            if (!hasId && !hasPath)
            {
                return Response.Error("必须提供'id'或'path'参数之一。");
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

            return Response.Error("未找到匹配的资产。");
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

            return $"类型: {type}, 名称: {name}, InstanceID: {instanceId}, 资产路径: {assetPath}";
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
                Debug.LogError($"查找所有{typeof(T).Name}资产时发生错误：{ex.Message}");
            }

            return assets;
        }

        /// <summary>
        /// 在指定文件夹中查找资产
        /// </summary>
        /// <param name="folderPath">文件夹路径，如"Assets/Scripts"</param>
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
                Debug.LogError($"在文件夹'{folderPath}'中查找{typeof(T).Name}资产时发生错误：{ex.Message}");
            }

            return assets;
        }

    }
}
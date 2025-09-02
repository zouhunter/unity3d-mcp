using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// 简化的对象选择器，按ID或path查找并返回唯一的T类型对象
    /// </summary>
    /// <typeparam name="T">要查找的Unity对象类型，必须继承自UnityEngine.Object</typeparam>
    public class ObjectSelector<T> : IObjectSelector where T : UnityEngine.Object
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        /// <returns>包含id和path参数的MethodKey数组</returns>
        public MethodKey[] CreateKeys()
        {
            return new MethodKey[]
            {
                new MethodKey("instance_id", "对象的InstanceID", true),
                new MethodKey("path", "对象的路径（Assets路径或Hierarchy路径）", true)
            };
        }

        /// <summary>
        /// 构建对象查找状态树，支持按ID或path查找
        /// </summary>
        /// <returns>构建的状态树</returns>
        public StateTree BuildStateTree()
        {
            return StateTreeBuilder.Create()
                .OptionalLeaf("instance_id", (Func<StateTreeContext, object>)HandleByIdSearch)
                .OptionalLeaf("path", (Func<StateTreeContext, object>)HandleByPathSearch)
                .DefaultLeaf((Func<StateTreeContext, object>)HandleDefaultSearch)
                .Build();
        }

        /// <summary>
        /// 按ID搜索处理，返回唯一的T类型对象
        /// </summary>
        /// <param name="context">状态树上下文</param>
        /// <returns>找到的对象或错误信息</returns>
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

        /// <summary>
        /// 按路径搜索处理，返回唯一的T类型对象
        /// </summary>
        /// <param name="context">状态树上下文</param>
        /// <returns>找到的对象或错误信息</returns>
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
                // 尝试作为Assets路径加载
                T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    return asset;
                }

                // 如果Assets路径失败，尝试作为Hierarchy路径查找
                T hierarchyObject = FindByHierarchyPath(path);
                if (hierarchyObject != null)
                {
                    return hierarchyObject;
                }

                return Response.Error($"未找到路径为'{path}'的{typeof(T).Name}类型对象。");
            }
            catch (Exception ex)
            {
                return Response.Error($"查找路径'{path}'时发生错误：{ex.Message}");
            }
        }

        /// <summary>
        /// 默认搜索处理，确保至少提供id或path参数之一
        /// </summary>
        /// <param name="context">状态树上下文</param>
        /// <returns>找到的对象或错误信息</returns>
        private object HandleDefaultSearch(StateTreeContext context)
        {
            // 检查是否至少提供了id或path参数之一
            bool hasId = context.TryGetValue("id", out object idObj) && idObj != null;
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

            return Response.Error("未找到匹配的对象。");
        }

        /// <summary>
        /// 通过Hierarchy路径查找对象
        /// </summary>
        /// <param name="path">Hierarchy路径，如"Parent/Child/Target"</param>
        /// <returns>找到的对象，未找到则返回null</returns>
        private T FindByHierarchyPath(string path)
        {
            // 使用GameObject.Find查找GameObject
            GameObject foundGameObject = GameObject.Find(path);

            if (foundGameObject == null)
            {
                return null;
            }

            // 如果T是GameObject类型，直接返回
            if (foundGameObject is T directMatch)
            {
                return directMatch;
            }

            // 如果T是Component类型，尝试获取组件
            if (typeof(UnityEngine.Component).IsAssignableFrom(typeof(T)))
            {
                return foundGameObject.GetComponent<T>();
            }

            return null;
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

            return $"类型: {type}, 名称: {name}, InstanceID: {instanceId}";
        }

        /// <summary>
        /// 通用的按ID查找方法，可直接调用
        /// </summary>
        /// <param name="instanceId">对象的InstanceID</param>
        /// <returns>找到的对象，如果未找到或类型不匹配则返回null</returns>
        public T FindById(int instanceId)
        {
            try
            {
                var foundObject = UnityEditor.EditorUtility.InstanceIDToObject(instanceId);
                return foundObject as T;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 通用的按路径查找方法，可直接调用
        /// </summary>
        /// <param name="path">对象的路径（Assets路径或Hierarchy路径）</param>
        /// <returns>找到的对象，如果未找到或类型不匹配则返回null</returns>
        public T FindByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            try
            {
                // 尝试作为Assets路径加载
                T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    return asset;
                }

                // 尝试作为Hierarchy路径查找
                return FindByHierarchyPath(path);
            }
            catch
            {
                return null;
            }
        }
    }
}

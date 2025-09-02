using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// 场景层级对象选择器
    /// 按ID或path查找并返回唯一的T类型对象，只在Hierarchy中查找
    /// </summary>
    /// <typeparam name="T">要查找的Unity对象类型</typeparam>
    public class HierarchySelector<T> : IObjectSelector where T : UnityEngine.Object
    {

        public MethodKey[] CreateKeys()
        {
            return new MethodKey[]
            {
                new MethodKey("instance_id", "对象的InstanceID", true),
                new MethodKey("path", "对象的Hierarchy路径", true)
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

                // 检查对象是否在当前场景的Hierarchy中
                if (!IsInCurrentSceneHierarchy(foundObject))
                {
                    return Response.Error($"ID为'{instanceId}'的对象不在当前场景的Hierarchy中。");
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
                // 只在当前场景的Hierarchy中查找
                T hierarchyObject = GameObjectUtils.FindByHierarchyPath<T>(path);
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

            return Response.Error("未找到匹配的对象。");
        }



        /// <summary>
        /// 检查对象是否在当前场景的Hierarchy中
        /// </summary>
        /// <param name="obj">要检查的对象</param>
        /// <returns>如果对象在当前场景Hierarchy中返回true，否则返回false</returns>
        private bool IsInCurrentSceneHierarchy(UnityEngine.Object obj)
        {
            if (obj == null) return false;

            // 如果是GameObject，检查其场景
            if (obj is GameObject gameObject)
            {
                return gameObject.scene == SceneManager.GetActiveScene();
            }

            // 如果是Component，检查其GameObject的场景
            if (obj is Component component)
            {
                return component.gameObject.scene == SceneManager.GetActiveScene();
            }

            // 其他类型的对象不在Hierarchy中
            return false;
        }

        /// <summary>
        /// 通用的按ID查找方法，只在当前场景Hierarchy中查找
        /// </summary>
        /// <param name="instanceId">对象的InstanceID</param>
        /// <returns>找到的对象，如果未找到或类型不匹配则返回null</returns>
        public T FindById(int instanceId)
        {
            try
            {
                var foundObject = UnityEditor.EditorUtility.InstanceIDToObject(instanceId);
                if (foundObject != null && IsInCurrentSceneHierarchy(foundObject) && foundObject is T typedObject)
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
        /// 通用的按路径查找方法，只在当前场景Hierarchy中查找
        /// </summary>
        /// <param name="path">Hierarchy路径</param>
        /// <returns>找到的对象，如果未找到或类型不匹配则返回null</returns>
        public T FindByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            try
            {
                return GameObjectUtils.FindByHierarchyPath<T>(path);
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

            return $"类型: {type}, 名称: {name}, InstanceID: {instanceId}";
        }

    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMcp.Models; // For Response class

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles GameObject creation operations in the scene hierarchy.
    /// 对应方法名: hierarchy_create
    /// </summary>
    [ToolName("hierarchy_create")]
    public class HierarchyCreate : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("from", "操作类型：menu, primitive, prefab", false),
                new MethodKey("name", "GameObject名称", true),
                new MethodKey("tag", "GameObject标签", true),
                new MethodKey("layer", "GameObject所在层", true),
                new MethodKey("parent", "父对象标识符", true),
                new MethodKey("position", "位置坐标 [x, y, z]", true),
                new MethodKey("rotation", "旋转角度 [x, y, z]", true),
                new MethodKey("scale", "缩放比例 [x, y, z]", true),
                new MethodKey("primitive_type", "基元类型：Cube, Sphere, Cylinder, Capsule, Plane, Quad", true),
                new MethodKey("prefab_path", "预制体路径", true),
                new MethodKey("menu_path", "菜单路径", true),
                new MethodKey("save_as_prefab", "是否保存为预制体", true),
                new MethodKey("set_active", "设置激活状态", true),
                new MethodKey("components_to_add", "要添加的组件列表", true)
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("menu", MenuUtils.HandleExecuteMenu)
                    .Branch("primitive")
                        .OptionalKey("primitive_type")
                            .Leaf("Cube", HandleCreateCube)
                            .Leaf("Sphere", HandleCreateSphere)
                            .Leaf("Cylinder", HandleCreateCylinder)
                            .Leaf("Capsule", HandleCreateCapsule)
                            .Leaf("Plane", HandleCreatePlane)
                            .Leaf("Quad", HandleCreateQuad)
                            .DefaultLeaf(HandleCreateFromPrimitive)
                        .Up()
                        .DefaultLeaf(HandleCreateFromPrimitive)
                    .Up()
                    .Leaf("prefab", HandleCreateFromPrefab)
                .Build();
        }

        /// <summary>
        /// 处理从预制体创建GameObject的操作
        /// </summary>
        private object HandleCreateFromPrefab(JObject args)
        {
            string prefabPath = args["prefab_path"]?.ToString();
            if (string.IsNullOrEmpty(prefabPath))
            {
                return Response.Error("'prefab_path' parameter is required for prefab instantiation.");
            }

            LogInfo($"[HierarchyCreate] Creating GameObject from prefab: '{prefabPath}'");
            return CreateGameObjectFromPrefab(args, prefabPath);
        }

        /// <summary>
        /// 处理从基元类型创建GameObject的操作
        /// </summary>
        private object HandleCreateFromPrimitive(JObject args)
        {
            string primitiveType = args["primitive_type"]?.ToString();
            if (string.IsNullOrEmpty(primitiveType))
            {
                // 默认使用Cube作为基元类型
                primitiveType = "Cube";
                LogInfo("[HierarchyCreate] No primitive_type specified, using default: Cube");
            }

            LogInfo($"[HierarchyCreate] Creating GameObject from primitive: '{primitiveType}'");
            return CreateGameObjectFromPrimitive(args, primitiveType);
        }

        /// <summary>
        /// 处理创建Cube的操作
        /// </summary>
        private object HandleCreateCube(JObject args)
        {
            LogInfo("[HierarchyCreate] Creating Cube primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Cube");
        }

        /// <summary>
        /// 处理创建Sphere的操作
        /// </summary>
        private object HandleCreateSphere(JObject args)
        {
            LogInfo("[HierarchyCreate] Creating Sphere primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Sphere");
        }

        /// <summary>
        /// 处理创建Cylinder的操作
        /// </summary>
        private object HandleCreateCylinder(JObject args)
        {
            LogInfo("[HierarchyCreate] Creating Cylinder primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Cylinder");
        }

        /// <summary>
        /// 处理创建Capsule的操作
        /// </summary>
        private object HandleCreateCapsule(JObject args)
        {
            LogInfo("[HierarchyCreate] Creating Capsule primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Capsule");
        }

        /// <summary>
        /// 处理创建Plane的操作
        /// </summary>
        private object HandleCreatePlane(JObject args)
        {
            LogInfo("[HierarchyCreate] Creating Plane primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Plane");
        }

        /// <summary>
        /// 处理创建Quad的操作
        /// </summary>
        private object HandleCreateQuad(JObject args)
        {
            LogInfo("[HierarchyCreate] Creating Quad primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Quad");
        }

        // --- Core Creation Methods ---

        /// <summary>
        /// 从预制体创建GameObject
        /// </summary>
        private object CreateGameObjectFromPrefab(JObject args, string prefabPath)
        {
            try
            {
                // 处理预制体路径查找逻辑
                string resolvedPath = ResolvePrefabPath(prefabPath);
                if (string.IsNullOrEmpty(resolvedPath))
                {
                    LogInfo($"[HierarchyCreate] Prefab not found at path: '{prefabPath}'");
                    return Response.Error($"Prefab not found at path: '{prefabPath}'");
                }

                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(resolvedPath);
                if (prefabAsset == null)
                {
                    LogInfo($"[HierarchyCreate] Failed to load prefab asset at: '{resolvedPath}'");
                    return Response.Error($"Failed to load prefab asset at: '{resolvedPath}'");
                }

                // 实例化预制体
                GameObject newGo = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                if (newGo == null)
                {
                    LogInfo($"[HierarchyCreate] Failed to instantiate prefab: '{resolvedPath}'");
                    return Response.Error($"Failed to instantiate prefab: '{resolvedPath}'");
                }

                // 设置名称
                string name = args["name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    newGo.name = name;
                }

                // 注册撤销操作
                Undo.RegisterCreatedObjectUndo(newGo, $"Instantiate Prefab '{prefabAsset.name}' as '{newGo.name}'");
                LogInfo($"[HierarchyCreate] Instantiated prefab '{prefabAsset.name}' from path '{resolvedPath}' as '{newGo.name}'");

                return FinalizeGameObjectCreation(args, newGo, false);
            }
            catch (Exception e)
            {
                LogInfo($"[HierarchyCreate] Error instantiating prefab '{prefabPath}': {e.Message}");
                return Response.Error($"Error instantiating prefab '{prefabPath}': {e.Message}");
            }
        }

        /// <summary>
        /// 从基元类型创建GameObject
        /// </summary>
        private object CreateGameObjectFromPrimitive(JObject args, string primitiveType)
        {
            try
            {
                PrimitiveType type = (PrimitiveType)Enum.Parse(typeof(PrimitiveType), primitiveType, true);
                GameObject newGo = GameObject.CreatePrimitive(type);

                // 设置名称
                string name = args["name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    newGo.name = name;
                }
                else
                {
                    LogInfo("[HierarchyCreate] 'name' parameter is recommended when creating a primitive.");
                }

                // 注册撤销操作
                Undo.RegisterCreatedObjectUndo(newGo, $"Create GameObject '{newGo.name}'");
                return FinalizeGameObjectCreation(args, newGo, true);
            }
            catch (ArgumentException)
            {
                LogInfo($"[HierarchyCreate] Invalid primitive type: '{primitiveType}'. Valid types: {string.Join(", ", Enum.GetNames(typeof(PrimitiveType)))}");
                return Response.Error($"Invalid primitive type: '{primitiveType}'. Valid types: {string.Join(", ", Enum.GetNames(typeof(PrimitiveType)))}");
            }
            catch (Exception e)
            {
                LogInfo($"[HierarchyCreate] Failed to create primitive '{primitiveType}': {e.Message}");
                return Response.Error($"Failed to create primitive '{primitiveType}': {e.Message}");
            }
        }

        /// <summary>
        /// 创建空GameObject
        /// </summary>
        private object CreateEmptyGameObject(JObject args, string name)
        {
            try
            {
                GameObject newGo = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(newGo, $"Create GameObject '{newGo.name}'");

                return FinalizeGameObjectCreation(args, newGo, true);
            }
            catch (Exception e)
            {
                LogInfo($"[HierarchyCreate] Failed to create empty GameObject '{name}': {e.Message}");
                return Response.Error($"Failed to create empty GameObject '{name}': {e.Message}");
            }
        }

        /// <summary>
        /// 解析预制体路径
        /// </summary>
        private string ResolvePrefabPath(string prefabPath)
        {
            // 如果没有路径分隔符且没有.prefab扩展名，搜索预制体
            if (!prefabPath.Contains("/") && !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                string prefabNameOnly = prefabPath;
                LogInfo($"[HierarchyCreate] Searching for prefab named: '{prefabNameOnly}'");

                string[] guids = AssetDatabase.FindAssets($"t:Prefab {prefabNameOnly}");
                if (guids.Length == 0)
                {
                    return null; // 未找到
                }
                else if (guids.Length > 1)
                {
                    string foundPaths = string.Join(", ", guids.Select(g => AssetDatabase.GUIDToAssetPath(g)));
                    LogInfo($"[HierarchyCreate] Multiple prefabs found matching name '{prefabNameOnly}': {foundPaths}. Using first one.");
                }

                string resolvedPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                LogInfo($"[HierarchyCreate] Found prefab at path: '{resolvedPath}'");
                return resolvedPath;
            }
            else if (!prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                // 自动添加.prefab扩展名
                LogInfo($"[HierarchyCreate] Adding .prefab extension to path: '{prefabPath}'");
                return prefabPath + ".prefab";
            }

            return prefabPath;
        }

        /// <summary>
        /// 完成GameObject创建的通用设置
        /// </summary>
        private object FinalizeGameObjectCreation(JObject args, GameObject newGo, bool createdNewObject)
        {
            if (newGo == null)
            {
                return Response.Error("GameObject creation failed.");
            }

            try
            {
                // 记录变换和属性的变更
                Undo.RecordObject(newGo.transform, "Set GameObject Transform");
                Undo.RecordObject(newGo, "Set GameObject Properties");

                // 应用通用设置
                GameObjectUtils.ApplyCommonGameObjectSettings(args, newGo, LogInfo);

                // 处理预制体保存
                GameObject finalInstance = newGo;
                bool saveAsPrefab = args["save_as_prefab"]?.ToObject<bool>() ?? false;

                if (createdNewObject && saveAsPrefab)
                {
                    finalInstance = HandlePrefabSaving(args, newGo);
                    if (finalInstance == null)
                    {
                        return Response.Error("Failed to save GameObject as prefab.");
                    }
                }

                // 选择对象
                Selection.activeGameObject = finalInstance;

                // 生成成功消息
                string successMessage = GenerateCreationSuccessMessage(args, finalInstance, createdNewObject, saveAsPrefab);
                return Response.Success(successMessage, GameObjectUtils.GetGameObjectData(finalInstance));
            }
            catch (Exception e)
            {
                // 清理失败的对象
                if (newGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(newGo);
                }
                return Response.Error($"Error finalizing GameObject creation: {e.Message}");
            }
        }













        /// <summary>
        /// 处理预制体保存
        /// </summary>
        private GameObject HandlePrefabSaving(JObject args, GameObject newGo)
        {
            string prefabPath = args["prefab_path"]?.ToString();
            if (string.IsNullOrEmpty(prefabPath))
            {
                LogInfo("[HierarchyCreate] 'prefab_path' is required when 'save_as_prefab' is true.");
                return null;
            }

            string finalPrefabPath = prefabPath;
            if (!finalPrefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                LogInfo($"[HierarchyCreate] Adding .prefab extension to save path: '{finalPrefabPath}'");
                finalPrefabPath += ".prefab";
            }

            try
            {
                // 确保目录存在
                string directoryPath = System.IO.Path.GetDirectoryName(finalPrefabPath);
                if (!string.IsNullOrEmpty(directoryPath) && !System.IO.Directory.Exists(directoryPath))
                {
                    System.IO.Directory.CreateDirectory(directoryPath);
                    AssetDatabase.Refresh();
                    LogInfo($"[HierarchyCreate] Created directory for prefab: {directoryPath}");
                }

                // 保存为预制体
                GameObject finalInstance = PrefabUtility.SaveAsPrefabAssetAndConnect(
                    newGo,
                    finalPrefabPath,
                    InteractionMode.UserAction
                );

                if (finalInstance == null)
                {
                    UnityEngine.Object.DestroyImmediate(newGo);
                    return null;
                }

                LogInfo($"[HierarchyCreate] GameObject '{newGo.name}' saved as prefab to '{finalPrefabPath}' and instance connected.");
                return finalInstance;
            }
            catch (Exception e)
            {
                LogInfo($"[HierarchyCreate] Error saving prefab '{finalPrefabPath}': {e.Message}");
                UnityEngine.Object.DestroyImmediate(newGo);
                return null;
            }
        }

        /// <summary>
        /// 生成创建成功消息
        /// </summary>
        private string GenerateCreationSuccessMessage(JObject args, GameObject finalInstance, bool createdNewObject, bool saveAsPrefab)
        {
            string messagePrefabPath = AssetDatabase.GetAssetPath(
                PrefabUtility.GetCorrespondingObjectFromSource(finalInstance) ?? (UnityEngine.Object)finalInstance
            );

            if (!createdNewObject && !string.IsNullOrEmpty(messagePrefabPath))
            {
                return $"Prefab '{messagePrefabPath}' instantiated successfully as '{finalInstance.name}'.";
            }
            else if (createdNewObject && saveAsPrefab && !string.IsNullOrEmpty(messagePrefabPath))
            {
                return $"GameObject '{finalInstance.name}' created and saved as prefab to '{messagePrefabPath}'.";
            }
            else
            {
                return $"GameObject '{finalInstance.name}' created successfully in scene.";
            }
        }
    }
}

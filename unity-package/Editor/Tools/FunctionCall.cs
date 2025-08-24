using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcpBridge.Editor.Helpers;

namespace UnityMcpBridge.Editor.Tools
{
    /// <summary>
    /// Handles single function calls from MCP server.
    /// Routes function calls to appropriate tool classes via reflection and name matching.
    /// </summary>
    public class FunctionCall : McpTool
    {
        public override string ToolName => "function_call";

        // 缓存已注册的工具实例 (key: snake_case名称, value: 工具实例)
        private static Dictionary<string, IToolMethod> _registeredMethods = null;
        private static readonly object _registrationLock = new object();

        /// <summary>
        /// Main handler for function calls.
        /// </summary>
        public override object HandleCommand(JObject cmd)
        {
            try
            {
                string functionName = cmd["func"]?.ToString();
                string argsJson = cmd["args"]?.ToString() ?? "{}";

                if (string.IsNullOrWhiteSpace(functionName))
                {
                    return Response.Error("Required parameter 'func' is missing or empty.");
                }

                return ExecuteFunction(functionName, argsJson);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FunctionCall] Command execution failed: {e}");
                return Response.Error($"Internal error processing function call: {e.Message}");
            }
        }

        /// <summary>
        /// Executes a specific function by routing to the appropriate method.
        /// </summary>
        private object ExecuteFunction(string functionName, string argsJson)
        {
            Debug.Log($"[FunctionCall] Executing function: {functionName}->{argsJson}");
            try
            {
                // 确保方法已注册
                EnsureMethodsRegistered();

                // 解析参数
                JObject args = JObject.Parse(argsJson);

                // 查找对应的工具方法
                if (!_registeredMethods.TryGetValue(functionName, out IToolMethod method))
                {
                    return Response.Error($"Unknown method: '{functionName}'. Available methods: {string.Join(", ", _registeredMethods.Keys)}");
                }

                // 调用工具的ExecuteMethod方法
                return method.ExecuteMethod(args);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FunctionCall] Failed to execute function '{functionName}': {e}");
                return Response.Error($"Error executing function '{functionName}->{argsJson}': {e.Message}");
            }
        }

        /// <summary>
        /// 确保所有方法已通过反射注册 (内部方法)
        /// </summary>
        private void EnsureMethodsRegistered()
        {
            EnsureMethodsRegisteredStatic();
        }

        /// <summary>
        /// 确保所有方法已通过反射注册 (静态方法，供其他类使用)
        /// </summary>
        public static void EnsureMethodsRegisteredStatic()
        {
            if (_registeredMethods != null) return;

            lock (_registrationLock)
            {
                if (_registeredMethods != null) return; // 双重检查锁定

                _registeredMethods = new Dictionary<string, IToolMethod>();

                try
                {
                    // 通过反射查找所有程序集中实现IToolMethod接口的类
                    var methodTypes = new List<Type>();
                    
                    // 遍历所有已加载的程序集
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            var types = assembly.GetTypes()
                                .Where(t => typeof(IToolMethod).IsAssignableFrom(t) && 
                                           !t.IsInterface && 
                                           !t.IsAbstract)
                                .ToList();
                            methodTypes.AddRange(types);
                        }
                        catch (ReflectionTypeLoadException ex)
                        {
                            // 某些程序集可能无法完全加载，但我们可以获取成功加载的类型
                            var loadedTypes = ex.Types.Where(t => t != null && 
                                typeof(IToolMethod).IsAssignableFrom(t) && 
                                !t.IsInterface && 
                                !t.IsAbstract).ToList();
                            methodTypes.AddRange(loadedTypes);
                            
                            Debug.LogWarning($"[FunctionCall] Partial load of assembly {assembly.FullName}: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            // 跳过无法访问的程序集
                            Debug.LogWarning($"[FunctionCall] Failed to load types from assembly {assembly.FullName}: {ex.Message}");
                            continue;
                        }
                    }

                    foreach (var methodType in methodTypes)
                    {
                        try
                        {
                            // 创建方法实例
                            var methodInstance = Activator.CreateInstance(methodType) as IToolMethod;
                            if (methodInstance != null)
                            {
                                // 将类名转换为snake_case格式作为方法名
                                string methodName = ConvertToSnakeCase(methodType.Name);
                                _registeredMethods[methodName] = methodInstance;
                                Debug.Log($"[FunctionCall] Registered method: {methodName} -> {methodType.FullName}");
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[FunctionCall] Failed to register method {methodType.FullName}: {e}");
                        }
                    }

                    Debug.Log($"[FunctionCall] Total registered methods: {_registeredMethods.Count}");
                    Debug.Log($"[FunctionCall] Available methods: {string.Join(", ", _registeredMethods.Keys)}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FunctionCall] Failed to register methods: {e}");
                    _registeredMethods = new Dictionary<string, IToolMethod>(); // 确保不为null
                }
            }
        }

        /// <summary>
        /// 获取已注册的方法实例 (静态方法，供其他类使用)
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <returns>方法实例，如果未找到则返回null</returns>
        public static IToolMethod GetRegisteredMethod(string methodName)
        {
            EnsureMethodsRegisteredStatic();
            _registeredMethods.TryGetValue(methodName, out IToolMethod method);
            return method;
        }

        /// <summary>
        /// 执行指定方法 (静态方法，供其他类使用)
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <param name="args">参数对象</param>
        /// <returns>执行结果</returns>
        public static object ExecuteMethodStatic(string methodName, JObject args)
        {
            try
            {
                EnsureMethodsRegisteredStatic();

                if (!_registeredMethods.TryGetValue(methodName, out IToolMethod method))
                {
                    return Response.Error($"Unknown method: '{methodName}'. Available methods: {string.Join(", ", _registeredMethods.Keys)}");
                }

                return method.ExecuteMethod(args);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FunctionCall] Failed to execute method '{methodName}': {e}");
                return Response.Error($"Error executing method '{methodName}': {e.Message}");
            }
        }

        /// <summary>
        /// 将Pascal命名法转换为snake_case命名法
        /// 例如: ManageAsset -> manage_asset, ExecuteMenuItem -> execute_menu_item
        /// </summary>
        private static string ConvertToSnakeCase(string pascalCase)
        {
            if (string.IsNullOrEmpty(pascalCase))
                return pascalCase;

            // 使用正则表达式在大写字母前插入下划线，然后转换为小写
            return Regex.Replace(pascalCase, "(?<!^)([A-Z])", "_$1").ToLower();
        }

        /// <summary>
        /// 手动注册方法（供外部调用）
        /// </summary>
        public static void RegisterMethod(string methodName, IToolMethod method)
        {
            lock (_registrationLock)
            {
                if (_registeredMethods == null)
                    _registeredMethods = new Dictionary<string, IToolMethod>();
                    
                _registeredMethods[methodName] = method;
                Debug.Log($"[FunctionCall] Manually registered method: {methodName}");
            }
        }

        /// <summary>
        /// 获取所有已注册的方法名称
        /// </summary>
        public static string[] GetRegisteredMethodNames()
        {
            EnsureMethodsRegisteredStatic();
            return _registeredMethods?.Keys.ToArray() ?? new string[0];
        }
    }
} 
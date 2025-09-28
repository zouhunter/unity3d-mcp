using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;
using UnityMcp;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles method calls by routing to specific tool methods.
    /// Acts as a dispatcher that forwards calls to the appropriate IToolMethod implementations.
    /// The ToolName property is determined by the "type" field in the command.
    /// Example: {"type": "hierarchy_create", "args": {"from": "primitive", "primitive_type": "Cube", "name": "Enemy"}}
    /// </summary>
    public class MethodsCall : McpTool
    {
        private string _methodType = "methods_call";
        public override string ToolName => _methodType;

        // 缓存已注册的工具实例 (key: snake_case名称, value: 工具实例)
        private static Dictionary<string, IToolMethod> _registeredMethods = null;
        private static readonly object _registrationLock = new object();

        internal void SetToolName(string toolName)
        {
            this._methodType = toolName;
        }

        /// <summary>
        /// 获取指定名称的工具方法
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <returns>工具方法实例，如果未找到则返回null</returns>
        public IToolMethod GetToolMethod(string toolName)
        {
            EnsureMethodsRegistered();
            _registeredMethods.TryGetValue(toolName, out IToolMethod method);
            return method;
        }

        /// <summary>
        /// Main handler for method calls (同步版本).
        /// Expects command format: {"type": "hierarchy_create", "args": {...}}
        /// </summary>
        public override void HandleCommand(JObject args, Action<object> callback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_methodType))
                {
                    callback(Response.Error("Required parameter 'type' is missing or empty."));
                    return;
                }

                if (args == null)
                {
                    callback(Response.Error("Required parameter 'args' is missing or not an object."));
                    return;
                }

                string argsJson = args.ToString();
                ExecuteMethod(_methodType, argsJson, callback);
            }
            catch (Exception e)
            {
                if (McpConnect.EnableLog) Debug.LogError($"[MethodsCall] Command execution failed: {e}");
                callback(Response.Error($"Internal error processing method call: {e.Message}"));
                return;
            }
        }

        /// <summary>
        /// Executes a specific method by routing to the appropriate tool method (同步版本).
        /// </summary>
        private void ExecuteMethod(string methodName, string argsJson, Action<object> callback)
        {
            if (McpConnect.EnableLog)
                Debug.Log($"[MethodsCall] Executing method: {methodName}->{argsJson}");
            try
            {
                // 确保方法已注册
                EnsureMethodsRegistered();

                // 解析参数
                JObject args = JObject.Parse(argsJson);

                // 查找对应的工具方法
                if (!_registeredMethods.TryGetValue(methodName, out IToolMethod method))
                {
                    callback(Response.Error($"Unknown method: '{methodName}'. Available methods: {string.Join(", ", _registeredMethods.Keys)}"));
                    return;
                }

                // 调用工具的ExecuteMethod方法
                var state = new StateTreeContext(args, new System.Collections.Generic.Dictionary<string, object>());
                method.ExecuteMethod(state);
                state.RegistComplete(callback);
            }
            catch (Exception e)
            {
                if (McpConnect.EnableLog) Debug.LogError($"[MethodsCall] Failed to execute method '{methodName}': {e}");
                callback(Response.Error($"Error executing method '{methodName}->{argsJson}': {e.Message}"));
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

                            if (McpConnect.EnableLog) Debug.LogWarning($"[MethodsCall] Partial load of assembly {assembly.FullName}: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            // 跳过无法访问的程序集
                            if (McpConnect.EnableLog) Debug.LogWarning($"[MethodsCall] Failed to load types from assembly {assembly.FullName}: {ex.Message}");
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
                                // 优先使用ToolNameAttribute指定的名称，否则转换类名为snake_case格式
                                string methodName = GetMethodName(methodType);
                                _registeredMethods[methodName] = methodInstance;
                                if (McpConnect.EnableLog) Debug.Log($"[MethodsCall] Registered method: {methodName} -> {methodType.FullName}");
                            }
                        }
                        catch (Exception e)
                        {
                            if (McpConnect.EnableLog) Debug.LogError($"[MethodsCall] Failed to register method {methodType.FullName}: {e}");
                        }
                    }

                    if (McpConnect.EnableLog) Debug.Log($"[MethodsCall] Total registered methods: {_registeredMethods.Count}");
                    if (McpConnect.EnableLog) Debug.Log($"[MethodsCall] Available methods: {string.Join(", ", _registeredMethods.Keys)}");
                }
                catch (Exception e)
                {
                    if (McpConnect.EnableLog) Debug.LogError($"[MethodsCall] Failed to register methods: {e}");
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
        /// 获取方法名称，优先使用ToolNameAttribute指定的名称，否则转换类名为snake_case格式
        /// </summary>
        /// <param name="methodType">方法类型</param>
        /// <returns>方法名称</returns>
        private static string GetMethodName(Type methodType)
        {
            // 检查是否有ToolNameAttribute
            var toolNameAttribute = methodType.GetCustomAttribute<ToolNameAttribute>();
            if (toolNameAttribute != null)
            {
                return toolNameAttribute.ToolName;
            }

            // 回退到类名转换为snake_case格式
            return ConvertToSnakeCase(methodType.Name);
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
                if (McpConnect.EnableLog) Debug.Log($"[MethodsCall] Manually registered method: {methodName}");
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

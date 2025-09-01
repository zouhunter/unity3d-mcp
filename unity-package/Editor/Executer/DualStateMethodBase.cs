using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEditor;
using UnityMcp.Models;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityMcp.Tools
{
    /// <summary>
    /// 双状态树方法基类，提供基于两棵状态树的方法调用框架。
    /// 第一棵树用于目标定位，第二棵树用于执行操作。
    /// 所有需要双阶段处理的工具方法类应继承此类。
    /// </summary>
    public abstract class DualStateMethodBase : IToolMethod
    {
        /// <summary>
        /// 目标定位状态树实例，用于定位操作目标。
        /// 懒加载模式：首次访问时才创建。
        /// </summary>
        private StateTree _targetTree;

        /// <summary>
        /// 执行操作状态树实例，用于执行具体操作。
        /// 懒加载模式：首次访问时才创建。
        /// </summary>
        private StateTree _actionTree;

        /// <summary>
        /// Keys的缓存字段，避免重复创建
        /// </summary>
        private MethodKey[] _keys;

        /// <summary>
        /// 当前方法支持的参数键列表，用于API文档生成和参数验证。
        /// 子类必须实现此属性，定义该方法接受的所有可能参数键。
        /// </summary>
        public MethodKey[] Keys
        {
            get
            {
                if (_keys == null)
                {
                    _keys = CreateKeys();
                }
                return _keys;
            }
        }

        /// <summary>
        /// 创建参数键列表的抽象方法，子类必须实现此方法来定义参数键。
        /// </summary>
        /// <returns>MethodKey数组</returns>
        protected abstract MethodKey[] CreateKeys();

        /// <summary>
        /// 创建目标定位状态树的抽象方法，子类必须实现此方法来定义目标定位逻辑。
        /// </summary>
        /// <returns>配置好的目标定位状态树实例</returns>
        protected abstract StateTree CreateTargetTree();

        /// <summary>
        /// 创建执行操作状态树的抽象方法，子类必须实现此方法来定义操作执行逻辑。
        /// </summary>
        /// <returns>配置好的执行操作状态树实例</returns>
        protected abstract StateTree CreateActionTree();

        /// <summary>
        /// 处理目标定位结果的虚方法，子类可以重写此方法来自定义目标处理逻辑。
        /// </summary>
        /// <param name="targetResult">目标定位的结果</param>
        /// <param name="originalArgs">原始参数</param>
        /// <returns>处理后的目标对象，如果为null则表示目标定位失败</returns>
        protected virtual object ProcessTargetResult(object targetResult, StateTreeContext originalArgs)
        {
            return targetResult;
        }

        /// <summary>
        /// 预览双状态树结构，用于调试和可视化状态路由逻辑。
        /// </summary>
        /// <returns>双状态树的文本表示</returns>
        public virtual string Preview()
        {
            // 确保状态树已初始化
            _targetTree = _targetTree ?? CreateTargetTree();
            _actionTree = _actionTree ?? CreateActionTree();

            var sb = new StringBuilder();
            sb.AppendLine("=== Dual State Method Preview ===");
            sb.AppendLine();
            sb.AppendLine(">>> Target Location Tree <<<");
            _targetTree.Print(sb);
            sb.AppendLine();
            sb.AppendLine(">>> Action Execution Tree <<<");
            _actionTree.Print(sb);
            sb.AppendLine();
            sb.AppendLine("=== End Preview ===");

            return sb.ToString();
        }

        /// <summary>
        /// 执行工具方法，实现 IToolMethod 接口（同步版本）。
        /// 分两个阶段：首先通过目标定位树找到目标，然后通过执行操作树执行操作。
        /// </summary>
        /// <param name="args">方法调用的参数对象</param>
        /// <returns>执行结果，若任一阶段失败则返回错误响应</returns>
        public virtual object ExecuteMethod(StateTreeContext args)
        {
            try
            {
                // 确保状态树已初始化
                _targetTree = _targetTree ?? CreateTargetTree();
                _actionTree = _actionTree ?? CreateActionTree();

                // 第一阶段：使用目标定位树找到目标
                LogInfo("[DualStateMethodBase] Phase 1: Target Location");
                var targetResult = _targetTree.Run(args);

                // 检查目标定位阶段的错误
                if (targetResult == null && !string.IsNullOrEmpty(_targetTree.ErrorMessage))
                {
                    LogError($"[DualStateMethodBase] Target location failed: {_targetTree.ErrorMessage}");
                    return Response.Error($"Target location failed: {_targetTree.ErrorMessage}");
                }

                // 处理目标定位结果
                var processedTarget = ProcessTargetResult(targetResult, args);
                if (processedTarget == null)
                {
                    LogError("[DualStateMethodBase] Target processing failed or returned null");
                    return Response.Error("Target could not be located or processed");
                }

                LogInfo($"[DualStateMethodBase] Target located successfully: {processedTarget?.GetType()?.Name ?? "Unknown"}");

                // 第二阶段：创建执行上下文并执行操作
                LogInfo("[DualStateMethodBase] Phase 2: Action Execution");
                args.SetObjectReference("_resolved_targets", processedTarget);

                var actionResult = _actionTree.Run(args);

                // 检查执行操作阶段的错误
                if (actionResult == null && !string.IsNullOrEmpty(_actionTree.ErrorMessage))
                {
                    LogError($"[DualStateMethodBase] Action execution failed: {_actionTree.ErrorMessage}");
                    return Response.Error($"Action execution failed: {_actionTree.ErrorMessage}");
                }

                LogInfo("[DualStateMethodBase] Action executed successfully");
                return actionResult;
            }
            catch (Exception e)
            {
                LogException(new Exception("[DualStateMethodBase] Unexpected error during dual-tree execution:", e));
                return Response.Error($"Unexpected error during execution: {e.Message}");
            }
        }

        /// <summary>
        /// 执行工具方法，实现 IToolMethod 接口（异步版本）。
        /// 通过主线程执行器确保双状态树在Unity主线程上执行。
        /// </summary>
        /// <param name="args">方法调用的参数对象</param>
        /// <returns>执行结果，若任一阶段失败则返回错误响应</returns>
        public virtual async Task<object> ExecuteMethodAsync(StateTreeContext args)
        {
            try
            {
                // 使用MainThreadExecutor确保在主线程执行
                return await MainThreadExecutor.ExecuteAsync(() => ExecuteMethod(args));
            }
            catch (Exception e)
            {
                LogException(new Exception($"[DualStateMethodBase] Failed to execute method on main thread:", e));
                return Response.Error($"Error executing method on main thread: {e.Message}");
            }
        }

        /// <summary>
        /// 记录信息日志，仅在 McpConnect.EnableLog 为 true 时输出。
        /// 子类可用此方法记录执行过程中的信息。
        /// </summary>
        /// <param name="message">要记录的日志消息</param>
        public virtual void LogInfo(string message)
        {
            if (McpConnect.EnableLog) Debug.Log(message);
        }

        public virtual void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        public virtual void LogError(string message)
        {
            Debug.LogError(message);
        }

        public virtual void LogException(Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}

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
        /// 执行工具方法，实现 IToolMethod 接口。
        /// 分两个阶段：首先通过目标定位树找到目标，然后通过执行操作树执行操作。
        /// </summary>
        /// <param name="args">方法调用的参数对象</param>
        public virtual void ExecuteMethod(StateTreeContext args)
        {
            try
            {
                // 确保状态树已初始化
                _targetTree = _targetTree ?? CreateTargetTree();
                _actionTree = _actionTree ?? CreateActionTree();
                ExecuteTargetTree(args);
            }
            catch (Exception e)
            {
                LogException(new Exception("[DualStateMethodBase] Unexpected error during dual-tree execution:", e));
                args.Complete(Response.Error($"Unexpected error during execution: {e.Message}"));
            }
        }
        /// <summary>
        /// 执行目标树
        /// </summary>
        /// <param name="args"></param>
        protected virtual void ExecuteTargetTree(StateTreeContext args)
        {
            var copyContext = new StateTreeContext(args.JsonData, args.ObjectReferences);
            // 第一阶段：使用目标定位树找到目标
            LogInfo("[DualStateMethodBase] Phase 1: Target Location");
            var targetResult = _targetTree.Run(copyContext);

            // 检查目标定位阶段的错误
            if (targetResult == null && !string.IsNullOrEmpty(_targetTree.ErrorMessage))
            {
                LogError($"[DualStateMethodBase] Target location failed: {_targetTree.ErrorMessage}");
                args.Complete(Response.Error($"Target location failed: {_targetTree.ErrorMessage}"));
            }
            else if (targetResult != null && targetResult != copyContext)
            {
                ExecuteActiontTree(targetResult, args);
            }
            else
            {
                copyContext.RegistComplete((x) => ExecuteActiontTree(x, args));
            }
        }
        /// <summary>
        /// 执行操作树
        /// </summary>
        /// <param name="targetResult"></param>
        /// <param name="args"></param>
        protected virtual void ExecuteActiontTree(object targetResult, StateTreeContext args)
        {
            // 处理目标定位结果
            var processedTarget = ProcessTargetResult(targetResult);
            if (processedTarget == null)
            {
                LogError("[DualStateMethodBase] Target processing failed or returned null");
                args.Complete(Response.Error("Target could not be located or processed"));
                return;
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
                args.Complete(Response.Error($"Action execution failed: {_actionTree.ErrorMessage}"));
                return;
            }

            LogInfo("[DualStateMethodBase] Action executed successfully");
            if (actionResult == null && !string.IsNullOrEmpty(_actionTree.ErrorMessage))
            {
                LogError($"[DualStateMethodBase] Action execution failed: {_actionTree.ErrorMessage}");
                args.Complete(Response.Error($"Action execution failed: {_actionTree.ErrorMessage}"));
            }
            // 完成执行
            else if (actionResult != null && actionResult != args)
            {
                args.Complete(actionResult);
            }
            else
            {
                // 异步执行完成
                LogInfo("[DualStateMethodBase] Execution completed!");
            }
        }


        /// <summary>
        /// 处理目标定位结果。如果目标结果是Response类型（即包含success字段），则直接返回，表示已是最终响应。
        /// 否则返回原始目标结果，供后续操作树处理。
        /// </summary>
        protected virtual object ProcessTargetResult(object targetResult)
        {
            // 判断是否为Response类型（即包含success字段的匿名对象）
            if (targetResult != null)
            {
                var type = targetResult.GetType();
                var successProp = type.GetProperty("success");
                if (successProp != null)
                {
                    return null;
                }
            }
            // 否则返回原始目标结果
            return targetResult;
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

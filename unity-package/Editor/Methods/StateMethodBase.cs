using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityMcp.Models;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityMcp.Tools
{
    /// <summary>
    /// 状态方法基类，提供基于状态树的方法调用框架。
    /// 所有工具方法类应继承此类，并实现 CreateStateTree 方法来定义状态路由逻辑。
    /// </summary>
    public abstract class StateMethodBase : IToolMethod
    {
        /// <summary>
        /// 状态树实例，用于路由和执行方法调用。
        /// 懒加载模式：首次访问时才创建。
        /// </summary>
        private StateTree _stateTree;

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
        /// 创建状态树的抽象方法，子类必须实现此方法来定义状态路由逻辑。
        /// </summary>
        /// <returns>配置好的状态树实例</returns>
        protected abstract StateTree CreateStateTree();

        /// <summary>
        /// 预览状态树结构，用于调试和可视化状态路由逻辑。
        /// </summary>
        /// <returns>状态树的文本表示</returns>
        public virtual string Preview()
        {
            // 确保状态树已初始化
            _stateTree = _stateTree ?? CreateStateTree();
            var sb = new StringBuilder();
            _stateTree.Print(sb);
            return sb.ToString();
        }

        /// <summary>
        /// 执行工具方法，实现 IToolMethod 接口（同步版本）。
        /// 通过状态树路由到对应的处理方法。
        /// </summary>
        /// <param name="args">方法调用的参数对象</param>
        /// <returns>执行结果，若状态树执行失败则返回错误响应</returns>
        public virtual object ExecuteMethod(JObject args)
        {
            // 确保状态树已初始化
            _stateTree = _stateTree ?? CreateStateTree();
            var result = _stateTree.Run(args);
            // 如果结果为空且有错误信息，返回错误响应
            if (result == null && !string.IsNullOrEmpty(_stateTree.ErrorMessage))
            {
                return Response.Error(_stateTree.ErrorMessage);
            }
            return result;
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
    }
}
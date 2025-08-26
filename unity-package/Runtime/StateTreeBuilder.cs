using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace UnityMcp.Tools
{
    public class StateTreeBuilder
    {
        private readonly StateTree root;
        private readonly Stack<StateTree> nodeStack = new();

        private StateTreeBuilder()
        {
            root = new StateTree();
            nodeStack.Push(root);
        }

        public static StateTreeBuilder Create()
        {
            return new StateTreeBuilder();
        }

        private StateTree Current => nodeStack.Peek();

        public StateTreeBuilder Key(string variableKey)
        {
            Current.key = variableKey;
            return this;
        }

        public StateTreeBuilder Branch(object edgeKey)
        {
            // 防止null key导致异常
            if (edgeKey == null)
            {
                throw new ArgumentNullException(nameof(edgeKey), "edgeKey cannot be null in Branch method");
            }

            if (!Current.select.TryGetValue(edgeKey, out var child))
            {
                child = new StateTree();
                Current.select[edgeKey] = child;
            }
            nodeStack.Push(child);
            return this;
        }

        public StateTreeBuilder DefaultBranch()
        {
            return Branch(StateTree.Default);
        }

        public StateTreeBuilder Leaf(object edgeKey, Func<JObject, object> action)
        {
            // 防止null key导致异常
            if (edgeKey == null)
            {
                throw new ArgumentNullException(nameof(edgeKey), "edgeKey cannot be null in Leaf method");
            }

            Current.select[edgeKey] = (StateTree)action;
            return this;
        }

        public StateTreeBuilder DefaultLeaf(Func<JObject, object> action)
        {
            return Leaf(StateTree.Default, action);
        }

        /// <summary>
        /// 添加可选参数分支：当指定的参数存在时执行对应的动作
        /// </summary>
        /// <param name="parameterName">要检查的参数名</param>
        /// <param name="action">参数存在时执行的动作</param>
        public StateTreeBuilder OptionalLeaf(string parameterName, Func<JObject, object> action)
        {
            // 使用特殊的键格式来标识这是一个可选参数检查
            string optionalKey = $"__OPTIONAL_PARAM__{parameterName}";
            Current.select[optionalKey] = (StateTree)action;
            return this;
        }

        /// <summary>
        /// 添加可选参数分支：当指定的参数存在时进入子分支
        /// </summary>
        /// <param name="parameterName">要检查的参数名</param>
        public StateTreeBuilder OptionalBranch(string parameterName)
        {
            string optionalKey = $"__OPTIONAL_PARAM__{parameterName}";
            return Branch(optionalKey);
        }

        /// <summary>
        /// 添加可选参数节点：当指定的参数存在时进入子分支并设置key
        /// 相当于 OptionalBranch + Key 的组合
        /// </summary>
        /// <param name="parameterName">要检查的参数名</param>
        /// <param name="variableKey">子分支的变量key</param>
        public StateTreeBuilder OptionalNode(string parameterName, string variableKey)
        {
            return OptionalBranch(parameterName).Key(variableKey);
        }

        public StateTreeBuilder Node(object edgeKey, string variableKey)
        {
            return Branch(edgeKey).Key(variableKey);
        }

        public StateTreeBuilder Up()
        {
            if (nodeStack.Count > 1)
            {
                nodeStack.Pop();
            }
            return this;
        }

        public StateTreeBuilder ULeaf(object edgeKey, Func<JObject, object> action)
        {
            return Up().Leaf(edgeKey, action);
        }

        public StateTreeBuilder UNode(object edgeKey, string variableKey)
        {
            return Up().Node(edgeKey, variableKey);
        }

        public StateTree Build()
        {
            return root;
        }
    }
}



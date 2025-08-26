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
            Current.select[edgeKey] = (StateTree)action;
            return this;
        }

        public StateTreeBuilder DefaultLeaf(Func<JObject, object> action)
        {
            return Leaf(StateTree.Default, action);
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



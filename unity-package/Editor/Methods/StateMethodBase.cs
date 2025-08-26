using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityMcp.Models;
using Newtonsoft.Json.Linq;
using System.Text;
using UnityMcp.Helpers;

namespace UnityMcp.Tools
{
    public abstract class StateMethodBase : IToolMethod
    {
        private StateTree _stateTree;
        protected abstract StateTree CreateStateTree();

        public virtual string Preview()
        {
            _stateTree = _stateTree ?? CreateStateTree();
            var sb = new StringBuilder();
            _stateTree.Print(sb);
            return sb.ToString();
        }
        public virtual object ExecuteMethod(JObject args)
        {
            _stateTree = _stateTree ?? CreateStateTree();
            var result = _stateTree.Run(args);
            if (result == null && !string.IsNullOrEmpty(_stateTree.ErrorMessage))
            {
                return Response.Error(_stateTree.ErrorMessage);
            }
            return result;
        }
    }
}
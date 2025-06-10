using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
namespace UnityMcpBridge.Editor.Tools
{
    public abstract class McpTool
    {
        public abstract string ToolName { get; }
        public abstract object HandleCommand(JObject @params);
    }
}
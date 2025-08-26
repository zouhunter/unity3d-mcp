using Newtonsoft.Json.Linq;

namespace UnityMcp.Tools
{
    public abstract class McpTool
    {
        public abstract string ToolName { get; }
        public abstract object HandleCommand(JObject cmd);
    }
}
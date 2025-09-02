using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace UnityMcp.Tools
{
    public abstract class McpTool
    {
        public abstract string ToolName { get; }
        /// <summary>
        /// 处理命令（同步版本，保持向后兼容）
        /// </summary>
        /// <param name="cmd">命令参数</param>
        /// <returns>处理结果</returns>
        public abstract void HandleCommand(JObject ctx, System.Action<object> callback);
    }
}
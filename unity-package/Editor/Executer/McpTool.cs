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
        public abstract object HandleCommand(JObject ctx);

        /// <summary>
        /// 处理命令（异步版本）
        /// 默认实现：在Task中包装同步执行，子类可重写以提供真正的异步实现
        /// </summary>
        /// <param name="cmd">命令参数</param>
        /// <returns>处理结果</returns>
        public virtual async Task<object> HandleCommandAsync(JObject cmd)
        {
            // 默认实现：在Task中包装同步执行
            // 子类可以重写此方法以提供真正的异步实现
            return await Task.Run(() => HandleCommand(cmd));
        }
    }
}
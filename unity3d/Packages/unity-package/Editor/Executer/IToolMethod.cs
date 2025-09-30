using Newtonsoft.Json.Linq;
using System.Collections;
using System.Threading.Tasks;

namespace UnityMcp.Tools
{
    /// <summary>
    /// 工具方法接口，所有具体工具类都应实现此接口
    /// </summary>
    public interface IToolMethod
    {
        MethodKey[] Keys { get; }
        /// <summary>
        /// 执行工具方法（同步版本，保持向后兼容）
        /// </summary>
        /// <param name="args">参数对象</param>
        /// <returns>执行结果</returns>
        void ExecuteMethod(StateTreeContext args);
        /// <summary>
        /// 预览方法
        /// </summary>
        /// <returns>预览结果</returns>
        string Preview();
    }
}
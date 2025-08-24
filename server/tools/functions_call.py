"""
批量函数调用工具，用于顺序调用Unity中的多个函数并收集返回值。
"""
import json
from typing import List, Dict, Any
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection

def register_functions_call_tools(mcp: FastMCP):
    """注册批量函数调用工具到MCP服务器。"""

    @mcp.tool()
    def functions_call(
        ctx: Context,
        funcs: List[str],
        args: List[Dict[str, Any]]
    ) -> Dict[str, Any]:
        """批量函数调用工具，可以按顺序调用Unity中的多个函数并收集所有返回值。

        Args:
            ctx: MCP上下文。
            funcs: 函数名称列表，例如：["Function1", "Function2", "Function3"]
            args: 对应的参数列表，每个元素是参数对象，例如：
                [
                    {"param1": "value1"}, 
                    {"param2": 123}, 
                    {"param3": true}
                ]
                注意：funcs数组和args数组的长度必须相等，按索引位置一一对应。

        Returns:
            包含所有函数调用结果的字典：
            {
                "success": bool,
                "results": [result1, result2, ...],
                "errors": [error1, error2, ...],
                "total_calls": int,
                "successful_calls": int,
                "failed_calls": int
            }
        """
        
        # 获取Unity连接实例
        bridge = get_unity_connection()
        
        results = []
        errors = []
        total_calls = 0
        successful_calls = 0
        failed_calls = 0
        
        try:
            # 验证输入参数
            if not isinstance(funcs, list) or not isinstance(args, list):
                return {
                    "success": False,
                    "results": [],
                    "errors": ["funcs和args参数必须是数组类型"],
                    "total_calls": 0,
                    "successful_calls": 0,
                    "failed_calls": 1
                }
            
            # 验证数组长度是否一致
            if len(funcs) != len(args):
                return {
                    "success": False,
                    "results": [],
                    "errors": [f"funcs数组长度({len(funcs)})与args数组长度({len(args)})不匹配"],
                    "total_calls": 0,
                    "successful_calls": 0,
                    "failed_calls": 1
                }
            
            total_calls = len(funcs)
            
            # 按顺序执行每个函数调用
            for i in range(total_calls):
                try:
                    func = funcs[i]
                    arg = args[i]
                    
                    # 验证函数名称
                    if not func or not isinstance(func, str):
                        error_msg = f"第{i+1}个函数名称无效或为空"
                        errors.append(error_msg)
                        results.append(None)
                        failed_calls += 1
                        continue
                    
                    # 验证参数格式（应该是字典对象）
                    if not isinstance(arg, dict):
                        error_msg = f"第{i+1}个参数必须是对象类型"
                        errors.append(error_msg)
                        results.append(None)
                        failed_calls += 1
                        continue
                    
                    # 准备发送给Unity的参数
                    params = {
                        "func": func,
                        "args": json.dumps(arg)  # 将对象序列化为JSON字符串发送给Unity
                    }
                    
                    # 调用Unity函数
                    result = bridge.send_command("function_call", params)
                    results.append(result)
                    errors.append(None)  # 成功调用时添加None到错误列表，保持索引对应
                    successful_calls += 1
                    
                except Exception as e:
                    error_msg = f"第{i+1}个函数调用失败: {str(e)}"
                    errors.append(error_msg)
                    results.append(None)
                    failed_calls += 1
            
        except Exception as e:
            return {
                "success": False,
                "results": [],
                "errors": [f"批量调用过程中发生未预期错误: {str(e)}"],
                "total_calls": total_calls,
                "successful_calls": successful_calls,
                "failed_calls": failed_calls + 1
            }
        
        # 返回汇总结果
        return {
            "success": failed_calls == 0,
            "results": results,
            "errors": errors,
            "total_calls": total_calls,
            "successful_calls": successful_calls,
            "failed_calls": failed_calls
        } 
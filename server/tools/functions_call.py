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
        function_calls: str
    ) -> Dict[str, Any]:
        """批量函数调用工具，可以按顺序调用Unity中的多个函数并收集所有返回值。

        Args:
            ctx: MCP上下文。
            function_calls: 函数调用列表的JSON字符串，格式如下：
                [
                    {
                        "func": "Function1",
                        "args": "{\"param1\": \"value1\"}"
                    },
                    {
                        "func": "Function2",
                        "args": "{\"param2\": 123}"
                    }
                ]

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
            # 解析函数调用列表
            calls_list = json.loads(function_calls)
            if not isinstance(calls_list, list):
                return {
                    "success": False,
                    "results": [],
                    "errors": ["function_calls参数必须是一个JSON数组"],
                    "total_calls": 0,
                    "successful_calls": 0,
                    "failed_calls": 1
                }
            
            total_calls = len(calls_list)
            
            # 按顺序执行每个函数调用
            for i, call_info in enumerate(calls_list):
                try:
                    # 验证调用信息格式
                    if not isinstance(call_info, dict):
                        error_msg = f"第{i+1}个调用信息必须是字典格式"
                        errors.append(error_msg)
                        results.append(None)
                        failed_calls += 1
                        continue
                    
                    func = call_info.get("func")
                    args = call_info.get("args", "{}")
                    
                    if not func:
                        error_msg = f"第{i+1}个调用缺少func参数"
                        errors.append(error_msg)
                        results.append(None)
                        failed_calls += 1
                        continue
                    
                    # 准备发送给Unity的参数
                    params = {
                        "func": func,
                        "args": args
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
            
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "results": [],
                "errors": [f"JSON解析失败: {str(e)}"],
                "total_calls": 0,
                "successful_calls": 0,
                "failed_calls": 1
            }
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
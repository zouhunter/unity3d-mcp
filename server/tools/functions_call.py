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
        funcs: List[Dict[str, Any]]
    ) -> Dict[str, Any]:
        """批量函数调用工具，可以按顺序调用Unity中的多个函数并收集所有返回值。

        Args:
            ctx: MCP上下文。
            funcs: 函数调用列表，每个元素包含func和args字段，例如：
                [
                    {"func": "manage_gameobject", "args": {"action": "create", "name": "Enemy", "primitive_type": "Cube"}},
                    {"func": "manage_gameobject", "args": {"action": "add_component", "target": "Enemy", "component_name": "Rigidbody"}}
                ]
                其中args字段是参数对象。

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
        
        try:
            # 验证输入参数
            if not isinstance(funcs, list):
                return {
                    "success": False,
                    "results": [],
                    "errors": ["funcs参数必须是数组类型"],
                    "total_calls": 0,
                    "successful_calls": 0,
                    "failed_calls": 1
                }
            
            # 基本的函数调用格式验证
            for i, func_call in enumerate(funcs):
                if not isinstance(func_call, dict):
                    return {
                        "success": False,
                        "results": [],
                        "errors": [f"第{i+1}个函数调用必须是对象类型"],
                        "total_calls": len(funcs),
                        "successful_calls": 0,
                        "failed_calls": 1
                    }
                
                if "func" not in func_call or not isinstance(func_call.get("func"), str):
                    return {
                        "success": False,
                        "results": [],
                        "errors": [f"第{i+1}个函数调用的func字段无效或为空"],
                        "total_calls": len(funcs),
                        "successful_calls": 0,
                        "failed_calls": 1
                    }
                
                if "args" not in func_call or not isinstance(func_call.get("args"), dict):
                    return {
                        "success": False,
                        "results": [],
                        "errors": [f"第{i+1}个函数调用的args字段必须是对象类型"],
                        "total_calls": len(funcs),
                        "successful_calls": 0,
                        "failed_calls": 1
                    }
            
            # 准备发送给Unity的参数，保持架构一致性
            params = {
                "args": funcs
            }
            
            # 使用带重试机制的命令发送到Unity的functions_call处理器
            result = bridge.send_command_with_retry("functions_call", params, max_retries=1)
            
            # Unity的functions_call处理器返回的结果已经是完整的格式，直接返回data部分
            if isinstance(result, dict) and "data" in result:
                return result["data"]
            else:
                # 如果返回格式不符合预期，包装成标准格式
                return {
                    "success": True,
                    "results": [result],
                    "errors": [None],
                    "total_calls": len(funcs),
                    "successful_calls": 1,
                    "failed_calls": 0
                }
            
        except Exception as e:
            return {
                "success": False,
                "results": [],
                "errors": [f"批量调用转发失败: {str(e)}"],
                "total_calls": len(funcs) if isinstance(funcs, list) else 0,
                "successful_calls": 0,
                "failed_calls": 1
            } 
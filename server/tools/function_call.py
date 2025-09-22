"""
通用函数调用工具，用于调用Unity中的任意函数。
"""
from typing import Dict, Any
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection
import json

def register_function_call_tools(mcp: FastMCP):
    """注册通用函数调用工具到MCP服务器。"""

    @mcp.tool()
    def function_call(
        ctx: Context,
        func: str,
        args: Dict[str, Any] = {}
    ) -> Dict[str, Any]:
        """通用函数调用工具，可以调用Unity中的任意函数。

        Args:
            ctx: MCP上下文。
            func: 要调用的函数名称，由用户输入或规则文件获取。
            args: 函数参数对象，由用户输入或规则文件获取。

        Returns:
            包含函数调用结果的字典。
        """
        
        try:
            # 验证函数名称
            if not func or not isinstance(func, str):
                return {
                    "success": False,
                    "error": "函数名称无效或为空",
                    "result": None
                }
            
            # 验证参数类型
            if not isinstance(args, dict):
                return {
                    "success": False,
                    "error": "参数必须是对象类型",
                    "result": None
                }
            
            # 获取Unity连接实例
            bridge = get_unity_connection()
            
            if bridge is None:
                return {
                    "success": False,
                    "error": "无法获取Unity连接",
                    "result": None
                }
            
            # 准备发送给Unity的参数
            params = {
                "func": func,
                "args": args  # 直接发送JSON字符串
            }
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("function_call", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "result": result,
                    "error": None
                }
                
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "error": f"参数序列化失败: {str(e)}",
                "result": None
            }
        except ConnectionError as e:
            return {
                "success": False,
                "error": f"Unity连接错误: {str(e)}",
                "result": None
            }
        except Exception as e:
            return {
                "success": False,
                "error": f"函数调用失败: {str(e)}",
                "result": None
            } 
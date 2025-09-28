"""
批量函数调用工具，包含单个和批量Unity函数调用功能。
"""
import json
from typing import List, Dict, Any
from mcp.server.fastmcp import FastMCP, Context
from mcp.types import ToolAnnotations
from unity_connection import get_unity_connection


def register_batch_tools(mcp: FastMCP):
    """注册批量函数调用工具到MCP服务器。"""

    @mcp.tool(
        name="batch.call",
        description="单个函数调用工具，用于调用Unity-MCP系统中的任意函数。支持调用所有已注册的Unity工具函数，如gameplay、hierarchy_create等。",
        annotations=ToolAnnotations(
            parameters={
                "func": {
                    "type": "string",
                    "description": "要调用的Unity函数名称",
                    "examples": ["gameplay", "hierarchy_create", "manage_editor", "project_search"]
                },
                "args": {
                    "type": "object",
                    "description": "传递给函数的参数字典，格式依据具体函数而定",
                    "examples": [
                        {"action": "screenshot", "format": "PNG"},
                        {"action": "create_object", "object_type": "Cube", "name": "MyCube"}
                    ]
                }
            }
        )
    )
    def batch_call(
        ctx: Context,
        func: str,
        args: Dict[str, Any] = {}
    ) -> Dict[str, Any]:
        """单个函数调用工具，可以调用Unity-MCP系统中的指定函数。

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
                "args": args
            }
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("batch", params, max_retries=2)
            
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
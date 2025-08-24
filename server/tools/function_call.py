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
        
        # 获取Unity连接实例
        bridge = get_unity_connection()
        
        # 准备发送给Unity的参数
        params = {
            "func": func,
            "args": json.dumps(args)  # 将对象序列化为JSON字符串发送给Unity
        }
        
        # 通过bridge发送命令
        return bridge.send_command("function_call", params) 
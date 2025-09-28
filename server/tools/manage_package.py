"""
Unity包管理工具，包含包安装、卸载、搜索、列表等功能。
"""
import json
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_manage_package_tools(mcp: FastMCP):
    @mcp.tool("manage_package")
    def manage_package(
        ctx: Context,
        action: Annotated[str, Field(
            title="包操作类型",
            description="要执行的包操作: add(添加包), remove(移除包), list(列出包), search(搜索包), get_info(获取包信息), update(更新包)",
            examples=["add", "remove", "list", "search", "get_info", "update"]
        )],
        package_name: Annotated[Optional[str], Field(
            title="包名称",
            description="要操作的包名称",
            default=None,
            examples=["com.unity.textmeshpro", "com.unity.cinemachine", "com.unity.inputsystem"]
        )] = None,
        version: Annotated[Optional[str], Field(
            title="包版本",
            description="要安装的包版本，留空安装最新版本",
            default=None,
            examples=["1.0.0", "latest", "1.2.3-preview.1"]
        )] = None,
        search_query: Annotated[Optional[str], Field(
            title="搜索查询",
            description="搜索包时使用的查询字符串",
            default=None,
            examples=["text", "input", "cinemachine", "ui"]
        )] = None,
        include_prerelease: Annotated[bool, Field(
            title="包含预发布版本",
            description="是否在搜索结果中包含预发布版本",
            default=False
        )] = False,
        timeout: Annotated[Optional[int], Field(
            title="超时时间",
            description="操作超时时间（秒）",
            default=60,
            ge=10,
            le=300
        )] = 60
    ) -> Dict[str, Any]:
        """Unity包管理工具，用于管理Unity Package Manager中的包。

        支持多种包管理操作，适用于：
        - 包安装：添加新的包到项目中
        - 包管理：移除不需要的包
        - 包搜索：查找可用的包
        - 包信息：获取包的详细信息
        - 包更新：更新包到最新版本

        Returns:
            包含包操作结果的字典：
            {
                "success": bool,        # 操作是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 包相关数据（如包列表、包信息）
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证操作类型
            valid_actions = ["add", "remove", "list", "search", "get_info", "update"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证包名称参数
            if action in ["add", "remove", "get_info", "update"]:
                if not package_name:
                    return {
                        "success": False,
                        "error": f"操作'{action}'需要提供package_name参数",
                        "data": None
                    }
            
            # 验证搜索参数
            if action == "search":
                if not search_query:
                    return {
                        "success": False,
                        "error": "search操作需要提供search_query参数",
                        "data": None
                    }
            
            # 验证超时参数
            if timeout and (timeout < 10 or timeout > 300):
                return {
                    "success": False,
                    "error": "超时时间必须在10-300秒之间",
                    "data": None
                }
            
            # 获取Unity连接实例
            bridge = get_unity_connection()
            
            if bridge is None:
                return {
                    "success": False,
                    "error": "无法获取Unity连接",
                    "data": None
                }
            
            # 准备发送给Unity的参数
            params = {"action": action}
            
            # 添加包名称参数
            if package_name:
                params["package_name"] = package_name
            
            # 添加版本参数
            if version:
                params["version"] = version
            
            # 添加搜索参数
            if search_query:
                params["search_query"] = search_query
            
            # 添加选项参数
            params["include_prerelease"] = include_prerelease
            params["timeout"] = timeout
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("manage_package", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"包管理操作'{action}'执行完成",
                    "data": result,
                    "error": None
                }
                
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "error": f"参数序列化失败: {str(e)}",
                "data": None
            }
        except ConnectionError as e:
            return {
                "success": False,
                "error": f"Unity连接错误: {str(e)}",
                "data": None
            }
        except Exception as e:
            return {
                "success": False,
                "error": f"包管理操作失败: {str(e)}",
                "data": None
            }

"""
Unity控制台操作工具，包含控制台读取和写入功能。
"""
import json
from typing import Annotated, List, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_console_tools(mcp: FastMCP):
    @mcp.tool("console_read")
    def console_read(
        ctx: Context,
        action: Annotated[str, Field(
            title="操作类型",
            description="控制台操作类型: get(获取日志-无堆栈跟踪), get_full(获取日志-包含堆栈跟踪), clear(清空控制台)",
            examples=["get", "get_full", "clear"]
        )],
        types: Annotated[Optional[List[str]], Field(
            title="消息类型列表",
            description="要获取的消息类型，可选择多种类型组合",
            default=None,
            examples=[
                ["error", "warning", "log"],
                ["error"],
                ["warning", "log"]
            ]
        )] = None,
        count: Annotated[Optional[int], Field(
            title="最大消息数",
            description="限制返回的消息数量，不设置则获取全部消息",
            default=None,
            ge=1,
            examples=[10, 50, 100]
        )] = None,
        filterText: Annotated[Optional[str], Field(
            title="文本过滤器",
            description="过滤包含指定文本的日志消息，支持模糊匹配",
            default=None,
            examples=["Error", "NullReference", "GameObject"]
        )] = None,
        format: Annotated[str, Field(
            title="输出格式",
            description="控制台输出格式类型",
            default="detailed",
            examples=["plain", "detailed", "json"]
        )] = "detailed"
    ) -> Dict[str, Any]:
        """Unity控制台读取工具，可以读取或清空Unity编辑器控制台消息。

        支持多种操作模式和灵活的过滤选项，适用于：
        - 调试信息收集：获取错误和警告消息
        - 日志分析：过滤特定内容的消息
        - 控制台管理：清空控制台历史记录

        Returns:
            包含控制台操作结果的字典：
            {
                "success": bool,        # 操作是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 控制台消息数据（当action为get/get_full时）
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证操作类型
            if action not in ["get", "get_full", "clear"]:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: get, get_full, clear",
                    "data": None
                }
            
            # 验证消息类型
            if types is None:
                types = ["error", "warning", "log"]
            else:
                valid_types = ["error", "warning", "log"]
                invalid_types = [t for t in types if t not in valid_types]
                if invalid_types:
                    return {
                        "success": False,
                        "error": f"无效的消息类型: {invalid_types}。支持的类型: {valid_types}",
                        "data": None
                    }
            
            # 验证输出格式
            if format not in ["plain", "detailed", "json"]:
                return {
                    "success": False,
                    "error": f"无效的输出格式: '{format}'。支持的格式: plain, detailed, json",
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
            params = {
                "action": action,
                "types": types,
                "format": format
            }
            
            # 添加可选参数
            if count is not None:
                params["count"] = count
            if filterText is not None:
                params["filterText"] = filterText
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("console_read", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": "控制台读取操作完成",
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
                "error": f"控制台读取失败: {str(e)}",
                "data": None
            }

    @mcp.tool("console_write")
    def console_write(
        ctx: Context,
        action: Annotated[str, Field(
            title="日志类型",
            description="要写入的日志消息类型，不同类型在Unity控制台中有不同的显示效果和颜色",
            examples=["error", "warning", "log", "assert", "exception"]
        )],
        message: Annotated[str, Field(
            title="日志消息",
            description="要写入到Unity控制台的具体消息内容",
            examples=[
                "GameObject not found",
                "Player health is low",
                "Loading scene completed",
                "NullReferenceException occurred"
            ]
        )],
        tag: Annotated[Optional[str], Field(
            title="日志标签",
            description="用于分类和过滤日志的标签，便于在控制台中筛选相关消息",
            default=None,
            examples=["Player", "GameManager", "UI", "Network"]
        )] = None,
        context: Annotated[Optional[str], Field(
            title="上下文对象",
            description="相关的GameObject名称，点击日志时可在Hierarchy中高亮显示该对象",
            default=None,
            examples=["Player", "MainCamera", "Canvas", "GameManager"]
        )] = None,
        condition: Annotated[Optional[str], Field(
            title="断言条件",
            description="断言条件表达式，仅在action为'assert'时使用，用于描述断言失败的条件",
            default=None,
            examples=["health > 0", "player != null", "lives >= 0"]
        )] = None
    ) -> Dict[str, Any]:
        """Unity控制台写入工具，可以向Unity编辑器控制台写入不同类型的日志消息。

        支持多种日志级别和丰富的标签功能，适用于：
        - 调试输出：记录程序运行状态和变量值
        - 错误报告：输出异常和错误信息
        - 性能监控：记录关键操作的时间和状态
        - 用户反馈：显示游戏状态和提示信息

        Returns:
            包含写入操作结果的字典：
            {
                "success": bool,        # 写入是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 写入操作的相关数据
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证操作类型
            if action not in ["error", "warning", "log", "assert", "exception"]:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: error, warning, log, assert, exception",
                    "data": None
                }
            
            # 验证消息内容
            if not message or not isinstance(message, str):
                return {
                    "success": False,
                    "error": "消息内容不能为空且必须是字符串类型",
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
            params = {
                "action": action,
                "message": message
            }
            
            # 添加可选参数
            if tag is not None:
                params["tag"] = tag
            if context is not None:
                params["context"] = context
            if condition is not None:
                params["condition"] = condition
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("console_write", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"日志写入操作完成: [{action.upper()}] {message}",
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
                "error": f"控制台写入失败: {str(e)}",
                "data": None
            }

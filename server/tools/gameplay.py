"""
Unity游戏玩法控制工具，包含游戏播放控制、截图、游戏状态等功能。
"""
import json
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_gameplay_tools(mcp: FastMCP):
    @mcp.tool("gameplay")
    def gameplay(
        ctx: Context,
        action: Annotated[str, Field(
            title="游戏操作类型",
            description="要执行的游戏操作: play(播放), pause(暂停), stop(停止), screenshot(截图), get_status(获取状态)",
            examples=["play", "pause", "stop", "screenshot", "get_status"]
        )],
        format: Annotated[Optional[str], Field(
            title="截图格式",
            description="截图时使用的图像格式，仅在action为screenshot时有效",
            default=None,
            examples=["PNG", "JPG", "JPEG"]
        )] = None,
        width: Annotated[Optional[int], Field(
            title="截图宽度",
            description="截图的宽度像素数，仅在action为screenshot时有效",
            default=None,
            ge=1,
            examples=[1920, 1280, 1024]
        )] = None,
        height: Annotated[Optional[int], Field(
            title="截图高度",
            description="截图的高度像素数，仅在action为screenshot时有效",
            default=None,
            ge=1,
            examples=[1080, 720, 768]
        )] = None,
        path: Annotated[Optional[str], Field(
            title="保存路径",
            description="截图保存的文件路径，仅在action为screenshot时有效",
            default=None,
            examples=["Screenshots/game.png", "Assets/Images/capture.jpg"]
        )] = None
    ) -> Dict[str, Any]:
        """Unity游戏玩法控制工具，用于控制游戏的播放状态和截图等操作。

        支持多种游戏控制操作，适用于：
        - 自动化测试：控制游戏播放进行测试
        - 内容创作：截图记录游戏画面
        - 开发调试：控制游戏状态进行调试
        - 演示录制：控制游戏流程进行演示

        Returns:
            包含游戏操作结果的字典：
            {
                "success": bool,        # 操作是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 操作相关数据（如截图路径）
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证操作类型
            valid_actions = ["play", "pause", "stop", "screenshot", "get_status"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证截图参数
            if action == "screenshot":
                if format and format.upper() not in ["PNG", "JPG", "JPEG"]:
                    return {
                        "success": False,
                        "error": f"无效的图像格式: '{format}'。支持的格式: PNG, JPG, JPEG",
                        "data": None
                    }
                
                if width is not None and width <= 0:
                    return {
                        "success": False,
                        "error": "截图宽度必须大于0",
                        "data": None
                    }
                
                if height is not None and height <= 0:
                    return {
                        "success": False,
                        "error": "截图高度必须大于0",
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
            
            # 添加截图相关参数
            if action == "screenshot":
                if format:
                    params["format"] = format
                if width is not None:
                    params["width"] = width
                if height is not None:
                    params["height"] = height
                if path:
                    params["path"] = path
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("gameplay", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"游戏操作'{action}'执行完成",
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
                "error": f"游戏操作失败: {str(e)}",
                "data": None
            }

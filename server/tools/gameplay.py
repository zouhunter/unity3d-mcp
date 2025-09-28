"""
Unity游戏玩法控制工具，包含游戏播放控制、截图、游戏状态等功能。
"""
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


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
        """Unity游戏玩法控制工具，用于控制游戏的播放状态和截图等操作。（二级工具）

        支持多种游戏控制操作，适用于：
        - 自动化测试：控制游戏播放进行测试
        - 内容创作：截图记录游戏画面
        - 开发调试：控制游戏状态进行调试
        - 演示录制：控制游戏流程进行演示
        """
        return get_common_call_response("gameplay")

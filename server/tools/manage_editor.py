"""
Unity编辑器管理工具，包含编辑器状态控制、场景管理、设置配置等功能。
"""
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_manage_editor_tools(mcp: FastMCP):
    @mcp.tool("manage_editor")
    def manage_editor(
        ctx: Context,
        action: Annotated[str, Field(
            title="编辑器操作类型",
            description="要执行的编辑器操作: play(播放模式), pause(暂停), stop(停止), save_scene(保存场景), load_scene(加载场景), get_state(获取状态), set_setting(设置配置)",
            examples=["play", "pause", "stop", "save_scene", "load_scene", "get_state"]
        )],
        scene_path: Annotated[Optional[str], Field(
            title="场景路径",
            description="场景文件的路径，仅在load_scene和save_scene操作时使用",
            default=None,
            examples=["Assets/Scenes/MainScene.unity", "Scenes/Level1.unity"]
        )] = None,
        setting_key: Annotated[Optional[str], Field(
            title="设置键名",
            description="要设置的配置项键名，仅在set_setting操作时使用",
            default=None,
            examples=["Quality", "Resolution", "VSyncCount"]
        )] = None,
        setting_value: Annotated[Optional[str], Field(
            title="设置值",
            description="要设置的配置项值，仅在set_setting操作时使用",
            default=None,
            examples=["High", "1920x1080", "1"]
        )] = None,
        force: Annotated[Optional[bool], Field(
            title="强制执行",
            description="是否强制执行操作，忽略某些检查和警告",
            default=False
        )] = False
    ) -> Dict[str, Any]:
        """Unity编辑器管理工具，用于控制编辑器的各种状态和操作。（二级工具）

        支持多种编辑器管理功能，适用于：
        - 自动化测试：控制编辑器播放模式
        - 项目管理：场景的保存和加载
        - 开发流程：编辑器状态查询和配置
        - CI/CD集成：自动化构建和测试
        """
        return get_common_call_response("manage_editor")

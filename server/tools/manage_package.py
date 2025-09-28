"""
Unity包管理工具，包含包安装、卸载、搜索、列表等功能。
"""
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


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
        """Unity包管理工具，用于管理Unity Package Manager中的包。（二级工具）
        支持多种包管理操作，适用于：
        - 包安装：添加新的包到项目中
        - 包管理：移除不需要的包
        - 包搜索：查找可用的包
        - 包信息：获取包的详细信息
        - 包更新：更新包到最新版本
        """
        return get_common_call_response("manage_package")

"""
Unity Figma管理工具，包含Figma图片下载、节点数据拉取等功能。
"""
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_figma_manage_tools(mcp: FastMCP):
    @mcp.tool("figma_manage")
    def figma_manage(
        ctx: Context,
        action: Annotated[str, Field(
            title="操作类型",
            description="要执行的Figma操作: download_image(下载单张图片), fetch_nodes(拉取节点数据), download_images(批量下载图片)",
            examples=["download_image", "fetch_nodes", "download_images"]
        )],
        file_key: Annotated[Optional[str], Field(
            title="文件密钥",
            description="Figma文件的密钥",
            default=None,
            examples=["abc123def456", "xyz789uvw012"]
        )] = None,
        node_id: Annotated[Optional[str], Field(
            title="节点ID",
            description="要下载的节点ID（用于download_image操作）",
            default=None,
            examples=["1:4", "1:5", "1:6"]
        )] = None,
        nodes: Annotated[Optional[str], Field(
            title="节点列表",
            description="节点列表，支持逗号分隔的节点ID字符串或JSON格式的节点名称映射",
            default=None,
            examples=["1:4,1:5,1:6", '{"1:4":"image1","1:5":"image2","1:6":"image3"}']
        )] = None,
        save_path: Annotated[Optional[str], Field(
            title="保存路径",
            description="图片保存路径（相对于Assets或绝对路径）",
            default=None,
            examples=["Assets/Images/Figma", "D:/Downloads/Figma"]
        )] = None,
        format: Annotated[Optional[str], Field(
            title="图片格式",
            description="图片格式",
            default="PNG",
            examples=["PNG", "JPG", "SVG"]
        )] = "PNG",
        scale: Annotated[Optional[float], Field(
            title="缩放比例",
            description="图片缩放比例",
            default=1.0,
            ge=0.1,
            le=4.0
        )] = 1.0,
        local_json_path: Annotated[Optional[str], Field(
            title="本地JSON路径",
            description="本地JSON文件路径（用于download_images操作）",
            default=None,
            examples=["Assets/Data/figma_nodes.json", "D:/Data/nodes.json"]
        )] = None,
        auto_convert_sprite: Annotated[Optional[bool], Field(
            title="自动转换Sprite",
            description="是否自动将下载的图片转换为Sprite格式",
            default=True
        )] = True,
        include_children: Annotated[Optional[bool], Field(
            title="包含子节点",
            description="是否包含子节点数据",
            default=False
        )] = False,
        depth: Annotated[Optional[int], Field(
            title="深度",
            description="节点数据拉取的深度",
            default=1,
            ge=1,
            le=10
        )] = 1
    ) -> Dict[str, Any]:
        """Unity Figma管理工具，用于管理Figma资源和数据。（二级工具）

        支持多种Figma操作，适用于：
        - 图片下载：从Figma下载单张或批量图片
        - 节点数据：拉取Figma文件的节点结构数据
        - 资源管理：自动转换和管理下载的资源
        - 批量处理：高效处理多个节点
        """
        return get_common_call_response("figma_manage")

"""
Unity Figma管理工具，包含Figma图片下载、节点数据拉取等功能。
"""
import json
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


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
        """Unity Figma管理工具，用于管理Figma资源和数据。

        支持多种Figma操作，适用于：
        - 图片下载：从Figma下载单张或批量图片
        - 节点数据：拉取Figma文件的节点结构数据
        - 资源管理：自动转换和管理下载的资源
        - 批量处理：高效处理多个节点

        Returns:
            包含Figma操作结果的字典：
            {
                "success": bool,        # 操作是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # Figma相关数据（如下载路径、节点数据）
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证操作类型
            valid_actions = ["download_image", "fetch_nodes", "download_images"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证文件密钥参数
            if not file_key:
                return {
                    "success": False,
                    "error": "必须提供file_key参数",
                    "data": None
                }
            
            # 验证下载图片参数
            if action == "download_image":
                if not node_id:
                    return {
                        "success": False,
                        "error": "download_image操作需要提供node_id参数",
                        "data": None
                    }
            
            # 验证批量下载参数
            if action == "download_images":
                if not nodes:
                    return {
                        "success": False,
                        "error": "download_images操作需要提供nodes参数",
                        "data": None
                    }
            
            # 验证保存路径参数
            if action in ["download_image", "download_images"]:
                if not save_path:
                    return {
                        "success": False,
                        "error": f"操作'{action}'需要提供save_path参数",
                        "data": None
                    }
            
            # 验证图片格式
            valid_formats = ["PNG", "JPG", "JPEG", "SVG"]
            if format.upper() not in valid_formats:
                return {
                    "success": False,
                    "error": f"无效的图片格式: '{format}'。支持的格式: {valid_formats}",
                    "data": None
                }
            
            # 验证缩放比例
            if scale and (scale < 0.1 or scale > 4.0):
                return {
                    "success": False,
                    "error": "缩放比例必须在0.1-4.0之间",
                    "data": None
                }
            
            # 验证深度参数
            if depth and (depth < 1 or depth > 10):
                return {
                    "success": False,
                    "error": "深度必须在1-10之间",
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
                "file_key": file_key
            }
            
            # 添加节点参数
            if node_id:
                params["node_id"] = node_id
            if nodes:
                params["nodes"] = nodes
            
            # 添加保存路径参数
            if save_path:
                params["save_path"] = save_path
            
            # 添加图片配置参数
            if format:
                params["format"] = format.upper()
            if scale:
                params["scale"] = scale
            
            # 添加其他参数
            if local_json_path:
                params["local_json_path"] = local_json_path
            if auto_convert_sprite:
                params["auto_convert_sprite"] = auto_convert_sprite
            if include_children:
                params["include_children"] = include_children
            if depth:
                params["depth"] = depth
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("figma_manage", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"Figma操作'{action}'执行完成",
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
                "error": f"Figma管理失败: {str(e)}",
                "data": None
            }

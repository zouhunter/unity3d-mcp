"""
纹理导入设置修改工具
修改Unity中纹理资源的导入设置，包括设置为Sprite类型、调整压缩质量等
"""

from typing import Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_edit_texture_tools(mcp: FastMCP):
    @mcp.tool("edit_texture")
    def edit_texture(
        ctx: Context,
        action: str = Field(
            ...,
            title="操作类型",
            description="操作类型",
            examples=["set_type", "set_sprite_settings", "get_settings"]
        ),
        texture_path: str = Field(
            ...,
            title="纹理资源路径",
            description="纹理资源路径（相对于Assets）",
            examples=["Assets/Pics/rabbit.jpg", "Assets/Textures/icon.png", "Assets/UI/button.png"]
        ),
        texture_type: Optional[str] = Field(
            None,
            title="纹理类型",
            description="纹理类型",
            examples=["Default", "NormalMap", "Sprite", "Cursor", "Cookie", "Lightmap", "HDR"]
        ),
        sprite_mode: Optional[str] = Field(
            None,
            title="Sprite模式",
            description="Sprite模式",
            examples=["Single", "Multiple", "Polygon"]
        ),
        pixels_per_unit: Optional[float] = Field(
            None,
            title="每单位像素数",
            description="每单位像素数",
            examples=[100, 200, 1]
        ),
        sprite_pivot: Optional[str] = Field(
            None,
            title="Sprite轴心点",
            description="Sprite轴心点",
            examples=["Center", "TopLeft", "TopCenter", "TopRight", "MiddleLeft", "MiddleCenter", "MiddleRight", "BottomLeft", "BottomCenter", "BottomRight"]
        ),
        generate_physics_shape: Optional[bool] = Field(
            None,
            title="生成物理形状",
            description="生成物理形状"
        ),
        mesh_type: Optional[str] = Field(
            None,
            title="网格类型",
            description="网格类型"
        ),
        compression: Optional[str] = Field(
            None,
            title="压缩格式",
            description="压缩格式",
            examples=["HighQuality", "NormalQuality", "LowQuality"]
        ),
        max_texture_size: Optional[int] = Field(
            None,
            title="最大纹理尺寸",
            description="最大纹理尺寸",
            examples=[1024, 2048, 4096]
        ),
        filter_mode: Optional[str] = Field(
            None,
            title="过滤模式",
            description="过滤模式",
            examples=["Point", "Bilinear", "Trilinear"]
        ),
        wrap_mode: Optional[str] = Field(
            None,
            title="包装模式",
            description="包装模式",
            examples=["Repeat", "Clamp", "Mirror", "MirrorOnce"]
        ),
        readable: Optional[bool] = Field(
            None,
            title="可读写",
            description="可读写"
        ),
        generate_mip_maps: Optional[bool] = Field(
            None,
            title="生成Mip贴图",
            description="生成Mip贴图"
        ),
        srgb_texture: Optional[bool] = Field(
            None,
            title="sRGB纹理",
            description="sRGB纹理"
        )
    ) -> Dict[str, Any]:
        """
        纹理导入设置修改工具
        
        支持的操作:
        - set_type: 设置纹理类型
        - set_sprite_settings: 设置Sprite详细参数
        - get_settings: 获取当前纹理设置
        """
        try:
            unity_conn = get_unity_connection()
            
            # 构建命令参数
            cmd = {
                "action": action,
                "texture_path": texture_path
            }
            
            # 添加可选参数
            optional_params = {
                "texture_type": texture_type,
                "sprite_mode": sprite_mode,
                "pixels_per_unit": pixels_per_unit,
                "sprite_pivot": sprite_pivot,
                "generate_physics_shape": generate_physics_shape,
                "mesh_type": mesh_type,
                "compression": compression,
                "max_texture_size": max_texture_size,
                "filter_mode": filter_mode,
                "wrap_mode": wrap_mode,
                "readable": readable,
                "generate_mip_maps": generate_mip_maps,
                "srgb_texture": srgb_texture
            }
            
            for key, value in optional_params.items():
                if value is not None:
                    cmd[key] = value
            
            # 发送命令到Unity
            result = unity_conn.send_command_with_retry("edit_texture", cmd)
            
            return {
                "success": True,
                "message": f"Texture operation '{action}' completed successfully",
                "data": result.get("data", {}),
                "error": None
            }
            
        except Exception as e:
            return {
                "success": False,
                "message": f"Texture operation '{action}' failed",
                "data": {},
                "error": str(e)
            }

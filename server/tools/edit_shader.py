"""
Shader管理工具
管理Unity中的Shader资源，包括创建、修改、删除和获取信息
"""

from typing import Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_edit_shader_tools(mcp: FastMCP):
    @mcp.tool("edit_shader")
    def edit_shader(
        ctx: Context,
        action: str = Field(
            ...,
            title="操作类型",
            description="操作类型",
            examples=["create", "modify", "delete", "get_info", "search", "duplicate", "move", "rename", "compile", "validate"]
        ),
        path: str = Field(
            ...,
            title="Shader路径",
            description="Shader路径，Unity标准格式：Assets/Shaders/MyShader.shader",
            examples=["Assets/Shaders/MyShader.shader", "Assets/Materials/Shaders/CustomShader.shader"]
        ),
        shader_name: Optional[str] = Field(
            None,
            title="Shader名称",
            description="Shader名称",
            examples=["Custom/MyShader", "Unlit/MyShader", "Standard/MyShader"]
        ),
        shader_type: Optional[str] = Field(
            None,
            title="Shader类型",
            description="Shader类型",
            examples=["Unlit", "Standard", "Custom", "UI", "Sprite", "Particle"]
        ),
        shader_code: Optional[str] = Field(
            None,
            title="Shader代码",
            description="Shader代码内容"
        ),
        properties: Optional[Dict[str, Any]] = Field(
            None,
            title="属性字典",
            description="属性字典，用于设置Shader的属性",
            examples=[{"_MainTex": "white", "_Color": [1, 0, 0, 1], "_Metallic": 0.5}]
        ),
        destination: Optional[str] = Field(
            None,
            title="目标路径",
            description="目标路径（移动/复制时使用）",
            examples=["Assets/Shaders/MyShaderCopy.shader", "Assets/Backup/CustomShader.shader"]
        ),
        query: Optional[str] = Field(
            None,
            title="搜索模式",
            description="搜索模式，如*.shader",
            examples=["*.shader", "Custom*", "Unlit*"]
        ),
        recursive: Optional[bool] = Field(
            True,
            title="递归搜索",
            description="是否递归搜索子文件夹"
        ),
        force: Optional[bool] = Field(
            False,
            title="强制执行",
            description="是否强制执行操作（覆盖现有文件等）"
        ),
        create_folder: Optional[bool] = Field(
            True,
            title="创建文件夹",
            description="是否自动创建不存在的文件夹"
        ),
        backup: Optional[bool] = Field(
            True,
            title="备份",
            description="是否在修改前备份原文件"
        ),
        validate_syntax: Optional[bool] = Field(
            True,
            title="验证语法",
            description="是否验证Shader语法"
        ),
        compile_shader: Optional[bool] = Field(
            True,
            title="编译Shader",
            description="是否编译Shader"
        ),
        check_errors: Optional[bool] = Field(
            True,
            title="检查错误",
            description="是否检查编译错误"
        ),
        apply_immediately: Optional[bool] = Field(
            True,
            title="立即应用",
            description="是否立即应用更改"
        ),
        mark_dirty: Optional[bool] = Field(
            True,
            title="标记为脏",
            description="是否标记资源为已修改"
        ),
        save_assets: Optional[bool] = Field(
            True,
            title="保存资源",
            description="是否保存资源到磁盘"
        ),
        refresh_assets: Optional[bool] = Field(
            True,
            title="刷新资源",
            description="是否刷新资源数据库"
        ),
        include_variants: Optional[bool] = Field(
            False,
            title="包含变体",
            description="是否包含Shader变体"
        ),
        platform_specific: Optional[bool] = Field(
            False,
            title="平台特定",
            description="是否生成平台特定的Shader"
        ),
        optimization_level: Optional[str] = Field(
            None,
            title="优化级别",
            description="优化级别",
            examples=["None", "Low", "Medium", "High"]
        ),
        debug_mode: Optional[bool] = Field(
            False,
            title="调试模式",
            description="是否启用调试模式"
        )
    ) -> Dict[str, Any]:
        """
        Shader管理工具
        
        支持的操作:
        - create: 创建Shader
        - modify: 修改Shader
        - delete: 删除Shader
        - get_info: 获取Shader信息
        - search: 搜索Shader
        - duplicate: 复制Shader
        - move: 移动/重命名Shader
        - rename: 移动/重命名Shader（与move相同）
        - compile: 编译Shader
        - validate: 验证Shader
        """
        try:
            unity_conn = get_unity_connection()
            
            # 构建命令参数
            cmd = {
                "action": action,
                "path": path
            }
            
            # 添加可选参数
            optional_params = {
                "shader_name": shader_name,
                "shader_type": shader_type,
                "shader_code": shader_code,
                "properties": properties,
                "destination": destination,
                "query": query,
                "recursive": recursive,
                "force": force,
                "create_folder": create_folder,
                "backup": backup,
                "validate_syntax": validate_syntax,
                "compile_shader": compile_shader,
                "check_errors": check_errors,
                "apply_immediately": apply_immediately,
                "mark_dirty": mark_dirty,
                "save_assets": save_assets,
                "refresh_assets": refresh_assets,
                "include_variants": include_variants,
                "platform_specific": platform_specific,
                "optimization_level": optimization_level,
                "debug_mode": debug_mode
            }
            
            for key, value in optional_params.items():
                if value is not None:
                    cmd[key] = value
            
            # 发送命令到Unity
            result = unity_conn.send_command_with_retry("edit_shader", cmd)
            
            return {
                "success": True,
                "message": f"Shader operation '{action}' completed successfully",
                "data": result.get("data", {}),
                "error": None
            }
            
        except Exception as e:
            return {
                "success": False,
                "message": f"Shader operation '{action}' failed",
                "data": {},
                "error": str(e)
            }

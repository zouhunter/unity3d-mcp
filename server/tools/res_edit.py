"""
Unity资源编辑工具，包含材质、纹理、模型、音频等资源的编辑和处理功能。
"""
import json
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_res_edit_tools(mcp: FastMCP):
    @mcp.tool("edit_material")
    def edit_material(
        ctx: Context,
        action: Annotated[str, Field(
            title="材质操作类型",
            description="要执行的材质操作: create(创建), modify(修改属性), duplicate(复制), delete(删除), apply_texture(应用纹理)",
            examples=["create", "modify", "duplicate", "delete", "apply_texture"]
        )],
        material_path: Annotated[str, Field(
            title="材质路径",
            description="材质文件的Assets路径",
            examples=["Assets/Materials/PlayerMaterial.mat", "Materials/Ground.mat"]
        )],
        shader_name: Annotated[Optional[str], Field(
            title="着色器名称",
            description="要使用的着色器名称，仅在create和modify操作时使用",
            default=None,
            examples=["Standard", "Universal Render Pipeline/Lit", "Unlit/Color"]
        )] = None,
        properties: Annotated[Optional[Dict[str, Any]], Field(
            title="材质属性",
            description="要设置的材质属性键值对，仅在create和modify操作时使用",
            default=None,
            examples=[
                {"_Color": [1.0, 0.0, 0.0, 1.0]},
                {"_Metallic": 0.5, "_Smoothness": 0.8},
                {"_MainTex": "Assets/Textures/diffuse.png"}
            ]
        )] = None,
        texture_path: Annotated[Optional[str], Field(
            title="纹理路径",
            description="要应用的纹理文件路径，仅在apply_texture操作时使用",
            default=None,
            examples=["Assets/Textures/brick.png", "Textures/wood_diffuse.jpg"]
        )] = None,
        texture_slot: Annotated[Optional[str], Field(
            title="纹理插槽",
            description="要应用纹理的插槽名称，仅在apply_texture操作时使用",
            default="_MainTex",
            examples=["_MainTex", "_BumpMap", "_MetallicGlossMap"]
        )] = "_MainTex"
    ) -> Dict[str, Any]:
        """Unity材质编辑工具，用于创建、修改和管理材质资源。

        支持完整的材质编辑功能，适用于：
        - 材质创建：从零开始创建新材质
        - 属性调整：修改材质的各种属性参数
        - 纹理管理：应用和更换材质纹理
        - 批量处理：对多个材质进行统一操作

        Returns:
            包含材质操作结果的字典：
            {
                "success": bool,        # 操作是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 材质相关数据
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证操作类型
            valid_actions = ["create", "modify", "duplicate", "delete", "apply_texture"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证材质路径
            if not material_path or not isinstance(material_path, str):
                return {
                    "success": False,
                    "error": "材质路径不能为空且必须是字符串类型",
                    "data": None
                }
            
            if not material_path.startswith("Assets") or not material_path.endswith(".mat"):
                return {
                    "success": False,
                    "error": "材质路径必须以'Assets'开头且以'.mat'结尾",
                    "data": None
                }
            
            # 验证创建和修改操作参数
            if action in ["create", "modify"]:
                if action == "create" and not shader_name:
                    return {
                        "success": False,
                        "error": "创建材质时必须指定shader_name参数",
                        "data": None
                    }
            
            # 验证纹理应用参数
            if action == "apply_texture":
                if not texture_path:
                    return {
                        "success": False,
                        "error": "apply_texture操作需要提供texture_path参数",
                        "data": None
                    }
                
                if not texture_path.startswith("Assets"):
                    return {
                        "success": False,
                        "error": "纹理路径必须以'Assets'开头",
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
                "material_path": material_path
            }
            
            # 添加着色器参数
            if shader_name:
                params["shader_name"] = shader_name
            
            # 添加属性参数
            if properties:
                params["properties"] = properties
            
            # 添加纹理参数
            if texture_path:
                params["texture_path"] = texture_path
            if texture_slot:
                params["texture_slot"] = texture_slot
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("edit_material", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"材质操作'{action}'执行完成",
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
                "error": f"材质编辑失败: {str(e)}",
                "data": None
            }

    @mcp.tool("edit_mesh")
    def edit_mesh(
        ctx: Context,
        action: Annotated[str, Field(
            title="网格操作类型",
            description="要执行的网格操作: import(导入), export(导出), optimize(优化), generate_uv(生成UV), calculate_normals(计算法线)",
            examples=["import", "export", "optimize", "generate_uv", "calculate_normals"]
        )],
        mesh_path: Annotated[str, Field(
            title="网格文件路径",
            description="网格文件的路径，可以是Assets内路径或外部文件路径",
            examples=["Assets/Models/character.fbx", "D:/Models/building.obj", "Models/weapon.dae"]
        )],
        target_path: Annotated[Optional[str], Field(
            title="目标路径",
            description="导入或导出的目标路径",
            default=None,
            examples=["Assets/Models/imported_model.fbx", "D:/Exports/optimized_mesh.obj"]
        )] = None,
        import_settings: Annotated[Optional[Dict[str, Any]], Field(
            title="导入设置",
            description="网格导入时的设置参数",
            default=None,
            examples=[
                {"scale_factor": 1.0, "generate_colliders": True},
                {"import_materials": True, "optimize_mesh": True}
            ]
        )] = None,
        optimization_level: Annotated[Optional[str], Field(
            title="优化级别",
            description="网格优化的级别：low(低), medium(中), high(高)",
            default="medium",
            examples=["low", "medium", "high"]
        )] = "medium"
    ) -> Dict[str, Any]:
        """Unity网格编辑工具，用于导入、导出、优化和处理3D网格资源。

        支持多种网格处理功能，适用于：
        - 模型导入：从外部文件导入3D模型到项目中
        - 网格优化：减少面数和提升性能
        - UV生成：为模型自动生成UV坐标
        - 法线计算：重新计算模型的顶点法线

        Returns:
            包含网格操作结果的字典：
            {
                "success": bool,        # 操作是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 网格相关数据（如面数、顶点数）
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证操作类型
            valid_actions = ["import", "export", "optimize", "generate_uv", "calculate_normals"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证网格路径
            if not mesh_path or not isinstance(mesh_path, str):
                return {
                    "success": False,
                    "error": "网格文件路径不能为空且必须是字符串类型",
                    "data": None
                }
            
            # 验证导入导出操作参数
            if action in ["import", "export"]:
                if not target_path:
                    return {
                        "success": False,
                        "error": f"操作'{action}'需要提供target_path参数",
                        "data": None
                    }
            
            # 验证优化级别
            if optimization_level and optimization_level not in ["low", "medium", "high"]:
                return {
                    "success": False,
                    "error": f"无效的优化级别: '{optimization_level}'。支持的级别: low, medium, high",
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
                "mesh_path": mesh_path
            }
            
            # 添加目标路径
            if target_path:
                params["target_path"] = target_path
            
            # 添加导入设置
            if import_settings:
                params["import_settings"] = import_settings
            
            # 添加优化级别
            if optimization_level:
                params["optimization_level"] = optimization_level
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("edit_mesh", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"网格操作'{action}'执行完成",
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
                "error": f"网格编辑失败: {str(e)}",
                "data": None
            }

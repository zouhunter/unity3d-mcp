"""
Unity对象删除工具，包含GameObject、资源和其他Unity对象的删除功能。
"""
import json
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_object_delete_tools(mcp: FastMCP):
    @mcp.tool("object_delete")
    def object_delete(
        ctx: Context,
        path: Annotated[Optional[str], Field(
            title="对象路径",
            description="要删除的对象的层次结构路径",
            default=None,
            examples=["Player", "Canvas/UI/Button", "Assets/Materials/OldMaterial.mat"]
        )] = None,
        instance_id: Annotated[Optional[int], Field(
            title="实例ID",
            description="要删除的对象的实例ID",
            default=None,
            examples=[12345, 67890]
        )] = None,
        confirm: Annotated[Optional[bool], Field(
            title="强制确认",
            description="是否强制显示确认对话框：true=总是确认，false/unset=智能确认（≤3个对象自动删除，>3个显示对话框）",
            default=None
        )] = None
    ) -> Dict[str, Any]:
        """Unity对象删除工具，用于删除GameObject、资源和其他Unity对象。

        支持多种删除方式和智能确认机制，适用于：
        - 场景对象删除：删除场景中的GameObject
        - 资源删除：删除项目中的资源文件
        - 批量删除：删除多个对象
        - 安全删除：带确认机制的删除操作

        Returns:
            包含删除操作结果的字典：
            {
                "success": bool,        # 删除是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 删除操作相关数据
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证参数：必须提供path或instance_id之一
            if not path and instance_id is None:
                return {
                    "success": False,
                    "error": "必须提供path或instance_id参数之一",
                    "data": None
                }
            
            # 验证路径参数
            if path and not isinstance(path, str):
                return {
                    "success": False,
                    "error": "path参数必须是字符串类型",
                    "data": None
                }
            
            # 验证实例ID参数
            if instance_id is not None and not isinstance(instance_id, int):
                return {
                    "success": False,
                    "error": "instance_id参数必须是整数类型",
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
            params = {}
            
            # 添加路径参数
            if path:
                params["path"] = path
            
            # 添加实例ID参数
            if instance_id is not None:
                params["instance_id"] = instance_id
            
            # 添加确认参数
            if confirm is not None:
                params["confirm"] = confirm
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("object_delete", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"对象删除操作完成",
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
                "error": f"对象删除失败: {str(e)}",
                "data": None
            }

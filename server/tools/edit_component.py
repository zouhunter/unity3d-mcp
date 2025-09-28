"""
Unity组件编辑工具，包含组件的获取、设置属性等功能。
"""
import json
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_edit_component_tools(mcp: FastMCP):
    @mcp.tool("edit_component")
    def edit_component(
        ctx: Context,
        instance_id: Annotated[Optional[int], Field(
            title="实例ID",
            description="GameObject的实例ID",
            default=None,
            examples=[12345, 67890]
        )] = None,
        path: Annotated[Optional[str], Field(
            title="对象路径",
            description="GameObject的层次结构路径",
            default=None,
            examples=["Player", "Canvas/UI/Button", "Enemy_01"]
        )] = None,
        action: Annotated[Optional[str], Field(
            title="操作类型",
            description="操作类型: get_component_propertys(获取组件属性), set_component_propertys(设置组件属性)",
            default="get_component_propertys",
            examples=["get_component_propertys", "set_component_propertys"]
        )] = "get_component_propertys",
        component_type: Annotated[Optional[str], Field(
            title="组件类型",
            description="组件类型名称（继承自Component的类型名称）",
            default=None,
            examples=["Rigidbody", "BoxCollider", "AudioSource", "Light", "Transform"]
        )] = None,
        properties: Annotated[Optional[Dict[str, Any]], Field(
            title="属性字典",
            description="要设置的属性字典（用于set_component_propertys操作）",
            default=None,
            examples=[
                {"mass": 2.0, "drag": 0.5},
                {"volume": 0.8, "pitch": 1.2},
                {"color": [1.0, 0.0, 0.0, 1.0]}
            ]
        )] = None
    ) -> Dict[str, Any]:
        """Unity组件编辑工具，用于获取和设置GameObject组件的属性。

        支持多种组件操作，适用于：
        - 属性获取：获取组件的所有属性值
        - 属性设置：设置组件的特定属性值
        - 组件查询：查找特定类型的组件
        - 批量操作：同时设置多个属性

        Returns:
            包含组件操作结果的字典：
            {
                "success": bool,        # 操作是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 组件相关数据（如属性值）
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证目标参数：必须提供path或instance_id之一
            if not path and instance_id is None:
                return {
                    "success": False,
                    "error": "必须提供path或instance_id参数之一",
                    "data": None
                }
            
            # 验证操作类型
            valid_actions = ["get_component_propertys", "set_component_propertys"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证组件类型参数
            if not component_type:
                return {
                    "success": False,
                    "error": "必须提供component_type参数",
                    "data": None
                }
            
            # 验证设置属性参数
            if action == "set_component_propertys":
                if not properties:
                    return {
                        "success": False,
                        "error": "set_component_propertys操作需要提供properties参数",
                        "data": None
                    }
                
                if not isinstance(properties, dict):
                    return {
                        "success": False,
                        "error": "properties参数必须是字典类型",
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
            params = {
                "action": action,
                "component_type": component_type
            }
            
            # 添加目标参数
            if path:
                params["path"] = path
            if instance_id is not None:
                params["instance_id"] = instance_id
            
            # 添加属性参数
            if properties:
                params["properties"] = properties
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("edit_component", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"组件操作'{action}'执行完成",
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
                "error": f"组件编辑失败: {str(e)}",
                "data": None
            }

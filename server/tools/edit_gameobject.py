"""
Unity GameObject编辑工具，包含GameObject的创建、修改、组件管理等功能。
"""
import json
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_edit_gameobject_tools(mcp: FastMCP):
    @mcp.tool("edit_gameobject")
    def edit_gameobject(
        ctx: Context,
        action: Annotated[str, Field(
            title="操作类型",
            description="操作类型: create(创建), modify(修改), get_components(获取组件), add_component(添加组件), remove_component(移除组件), set_parent(设置父对象)",
            examples=["create", "modify", "get_components", "add_component", "remove_component", "set_parent"]
        )],
        path: Annotated[Optional[str], Field(
            title="对象路径",
            description="GameObject的层次结构路径",
            default=None,
            examples=["Player", "Canvas/UI/Button", "Enemy_01"]
        )] = None,
        instance_id: Annotated[Optional[int], Field(
            title="实例ID",
            description="GameObject的实例ID",
            default=None,
            examples=[12345, 67890]
        )] = None,
        name: Annotated[Optional[str], Field(
            title="GameObject名称",
            description="GameObject的名称",
            default=None,
            examples=["NewObject", "Player", "Enemy"]
        )] = None,
        tag: Annotated[Optional[str], Field(
            title="标签",
            description="GameObject的标签",
            default=None,
            examples=["Player", "Enemy", "Untagged"]
        )] = None,
        layer: Annotated[Optional[int], Field(
            title="图层",
            description="GameObject的图层",
            default=None,
            examples=[0, 8, 10]
        )] = None,
        parent_id: Annotated[Optional[int], Field(
            title="父对象ID",
            description="父对象的实例ID",
            default=None,
            examples=[12345, 67890]
        )] = None,
        parent_path: Annotated[Optional[str], Field(
            title="父对象路径",
            description="父对象的层次结构路径",
            default=None,
            examples=["Canvas", "Player", "Environment"]
        )] = None,
        position: Annotated[Optional[List[float]], Field(
            title="位置",
            description="GameObject的位置坐标 [x, y, z]",
            default=None,
            examples=[[0, 0, 0], [1.5, 2.0, -3.0]]
        )] = None,
        rotation: Annotated[Optional[List[float]], Field(
            title="旋转",
            description="GameObject的旋转角度 [x, y, z]",
            default=None,
            examples=[[0, 0, 0], [0, 90, 0]]
        )] = None,
        scale: Annotated[Optional[List[float]], Field(
            title="缩放",
            description="GameObject的缩放比例 [x, y, z]",
            default=None,
            examples=[[1, 1, 1], [2.0, 2.0, 2.0]]
        )] = None,
        component_type: Annotated[Optional[str], Field(
            title="组件类型",
            description="要添加或移除的组件类型名称",
            default=None,
            examples=["Rigidbody", "BoxCollider", "AudioSource", "Light"]
        )] = None,
        active: Annotated[Optional[bool], Field(
            title="激活状态",
            description="GameObject的激活状态",
            default=None
        )] = None,
        static_flags: Annotated[Optional[int], Field(
            title="静态标志",
            description="GameObject的静态标志",
            default=None,
            examples=[0, 1, 2, 4]
        )] = None
    ) -> Dict[str, Any]:
        """Unity GameObject编辑工具，用于创建、修改和管理GameObject。

        支持多种GameObject操作，适用于：
        - 对象创建：创建新的GameObject
        - 属性修改：修改GameObject的基本属性
        - 组件管理：添加、移除和获取组件
        - 层次结构：设置父子关系
        - 变换操作：设置位置、旋转、缩放

        Returns:
            包含GameObject操作结果的字典：
            {
                "success": bool,        # 操作是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # GameObject相关数据
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证操作类型
            valid_actions = ["create", "modify", "get_components", "add_component", "remove_component", "set_parent"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证目标参数：必须提供path或instance_id之一（除了create操作）
            if action != "create":
                if not path and instance_id is None:
                    return {
                        "success": False,
                        "error": "必须提供path或instance_id参数之一",
                        "data": None
                    }
            
            # 验证创建操作参数
            if action == "create":
                if not name:
                    return {
                        "success": False,
                        "error": "create操作需要提供name参数",
                        "data": None
                    }
            
            # 验证组件操作参数
            if action in ["add_component", "remove_component"]:
                if not component_type:
                    return {
                        "success": False,
                        "error": f"操作'{action}'需要提供component_type参数",
                        "data": None
                    }
            
            # 验证父对象设置参数
            if action == "set_parent":
                if not parent_id and not parent_path:
                    return {
                        "success": False,
                        "error": "set_parent操作需要提供parent_id或parent_path参数",
                        "data": None
                    }
            
            # 验证位置参数
            if position and len(position) != 3:
                return {
                    "success": False,
                    "error": "position参数必须是包含3个元素的数组 [x, y, z]",
                    "data": None
                }
            
            # 验证旋转参数
            if rotation and len(rotation) != 3:
                return {
                    "success": False,
                    "error": "rotation参数必须是包含3个元素的数组 [x, y, z]",
                    "data": None
                }
            
            # 验证缩放参数
            if scale and len(scale) != 3:
                return {
                    "success": False,
                    "error": "scale参数必须是包含3个元素的数组 [x, y, z]",
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
            params = {"action": action}
            
            # 添加目标参数
            if path:
                params["path"] = path
            if instance_id is not None:
                params["instance_id"] = instance_id
            
            # 添加基本属性参数
            if name:
                params["name"] = name
            if tag:
                params["tag"] = tag
            if layer is not None:
                params["layer"] = layer
            if active is not None:
                params["active"] = active
            if static_flags is not None:
                params["static_flags"] = static_flags
            
            # 添加父对象参数
            if parent_id is not None:
                params["parent_id"] = parent_id
            if parent_path:
                params["parent_path"] = parent_path
            
            # 添加变换参数
            if position:
                params["position"] = position
            if rotation:
                params["rotation"] = rotation
            if scale:
                params["scale"] = scale
            
            # 添加组件参数
            if component_type:
                params["component_type"] = component_type
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("edit_gameobject", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"GameObject操作'{action}'执行完成",
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
                "error": f"GameObject编辑失败: {str(e)}",
                "data": None
            }

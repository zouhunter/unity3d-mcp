"""
Unity UGUI布局工具，包含RectTransform的布局修改和属性获取功能。
"""
import json
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_ugui_layout_tools(mcp: FastMCP):
    @mcp.tool("ugui_layout")
    def ugui_layout(
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
            examples=["Canvas/UI/Button", "Canvas/Panel/Text"]
        )] = None,
        action: Annotated[Optional[str], Field(
            title="操作类型",
            description="操作类型: do_layout(综合布局), get_layout(获取属性)",
            default="do_layout",
            examples=["do_layout", "get_layout"]
        )] = "do_layout",
        anchor_preset: Annotated[Optional[str], Field(
            title="锚点预设",
            description="锚点预设类型",
            default=None,
            examples=["stretch_all", "top_center", "middle_center", "bottom_center", "stretch_width", "stretch_height"]
        )] = None,
        anchor_min: Annotated[Optional[List[float]], Field(
            title="锚点最小值",
            description="锚点最小值 [x, y]",
            default=None,
            examples=[[0, 0], [0.5, 0.5], [1, 1]]
        )] = None,
        anchor_max: Annotated[Optional[List[float]], Field(
            title="锚点最大值",
            description="锚点最大值 [x, y]",
            default=None,
            examples=[[0, 0], [0.5, 0.5], [1, 1]]
        )] = None,
        anchored_position: Annotated[Optional[List[float]], Field(
            title="锚点位置",
            description="锚点位置 [x, y]",
            default=None,
            examples=[[0, 0], [100, -50], [200, 100]]
        )] = None,
        size_delta: Annotated[Optional[List[float]], Field(
            title="尺寸增量",
            description="尺寸增量 [width, height]",
            default=None,
            examples=[[0, 0], [200, 100], [400, 200]]
        )] = None,
        pivot: Annotated[Optional[List[float]], Field(
            title="轴心点",
            description="轴心点 [x, y]",
            default=None,
            examples=[[0.5, 0.5], [0, 0], [1, 1]]
        )] = None,
        rotation: Annotated[Optional[List[float]], Field(
            title="旋转",
            description="旋转角度 [x, y, z]",
            default=None,
            examples=[[0, 0, 0], [0, 0, 45], [0, 0, 90]]
        )] = None,
        scale: Annotated[Optional[List[float]], Field(
            title="缩放",
            description="缩放比例 [x, y, z]",
            default=None,
            examples=[[1, 1, 1], [2, 2, 1], [0.5, 0.5, 1]]
        )] = None,
        anchor_self: Annotated[Optional[bool], Field(
            title="锚点自身",
            description="当为true时，锚点预设将基于元素当前位置而不是父容器的预设位置",
            default=False
        )] = False,
        offset_min: Annotated[Optional[List[float]], Field(
            title="偏移最小值",
            description="偏移最小值 [left, bottom]",
            default=None,
            examples=[[0, 0], [10, 10], [-10, -10]]
        )] = None,
        offset_max: Annotated[Optional[List[float]], Field(
            title="偏移最大值",
            description="偏移最大值 [right, top]",
            default=None,
            examples=[[0, 0], [-10, -10], [10, 10]]
        )] = None
    ) -> Dict[str, Any]:
        """Unity UGUI布局工具，用于修改和获取RectTransform的布局属性。

        支持多种布局操作，适用于：
        - 布局修改：设置UI元素的位置、大小、锚点等属性
        - 属性获取：获取UI元素的当前布局属性
        - 锚点预设：使用预定义的锚点配置
        - 智能布局：基于元素当前位置设置锚点

        Returns:
            包含布局操作结果的字典：
            {
                "success": bool,        # 操作是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 布局相关数据（如属性值）
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
            valid_actions = ["do_layout", "get_layout"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证锚点预设
            if anchor_preset:
                valid_presets = ["stretch_all", "top_center", "middle_center", "bottom_center", 
                               "stretch_width", "stretch_height", "top_left", "top_right", 
                               "middle_left", "middle_right", "bottom_left", "bottom_right"]
                if anchor_preset not in valid_presets:
                    return {
                        "success": False,
                        "error": f"无效的锚点预设: '{anchor_preset}'。支持的预设: {valid_presets}",
                        "data": None
                    }
            
            # 验证向量参数
            vector_params = {
                "anchor_min": anchor_min,
                "anchor_max": anchor_max,
                "anchored_position": anchored_position,
                "size_delta": size_delta,
                "pivot": pivot,
                "rotation": rotation,
                "scale": scale,
                "offset_min": offset_min,
                "offset_max": offset_max
            }
            
            for param_name, param_value in vector_params.items():
                if param_value is not None:
                    if not isinstance(param_value, list) or len(param_value) != 2:
                        if param_name in ["rotation", "scale"] and len(param_value) == 3:
                            continue  # rotation和scale可以是3个元素
                        return {
                            "success": False,
                            "error": f"{param_name}参数必须是包含2个元素的数组 [x, y]",
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
            
            # 添加布局参数
            if anchor_preset:
                params["anchor_preset"] = anchor_preset
            if anchor_min:
                params["anchor_min"] = anchor_min
            if anchor_max:
                params["anchor_max"] = anchor_max
            if anchored_position:
                params["anchored_position"] = anchored_position
            if size_delta:
                params["size_delta"] = size_delta
            if pivot:
                params["pivot"] = pivot
            if rotation:
                params["rotation"] = rotation
            if scale:
                params["scale"] = scale
            if offset_min:
                params["offset_min"] = offset_min
            if offset_max:
                params["offset_max"] = offset_max
            
            # 添加特殊参数
            if anchor_self:
                params["anchor_self"] = anchor_self
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("ugui_layout", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"UGUI布局操作'{action}'执行完成",
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
                "error": f"UGUI布局操作失败: {str(e)}",
                "data": None
            }

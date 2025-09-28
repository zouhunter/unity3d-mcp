"""
Unity UGUI布局工具，包含RectTransform的布局修改和属性获取功能。
"""
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


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
        """Unity UGUI布局工具，用于修改和获取RectTransform的布局属性。（二级工具）

        支持多种布局操作，适用于：
        - 布局修改：设置UI元素的位置、大小、锚点等属性
        - 属性获取：获取UI元素的当前布局属性
        - 锚点预设：使用预定义的锚点配置
        - 智能布局：基于元素当前位置设置锚点
        """
        # ⚠️ 重要提示：此函数仅用于提供参数说明和文档
        # 实际调用请使用 single_call 函数
        # 示例：single_call(func="ugui_layout", args={...})
        
        return get_common_call_response("ugui_layout")

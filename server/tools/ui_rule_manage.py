"""
UI制作规则文件管理工具
管理UI制作规则文件，包括创建、修改、删除和获取规则
"""

from typing import Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_ui_rule_manage_tools(mcp: FastMCP):
    @mcp.tool("ui_rule_manage")
    def ui_rule_manage(
        ctx: Context,
        action: str = Field(
            ...,
            title="操作类型",
            description="操作类型",
            examples=["create", "modify", "delete", "get", "list", "apply", "validate"]
        ),
        rule_name: str = Field(
            ...,
            title="规则名称",
            description="规则名称",
            examples=["ButtonRule", "PanelRule", "TextRule"]
        ),
        rule_type: Optional[str] = Field(
            None,
            title="规则类型",
            description="规则类型",
            examples=["Button", "Panel", "Text", "Image", "InputField", "ScrollView", "Dropdown", "Toggle", "Slider", "Scrollbar"]
        ),
        properties: Optional[Dict[str, Any]] = Field(
            None,
            title="规则属性",
            description="规则属性字典",
            examples=[{"width": 200, "height": 50, "color": [1, 0, 0, 1]}]
        ),
        constraints: Optional[Dict[str, Any]] = Field(
            None,
            title="约束条件",
            description="约束条件字典",
            examples=[{"min_width": 100, "max_width": 500, "required_components": ["Button", "Text"]}]
        ),
        validation_rules: Optional[List[str]] = Field(
            None,
            title="验证规则",
            description="验证规则列表",
            examples=[["width > 0", "height > 0", "has_button_component"]]
        ),
        target_object: Optional[str] = Field(
            None,
            title="目标对象",
            description="目标对象名称或路径",
            examples=["Canvas/UI/Button", "Button", "Panel"]
        ),
        force_apply: Optional[bool] = Field(
            False,
            title="强制应用",
            description="是否强制应用规则"
        ),
        validate_only: Optional[bool] = Field(
            False,
            title="仅验证",
            description="是否仅验证而不应用"
        ),
        create_missing: Optional[bool] = Field(
            False,
            title="创建缺失",
            description="是否创建缺失的组件"
        ),
        remove_extra: Optional[bool] = Field(
            False,
            title="移除多余",
            description="是否移除多余的组件"
        ),
        preserve_hierarchy: Optional[bool] = Field(
            True,
            title="保持层级",
            description="是否保持对象层级结构"
        ),
        backup_before_apply: Optional[bool] = Field(
            True,
            title="应用前备份",
            description="是否在应用前备份对象"
        ),
        log_changes: Optional[bool] = Field(
            True,
            title="记录更改",
            description="是否记录更改日志"
        ),
        rule_file_path: Optional[str] = Field(
            None,
            title="规则文件路径",
            description="规则文件路径",
            examples=["Assets/UI/Rules/ButtonRule.json", "Assets/Config/UI/"]
        ),
        export_format: Optional[str] = Field(
            None,
            title="导出格式",
            description="导出格式",
            examples=["json", "xml", "yaml"]
        ),
        import_format: Optional[str] = Field(
            None,
            title="导入格式",
            description="导入格式",
            examples=["json", "xml", "yaml"]
        ),
        template_name: Optional[str] = Field(
            None,
            title="模板名称",
            description="模板名称",
            examples=["StandardButton", "ModernPanel", "ClassicText"]
        ),
        category: Optional[str] = Field(
            None,
            title="规则分类",
            description="规则分类",
            examples=["UI", "Gameplay", "System", "Custom"]
        ),
        tags: Optional[List[str]] = Field(
            None,
            title="标签",
            description="标签列表",
            examples=[["button", "ui", "interactive"], ["panel", "container", "layout"]]
        ),
        description: Optional[str] = Field(
            None,
            title="规则描述",
            description="规则描述",
            examples=["标准按钮规则", "面板布局规则", "文本显示规则"]
        ),
        version: Optional[str] = Field(
            None,
            title="规则版本",
            description="规则版本",
            examples=["1.0", "2.1", "3.0.1"]
        ),
        author: Optional[str] = Field(
            None,
            title="规则作者",
            description="规则作者",
            examples=["Designer", "Developer", "Artist"]
        ),
        created_date: Optional[str] = Field(
            None,
            title="创建日期",
            description="创建日期",
            examples=["2024-01-01", "2024-12-25"]
        ),
        modified_date: Optional[str] = Field(
            None,
            title="修改日期",
            description="修改日期",
            examples=["2024-01-01", "2024-12-25"]
        ),
        is_active: Optional[bool] = Field(
            True,
            title="是否激活",
            description="是否激活规则"
        ),
        priority: Optional[int] = Field(
            0,
            title="优先级",
            description="规则优先级",
            examples=[0, 1, 5, 10]
        ),
        dependencies: Optional[List[str]] = Field(
            None,
            title="依赖规则",
            description="依赖规则列表",
            examples=[["BaseUIRule", "ColorRule"], ["LayoutRule"]]
        ),
        conflicts: Optional[List[str]] = Field(
            None,
            title="冲突规则",
            description="冲突规则列表",
            examples=[["OldButtonRule", "LegacyPanelRule"]]
        ),
        conditions: Optional[Dict[str, Any]] = Field(
            None,
            title="应用条件",
            description="应用条件字典",
            examples=[{"platform": "mobile", "resolution": "high", "theme": "dark"}]
        ),
        effects: Optional[Dict[str, Any]] = Field(
            None,
            title="应用效果",
            description="应用效果字典",
            examples=[{"animation": "fade", "sound": "click", "haptic": "light"}]
        ),
        metadata: Optional[Dict[str, Any]] = Field(
            None,
            title="元数据",
            description="元数据字典",
            examples=[{"project": "MyGame", "team": "UI", "status": "approved"}]
        )
    ) -> Dict[str, Any]:
        """
        UI制作规则文件管理工具
        
        支持的操作:
        - create: 创建新规则
        - modify: 修改现有规则
        - delete: 删除规则
        - get: 获取规则信息
        - list: 列出所有规则
        - apply: 应用规则到对象
        - validate: 验证规则
        """
        try:
            unity_conn = get_unity_connection()
            
            # 构建命令参数
            cmd = {
                "action": action,
                "rule_name": rule_name
            }
            
            # 添加可选参数
            optional_params = {
                "rule_type": rule_type,
                "properties": properties,
                "constraints": constraints,
                "validation_rules": validation_rules,
                "target_object": target_object,
                "force_apply": force_apply,
                "validate_only": validate_only,
                "create_missing": create_missing,
                "remove_extra": remove_extra,
                "preserve_hierarchy": preserve_hierarchy,
                "backup_before_apply": backup_before_apply,
                "log_changes": log_changes,
                "rule_file_path": rule_file_path,
                "export_format": export_format,
                "import_format": import_format,
                "template_name": template_name,
                "category": category,
                "tags": tags,
                "description": description,
                "version": version,
                "author": author,
                "created_date": created_date,
                "modified_date": modified_date,
                "is_active": is_active,
                "priority": priority,
                "dependencies": dependencies,
                "conflicts": conflicts,
                "conditions": conditions,
                "effects": effects,
                "metadata": metadata
            }
            
            for key, value in optional_params.items():
                if value is not None:
                    cmd[key] = value
            
            # 发送命令到Unity
            result = unity_conn.send_command_with_retry("ui_rule_manage", cmd)
            
            return {
                "success": True,
                "message": f"UI rule operation '{action}' completed successfully",
                "data": result.get("data", {}),
                "error": None
            }
            
        except Exception as e:
            return {
                "success": False,
                "message": f"UI rule operation '{action}' failed",
                "data": {},
                "error": str(e)
            }

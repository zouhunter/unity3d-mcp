from .call_up import register_call_tools
from .console import register_console_tools
from .run_code import register_run_code_tools
from .hierarchy_create import register_hierarchy_create_tools
from .hierarchy_search import register_hierarchy_search_tools
from .project_search import register_project_search_tools
from .project_operate import register_project_operate_tools
from .edit_material import register_edit_material_tools
from .edit_mesh import register_edit_mesh_tools
from .gameplay import register_gameplay_tools
from .manage_editor import register_manage_editor_tools
from .manage_package import register_manage_package_tools
from .object_delete import register_object_delete_tools
from .request_http import register_request_http_tools
from .hierarchy_apply import register_hierarchy_apply_tools
from .edit_gameobject import register_edit_gameobject_tools
from .edit_component import register_edit_component_tools
from .edit_script import register_edit_script_tools
from .ugui_layout import register_ugui_layout_tools
from .figma_manage import register_figma_manage_tools
from .edit_asset import register_edit_asset_tools
from .edit_prefab import register_edit_prefab_tools
from .edit_scene import register_edit_scene_tools
from .edit_texture import register_edit_texture_tools
from .ui_rule_manage import register_ui_rule_manage_tools
from .edit_scriptableobject import register_edit_scriptableobject_tools
from .edit_shader import register_edit_shader_tools

def register_all_tools(mcp):
    """Register all refactored tools with the MCP server."""
    print("Registering Unity MCP Server refactored tools...")
    register_call_tools(mcp)
    register_console_tools(mcp)
    register_run_code_tools(mcp)
    register_hierarchy_create_tools(mcp)
    register_hierarchy_search_tools(mcp)
    register_project_search_tools(mcp)
    register_project_operate_tools(mcp)
    register_edit_material_tools(mcp)
    register_edit_mesh_tools(mcp)
    register_gameplay_tools(mcp)
    register_manage_editor_tools(mcp)
    register_manage_package_tools(mcp)
    register_object_delete_tools(mcp)
    register_request_http_tools(mcp)
    register_hierarchy_apply_tools(mcp)
    register_edit_gameobject_tools(mcp)
    register_edit_component_tools(mcp)
    register_edit_script_tools(mcp)
    register_ugui_layout_tools(mcp)
    register_figma_manage_tools(mcp)
    register_edit_asset_tools(mcp)
    register_edit_prefab_tools(mcp)
    register_edit_scene_tools(mcp)
    register_edit_texture_tools(mcp)
    register_ui_rule_manage_tools(mcp)
    register_edit_scriptableobject_tools(mcp)
    register_edit_shader_tools(mcp)
    print("Unity MCP Server tool registration complete.")

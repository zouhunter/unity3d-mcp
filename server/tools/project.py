"""
Unity项目管理工具，包含项目搜索、资源管理、项目操作等功能。
"""
import json
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_project_tools(mcp: FastMCP):
    @mcp.tool("project_search")
    def project_search(
        ctx: Context,
        search_target: Annotated[str, Field(
            title="搜索目标类型",
            description="要搜索的目标类型: asset(资源文件), script(脚本文件), scene(场景文件), prefab(预制体), material(材质), texture(纹理)",
            examples=["asset", "script", "scene", "prefab", "material", "texture"]
        )],
        query: Annotated[str, Field(
            title="搜索查询",
            description="搜索关键词或文件名模式",
            examples=["Player*", "*.cs", "MainScene", "UI_*"]
        )],
        folder: Annotated[Optional[str], Field(
            title="搜索文件夹",
            description="限制搜索范围的文件夹路径，留空搜索整个项目",
            default=None,
            examples=["Assets/Scripts", "Assets/Prefabs", "Assets/Scenes"]
        )] = None,
        include_packages: Annotated[bool, Field(
            title="包含包文件",
            description="是否在搜索结果中包含Packages目录下的文件",
            default=False
        )] = False,
        case_sensitive: Annotated[bool, Field(
            title="区分大小写",
            description="搜索时是否区分大小写",
            default=False
        )] = False
    ) -> Dict[str, Any]:
        """Unity项目搜索工具，用于在项目中搜索各种类型的资源和文件。

        支持多种搜索类型和过滤条件，适用于：
        - 快速定位：找到特定名称的资源文件
        - 批量处理：获取同类型文件列表进行批量操作
        - 项目清理：查找未使用或重复的资源
        - 依赖分析：查找资源之间的引用关系

        Returns:
            包含搜索结果的字典：
            {
                "success": bool,        # 搜索是否成功
                "message": str,         # 操作结果描述
                "data": List[Dict],    # 找到的文件/资源列表
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证搜索目标类型
            valid_targets = ["asset", "script", "scene", "prefab", "material", "texture", "audio", "model"]
            if search_target not in valid_targets:
                return {
                    "success": False,
                    "error": f"无效的搜索目标类型: '{search_target}'。支持的类型: {valid_targets}",
                    "data": None
                }
            
            # 验证查询字符串
            if not query or not isinstance(query, str):
                return {
                    "success": False,
                    "error": "查询字符串不能为空且必须是字符串类型",
                    "data": None
                }
            
            # 验证文件夹路径
            if folder and not folder.startswith("Assets"):
                return {
                    "success": False,
                    "error": "搜索文件夹路径必须以'Assets'开头",
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
                "search_target": search_target,
                "query": query,
                "include_packages": include_packages,
                "case_sensitive": case_sensitive
            }
            
            # 添加文件夹限制
            if folder:
                params["folder"] = folder
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("project_search", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"项目搜索完成，查询: {query}",
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
                "error": f"项目搜索失败: {str(e)}",
                "data": None
            }

    @mcp.tool("project_operate")
    def project_operate(
        ctx: Context,
        action: Annotated[str, Field(
            title="项目操作类型",
            description="要执行的项目操作: refresh(刷新项目), import_asset(导入资源), export_package(导出包), create_folder(创建文件夹), delete_asset(删除资源)",
            examples=["refresh", "import_asset", "export_package", "create_folder", "delete_asset"]
        )],
        target_path: Annotated[Optional[str], Field(
            title="目标路径",
            description="操作的目标路径，根据action类型而定",
            default=None,
            examples=["Assets/NewFolder", "Assets/Models/model.fbx", "Assets/Scripts"]
        )] = None,
        source_path: Annotated[Optional[str], Field(
            title="源路径",
            description="源文件路径，仅在import_asset操作时使用",
            default=None,
            examples=["D:/Models/character.fbx", "C:/Textures/grass.png"]
        )] = None,
        package_name: Annotated[Optional[str], Field(
            title="包名称",
            description="导出包的名称，仅在export_package操作时使用",
            default=None,
            examples=["MyAssets.unitypackage", "ScriptsPackage"]
        )] = None,
        include_dependencies: Annotated[bool, Field(
            title="包含依赖",
            description="是否包含资源的依赖项，适用于export_package操作",
            default=True
        )] = True
    ) -> Dict[str, Any]:
        """Unity项目操作工具，用于执行各种项目管理操作。

        支持多种项目操作，适用于：
        - 资源管理：导入外部资源文件到项目中
        - 项目组织：创建文件夹结构，删除不需要的资源
        - 包管理：导出资源包用于分享或备份
        - 项目维护：刷新项目状态，清理无效引用

        Returns:
            包含操作结果的字典：
            {
                "success": bool,        # 操作是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 操作相关数据
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证操作类型
            valid_actions = ["refresh", "import_asset", "export_package", "create_folder", "delete_asset"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证路径参数
            if action in ["import_asset", "create_folder", "delete_asset", "export_package"]:
                if not target_path:
                    return {
                        "success": False,
                        "error": f"操作'{action}'需要提供target_path参数",
                        "data": None
                    }
            
            # 验证导入操作参数
            if action == "import_asset":
                if not source_path:
                    return {
                        "success": False,
                        "error": "import_asset操作需要提供source_path参数",
                        "data": None
                    }
            
            # 验证导出包参数
            if action == "export_package":
                if not package_name:
                    return {
                        "success": False,
                        "error": "export_package操作需要提供package_name参数",
                        "data": None
                    }
            
            # 验证创建文件夹参数
            if action == "create_folder" and target_path:
                if not target_path.startswith("Assets"):
                    return {
                        "success": False,
                        "error": "创建文件夹路径必须以'Assets'开头",
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
            
            # 添加路径参数
            if target_path:
                params["target_path"] = target_path
            if source_path:
                params["source_path"] = source_path
            if package_name:
                params["package_name"] = package_name
            
            # 添加选项参数
            params["include_dependencies"] = include_dependencies
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("project_operate", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"项目操作'{action}'执行完成",
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
                "error": f"项目操作失败: {str(e)}",
                "data": None
            }

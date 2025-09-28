"""
Unity层次结构管理工具，包含GameObject的创建、删除、查找、移动等功能。
"""
import json
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_hierarchy_tools(mcp: FastMCP):
    @mcp.tool("hierarchy_create")
    def hierarchy_create(
        ctx: Context,
        source: Annotated[str, Field(
            title="创建来源类型",
            description="GameObject的创建来源: primitive(基础图元), prefab(预制体), empty(空对象), copy(复制现有对象)",
            examples=["primitive", "prefab", "empty", "copy"]
        )],
        name: Annotated[str, Field(
            title="对象名称",
            description="要创建的GameObject的名称",
            examples=["Player", "Enemy", "MainCamera", "UI_Canvas"]
        )],
        primitive_type: Annotated[Optional[str], Field(
            title="基础图元类型",
            description="当source为primitive时，指定图元类型",
            default=None,
            examples=["Cube", "Sphere", "Cylinder", "Plane", "Capsule"]
        )] = None,
        prefab_path: Annotated[Optional[str], Field(
            title="预制体路径",
            description="当source为prefab时，指定预制体的资源路径",
            default=None,
            examples=["Assets/Prefabs/Player.prefab", "Prefabs/Enemy.prefab"]
        )] = None,
        copy_source: Annotated[Optional[str], Field(
            title="复制源对象",
            description="当source为copy时，指定要复制的GameObject名称",
            default=None,
            examples=["Player", "ExistingObject", "Template"]
        )] = None,
        parent: Annotated[Optional[str], Field(
            title="父对象",
            description="创建的GameObject的父对象名称，留空则在根层级创建",
            default=None,
            examples=["Canvas", "Player", "Environment"]
        )] = None
    ) -> Dict[str, Any]:
        """Unity层次结构创建工具，用于在场景中创建各种类型的GameObject。

        支持多种创建方式，适用于：
        - 快速原型制作：创建基础几何体进行测试
        - 场景构建：从预制体创建复杂对象
        - 对象复制：复制现有对象进行批量创建
        - UI构建：创建空对象作为容器

        Returns:
            包含创建操作结果的字典：
            {
                "success": bool,        # 创建是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 创建的GameObject信息
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证创建来源类型
            valid_types = ["primitive", "prefab", "empty", "copy"]
            if source not in valid_types:
                return {
                    "success": False,
                    "error": f"无效的创建来源类型: '{source}'。支持的类型: {valid_types}",
                    "data": None
                }
            
            # 验证名称
            if not name or not isinstance(name, str):
                return {
                    "success": False,
                    "error": "GameObject名称不能为空且必须是字符串类型",
                    "data": None
                }
            
            # 验证基础图元类型
            if source == "primitive":
                if not primitive_type:
                    return {
                        "success": False,
                        "error": "创建基础图元时必须指定primitive_type参数",
                        "data": None
                    }
                
                valid_primitives = ["Cube", "Sphere", "Cylinder", "Plane", "Capsule", "Quad"]
                if primitive_type not in valid_primitives:
                    return {
                        "success": False,
                        "error": f"无效的基础图元类型: '{primitive_type}'。支持的类型: {valid_primitives}",
                        "data": None
                    }
            
            # 验证预制体路径
            if source == "prefab":
                if not prefab_path:
                    return {
                        "success": False,
                        "error": "创建预制体实例时必须指定prefab_path参数",
                        "data": None
                    }
                
                if not prefab_path.endswith(".prefab"):
                    return {
                        "success": False,
                        "error": "预制体路径必须以.prefab结尾",
                        "data": None
                    }
            
            # 验证复制源
            if source == "copy":
                if not copy_source:
                    return {
                        "success": False,
                        "error": "复制对象时必须指定copy_source参数",
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
                "from": source,
                "name": name
            }
            
            # 添加类型特定参数
            if primitive_type:
                params["primitive_type"] = primitive_type
            if prefab_path:
                params["prefab_path"] = prefab_path
            if copy_source:
                params["copy_source"] = copy_source
            if parent:
                params["parent"] = parent
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("hierarchy_create", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"成功创建GameObject: {name}",
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
                "error": f"GameObject创建失败: {str(e)}",
                "data": None
            }

    @mcp.tool("hierarchy_search")
    def hierarchy_search(
        ctx: Context,
        query: Annotated[str, Field(
            title="搜索查询",
            description="要搜索的GameObject名称或名称模式",
            examples=["Player", "Enemy*", "*Camera", "UI_*"]
        )],
        search_type: Annotated[str, Field(
            title="搜索类型",
            description="搜索的类型: name(按名称), tag(按标签), component(按组件)",
            default="name",
            examples=["name", "tag", "component"]
        )] = "name",
        include_inactive: Annotated[bool, Field(
            title="包含非活动对象",
            description="是否在搜索结果中包含非活动的GameObject",
            default=False
        )] = False
    ) -> Dict[str, Any]:
        """Unity层次结构搜索工具，用于查找场景中的GameObject。

        支持多种搜索方式，适用于：
        - 对象定位：快速找到特定名称的GameObject
        - 批量操作：查找所有符合条件的对象
        - 调试分析：检查场景中的对象状态
        - 自动化脚本：获取对象列表进行处理

        Returns:
            包含搜索结果的字典：
            {
                "success": bool,        # 搜索是否成功
                "message": str,         # 操作结果描述
                "data": List[Dict],    # 找到的GameObject列表
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证查询字符串
            if not query or not isinstance(query, str):
                return {
                    "success": False,
                    "error": "查询字符串不能为空且必须是字符串类型",
                    "data": None
                }
            
            # 验证搜索类型
            valid_search_types = ["name", "tag", "component"]
            if search_type not in valid_search_types:
                return {
                    "success": False,
                    "error": f"无效的搜索类型: '{search_type}'。支持的类型: {valid_search_types}",
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
                "query": query,
                "search_type": search_type,
                "include_inactive": include_inactive
            }
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("hierarchy_search", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"搜索完成，查询: {query}",
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
                "error": f"层次结构搜索失败: {str(e)}",
                "data": None
            }

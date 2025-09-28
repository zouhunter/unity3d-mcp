"""
Unity脚本编辑工具，包含C#脚本的创建、读取、更新、删除等功能。
"""
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_edit_script_tools(mcp: FastMCP):
    @mcp.tool("edit_script")
    def edit_script(
        ctx: Context,
        action: Annotated[str, Field(
            title="操作类型",
            description="操作类型：create(创建), read(读取), update(更新), delete(删除)",
            examples=["create", "read", "update", "delete"]
        )],
        name: Annotated[Optional[str], Field(
            title="脚本名称",
            description="脚本名称（不含.cs扩展名）",
            default=None,
            examples=["PlayerController", "GameManager", "EnemyAI"]
        )] = None,
        path: Annotated[Optional[str], Field(
            title="脚本路径",
            description="脚本资产路径",
            default=None,
            examples=["Assets/Scripts/PlayerController.cs", "Scripts/GameManager.cs"]
        )] = None,
        lines: Annotated[Optional[List[str]], Field(
            title="代码内容",
            description="C#代码内容（已换行的字符串数组）",
            default=None,
            examples=[
                ["using UnityEngine;", "public class PlayerController : MonoBehaviour", "{", "    void Start()", "    {", "    }", "}"]
            ]
        )] = None,
        script_type: Annotated[Optional[str], Field(
            title="脚本类型",
            description="脚本类型：MonoBehaviour, ScriptableObject等",
            default="MonoBehaviour",
            examples=["MonoBehaviour", "ScriptableObject", "EditorWindow", "CustomEditor"]
        )] = "MonoBehaviour",
        namespace: Annotated[Optional[str], Field(
            title="命名空间",
            description="命名空间",
            default=None,
            examples=["MyGame", "MyGame.Player", "MyGame.Managers"]
        )] = None
    ) -> Dict[str, Any]:
        """Unity脚本编辑工具，用于管理C#脚本文件。（二级工具）

        支持多种脚本操作，适用于：
        - 脚本创建：创建新的C#脚本文件
        - 脚本读取：读取现有脚本的内容
        - 脚本更新：修改现有脚本的内容
        - 脚本删除：删除不需要的脚本文件
        - 代码生成：自动生成常用脚本模板
        """
        return get_common_call_response("edit_script")

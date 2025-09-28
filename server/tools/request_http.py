"""
Unity网络请求工具，包含HTTP请求、文件下载、API调用等功能。
"""
import json
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_request_http_tools(mcp: FastMCP):
    @mcp.tool("request_http")
    def request_http(
        ctx: Context,
        action: Annotated[str, Field(
            title="请求操作类型",
            description="要执行的网络操作: get(GET请求), post(POST请求), put(PUT请求), delete(DELETE请求), download(下载文件), upload(上传文件), ping(网络测试), batch_download(批量下载)",
            examples=["get", "post", "put", "delete", "download", "upload", "ping", "batch_download"]
        )],
        url: Annotated[Optional[str], Field(
            title="请求URL",
            description="请求的URL地址",
            default=None,
            examples=["https://api.github.com/repos/unity3d/unity", "https://httpbin.org/get"]
        )] = None,
        data: Annotated[Optional[Dict[str, Any]], Field(
            title="请求数据",
            description="请求数据（用于POST/PUT，JSON格式）",
            default=None,
            examples=[{"name": "test", "value": 123}, {"key": "value"}]
        )] = None,
        headers: Annotated[Optional[Dict[str, str]], Field(
            title="请求头",
            description="请求头字典",
            default=None,
            examples=[{"Content-Type": "application/json", "Authorization": "Bearer token"}]
        )] = None,
        save_path: Annotated[Optional[str], Field(
            title="保存路径",
            description="保存路径（用于下载，相对于Assets或绝对路径）",
            default=None,
            examples=["Assets/Downloads/file.zip", "D:/Downloads/image.png"]
        )] = None,
        file_path: Annotated[Optional[str], Field(
            title="文件路径",
            description="文件路径（用于上传）",
            default=None,
            examples=["Assets/Data/config.json", "D:/Files/document.pdf"]
        )] = None,
        timeout: Annotated[Optional[int], Field(
            title="超时时间",
            description="超时时间（秒），默认30秒",
            default=30,
            ge=1,
            le=300
        )] = 30,
        method: Annotated[Optional[str], Field(
            title="HTTP方法",
            description="HTTP方法（GET, POST, PUT, DELETE等）",
            default=None,
            examples=["GET", "POST", "PUT", "DELETE", "PATCH"]
        )] = None,
        content_type: Annotated[Optional[str], Field(
            title="内容类型",
            description="内容类型，默认application/json",
            default="application/json",
            examples=["application/json", "application/x-www-form-urlencoded", "multipart/form-data"]
        )] = "application/json",
        user_agent: Annotated[Optional[str], Field(
            title="用户代理",
            description="用户代理字符串",
            default=None,
            examples=["Unity-MCP/1.0", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"]
        )] = None,
        accept_certificates: Annotated[Optional[bool], Field(
            title="接受证书",
            description="是否接受所有证书（用于测试）",
            default=False
        )] = False,
        follow_redirects: Annotated[Optional[bool], Field(
            title="跟随重定向",
            description="是否跟随重定向",
            default=True
        )] = True,
        encoding: Annotated[Optional[str], Field(
            title="文本编码",
            description="文本编码，默认UTF-8",
            default="UTF-8",
            examples=["UTF-8", "GBK", "ISO-8859-1"]
        )] = "UTF-8",
        form_data: Annotated[Optional[Dict[str, str]], Field(
            title="表单数据",
            description="表单数据（键值对）",
            default=None,
            examples=[{"username": "user", "password": "pass"}, {"field1": "value1"}]
        )] = None,
        query_params: Annotated[Optional[Dict[str, str]], Field(
            title="查询参数",
            description="查询参数（键值对）",
            default=None,
            examples=[{"page": "1", "limit": "10"}, {"search": "unity"}]
        )] = None,
        auth_token: Annotated[Optional[str], Field(
            title="认证令牌",
            description="认证令牌（Bearer Token）",
            default=None,
            examples=["eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...", "your-api-key"]
        )] = None,
        basic_auth: Annotated[Optional[str], Field(
            title="基本认证",
            description="基本认证（用户名:密码）",
            default=None,
            examples=["username:password", "admin:secret123"]
        )] = None,
        retry_count: Annotated[Optional[int], Field(
            title="重试次数",
            description="重试次数，默认0",
            default=0,
            ge=0,
            le=5
        )] = 0,
        retry_delay: Annotated[Optional[int], Field(
            title="重试延迟",
            description="重试延迟（秒），默认1秒",
            default=1,
            ge=1,
            le=10
        )] = 1,
        urls: Annotated[Optional[List[str]], Field(
            title="URL列表",
            description="URL数组（用于批量下载）",
            default=None,
            examples=[["https://example.com/file1.zip", "https://example.com/file2.zip"]]
        )] = None
    ) -> Dict[str, Any]:
        """Unity网络请求工具，用于执行各种网络操作。

        支持多种网络操作，适用于：
        - HTTP请求：GET、POST、PUT、DELETE等标准HTTP方法
        - 文件操作：下载和上传文件
        - API调用：与外部API进行交互
        - 网络测试：ping和连接测试
        - 批量操作：批量下载多个文件

        Returns:
            包含网络操作结果的字典：
            {
                "success": bool,        # 操作是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 网络操作相关数据（如响应内容、下载路径）
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证操作类型
            valid_actions = ["get", "post", "put", "delete", "download", "upload", "ping", "batch_download"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证URL参数
            if action in ["get", "post", "put", "delete", "download", "upload", "ping"]:
                if not url:
                    return {
                        "success": False,
                        "error": f"操作'{action}'需要提供url参数",
                        "data": None
                    }
            
            # 验证批量下载参数
            if action == "batch_download":
                if not urls or not isinstance(urls, list):
                    return {
                        "success": False,
                        "error": "batch_download操作需要提供urls参数（URL数组）",
                        "data": None
                    }
            
            # 验证下载参数
            if action == "download":
                if not save_path:
                    return {
                        "success": False,
                        "error": "download操作需要提供save_path参数",
                        "data": None
                    }
            
            # 验证上传参数
            if action == "upload":
                if not file_path:
                    return {
                        "success": False,
                        "error": "upload操作需要提供file_path参数",
                        "data": None
                    }
            
            # 验证超时参数
            if timeout and (timeout < 1 or timeout > 300):
                return {
                    "success": False,
                    "error": "超时时间必须在1-300秒之间",
                    "data": None
                }
            
            # 验证重试参数
            if retry_count and (retry_count < 0 or retry_count > 5):
                return {
                    "success": False,
                    "error": "重试次数必须在0-5之间",
                    "data": None
                }
            
            if retry_delay and (retry_delay < 1 or retry_delay > 10):
                return {
                    "success": False,
                    "error": "重试延迟必须在1-10秒之间",
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
            
            # 添加URL参数
            if url:
                params["url"] = url
            
            # 添加数据参数
            if data:
                params["data"] = data
            
            # 添加请求头参数
            if headers:
                params["headers"] = headers
            
            # 添加路径参数
            if save_path:
                params["save_path"] = save_path
            if file_path:
                params["file_path"] = file_path
            
            # 添加HTTP配置参数
            if timeout:
                params["timeout"] = timeout
            if method:
                params["method"] = method
            if content_type:
                params["content_type"] = content_type
            if user_agent:
                params["user_agent"] = user_agent
            if accept_certificates:
                params["accept_certificates"] = accept_certificates
            if follow_redirects:
                params["follow_redirects"] = follow_redirects
            if encoding:
                params["encoding"] = encoding
            
            # 添加数据参数
            if form_data:
                params["form_data"] = form_data
            if query_params:
                params["query_params"] = query_params
            
            # 添加认证参数
            if auth_token:
                params["auth_token"] = auth_token
            if basic_auth:
                params["basic_auth"] = basic_auth
            
            # 添加重试参数
            if retry_count:
                params["retry_count"] = retry_count
            if retry_delay:
                params["retry_delay"] = retry_delay
            
            # 添加批量下载参数
            if urls:
                params["urls"] = urls
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("request_http", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"网络请求操作'{action}'执行完成",
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
                "error": f"网络请求失败: {str(e)}",
                "data": None
            }

#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Claude API Demo - 演示如何使用 Anthropic Claude API

安装依赖：
pip install anthropic
"""

import os
import anthropic
from anthropic import Anthropic
import httpx

# API Key 配置 - 直接硬编码
API_KEY = ""

# 代理配置
PROXY_URL = "http://127.0.0.1:9091"

# 创建带代理的 HTTP 客户端
http_client = httpx.Client(proxy=PROXY_URL)


def basic_chat_example():
    """基础对话示例"""
    print("=" * 60)
    print("示例 1: 基础对话")
    print("=" * 60)
    
    # 初始化客户端
    client = Anthropic(
        api_key=API_KEY,
        http_client=http_client
    )
    
    # 发送消息
    message = client.messages.create(
        model="claude-sonnet-4-20250514",  # 使用最新的模型
        max_tokens=1024,
        messages=[
            {
                "role": "user",
                "content": "你好！请用中文介绍一下你自己。"
            }
        ]
    )
    
    print(f"模型: {message.model}")
    print(f"使用 tokens: {message.usage.input_tokens} 输入 + {message.usage.output_tokens} 输出")
    print(f"\n回复:\n{message.content[0].text}")
    print()


def multi_turn_conversation():
    """多轮对话示例"""
    print("=" * 60)
    print("示例 2: 多轮对话")
    print("=" * 60)
    
    client = Anthropic(
        api_key=API_KEY,
        http_client=http_client
    )
    
    # 对话历史
    conversation = [
        {
            "role": "user",
            "content": "我想学习 Python，应该从哪里开始？"
        },
        {
            "role": "assistant",
            "content": "太好了！学习 Python 是个很好的选择。我建议你从这些方面开始：\n\n1. 基础语法：变量、数据类型、控制流\n2. 函数和模块\n3. 文件操作\n4. 面向对象编程\n\n你想先了解哪个部分？"
        },
        {
            "role": "user",
            "content": "我想先学习基础语法，能给我一个简单的例子吗？"
        }
    ]
    
    message = client.messages.create(
        model="claude-sonnet-4-20250514",
        max_tokens=1024,
        messages=conversation
    )
    
    print("对话历史:")
    for i, msg in enumerate(conversation):
        role = "用户" if msg["role"] == "user" else "助手"
        print(f"\n{role}: {msg['content'][:100]}...")
    
    print(f"\n助手最新回复:\n{message.content[0].text}")
    print()


def streaming_example():
    """流式响应示例"""
    print("=" * 60)
    print("示例 3: 流式响应（实时输出）")
    print("=" * 60)
    
    client = Anthropic(
        api_key=API_KEY,
        http_client=http_client
    )
    
    print("正在生成回复...\n")
    
    # 使用 stream=True 启用流式响应
    with client.messages.stream(
        model="claude-sonnet-4-20250514",
        max_tokens=1024,
        messages=[
            {
                "role": "user",
                "content": "请写一首关于编程的简短诗歌。"
            }
        ]
    ) as stream:
        for text in stream.text_stream:
            print(text, end="", flush=True)
    
    print("\n")


def system_prompt_example():
    """系统提示词示例"""
    print("=" * 60)
    print("示例 4: 使用系统提示词")
    print("=" * 60)
    
    client = Anthropic(
        api_key=API_KEY,
        http_client=http_client
    )
    
    message = client.messages.create(
        model="claude-sonnet-4-20250514",
        max_tokens=1024,
        system="你是一个专业的 Python 代码审查员。请用专业、简洁的语言点评代码，指出潜在问题并提供改进建议。",
        messages=[
            {
                "role": "user",
                "content": """请审查这段代码：

def calculate(x, y):
    return x + y

result = calculate(5, "10")
print(result)
"""
            }
        ]
    )
    
    print(f"系统角色: Python 代码审查员")
    print(f"\n代码审查结果:\n{message.content[0].text}")
    print()


def error_handling_example():
    """错误处理示例"""
    print("=" * 60)
    print("示例 5: 错误处理")
    print("=" * 60)
    
    client = Anthropic(
        api_key=API_KEY,
        http_client=http_client
    )
    
    try:
        message = client.messages.create(
            model="claude-sonnet-4-20250514",
            max_tokens=1024,
            messages=[
                {
                    "role": "user",
                    "content": "测试正常请求"
                }
            ]
        )
        print(f"[成功] 请求成功: {message.content[0].text[:50]}...")
        
    except anthropic.APIConnectionError as e:
        print(f"[失败] 网络连接错误: {e}")
    except anthropic.RateLimitError as e:
        print(f"[失败] 速率限制错误: {e}")
    except anthropic.APIStatusError as e:
        print(f"[失败] API 状态错误: {e.status_code} - {e.message}")
    except Exception as e:
        print(f"[失败] 未知错误: {e}")
    
    print()


def token_counting_example():
    """Token 计数示例"""
    print("=" * 60)
    print("示例 6: Token 使用统计")
    print("=" * 60)
    
    client = Anthropic(
        api_key=API_KEY,
        http_client=http_client
    )
    
    prompts = [
        "你好",
        "请写一段 100 字的文章介绍 Python",
        "详细解释什么是机器学习，包括监督学习、无监督学习和强化学习的区别"
    ]
    
    print("不同提示词的 token 使用情况:\n")
    
    for i, prompt in enumerate(prompts, 1):
        message = client.messages.create(
            model="claude-sonnet-4-20250514",
            max_tokens=512,
            messages=[{"role": "user", "content": prompt}]
        )
        
        print(f"{i}. 提示词: {prompt[:40]}...")
        print(f"   输入 tokens: {message.usage.input_tokens}")
        print(f"   输出 tokens: {message.usage.output_tokens}")
        print(f"   总计: {message.usage.input_tokens + message.usage.output_tokens}")
        print()


def vision_example():
    """视觉能力示例（如果有图片的话）"""
    print("=" * 60)
    print("示例 7: 图片分析（需要提供图片 URL 或 base64）")
    print("=" * 60)
    
    client = Anthropic(
        api_key=API_KEY,
        http_client=http_client
    )
    
    # 使用公开的图片 URL 作为示例
    message = client.messages.create(
        model="claude-sonnet-4-20250514",
        max_tokens=1024,
        messages=[
            {
                "role": "user",
                "content": [
                    {
                        "type": "text",
                        "text": "请描述这张图片的内容。"
                    },
                    {
                        "type": "image",
                        "source": {
                            "type": "url",
                            "url": "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3c/Giant_Panda_2004-03-2.jpg/800px-Giant_Panda_2004-03-2.jpg"
                        }
                    }
                ]
            }
        ]
    )
    
    print(f"图片分析结果:\n{message.content[0].text}")
    print()


def main():
    """主函数"""
    print("\n")
    print("╔════════════════════════════════════════════════════════════╗")
    print("║          Claude API Demo - Python 示例集合                ║")
    print("╚════════════════════════════════════════════════════════════╝")
    print()
    
    # 检查 API key
    if not API_KEY or API_KEY == "your-api-key-here":
        print("[错误] 请在代码中设置正确的 API_KEY")
        return
    
    print(f"[成功] 使用 API Key: {API_KEY[:20]}...\n")
    
    try:
        # 运行各个示例
        basic_chat_example()
        multi_turn_conversation()
        streaming_example()
        system_prompt_example()
        error_handling_example()
        token_counting_example()
        
        # 可选：如果想测试视觉功能，取消下面的注释
        # vision_example()
        
    except Exception as e:
        print(f"\n[错误] 发生错误: {e}")
        import traceback
        traceback.print_exc()
    
    print("=" * 60)
    print("Demo 完成！")
    print("=" * 60)


if __name__ == "__main__":
    main()

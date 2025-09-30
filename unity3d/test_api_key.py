#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""测试 Claude API key 是否有效"""

from anthropic import Anthropic
import httpx

# 你的 API Key
API_KEY = "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"

# 代理配置
PROXY_URL = "http://127.0.0.1:9091"
http_client = httpx.Client(proxy=PROXY_URL)

print("正在测试 API Key (使用代理: {})...".format(PROXY_URL))
print(f"API Key: {API_KEY[:20]}...\n")

try:
    client = Anthropic(
        api_key=API_KEY,
        http_client=http_client
    )
    
    # 发送一个最简单的测试请求
    message = client.messages.create(
        model="claude-3-5-sonnet-20241022",  # 使用稳定的模型版本
        max_tokens=100,
        messages=[
            {
                "role": "user",
                "content": "Hi"
            }
        ]
    )
    
    print("[成功] API Key 有效!")
    print(f"模型: {message.model}")
    print(f"回复: {message.content[0].text}")
    
except Exception as e:
    print(f"[失败] API Key 测试失败")
    print(f"错误类型: {type(e).__name__}")
    print(f"错误信息: {e}")
    print("\n可能的原因:")
    print("1. API key 无效或已过期")
    print("2. API key 没有足够权限")
    print("3. 需要使用代理访问 (某些地区限制)")
    print("4. 账户余额不足")
    print("\n请检查你的 Anthropic 账户: https://console.anthropic.com/")

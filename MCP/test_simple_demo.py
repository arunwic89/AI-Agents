"""
Simple Test Client for MCP Demo
================================
"""

import asyncio
import json
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client


async def test_simple_server():
    print("=" * 60)
    print("Testing Simple MCP Server")
    print("=" * 60)
    
    server_params = StdioServerParameters(
        command="python",
        args=["simple_mcp_demo.py"],
    )
    
    async with stdio_client(server_params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            print("\n✅ Connected to server!\n")
            
            # Test 1: List tools
            print("TEST 1: List Available Tools")
            print("-" * 40)
            tools = await session.list_tools()
            for tool in tools.tools:
                print(f"  • {tool.name}: {tool.description}")
            
            # Test 2: Get user
            print("\n\nTEST 2: Get User by ID")
            print("-" * 40)
            result = await session.call_tool("get_user", {"user_id": 1})
            print(f"Result: {result.content[0].text}")
            
            # Test 3: List all users
            print("\n\nTEST 3: List All Users")
            print("-" * 40)
            result = await session.call_tool("list_all_users", {})
            print(f"Result: {result.content[0].text}")
            
            # Test 4: Add new user
            print("\n\nTEST 4: Add New User")
            print("-" * 40)
            result = await session.call_tool("add_user", {
                "name": "Charlie",
                "email": "charlie@example.com"
            })
            print(f"Result: {result.content[0].text}")
            
            # Test 5: Verify user was added
            print("\n\nTEST 5: Verify User Was Added")
            print("-" * 40)
            result = await session.call_tool("get_user", {"user_id": 3})
            print(f"Result: {result.content[0].text}")
            
            # Test 6: Error handling - non-existent user
            print("\n\nTEST 6: Error Handling (Non-existent User)")
            print("-" * 40)
            result = await session.call_tool("get_user", {"user_id": 999})
            print(f"Result: {result.content[0].text}")
            
            print("\n" + "=" * 60)
            print("✅ All tests completed!")
            print("=" * 60)


if __name__ == "__main__":
    asyncio.run(test_simple_server())

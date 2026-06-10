"""
MCP Client Example for Testing
================================
Demonstrates how to interact with an MCP server programmatically.
"""

import asyncio
import json
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client


async def test_mcp_server():
    """
    Test the MCP server by calling various tools and reading resources.
    """
    print("=" * 60)
    print("MCP Client - Testing MCP Server")
    print("=" * 60)
    
    # Create server parameters (pointing to our server)
    server_params = StdioServerParameters(
        command="python",
        args=["mcp_server_example.py"],
        env=None
    )
    
    async with stdio_client(server_params) as (read, write):
        async with ClientSession(read, write) as session:
            # Initialize the session
            await session.initialize()
            print("\n✅ Connected to MCP Server\n")
            
            # ==========================================================
            # TEST 1: List available tools
            # ==========================================================
            print("📋 Available Tools:")
            tools = await session.list_tools()
            for tool in tools.tools:
                print(f"  - {tool.name}: {tool.description}")
            
            # ==========================================================
            # TEST 2: List and read resources
            # ==========================================================
            print("\n📂 Available Resources:")
            resources = await session.list_resources()
            for resource in resources.resources:
                print(f"  - {resource.name} ({resource.uri})")
            
            print("\n📖 Reading 'db://users' resource:")
            users_data = await session.read_resource("db://users")
            print(users_data.contents[0].text[:200] + "...")
            
            # ==========================================================
            # TEST 3: Call get_user tool
            # ==========================================================
            print("\n🔧 Testing Tool: get_user")
            result = await session.call_tool("get_user", {"user_id": 1})
            print(f"Result: {result.content[0].text}")
            
            # ==========================================================
            # TEST 4: Call search_users tool
            # ==========================================================
            print("\n🔍 Testing Tool: search_users")
            result = await session.call_tool("search_users", {
                "query": "alice",
                "limit": 5
            })
            print(f"Result: {result.content[0].text}")
            
            # ==========================================================
            # TEST 5: Call validate_email tool
            # ==========================================================
            print("\n✉️ Testing Tool: validate_email")
            result = await session.call_tool("validate_email", {
                "email": "test@example.com"
            })
            print(f"Result: {result.content[0].text}")
            
            # ==========================================================
            # TEST 6: Call create_user tool
            # ==========================================================
            print("\n➕ Testing Tool: create_user")
            result = await session.call_tool("create_user", {
                "name": "David Brown",
                "email": "david@example.com",
                "role": "user"
            })
            print(f"Result: {result.content[0].text}")
            
            # ==========================================================
            # TEST 7: Call calculate_stats tool
            # ==========================================================
            print("\n📊 Testing Tool: calculate_stats")
            result = await session.call_tool("calculate_stats", {
                "metric": "users_by_role"
            })
            print(f"Result: {result.content[0].text}")
            
            # ==========================================================
            # TEST 8: Error handling - invalid user ID
            # ==========================================================
            print("\n❌ Testing Error Handling: get_user with invalid ID")
            result = await session.call_tool("get_user", {"user_id": 9999})
            print(f"Result: {result.content[0].text}")
            
            print("\n" + "=" * 60)
            print("✅ All tests completed!")
            print("=" * 60)


async def interactive_mode():
    """
    Interactive mode to manually test tools.
    """
    server_params = StdioServerParameters(
        command="python",
        args=["mcp_server_example.py"],
        env=None
    )
    
    async with stdio_client(server_params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            
            print("\n🎮 Interactive MCP Client")
            print("Commands: list_tools, list_resources, call_tool, exit")
            
            while True:
                command = input("\n> ").strip()
                
                if command == "exit":
                    break
                
                elif command == "list_tools":
                    tools = await session.list_tools()
                    for tool in tools.tools:
                        print(f"  - {tool.name}")
                
                elif command == "list_resources":
                    resources = await session.list_resources()
                    for resource in resources.resources:
                        print(f"  - {resource.uri}")
                
                elif command.startswith("call_tool"):
                    parts = command.split(" ", 2)
                    if len(parts) < 3:
                        print("Usage: call_tool <tool_name> <json_args>")
                        continue
                    
                    tool_name = parts[1]
                    try:
                        args = json.loads(parts[2])
                        result = await session.call_tool(tool_name, args)
                        print(result.content[0].text)
                    except Exception as e:
                        print(f"Error: {e}")
                
                else:
                    print("Unknown command")


if __name__ == "__main__":
    import sys
    
    if len(sys.argv) > 1 and sys.argv[1] == "--interactive":
        asyncio.run(interactive_mode())
    else:
        asyncio.run(test_mcp_server())

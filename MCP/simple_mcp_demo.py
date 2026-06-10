"""
Simple MCP Server Demo - Perfect for Interview Practice
========================================================
Focuses on the essential concepts: Tools, Error Handling, Validation
"""

import json
import sys
from mcp.server import Server
from mcp.types import Tool, TextContent
from typing import Any, Sequence

# Helper to log to stderr (stdout is for JSON-RPC)
def log(msg):
    print(msg, file=sys.stderr)

# Create server instance
app = Server("simple-demo-server")

# Simple in-memory database
USERS = {
    1: {"id": 1, "name": "Alice", "email": "alice@example.com"},
    2: {"id": 2, "name": "Bob", "email": "bob@example.com"},
}

log("=" * 60)
log("Simple MCP Server - Interview Demo")
log("=" * 60)

# ============================================================================
# TOOLS - The heart of MCP servers
# ============================================================================

@app.list_tools()
async def list_tools() -> list[Tool]:
    """Define what the AI can do"""
    log("\n📋 AI Agent requested list of tools")
    return [
        Tool(
            name="get_user",
            description="Get a user by their ID",
            inputSchema={
                "type": "object",
                "properties": {
                    "user_id": {"type": "integer", "description": "User ID"}
                },
                "required": ["user_id"]
            }
        ),
        Tool(
            name="list_all_users",
            description="List all users in the database",
            inputSchema={"type": "object", "properties": {}}
        ),
        Tool(
            name="add_user",
            description="Add a new user",
            inputSchema={
                "type": "object",
                "properties": {
                    "name": {"type": "string"},
                    "email": {"type": "string"}
                },
                "required": ["name", "email"]
            }
        ),
    ]


@app.call_tool()
async def call_tool(name: str, arguments: Any) -> Sequence[TextContent]:
    """Execute a tool - this is where the magic happens"""
    log(f"\n🔧 Tool Called: {name}")
    log(f"   Arguments: {arguments}")
    
    try:
        if name == "get_user":
            user_id = arguments["user_id"]
            user = USERS.get(user_id)
            
            if not user:
                result = {"status": "error", "message": f"User {user_id} not found"}
            else:
                result = {"status": "success", "user": user}
        
        elif name == "list_all_users":
            result = {"status": "success", "users": list(USERS.values())}
        
        elif name == "add_user":
            new_id = max(USERS.keys()) + 1
            new_user = {
                "id": new_id,
                "name": arguments["name"],
                "email": arguments["email"]
            }
            USERS[new_id] = new_user
            result = {"status": "success", "user": new_user}
        
        else:
            result = {"status": "error", "message": f"Unknown tool: {name}"}
        
        log(f"   Result: {result}")
        return [TextContent(type="text", text=json.dumps(result, indent=2))]
    
    except Exception as e:
        error_result = {"status": "error", "message": str(e)}
        log(f"   Error: {e}")
        return [TextContent(type="text", text=json.dumps(error_result, indent=2))]


# ============================================================================
# MAIN - Run the server
# ============================================================================

async def main():
    from mcp.server.stdio import stdio_server
    
    log("\n✅ Server starting on stdio...")
    log("   Waiting for client connections...\n")
    
    async with stdio_server() as (read_stream, write_stream):
        await app.run(read_stream, write_stream, app.create_initialization_options())


if __name__ == "__main__":
    import asyncio
    asyncio.run(main())

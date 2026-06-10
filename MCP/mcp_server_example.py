"""
Production-Ready MCP Server Example
====================================
Demonstrates key patterns for principal engineer interview:
- Tool registration and execution
- Resource management
- Error handling and validation
- Async operations
- Logging and observability
- Security considerations
"""

import asyncio
import logging
import json
from datetime import datetime
from typing import Any, Sequence
from contextlib import asynccontextmanager

# MCP SDK imports
from mcp.server import Server
from mcp.types import (
    Resource,
    Tool,
    TextContent,
    ImageContent,
    EmbeddedResource,
)
from mcp.server.stdio import stdio_server

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger("mcp-server")

# ============================================================================
# SERVER INITIALIZATION
# ============================================================================

app = Server("demo-mcp-server")

# In-memory data store (replace with database in production)
USERS_DB = {
    1: {"id": 1, "name": "Alice Johnson", "email": "alice@example.com", "role": "admin"},
    2: {"id": 2, "name": "Bob Smith", "email": "bob@example.com", "role": "user"},
    3: {"id": 3, "name": "Carol White", "email": "carol@example.com", "role": "user"},
}

# ============================================================================
# RESOURCES - Data sources the AI can access
# ============================================================================

@app.list_resources()
async def list_resources() -> list[Resource]:
    """
    List available resources.
    Resources represent data sources (files, databases, APIs).
    """
    logger.info("Listing resources")
    return [
        Resource(
            uri="db://users",
            name="Users Database",
            description="Access to user records",
            mimeType="application/json",
        ),
        Resource(
            uri="config://server",
            name="Server Configuration",
            description="Current server configuration settings",
            mimeType="application/json",
        ),
    ]


@app.read_resource()
async def read_resource(uri: str) -> list[TextContent]:
    """
    Read a specific resource by URI.
    Implements resource-based access pattern.
    """
    logger.info(f"Reading resource: {uri}")
    
    if uri == "db://users":
        content = json.dumps(list(USERS_DB.values()), indent=2)
        return [TextContent(type="text", text=content)]
    
    elif uri == "config://server":
        config = {
            "version": "1.0.0",
            "max_connections": 100,
            "timeout_seconds": 30,
            "features": ["user_management", "analytics", "reporting"]
        }
        content = json.dumps(config, indent=2)
        return [TextContent(type="text", text=content)]
    
    else:
        raise ValueError(f"Unknown resource: {uri}")


# ============================================================================
# TOOLS - Functions the AI can invoke
# ============================================================================

@app.list_tools()
async def list_tools() -> list[Tool]:
    """
    List all available tools with their schemas.
    This is crucial for AI to understand what actions it can take.
    """
    logger.info("Listing tools")
    return [
        Tool(
            name="get_user",
            description="Retrieve a user by ID from the database",
            inputSchema={
                "type": "object",
                "properties": {
                    "user_id": {
                        "type": "integer",
                        "description": "The unique identifier of the user"
                    }
                },
                "required": ["user_id"]
            }
        ),
        Tool(
            name="search_users",
            description="Search users by name or email (supports partial matching)",
            inputSchema={
                "type": "object",
                "properties": {
                    "query": {
                        "type": "string",
                        "description": "Search term to match against name or email"
                    },
                    "limit": {
                        "type": "integer",
                        "description": "Maximum number of results to return",
                        "default": 10
                    }
                },
                "required": ["query"]
            }
        ),
        Tool(
            name="create_user",
            description="Create a new user in the database",
            inputSchema={
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "description": "Full name of the user"
                    },
                    "email": {
                        "type": "string",
                        "description": "Email address (must be valid format)"
                    },
                    "role": {
                        "type": "string",
                        "enum": ["admin", "user", "guest"],
                        "description": "User role for access control"
                    }
                },
                "required": ["name", "email", "role"]
            }
        ),
        Tool(
            name="calculate_stats",
            description="Calculate statistics about users (async operation example)",
            inputSchema={
                "type": "object",
                "properties": {
                    "metric": {
                        "type": "string",
                        "enum": ["total_users", "users_by_role", "activity_summary"],
                        "description": "Type of statistics to calculate"
                    }
                },
                "required": ["metric"]
            }
        ),
        Tool(
            name="validate_email",
            description="Validate if an email address is properly formatted",
            inputSchema={
                "type": "object",
                "properties": {
                    "email": {
                        "type": "string",
                        "description": "Email address to validate"
                    }
                },
                "required": ["email"]
            }
        ),
    ]


# ============================================================================
# TOOL IMPLEMENTATIONS
# ============================================================================

def validate_email_format(email: str) -> bool:
    """Basic email validation (use email-validator in production)"""
    import re
    pattern = r'^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$'
    return bool(re.match(pattern, email))


async def get_user_handler(user_id: int) -> dict:
    """
    Get user by ID with error handling.
    Demonstrates: Input validation, error handling, logging
    """
    logger.info(f"Getting user with ID: {user_id}")
    
    # Validation
    if not isinstance(user_id, int) or user_id < 1:
        raise ValueError("user_id must be a positive integer")
    
    # Database lookup
    user = USERS_DB.get(user_id)
    if not user:
        raise ValueError(f"User with ID {user_id} not found")
    
    return {
        "status": "success",
        "user": user,
        "retrieved_at": datetime.utcnow().isoformat()
    }


async def search_users_handler(query: str, limit: int = 10) -> dict:
    """
    Search users with pagination.
    Demonstrates: Search logic, pagination, case-insensitive matching
    """
    logger.info(f"Searching users with query: '{query}', limit: {limit}")
    
    query_lower = query.lower()
    results = []
    
    for user in USERS_DB.values():
        if (query_lower in user["name"].lower() or 
            query_lower in user["email"].lower()):
            results.append(user)
            
            if len(results) >= limit:
                break
    
    return {
        "status": "success",
        "query": query,
        "count": len(results),
        "limit": limit,
        "results": results
    }


async def create_user_handler(name: str, email: str, role: str) -> dict:
    """
    Create new user with validation.
    Demonstrates: Data validation, idempotency, state modification
    """
    logger.info(f"Creating user: {name} ({email})")
    
    # Validation
    if not name or len(name.strip()) < 2:
        raise ValueError("Name must be at least 2 characters")
    
    if not validate_email_format(email):
        raise ValueError(f"Invalid email format: {email}")
    
    if role not in ["admin", "user", "guest"]:
        raise ValueError(f"Invalid role: {role}. Must be admin, user, or guest")
    
    # Check for duplicate email (idempotency)
    for user in USERS_DB.values():
        if user["email"].lower() == email.lower():
            logger.warning(f"User with email {email} already exists")
            return {
                "status": "exists",
                "message": "User with this email already exists",
                "user": user
            }
    
    # Create new user
    new_id = max(USERS_DB.keys()) + 1 if USERS_DB else 1
    new_user = {
        "id": new_id,
        "name": name.strip(),
        "email": email.lower(),
        "role": role
    }
    
    USERS_DB[new_id] = new_user
    logger.info(f"User created successfully with ID: {new_id}")
    
    return {
        "status": "created",
        "user": new_user,
        "created_at": datetime.utcnow().isoformat()
    }


async def calculate_stats_handler(metric: str) -> dict:
    """
    Calculate statistics (async operation simulation).
    Demonstrates: Async operations, aggregations, data analysis
    """
    logger.info(f"Calculating stats for metric: {metric}")
    
    # Simulate async processing delay
    await asyncio.sleep(0.5)
    
    if metric == "total_users":
        return {
            "status": "success",
            "metric": "total_users",
            "value": len(USERS_DB),
            "calculated_at": datetime.utcnow().isoformat()
        }
    
    elif metric == "users_by_role":
        role_counts = {}
        for user in USERS_DB.values():
            role = user["role"]
            role_counts[role] = role_counts.get(role, 0) + 1
        
        return {
            "status": "success",
            "metric": "users_by_role",
            "breakdown": role_counts,
            "calculated_at": datetime.utcnow().isoformat()
        }
    
    elif metric == "activity_summary":
        return {
            "status": "success",
            "metric": "activity_summary",
            "summary": {
                "total_users": len(USERS_DB),
                "admin_users": sum(1 for u in USERS_DB.values() if u["role"] == "admin"),
                "regular_users": sum(1 for u in USERS_DB.values() if u["role"] == "user"),
                "guest_users": sum(1 for u in USERS_DB.values() if u["role"] == "guest"),
            },
            "calculated_at": datetime.utcnow().isoformat()
        }
    
    else:
        raise ValueError(f"Unknown metric: {metric}")


async def validate_email_handler(email: str) -> dict:
    """
    Validate email format.
    Demonstrates: Validation utilities, simple tool pattern
    """
    logger.info(f"Validating email: {email}")
    
    is_valid = validate_email_format(email)
    
    return {
        "status": "success",
        "email": email,
        "is_valid": is_valid,
        "message": "Valid email format" if is_valid else "Invalid email format"
    }


# ============================================================================
# TOOL EXECUTION ROUTER
# ============================================================================

@app.call_tool()
async def call_tool(name: str, arguments: Any) -> Sequence[TextContent]:
    """
    Main tool execution router.
    Demonstrates: Error handling, logging, response formatting
    
    This is the entry point for all tool calls from the AI.
    """
    logger.info(f"Tool called: {name} with arguments: {arguments}")
    
    try:
        # Route to appropriate handler
        if name == "get_user":
            result = await get_user_handler(arguments["user_id"])
        
        elif name == "search_users":
            result = await search_users_handler(
                arguments["query"],
                arguments.get("limit", 10)
            )
        
        elif name == "create_user":
            result = await create_user_handler(
                arguments["name"],
                arguments["email"],
                arguments["role"]
            )
        
        elif name == "calculate_stats":
            result = await calculate_stats_handler(arguments["metric"])
        
        elif name == "validate_email":
            result = await validate_email_handler(arguments["email"])
        
        else:
            raise ValueError(f"Unknown tool: {name}")
        
        # Return formatted response
        return [TextContent(
            type="text",
            text=json.dumps(result, indent=2)
        )]
    
    except Exception as e:
        logger.error(f"Error executing tool {name}: {str(e)}", exc_info=True)
        
        # Return structured error
        error_response = {
            "status": "error",
            "error": {
                "type": type(e).__name__,
                "message": str(e),
                "tool": name,
                "arguments": arguments
            }
        }
        
        return [TextContent(
            type="text",
            text=json.dumps(error_response, indent=2)
        )]


# ============================================================================
# SERVER LIFECYCLE
# ============================================================================

async def run():
    """
    Main server entry point.
    Uses stdio transport for local development/testing.
    """
    logger.info("Starting MCP Server...")
    
    async with stdio_server() as (read_stream, write_stream):
        logger.info("Server ready and listening on stdio")
        await app.run(
            read_stream,
            write_stream,
            app.create_initialization_options()
        )


# ============================================================================
# ENTRY POINT
# ============================================================================

if __name__ == "__main__":
    logger.info("=" * 60)
    logger.info("MCP Server Example - Principal Engineer Demo")
    logger.info("=" * 60)
    asyncio.run(run())

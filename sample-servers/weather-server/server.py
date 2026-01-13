"""
Sample MCP Weather Server for Testing
=====================================
A simple MCP server that provides weather-related tools.
Used to test the MCP Jurisdiction scanning and registration flow.
"""

import asyncio
import json
import logging
from datetime import datetime
from typing import Any

from mcp.server import Server
from mcp.server.sse import SseServerTransport
from mcp.types import Tool, TextContent
from starlette.applications import Starlette
from starlette.routing import Route
from starlette.responses import JSONResponse
import uvicorn

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Create MCP server
app = Server("weather-server")

# Mock weather data
WEATHER_DATA = {
    "new york": {"temp": 72, "condition": "Partly Cloudy", "humidity": 65},
    "london": {"temp": 58, "condition": "Rainy", "humidity": 80},
    "tokyo": {"temp": 68, "condition": "Clear", "humidity": 55},
    "sydney": {"temp": 82, "condition": "Sunny", "humidity": 45},
    "paris": {"temp": 62, "condition": "Overcast", "humidity": 70},
}


@app.list_tools()
async def list_tools() -> list[Tool]:
    """List available weather tools."""
    return [
        Tool(
            name="get_weather",
            description="Get current weather for a city",
            inputSchema={
                "type": "object",
                "properties": {
                    "city": {
                        "type": "string",
                        "description": "City name (e.g., 'New York', 'London')"
                    }
                },
                "required": ["city"]
            }
        ),
        Tool(
            name="get_forecast",
            description="Get 5-day weather forecast for a city",
            inputSchema={
                "type": "object",
                "properties": {
                    "city": {
                        "type": "string",
                        "description": "City name"
                    },
                    "days": {
                        "type": "integer",
                        "description": "Number of days (1-5)",
                        "default": 5
                    }
                },
                "required": ["city"]
            }
        ),
        Tool(
            name="get_alerts",
            description="Get weather alerts for a region",
            inputSchema={
                "type": "object",
                "properties": {
                    "region": {
                        "type": "string",
                        "description": "Region or state code"
                    }
                },
                "required": ["region"]
            }
        )
    ]


@app.call_tool()
async def call_tool(name: str, arguments: dict[str, Any]) -> list[TextContent]:
    """Handle tool calls."""
    
    if name == "get_weather":
        city = arguments.get("city", "").lower()
        weather = WEATHER_DATA.get(city, {
            "temp": 70,
            "condition": "Unknown",
            "humidity": 50
        })
        
        result = {
            "city": arguments.get("city"),
            "temperature": weather["temp"],
            "condition": weather["condition"],
            "humidity": weather["humidity"],
            "unit": "fahrenheit",
            "timestamp": datetime.now().isoformat()
        }
        
        return [TextContent(type="text", text=json.dumps(result, indent=2))]
    
    elif name == "get_forecast":
        city = arguments.get("city", "")
        days = min(arguments.get("days", 5), 5)
        
        forecast = []
        base_temp = WEATHER_DATA.get(city.lower(), {"temp": 70})["temp"]
        
        for i in range(days):
            forecast.append({
                "day": i + 1,
                "high": base_temp + 5 - i,
                "low": base_temp - 10 + i,
                "condition": ["Sunny", "Cloudy", "Rainy", "Clear", "Windy"][i % 5]
            })
        
        result = {
            "city": city,
            "forecast": forecast
        }
        
        return [TextContent(type="text", text=json.dumps(result, indent=2))]
    
    elif name == "get_alerts":
        region = arguments.get("region", "")
        
        # Mock alerts
        alerts = []
        if region.lower() in ["fl", "florida", "tx", "texas"]:
            alerts.append({
                "type": "Hurricane Watch",
                "severity": "high",
                "message": "Hurricane conditions possible within 48 hours"
            })
        
        result = {
            "region": region,
            "alerts": alerts,
            "count": len(alerts)
        }
        
        return [TextContent(type="text", text=json.dumps(result, indent=2))]
    
    else:
        return [TextContent(type="text", text=f"Unknown tool: {name}")]


# SSE Transport setup
async def handle_sse(request):
    """Handle SSE connections for MCP."""
    transport = SseServerTransport("/messages")
    async with transport.connect_sse(
        request.scope,
        request.receive,
        request._send
    ) as streams:
        await app.run(
            streams[0],
            streams[1],
            app.create_initialization_options()
        )


async def handle_messages(request):
    """Handle POST messages for SSE transport."""
    transport = SseServerTransport("/messages")
    await transport.handle_post_message(
        request.scope,
        request.receive,
        request._send
    )


async def health(request):
    """Health check endpoint."""
    return JSONResponse({"status": "healthy", "server": "weather-server"})


# Starlette app
starlette_app = Starlette(
    routes=[
        Route("/health", health),
        Route("/sse", handle_sse),
        Route("/messages", handle_messages, methods=["POST"]),
    ]
)


if __name__ == "__main__":
    import os
    port = int(os.environ.get("PORT", 3001))
    logger.info(f"Starting Weather MCP Server on port {port}")
    uvicorn.run(starlette_app, host="0.0.0.0", port=port)

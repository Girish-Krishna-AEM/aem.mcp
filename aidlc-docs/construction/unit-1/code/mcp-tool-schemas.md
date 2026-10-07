# MCP Tool Schemas — Lightning MCP Server

## Tool 1: get_lightning_strikes_near_location

**Description**: Get lightning strikes near a specific geographic location within a specified radius.

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "latitude": {
      "type": "number",
      "description": "Latitude of the location (WGS84 decimal degrees)"
    },
    "longitude": {
      "type": "number",
      "description": "Longitude of the location (WGS84 decimal degrees)"
    },
    "radius": {
      "type": "number",
      "description": "Search radius (positive number)"
    },
    "radiusUnit": {
      "type": "string",
      "description": "Unit of radius measurement",
      "enum": ["km", "miles"],
      "default": "km"
    }
  },
  "required": ["latitude", "longitude", "radius"]
}
```

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "strikeCount": {
      "type": "integer",
      "description": "Total number of strikes detected"
    },
    "strikes": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "latitude": { "type": "number" },
          "longitude": { "type": "number" },
          "intensity": { "type": "number", "description": "Lightning intensity (arbitrary units)" },
          "timestamp": { "type": "string", "format": "date-time" }
        }
      }
    },
    "nearestStrikeDistance": {
      "type": "number",
      "description": "Distance to nearest strike in kilometers"
    }
  }
}
```

### Example Request

```json
{
  "method": "tools/call",
  "params": {
    "name": "get_lightning_strikes_near_location",
    "arguments": {
      "latitude": 30.2672,
      "longitude": -97.7431,
      "radius": 50,
      "radiusUnit": "km"
    }
  }
}
```

### Example Response

```json
{
  "result": {
    "strikeCount": 8,
    "strikes": [
      {
        "latitude": 30.2845,
        "longitude": -97.7203,
        "intensity": 3250.5,
        "timestamp": "2026-10-04T14:45:00Z"
      },
      {
        "latitude": 30.2511,
        "longitude": -97.7589,
        "intensity": 2840.2,
        "timestamp": "2026-10-04T14:38:00Z"
      }
    ],
    "nearestStrikeDistance": 3.45
  }
}
```

---

## Tool 2: get_weather_forecast

**Description**: Get weather forecast for a location with lightning risk assessment.

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "latitude": {
      "type": "number",
      "description": "Latitude of the location"
    },
    "longitude": {
      "type": "number",
      "description": "Longitude of the location"
    },
    "forecastType": {
      "type": "string",
      "description": "Type of forecast",
      "enum": ["daily", "15-day"],
      "default": "daily"
    }
  },
  "required": ["latitude", "longitude"]
}
```

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "forecast": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "date": { "type": "string", "format": "date" },
          "condition": { "type": "string", "description": "Weather condition (e.g., Sunny, Stormy)" },
          "temp": { "type": "integer", "description": "Temperature in Celsius" },
          "precipitationPercent": { "type": "integer", "description": "Precipitation probability (0-100)" },
          "lightningRisk": { "type": "integer", "description": "Lightning risk level (0-100)" }
        }
      }
    }
  }
}
```

### Example Request

```json
{
  "method": "tools/call",
  "params": {
    "name": "get_weather_forecast",
    "arguments": {
      "latitude": 30.2672,
      "longitude": -97.7431,
      "forecastType": "15-day"
    }
  }
}
```

### Example Response (truncated)

```json
{
  "result": {
    "forecast": [
      {
        "date": "2026-10-05",
        "condition": "Partly Cloudy",
        "temp": 28,
        "precipitationPercent": 15,
        "lightningRisk": 8
      },
      {
        "date": "2026-10-06",
        "condition": "Stormy",
        "temp": 22,
        "precipitationPercent": 85,
        "lightningRisk": 72
      }
    ]
  }
}
```

---

## Tool 3: get_sensor_diagnostics

**Description**: Get health and performance diagnostics for a lightning detection sensor.

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "sensorId": {
      "type": "string",
      "description": "Unique identifier of the sensor"
    }
  },
  "required": ["sensorId"]
}
```

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "detectionEfficiency": { "type": "number", "description": "Detection efficiency percentage (0-100)" },
    "gpsVisibility": { "type": "number", "description": "GPS signal visibility percentage" },
    "trackedSatellites": { "type": "integer", "description": "Number of tracked GPS satellites" },
    "noiseLevel": { "type": "number", "description": "Electromagnetic noise level" },
    "uptime": { "type": "number", "description": "System uptime percentage" },
    "lastCalibration": { "type": "string", "format": "date", "description": "Last calibration date" },
    "snr": { "type": "number", "description": "Signal-to-noise ratio" },
    "status": { "type": "string", "enum": ["healthy", "degraded"], "description": "Overall sensor status" }
  }
}
```

### Example Request

```json
{
  "method": "tools/call",
  "params": {
    "name": "get_sensor_diagnostics",
    "arguments": {
      "sensorId": "SENSOR-TX-001"
    }
  }
}
```

### Example Response

```json
{
  "result": {
    "detectionEfficiency": 94.2,
    "gpsVisibility": 98.5,
    "trackedSatellites": 18,
    "noiseLevel": 1.2,
    "uptime": 99.7,
    "lastCalibration": "2026-09-28",
    "snr": 28.5,
    "status": "healthy"
  }
}
```

---

## Tool 4: get_informer_status

**Description**: Get status of a warning informer device (strobe light, siren, horn, or hybrid).

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "informerId": {
      "type": "string",
      "description": "Unique identifier of the informer device"
    },
    "zone": {
      "type": "string",
      "description": "Zone identifier"
    }
  },
  "additionalProperties": false,
  "minProperties": 1,
  "description": "At least one of informerId or zone must be provided"
}
```

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "status": { "type": "string", "enum": ["active", "inactive"], "description": "Current operational status" },
    "lastActivation": { "type": "string", "format": "date-time", "description": "Timestamp of last activation" },
    "powerStatus": { "type": "string", "enum": ["normal", "low"], "description": "Power level status" },
    "zone": { "type": "string", "description": "Assigned zone" },
    "deviceType": { "type": "string", "enum": ["Strobe", "Horn", "Siren", "Hybrid"], "description": "Type of informer device" }
  }
}
```

### Example Request (by informer ID)

```json
{
  "method": "tools/call",
  "params": {
    "name": "get_informer_status",
    "arguments": {
      "informerId": "INFORM-001"
    }
  }
}
```

### Example Response

```json
{
  "result": {
    "status": "active",
    "lastActivation": "2026-10-04T14:25:00Z",
    "powerStatus": "normal",
    "zone": "Zone-5",
    "deviceType": "Hybrid"
  }
}
```

---

## Common Error Responses

### 400 Bad Request

```json
{
  "error": "Missing required field: latitude"
}
```

### 404 Not Found (Unknown Tool)

```json
{
  "error": "Unknown tool: unknown_method"
}
```

### 500 Internal Server Error

```json
{
  "error": "Unexpected error during tool execution"
}
```

---

## Deterministic Data Characteristics

All tools generate **deterministic stub data**:
- Same input parameters → identical output across multiple calls
- Seeded by input (location coordinates, IDs, etc.)
- Enables reproducible testing and validation
- Phase 2: Can be replaced with real data sources without API changes

---

**Last Updated**: 2026-10-04

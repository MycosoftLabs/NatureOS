# NatureOS - Earth Systems Simulation Platform

> **Version**: 2.0.0  
> **Last Updated**: 2026-01-15T14:30:00Z

## Overview

NatureOS is Mycosoft's comprehensive Earth systems simulation platform that provides:

- **Live Map** - Real-time global environmental monitoring
- **Earth Simulator** - Physics-based environmental simulation
- **AI Studio** - Machine learning model training
- **Monitoring** - System health and metrics
- **Workflows** - Automated data processing pipelines

## 🌍 Features

### Live Map
Real-time visualization of:
- Weather patterns
- Seismic activity
- Air quality
- Species observations
- Satellite imagery

### Earth Simulator
Physics-based simulations:
- Weather systems
- Geospatial data
- Magnetic fields
- Tectonic activity
- Biological interactions
- Chemical processes

### Data Sources
- NOAA Weather
- USGS Earthquakes
- NASA EONET
- CelesTrak Satellites
- OpenSky Aircraft
- AISstream Vessels

## 🔗 Website Integration

NatureOS is accessible via the Mycosoft Website at:
- `/natureos` - Main dashboard
- `/natureos/live-map` - Real-time map
- `/natureos/monitoring` - System metrics
- `/natureos/mindex` - MINDEX integration

## 📡 API Endpoints

| Endpoint | Description |
|----------|-------------|
| `/api/natureos/global-events` | Aggregated global events |
| `/api/natureos/weather` | Weather data |
| `/api/earth-simulator/*` | Simulation endpoints |

## 🔧 Configuration

NatureOS Core API is a standalone .NET 8 service deployed to Azure.
The Mycosoft Website (separate repo) integrates with NatureOS via its REST and SignalR APIs.

Required environment variables:
```env
NEXT_PUBLIC_GOOGLE_MAPS_API_KEY=...
NASA_API_KEY=...
NOAA_API_KEY=...
```

## 📚 Documentation

- System Architecture — see [MycosoftLabs/website](https://github.com/MycosoftLabs/website) repo (`docs/SYSTEM_ARCHITECTURE.md`)
- [Mycosoft Integration](./docs/mycosoft-integration.md)
- [MycoBrain Integration](./docs/mycobrain-integration.md)
- [Frontend Integration Guide](./docs/frontend-integration-guide.md)

## 📝 Changelog

### 2026-01-15
- Integrated with CREP dashboard
- Added real-time event streaming
- Enhanced data collector redundancy
- Added containerized data collectors (aviation, maritime, satellite)
- Implemented geocoding pipeline for MINDEX observations
- Added Carbon Mapper and OpenRailwayMap integrations
- Enhanced trajectory visualization with animated paths

## 📜 License

Copyright © 2026 Mycosoft. All rights reserved.

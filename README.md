# NatureOS - Earth Systems Simulation Platform


> **Proprietary — Mycosoft, Inc.** Authorized use only. See [LICENSE](./LICENSE) and [NOTICE](./NOTICE). U.S. defense/government and export-control terms may apply.
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

NatureOS runs as part of the Mycosoft Website (port 3000).

Required environment variables:
```env
NEXT_PUBLIC_GOOGLE_MAPS_API_KEY=...
NASA_API_KEY=...
NOAA_API_KEY=...
```

## 📚 Documentation

- [System Architecture](../../WEBSITE/website/docs/SYSTEM_ARCHITECTURE.md)
- [Integration Guide](./docs/INTEGRATION_GUIDE.md)

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

---

## License and export control

**Proprietary — Mycosoft, Inc. All Rights Reserved.**

This repository is proprietary software. No use, copy, modification, distribution,
or disclosure is permitted without **prior written authorization** from Mycosoft, Inc.

- See [LICENSE](./LICENSE) and [NOTICE](./NOTICE) in this repository.
- Portions may relate to U.S. defense, government, marine, acoustic, or environmental
  sensing use cases subject to applicable law, including **EAR** and potentially **ITAR**
  export controls. This repository is **not** marked as ITAR-classified unless explicitly
  labeled elsewhere.
- Mycosoft aligns engineering and security practices with **NIST** cybersecurity
  frameworks and **CMMC**-oriented controls at the organizational level; no certification
  is claimed by presence of this notice alone.
- U.S. Department of Defense and government use is subject to applicable federal law
  and contract terms.

**Contact:** legal@mycosoft.org


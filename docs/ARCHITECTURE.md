# Architecture (Draft)

## Runtime
- Windows Core Service: discovery orchestration, identity stitching, event engine
- UI: local web UI (Electron wrapper optional)
- Optional Kali agent: passive observation and validation

## Data Flow
1) Windows enumerates interfaces and subnets
2) Connectors pull router/controller data
3) Active and passive discovery runs locally
4) Optional Kali agent streams observations
5) Identity resolver merges observations into devices
6) Event engine produces join/leave/change events

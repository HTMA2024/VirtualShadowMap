[中文版本 (Chinese Version)](README_CN.md)

# Unity Virtual Shadow Maps

A virtual shadow map system for Unity Built-in Render Pipeline, using virtual texture technology for efficient dynamic shadow streaming.

## Showcase

CSM | VSM

![compare](Images/compare.gif)

## System Architecture

### Core Components

- **VirtualShadowMaps** — Attached to directional light; manages virtual texture page table, Shadow Atlas, and shadow rendering
- **VirtualShadowCamera** — Attached to camera; handles page requests and LOD management based on view frustum
- **VirtualShadowVolume** — Defines shadow coverage area; collects shadow casters in the scene
- **VirtualShadowDebug** — Provides split-screen comparison and debug visualization

### Technical Features

- Virtual texture page table system with 8-level Mipmap LOD
- Distance and frustum-based dynamic page request algorithm
- LRU cache management, maximizing tile reuse
- Supports both pre-baked static shadows and runtime dynamic rendering
- PCSS soft shadow filtering (26-point Poisson sampling)
- Frame-distributed async loading to avoid single-frame stalls

## Rendering Pipeline

```
Page Request Phase:
  VirtualShadowCamera.UpdateRequest → Distance estimation → Frustum culling
  → View angle filtering → Generate request queue → Update IndirectionTexture

Shadow Rendering Phase:
  Pre-bake mode: Load EXR from disk → Copy to Shadow Atlas
  Runtime mode: VirtualShadowMapBaker → Render depth → Copy to Shadow Atlas

Shadow Sampling Phase:
  World position → IndirectionTexture lookup → Get tile info
  → Transform to Shadow Atlas coordinates → PCF/PCSS sampling → Shadow value
```

## Key Technical Details

### Virtual Texture System

- **IndirectionTexture (page table):** 256×256 ARGBHalf texture storing (TileX, TileY, MipLevel, MipScale)
- **Shadow Atlas:** Dynamic tile pool managed via LRU algorithm with limited memory slots
- **TileIndexTable:** Multi-level page table structure with fast lookup and nearest-neighbor fallback

### Page Selection Algorithm

Triple filtering mechanism:

1. **Distance estimation** — `estimate = distance² × cellSize²`; near regions load high-precision pages
2. **Frustum overlap** — Only loads regions overlapping the camera's field of view
3. **View angle** — Pre-loads regions the camera is about to see

Coarsest levels (Level 7-8) are loaded unconditionally to ensure baseline shadow coverage at distance.

### Performance Optimizations

- GPU Instancing for IndirectionTexture drawing — single DrawCall handles all pages
- Per-frame max request limit (`maxPageRequestLimit`) for smooth loading curves
- Camera movement detection — skips unnecessary updates when stationary
- Separate CommandBuffer rendering to avoid conflicts with Unity's built-in shadows

### Editor Tools

- Modern Inspector UI with Low/Medium/High/Ultra quality presets
- Setup Wizard for one-click scene component configuration
- Real-time stats panel showing active pages, requests, and memory usage
- Texture preview and debug visualization

## Directory Structure

```
Assets/
├── Scripts/
│   ├── VirtualShadowMaps.cs          Core manager: page table and rendering
│   ├── VirtualShadowCamera.cs        Camera-side page request logic
│   ├── VirtualShadowMapBaker.cs      Runtime shadow baker
│   ├── StreamingTile/                Virtual texture infrastructure
│   │   ├── TileIndexTable.cs         Multi-level page table data structure
│   │   ├── IndirectionTexture.cs     Page table texture management
│   │   ├── MosaicTexture.cs          Shadow Atlas management
│   │   └── EvictionCache.cs          LRU cache implementation
│   └── ...
│
├── Shaders/
│   ├── CascadedOcclusionMaps.hlsl    Core shadow sampling functions
│   ├── AdaptiveRendering/
│   │   ├── DrawIndirection.shader    IndirectionTexture drawing
│   │   └── DrawMosaic.shader         Shadow Atlas update
│   └── Internal-DeferredShading.shader  Deferred rendering integration
│
├── Editor/
│   ├── VirtualShadowMapsInspector.cs      Main Inspector + bake tools
│   ├── VirtualShadowCameraInspector.cs    Camera Inspector
│   ├── VirtualShadowMapSetupWizard.cs     Setup wizard
│   └── ...
│
└── Bistro/                           Amazon Lumberyard Bistro test scene
```

## Requirements & Setup

- Unity 2022.3+, Windows/Mac/Linux
- Built-in Render Pipeline (URP/HDRP not supported)

### Quick Start

1. Open menu Window > Virtual Shadow Maps > Setup Wizard
2. Select the Directional Light and main camera in the scene
3. Choose a quality preset (Medium recommended to start)
4. Click "Setup Virtual Shadow Maps" to complete configuration
5. Adjust parameters in the VirtualShadowMaps Inspector:
   - Max Tile Pool: Controls memory usage (64-256)
   - Bias / Normal Bias: Adjust shadow offset to eliminate acne
   - PCSS Filter: Enable soft shadows
6. Adjust Level of Detail in VirtualShadowCamera Inspector (default 5.0)
7. Optional: Use Baking Tools to pre-bake static shadows

## Notes

- This project modifies `Internal-DeferredShading.shader`; manual merge required when upgrading Unity
- Runtime mode has higher performance overhead; pre-baking recommended for static scenes
- `VirtualShadowVolume.Collect()` scans all Renderers in the scene, which may be slow in large scenes
- Maximum 256×256 page table (65,536 virtual pages); adjust `pageSize` for very large scenes

## Technical Documentation

- `Assets/Editor/LOOKUP_TEXTURE_DESIGN.md` — IndirectionTexture design principles
- `Assets/Editor/BAKING_GUIDE_CN.md` — Complete shadow baking guide

## License

This project contains Bistro scene assets under the following license:
- Bistro scene: © 2017 Amazon Lumberyard

See `Assets/Bistro/Bistro_v5_2/LICENSE.txt` for full copyright information.

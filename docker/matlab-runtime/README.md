# MATLAB Runtime Docker Image

For NatureOS MATLAB integration when deploying without full MATLAB installation.

## Build

From NatureOS repo root:

```bash
docker build -t natureos-matlab-runtime:r2024a -f docker/matlab-runtime/Dockerfile .
```

## Usage

- **MATLAB Production Server**: Deploy compiled MATLAB functions and expose REST endpoints on port 9910.
- **Script execution**: Mount this image's `/opt/natureos/matlab` and call via MATLAB Engine API from .NET.

## Dependencies

- MATLAB R2024a or later (for compiling to Production Server)
- Statistics and Machine Learning Toolbox
- Deep Learning Toolbox
- Signal Processing Toolbox
- Image Processing Toolbox
- Bioinformatics Toolbox (genomics)

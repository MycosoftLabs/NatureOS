# NatureOS MATLAB Runtime - Feb 19, 2026
# For deploying MATLAB scripts without full MATLAB installation.
# Requires: MATLAB R2024a or later for building compiled artifacts.
# Use with MATLAB Production Server or compiled .NET assemblies.

FROM mathworks/matlab-runtime:R2024a

WORKDIR /opt/natureos

# Copy MATLAB scripts (preprocessing, models, lab, hardware, genomics, chem)
COPY matlab/ /opt/natureos/matlab/

# Production Server runs on 9910 when deployed
EXPOSE 9910

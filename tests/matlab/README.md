# MATLAB Tests for NatureOS

Run MATLAB unit tests when MATLAB R2024a+ is installed.

## Prerequisites

- MATLAB R2024a or later
- Statistics and Machine Learning Toolbox
- Image Processing Toolbox (for fungalClassifier tests)

## Run tests

From MATLAB:

```matlab
cd('matlab')
results = runtests('tests/matlab')
```

Or from command line with MATLAB installed:

```bash
matlab -batch "runtests('tests/matlab')"
```

## CI/CD

To run in GitHub Actions, use MathWorks MATLAB Actions:

```yaml
- uses: matlab-actions/setup-matlab@v2
- uses: matlab-actions/run-tests@v2
  with:
    select-by-folder: tests/matlab
```

Requires MATLAB license for GitHub Actions runners.

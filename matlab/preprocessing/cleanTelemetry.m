function [cleanedData, outlierIndices] = cleanTelemetry(rawData, varargin)
% cleanTelemetry - Clean sensor telemetry: interpolate missing data, remove outliers
%
%   [cleanedData, outlierIndices] = cleanTelemetry(rawData)
%   [cleanedData, outlierIndices] = cleanTelemetry(rawData, 'Method', 'linear')
%   [cleanedData, outlierIndices] = cleanTelemetry(rawData, 'ZScoreThreshold', 3)
%
% Inputs:
%   rawData - Nx1 or 1xN vector of sensor readings (may contain NaN/Inf)
%
% Optional name-value pairs:
%   Method - Interpolation method for missing values: 'linear', 'nearest', 'spline'
%   ZScoreThreshold - Threshold for outlier detection (default: 3)
%
% Outputs:
%   cleanedData - Cleaned vector with same size as rawData
%   outlierIndices - Logical array of detected outlier indices

p = inputParser;
addRequired(p, 'rawData', @isnumeric);
addParameter(p, 'Method', 'linear', @ischar);
addParameter(p, 'ZScoreThreshold', 3, @(x) isnumeric(x) && x > 0);
parse(p, rawData, varargin{:});

data = rawData(:);
validIdx = ~isnan(data) & ~isinf(data);
n = numel(data);

% Interpolate missing values
if any(~validIdx)
    x = find(validIdx);
    v = data(validIdx);
    xi = (1:n)';
    data = interp1(x, v, xi, p.Results.Method, 'extrap');
end

% Outlier detection using z-score
mu = mean(data);
sigma = std(data);
if sigma < eps
    sigma = 1;
end
z = abs((data - mu) / sigma);
outlierIndices = z > p.Results.ZScoreThreshold;

% Replace outliers with interpolated values from neighbors
if any(outlierIndices)
    data(outlierIndices) = interp1(find(~outlierIndices), data(~outlierIndices), ...
        find(outlierIndices), 'linear', 'extrap');
end

cleanedData = reshape(data, size(rawData));
outlierIndices = reshape(outlierIndices, size(rawData));

end

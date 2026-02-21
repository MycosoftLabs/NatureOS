function [alignedData, timeAligned] = synchronizeSensors(timeVectors, dataVectors, targetFs, varargin)
% synchronizeSensors - Time-align multi-sensor data to common timestamps
%
%   [alignedData, timeAligned] = synchronizeSensors(timeVectors, dataVectors, targetFs)
%   [alignedData, timeAligned] = synchronizeSensors(..., 'StartTime', t0, 'EndTime', t1)
%
% Inputs:
%   timeVectors - Cell array of time vectors (datetime or numeric seconds)
%   dataVectors - Cell array of corresponding data vectors
%   targetFs - Target sampling frequency (Hz) for output
%
% Optional:
%   StartTime - Start of output time range
%   EndTime - End of output time range
%
% Outputs:
%   alignedData - NxM matrix (N=samples, M=sensors)
%   timeAligned - Nx1 aligned time vector

p = inputParser;
addRequired(p, 'timeVectors', @iscell);
addRequired(p, 'dataVectors', @iscell);
addRequired(p, 'targetFs', @(x) isnumeric(x) && x > 0);
addParameter(p, 'StartTime', [], @(x) isempty(x) || isnumeric(x) || isdatetime(x));
addParameter(p, 'EndTime', [], @(x) isempty(x) || isnumeric(x) || isdatetime(x));
parse(p, timeVectors, dataVectors, targetFs, varargin{:});

% Determine time range
allTimes = cellfun(@(t) t(:), timeVectors, 'UniformOutput', false);
allTimesCat = vertcat(allTimes{:});
if isdatetime(allTimesCat)
    tMin = min(allTimesCat);
    tMax = max(allTimesCat);
else
    tMin = min(allTimesCat);
    tMax = max(allTimesCat);
end

if ~isempty(p.Results.StartTime), tMin = p.Results.StartTime; end
if ~isempty(p.Results.EndTime), tMax = p.Results.EndTime; end

% Build target time grid
dt = 1 / targetFs;
if isdatetime(tMin)
    nSamples = round(seconds(tMax - tMin) / dt) + 1;
    timeAligned = linspace(tMin, tMax, nSamples)';
else
    nSamples = round((tMax - tMin) / dt) + 1;
    timeAligned = linspace(tMin, tMax, nSamples)';
end

nSensors = numel(timeVectors);
alignedData = zeros(numel(timeAligned), nSensors);

for k = 1:nSensors
    t = timeVectors{k}(:);
    d = dataVectors{k}(:);
    if isdatetime(t)
        tNum = datenum(t);
        tAlignNum = datenum(timeAligned);
    else
        tNum = t;
        tAlignNum = timeAligned;
    end
    alignedData(:, k) = interp1(tNum, d, tAlignNum, 'linear', NaN);
end

end

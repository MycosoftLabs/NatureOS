function [resampledData, resampledTime] = resampleTimeSeries(data, time, targetFs, varargin)
% resampleTimeSeries - Resample time-series to uniform sampling rate
%
%   [resampledData, resampledTime] = resampleTimeSeries(data, time, targetFs)
%   [resampledData, resampledTime] = resampleTimeSeries(data, time, targetFs, 'Method', 'resample')
%
% Inputs:
%   data - Nx1 or 1xN vector of readings
%   time - Nx1 or 1xN time vector (seconds or datetime)
%   targetFs - Target sampling frequency (Hz)
%
% Optional:
%   Method - 'interp' (default) or 'resample' (anti-aliasing filter)
%
% Outputs:
%   resampledData - Resampled data vector
%   resampledTime - Corresponding time vector

p = inputParser;
addRequired(p, 'data', @isnumeric);
addRequired(p, 'time', @(x) isnumeric(x) || isdatetime(x));
addRequired(p, 'targetFs', @(x) isnumeric(x) && x > 0);
addParameter(p, 'Method', 'interp', @(x) ismember(x, {'interp', 'resample'}));
parse(p, data, time, targetFs, varargin{:});

data = data(:);
time = time(:);

if isdatetime(time)
    tNum = datenum(time);
    tMin = min(tNum);
    tMax = max(tNum);
else
    tNum = time;
    tMin = min(time);
    tMax = max(time);
end

dt = 1 / targetFs;
nNew = round((tMax - tMin) / dt) + 1;
resampledTimeNum = linspace(tMin, tMax, nNew)';

if strcmpi(p.Results.Method, 'resample')
    % Use MATLAB resample for anti-aliasing
    origFs = 1 / median(diff(tNum));
    [p, q] = rat(targetFs / origFs);
    resampledData = resample(data, p, q);
    nNew = numel(resampledData);
    resampledTimeNum = linspace(tMin, tMin + (nNew - 1) * dt, nNew)';
else
    resampledData = interp1(tNum, data, resampledTimeNum, 'linear', 'extrap');
end

if isdatetime(time)
    resampledTime = datetime(resampledTimeNum, 'ConvertFrom', 'datenum');
else
    resampledTime = resampledTimeNum;
end

end

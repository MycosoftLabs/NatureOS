function [predictions, timestamps] = environmentalForecaster(historicalData, horizonHours, varargin)
% environmentalForecaster - Forecast temperature/humidity for environmental metrics
%
%   [predictions, timestamps] = environmentalForecaster(historicalData, horizonHours)
%   [predictions, timestamps] = environmentalForecaster(historicalData, horizonHours, 'Method', 'arima')
%
% Inputs:
%   historicalData - Nx1 vector of past readings (e.g., hourly)
%   horizonHours - Number of hours to forecast
%
% Optional:
%   Method - 'linear', 'arima', 'movavg'
%
% Outputs:
%   predictions - horizonHours x 1 forecast values
%   timestamps - Corresponding future timestamps (datetime)

p = inputParser;
addRequired(p, 'historicalData', @isnumeric);
addRequired(p, 'horizonHours', @(x) isnumeric(x) && x > 0 && x == round(x));
addParameter(p, 'Method', 'linear', @ischar);
addParameter(p, 'BaseTime', datetime('now'), @(x) isdatetime(x) || isnumeric(x));
parse(p, historicalData, horizonHours, varargin{:});

x = historicalData(:);
n = numel(x);
h = round(p.Results.horizonHours);
baseTime = p.Results.BaseTime;

if isempty(x)
    predictions = nan(h, 1);
    timestamps = baseTime + hours(1:h)';
    return;
end

switch lower(p.Results.Method)
    case 'linear'
        % Simple linear extrapolation
        p_fit = polyfit((1:n)', x, 1);
        futureIdx = (n+1:n+h)';
        predictions = polyval(p_fit, futureIdx);

    case 'movavg'
        % Moving average with trend
        w = min(24, floor(n/2));
        ma = movmean(x, w, 'Endpoints', 'shrink');
        trend = (ma(end) - ma(1)) / max(1, n-1);
        lastVal = ma(end);
        predictions = lastVal + trend * (1:h)';

    case 'arima'
        % ARIMA if Econometrics Toolbox available
        if exist('arima', 'file') && exist('estimate', 'file') && n >= 24
            try
                mdl = arima(2, 1, 2);
                fit = estimate(mdl, x, 'Display', 'off');
                [yF, ~] = forecast(fit, h, 'Y0', x);
                predictions = yF;
            catch
                predictions = repmat(mean(x), h, 1);
            end
        else
            predictions = repmat(mean(x), h, 1);
        end

    otherwise
        predictions = repmat(mean(x), h, 1);
end

if isdatetime(baseTime)
    timestamps = baseTime + hours(1:h)';
else
    timestamps = (1:h)' * 3600;
end

end

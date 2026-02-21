function [isAnomaly, scores] = anomalyDetector(timeSeries, varargin)
% anomalyDetector - Detect anomalies in sensor time-series using statistical/ML methods
%
%   [isAnomaly, scores] = anomalyDetector(timeSeries)
%   [isAnomaly, scores] = anomalyDetector(timeSeries, 'Method', 'zscore', 'Threshold', 3)
%
% Inputs:
%   timeSeries - Nx1 vector of sensor readings
%
% Optional:
%   Method - 'zscore', 'mad', 'isolationforest'
%   Threshold - Detection threshold (default: 3 for z-score)
%
% Outputs:
%   isAnomaly - Nx1 logical array
%   scores - Nx1 anomaly scores (higher = more anomalous)

p = inputParser;
addRequired(p, 'timeSeries', @isnumeric);
addParameter(p, 'Method', 'zscore', @ischar);
addParameter(p, 'Threshold', 3, @(x) isnumeric(x) && x > 0);
parse(p, timeSeries, varargin{:});

x = timeSeries(:);
n = numel(x);

switch lower(p.Results.Method)
    case 'zscore'
        mu = mean(x);
        sigma = std(x);
        if sigma < eps, sigma = 1; end
        scores = abs((x - mu) / sigma);
        isAnomaly = scores > p.Results.Threshold;

    case 'mad'
        % Median absolute deviation - robust to outliers
        med = median(x);
        mad_val = median(abs(x - med));
        if mad_val < eps, mad_val = 1; end
        scores = abs(x - med) / (1.4826 * mad_val);
        isAnomaly = scores > p.Results.Threshold;

    case 'isolationforest'
        % Requires Statistics and Machine Learning Toolbox
        if exist('iforest', 'file')
            [mdl, ~] = iforest(x, 'NumLearners', 100);
            scores = -log1p(-mdl.Score);
            isAnomaly = scores > prctile(scores, 100 - 100/p.Results.Threshold);
        else
            % Fallback to z-score
            [isAnomaly, scores] = anomalyDetector(timeSeries, 'Method', 'zscore', ...
                'Threshold', p.Results.Threshold);
        end

    otherwise
        [isAnomaly, scores] = anomalyDetector(timeSeries, 'Method', 'zscore', ...
            'Threshold', p.Results.Threshold);
end

scores = reshape(scores, size(timeSeries));
isAnomaly = reshape(isAnomaly, size(timeSeries));

end

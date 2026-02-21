function [params, fitCurve, EC50] = fitDoseResponse(dose, response, varargin)
% fitDoseResponse - Fit dose-response curve (sigmoid/IC50/EC50)
%
%   [params, fitCurve, EC50] = fitDoseResponse(dose, response)
%   [params, fitCurve, EC50] = fitDoseResponse(dose, response, 'Top', 100, 'Bottom', 0)
%
% Inputs:
%   dose - Nx1 dose/concentration (log scale recommended)
%   response - Nx1 response (0-100 or normalized)
%
% Optional:
%   Top, Bottom - Asymptotes for 4-parameter fit
%
% Outputs:
%   params - Struct with slope, inflection (logEC50), top, bottom
%   fitCurve - Fitted response values
%   EC50 - EC50/IC50 (dose at 50% response)

p = inputParser;
addRequired(p, 'dose', @isnumeric);
addRequired(p, 'response', @isnumeric);
addParameter(p, 'Top', max(response), @isnumeric);
addParameter(p, 'Bottom', min(response), @isnumeric);
parse(p, dose, response, varargin{:});

d = dose(:);
r = response(:);
n = numel(d);

if n < 4
    params = struct('slope', 1, 'inflection', 0, 'top', p.Results.Top, 'bottom', p.Results.Bottom);
    fitCurve = r;
    EC50 = median(d);
    return;
end

% Log-transform dose if needed
if any(d <= 0)
    d = d + abs(min(d)) + 1e-6;
end
logD = log10(d);

% 4-parameter logistic: R = Bottom + (Top-Bottom)/(1+10^(slope*(logEC50-logD)))
fourPL = @(p, x) p(4) + (p(3) - p(4)) ./ (1 + 10.^(p(1) * (p(2) - x)));
p0 = [1, median(logD), p.Results.Top, p.Results.Bottom];

opts = optimset('Display', 'off');
try
    pFit = lsqcurvefit(fourPL, p0, logD, r, [-inf -inf -inf -inf], [inf inf inf inf], opts);
    fitCurve = fourPL(pFit, logD);
    params = struct('slope', pFit(1), 'inflection', pFit(2), 'top', pFit(3), 'bottom', pFit(4));
    EC50 = 10^pFit(2);
catch
    fitCurve = r;
    params = struct('slope', 1, 'inflection', median(logD), 'top', p.Results.Top, 'bottom', p.Results.Bottom);
    EC50 = 10^median(logD);
end

end

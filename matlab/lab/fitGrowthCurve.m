function [params, fitCurve, R2] = fitGrowthCurve(time, biomass, varargin)
% fitGrowthCurve - Fit bacterial/fungal growth curve (logistic or Gompertz)
%
%   [params, fitCurve, R2] = fitGrowthCurve(time, biomass)
%   [params, fitCurve, R2] = fitGrowthCurve(time, biomass, 'Model', 'logistic')
%
% Inputs:
%   time - Nx1 time vector (hours)
%   biomass - Nx1 biomass/OD values
%
% Optional:
%   Model - 'logistic' or 'gompertz'
%
% Outputs:
%   params - Struct with K (carrying capacity), r (rate), lag
%   fitCurve - Fitted values
%   R2 - Coefficient of determination

p = inputParser;
addRequired(p, 'time', @isnumeric);
addRequired(p, 'biomass', @isnumeric);
addParameter(p, 'Model', 'logistic', @ischar);
parse(p, time, biomass, varargin{:});

t = time(:);
y = biomass(:);
n = numel(t);

if n < 4
    params = struct('K', max(y), 'r', 0, 'lag', 0);
    fitCurve = y;
    R2 = 0;
    return;
end

% Initial guess: K ~ max, r from slope
K0 = max(y) * 1.1;
r0 = (y(end) - y(1)) / (t(end) - t(1) + eps) / K0 * 10;
lag0 = t(1);

switch lower(p.Results.Model)
    case 'logistic'
        % N(t) = K / (1 + exp(-r*(t-lag)))
        logistic = @(p, x) p(1) ./ (1 + exp(-p(2)*(x - p(3))));
        p0 = [K0, max(0.01, r0), lag0];
        opts = optimset('Display', 'off');
        try
            pFit = lsqcurvefit(logistic, p0, t, y, [0 0 -inf], [inf inf inf], opts);
            fitCurve = logistic(pFit, t);
            params = struct('K', pFit(1), 'r', pFit(2), 'lag', pFit(3));
        catch
            fitCurve = y;
            params = struct('K', K0, 'r', r0, 'lag', lag0);
        end

    case 'gompertz'
        % N(t) = K * exp(-exp(-r*(t-lag)))
        gomp = @(p, x) p(1) * exp(-exp(-p(2)*(x - p(3))));
        p0 = [K0, max(0.01, r0), lag0];
        opts = optimset('Display', 'off');
        try
            pFit = lsqcurvefit(gomp, p0, t, y, [0 0 -inf], [inf inf inf], opts);
            fitCurve = gomp(pFit, t);
            params = struct('K', pFit(1), 'r', pFit(2), 'lag', pFit(3));
        catch
            fitCurve = y;
            params = struct('K', K0, 'r', r0, 'lag', lag0);
        end

    otherwise
        fitCurve = y;
        params = struct('K', K0, 'r', r0, 'lag', lag0);
end

% R²
SSres = sum((y - fitCurve).^2);
SStot = sum((y - mean(y)).^2);
R2 = 1 - SSres / max(eps, SStot);

end

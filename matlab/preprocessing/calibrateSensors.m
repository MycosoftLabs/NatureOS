function calibratedData = calibrateSensors(rawData, calibrationCoeffs, varargin)
% calibrateSensors - Apply calibration coefficients to raw sensor readings
%
%   calibratedData = calibrateSensors(rawData, calibrationCoeffs)
%   calibratedData = calibrateSensors(rawData, calibrationCoeffs, 'Model', 'linear')
%
% Inputs:
%   rawData - Nx1 or NxM raw ADC/sensor readings
%   calibrationCoeffs - Struct or vector of calibration parameters
%     For 'linear': [offset, scale] or struct with .offset, .scale
%     For 'poly2': [c0, c1, c2] quadratic
%     For 'custom': function handle calibrationCoeffs.fn(raw)
%
% Optional:
%   Model - 'linear' (default), 'poly2', 'poly3', 'custom'
%
% Output:
%   calibratedData - Calibrated values (physical units)

p = inputParser;
addRequired(p, 'rawData', @isnumeric);
addRequired(p, 'calibrationCoeffs', @(x) isnumeric(x) || isstruct(x));
addParameter(p, 'Model', 'linear', @ischar);
parse(p, rawData, calibrationCoeffs, varargin{:});

data = rawData(:);
coeffs = calibrationCoeffs;

switch lower(p.Results.Model)
    case 'linear'
        if isstruct(coeffs)
            offset = coeffs.offset;
            scale = coeffs.scale;
        else
            offset = coeffs(1);
            scale = coeffs(2);
        end
        calibratedData = offset + scale * data;

    case 'poly2'
        c = coeffs(:);
        calibratedData = c(1) + c(2)*data + c(3)*data.^2;

    case 'poly3'
        c = coeffs(:);
        calibratedData = c(1) + c(2)*data + c(3)*data.^2 + c(4)*data.^3;

    case 'custom'
        if isstruct(coeffs) && isfield(coeffs, 'fn')
            calibratedData = arrayfun(coeffs.fn, data);
        else
            error('calibrateSensors:custom', 'Custom model requires coeffs.fn function handle');
        end

    otherwise
        error('calibrateSensors:model', 'Unknown model: %s', p.Results.Model);
end

calibratedData = reshape(calibratedData, size(rawData));

end

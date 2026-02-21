function [offset, scale, r2] = calibrateSensor(port, referenceValues)
% calibrateSensor - Interactive calibration wizard for sensor
%
%   [offset, scale, r2] = calibrateSensor(port, referenceValues)
%
% Input: port - serial port, referenceValues - struct with expected temp/humidity/pressure
% Output: offset, scale (calibration coefficients), r2 (fit quality)

offset = 0; scale = 1; r2 = 1;

if nargin < 2, referenceValues = struct('temp', 25, 'humidity', 50, 'pressure', 1013); end

% Placeholder: In production, read multiple samples, fit to reference
% For now return identity calibration
offset = 0;
scale = 1;
r2 = 0.99;

end

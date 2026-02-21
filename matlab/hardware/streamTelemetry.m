function data = streamTelemetry(port, durationSec, sampleIntervalSec)
% streamTelemetry - Real-time data acquisition from device
%
%   data = streamTelemetry(port, durationSec, sampleIntervalSec)
%
% Input: port - serial port, durationSec, sampleIntervalSec
% Output: data - struct with time, temp, humidity, pressure, gasResistance

data = struct('time', [], 'temp', [], 'humidity', [], 'pressure', [], 'gasResistance', []);

if nargin < 3, sampleIntervalSec = 1; end
if nargin < 2, durationSec = 60; end

nSamples = max(1, round(durationSec / sampleIntervalSec));
data.time = (0:(nSamples-1))' * sampleIntervalSec;

% Placeholder: In production, read from serial in loop
% For now return synthetic data
data.temp = 25 + 2*randn(nSamples, 1);
data.humidity = 50 + 5*randn(nSamples, 1);
data.pressure = 1013 + 1*randn(nSamples, 1);
data.gasResistance = 100000 * (1 + 0.05*randn(nSamples, 1));

end

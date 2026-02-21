function [peaks, metrics] = analyzeSpectrum(data, fs, varargin)
% analyzeSpectrum - Spectral analysis for mass spec, NMR, etc.
%
%   [peaks, metrics] = analyzeSpectrum(data, fs)
%   [peaks, metrics] = analyzeSpectrum(data, fs, 'MinPeakHeight', 0.1)
%
% Inputs:
%   data - Nx1 signal vector
%   fs - Sampling frequency (Hz)
%
% Optional:
%   MinPeakHeight - Minimum peak prominence
%   MinPeakDistance - Minimum samples between peaks
%
% Outputs:
%   peaks - Struct with .freq, .amp, .prominence
%   metrics - Struct with spectral metrics

p = inputParser;
addRequired(p, 'data', @isnumeric);
addRequired(p, 'fs', @(x) isnumeric(x) && x > 0);
addParameter(p, 'MinPeakHeight', 0.1, @isnumeric);
addParameter(p, 'MinPeakDistance', 10, @(x) isnumeric(x) && x >= 0);
parse(p, data, fs, varargin{:});

x = data(:);
n = numel(x);

% Compute spectrum
N = 2^nextpow2(n);
Y = fft(x, N);
P2 = abs(Y/N);
P1 = P2(1:N/2+1);
P1(2:end-1) = 2*P1(2:end-1);
f = fs*(0:(N/2))/N;

% Find peaks
[pks, locs, prom] = findpeaks(P1, f, ...
    'MinPeakHeight', p.Results.MinPeakHeight, ...
    'MinPeakDistance', fs/N * p.Results.MinPeakDistance);

peaks = struct('freq', num2cell(locs), 'amp', num2cell(pks), 'prominence', num2cell(prom));
if isempty(peaks)
    peaks = struct('freq', {}, 'amp', {}, 'prominence', {});
end

% Spectral metrics
metrics = struct();
metrics.dominantFreq = f(P1 == max(P1));
metrics.totalPower = sum(P1.^2);
metrics.peakCount = numel(pks);

end

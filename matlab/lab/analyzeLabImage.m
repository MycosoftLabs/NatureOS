function [count, areas, metrics] = analyzeLabImage(img, varargin)
% analyzeLabImage - Analyze microscopy/colony images (counting, area)
%
%   [count, areas, metrics] = analyzeLabImage(img)
%   [count, areas, metrics] = analyzeLabImage(img, 'Threshold', 0.5)
%
% Inputs:
%   img - Grayscale or RGB image (MxN or MxNx3)
%
% Optional:
%   Threshold - Binarization threshold (0-1) or 'auto'
%   MinArea - Minimum object area (pixels)
%
% Outputs:
%   count - Number of detected objects (colonies, cells)
%   areas - Vector of object areas
%   metrics - Struct with additional metrics

p = inputParser;
addRequired(p, 'img', @(x) isnumeric(x) && ndims(x) >= 2);
addParameter(p, 'Threshold', 'auto', @(x) isnumeric(x) || (ischar(x) && strcmpi(x, 'auto')));
addParameter(p, 'MinArea', 10, @(x) isnumeric(x) && x > 0);
parse(p, img, varargin{:});

if ndims(img) == 3
    img = rgb2gray(img);
end

% Binarize
if ischar(p.Results.Threshold) && strcmpi(p.Results.Threshold, 'auto')
    bw = imbinarize(img);
else
    bw = imbinarize(img, p.Results.Threshold);
end

% Invert if background is light
if mean(bw(:)) > 0.5
    bw = ~bw;
end

% Connected components
cc = bwconncomp(bw);
stats = regionprops(cc, 'Area');
areas = [stats.Area]';
areas = areas(areas >= p.Results.MinArea);
count = numel(areas);

metrics = struct();
metrics.totalArea = sum(areas);
metrics.meanArea = mean(areas);
metrics.medianArea = median(areas);
metrics.coveragePercent = 100 * sum(areas) / numel(bw);

end

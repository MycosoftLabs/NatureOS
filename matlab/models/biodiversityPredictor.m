function [shannon, simpson, chao1, rarefaction] = biodiversityPredictor(speciesCounts, varargin)
% biodiversityPredictor - Compute biodiversity indices from species abundance
%
%   [shannon, simpson, chao1, rarefaction] = biodiversityPredictor(speciesCounts)
%   [shannon, simpson, chao1, rarefaction] = biodiversityPredictor(speciesCounts, 'RarefactionSteps', 100)
%
% Inputs:
%   speciesCounts - Vector of per-species counts, or cell of species IDs
%
% Optional:
%   RarefactionSteps - Number of rarefaction curve points
%
% Outputs:
%   shannon - Shannon diversity index
%   simpson - Simpson diversity index (1-D)
%   chao1 - Chao1 species richness estimator
%   rarefaction - Rarefaction curve (expected species vs. sample size)

p = inputParser;
addRequired(p, 'speciesCounts', @(x) isnumeric(x) || iscell(x));
addParameter(p, 'RarefactionSteps', 100, @(x) isnumeric(x) && x > 0);
parse(p, speciesCounts, varargin{:});

if iscell(speciesCounts)
    counts = accumarray(grp2idx(categorical(speciesCounts)), 1);
else
    counts = speciesCounts(:);
end

counts = counts(counts > 0);
n = sum(counts);
S = numel(counts);
p_i = counts / n;

% Shannon: -sum(p*log(p))
shannon = -sum(p_i .* log(p_i + eps));

% Simpson: 1 - sum(p^2)
simpson = 1 - sum(p_i.^2);

% Chao1: S_obs + f1^2/(2*f2)
f1 = sum(counts == 1);
f2 = sum(counts == 2);
if f2 > 0
    chao1 = S + f1^2 / (2 * f2);
else
    chao1 = S + f1 * (f1 - 1) / 2;
end

% Rarefaction: expected species for subsample sizes
nSteps = min(p.Results.RarefactionSteps, n);
subSizes = round(linspace(1, n, nSteps));
rarefaction = zeros(size(subSizes));

for k = 1:numel(subSizes)
    m = subSizes(k);
    if m >= n
        rarefaction(k) = S;
    else
        % Expected species: sum(1 - C(n-n_i,m)/C(n,m))
        E = 0;
        for i = 1:S
            ni = counts(i);
            if ni <= n - m
                num = nchoosek(n - ni, m);
                den = nchoosek(n, m);
                E = E + (1 - num/den);
            else
                E = E + 1;
            end
        end
        rarefaction(k) = E;
    end
end

end

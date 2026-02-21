function [topSpecies, confidence, alternatives] = fungalClassifier(signalVector, varargin)
% fungalClassifier - Classify fungal morphology from signal/feature vector
%
%   [topSpecies, confidence, alternatives] = fungalClassifier(signalVector)
%   [topSpecies, confidence, alternatives] = fungalClassifier(signalVector, 'ModelPath', path)
%
% Inputs:
%   signalVector - Nx1 feature vector (morphological features, spectral, etc.)
%
% Optional:
%   ModelPath - Path to trained classifier model (.mat)
%
% Outputs:
%   topSpecies - Top predicted species label
%   confidence - Confidence score 0-1
%   alternatives - Struct array of {species, confidence} for runner-ups

p = inputParser;
addRequired(p, 'signalVector', @isnumeric);
addParameter(p, 'ModelPath', '', @ischar);
parse(p, signalVector, varargin{:});

x = signalVector(:)';

% Check for pre-trained model
if ~isempty(p.Results.ModelPath) && exist(p.Results.ModelPath, 'file')
    loaded = load(p.Results.ModelPath);
    if isfield(loaded, 'classifier')
        [label, scores] = predict(loaded.classifier, x);
        topSpecies = char(label);
        confidence = max(scores);
        [~, idx] = sort(scores, 'descend');
        alternatives = struct('species', {}, 'confidence', {});
        for k = 2:min(5, numel(idx))
            alternatives(end+1).species = char(loaded.classifier.ClassNames(idx(k)));
            alternatives(end).confidence = scores(idx(k));
        end
        return;
    end
end

% Fallback: rule-based / template matching
% In production, replace with trained model
speciesDB = {'Agaricus_bisporus', 'Pleurotus_ostreatus', 'Ganoderma_lucidum', ...
    'Lentinula_edodes', 'Unknown'};

% Simple similarity to mock templates (random for demo)
rng(sum(x));
scores = rand(1, numel(speciesDB));
scores = scores / sum(scores);
[confidence, idx] = max(scores);
topSpecies = speciesDB{idx};

alternatives = struct('species', {}, 'confidence', {});
[~, sIdx] = sort(scores, 'descend');
for k = 2:min(4, numel(sIdx))
    alternatives(end+1).species = speciesDB{sIdx(k)};
    alternatives(end+1).confidence = scores(sIdx(k));
end

end

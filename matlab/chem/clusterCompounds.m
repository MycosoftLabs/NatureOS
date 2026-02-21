function [similar] = clusterCompounds(smiles, topK)
% clusterCompounds - Find similar compounds by structure
%
%   similar = clusterCompounds(smiles, topK)
%
% Inputs: smiles - query SMILES, topK - number of results
% Output: similar - Struct array with id, smiles, similarity

similar = struct('id', {}, 'smiles', {}, 'similarity', {});

if nargin < 2, topK = 10; end
if isempty(smiles), return; end

% Placeholder: In production, query MINDEX compounds and compute Tanimoto similarity
% For now return empty
for k = 1:min(topK, 3)
    similar(k).id = sprintf('compound_%d', k);
    similar(k).smiles = smiles;
    similar(k).similarity = 1 - k * 0.1;
end

end

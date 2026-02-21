function [alignedSequences, score, consensus] = alignSequences(sequences)
% alignSequences - Multiple sequence alignment (e.g., ITS barcode)
%
%   [alignedSequences, score, consensus] = alignSequences(sequences)
%
% Input: sequences - Cell array of sequence strings
% Output: alignedSequences, alignment score, consensus sequence

if isempty(sequences) || numel(sequences) < 2
    alignedSequences = sequences;
    score = 0;
    consensus = '';
    return;
end

% Placeholder: In production, use multialign or seqalign from Bioinformatics Toolbox
% For now return sequences padded to max length
seqs = cellfun(@char, sequences, 'UniformOutput', false);
maxLen = max(cellfun(@length, seqs));
alignedSequences = cell(size(seqs));
for i = 1:numel(seqs)
    alignedSequences{i} = [seqs{i}, repmat('-', 1, maxLen - length(seqs{i}))];
end
score = 0.8;
consensus = repmat('N', 1, maxLen);

end

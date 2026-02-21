function [density, clustering, metrics] = mycorrhizalNetworkAnalyzer(adjacencyMatrix, varargin)
% mycorrhizalNetworkAnalyzer - Analyze mycorrhizal network graph metrics
%
%   [density, clustering, metrics] = mycorrhizalNetworkAnalyzer(adjacencyMatrix)
%   [density, clustering, metrics] = mycorrhizalNetworkAnalyzer(adjacencyMatrix, 'Directed', false)
%
% Inputs:
%   adjacencyMatrix - NxN adjacency matrix (symmetric for undirected)
%
% Optional:
%   Directed - true if directed graph
%
% Outputs:
%   density - Network density (edges / max possible)
%   clustering - Average clustering coefficient
%   metrics - Struct with additional metrics

p = inputParser;
addRequired(p, 'adjacencyMatrix', @isnumeric);
addParameter(p, 'Directed', false, @islogical);
parse(p, adjacencyMatrix, varargin{:});

A = adjacencyMatrix;
if ~p.Results.Directed
    A = (A + A') / 2;
    A(A > 0) = 1;
end

n = size(A, 1);
if n < 2
    density = 0;
    clustering = 0;
    metrics = struct();
    return;
end

% Density
if p.Results.Directed
    maxEdges = n * (n - 1);
else
    maxEdges = n * (n - 1) / 2;
end
numEdges = sum(A(:)) / (1 + ~p.Results.Directed);
density = numEdges / max(1, maxEdges);

% Clustering coefficient (Watts-Strogatz)
% For each node: fraction of triangles among neighbors
clust = zeros(n, 1);
for i = 1:n
    neighbors = find(A(i, :));
    k = numel(neighbors);
    if k < 2
        clust(i) = 0;
        continue;
    end
    subA = A(neighbors, neighbors);
    triangles = sum(subA(:)) / 2;
    possible = k * (k - 1) / 2;
    clust(i) = triangles / max(1, possible);
end
clustering = mean(clust);

% Degree statistics
deg = sum(A, 2);
metrics = struct();
metrics.meanDegree = mean(deg);
metrics.maxDegree = max(deg);
metrics.numNodes = n;
metrics.numEdges = round(numEdges);

end

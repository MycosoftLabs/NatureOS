// React component for live data integration with NatureOS
// This should be placed in the Mycosoft website repository

'use client';

import { useState, useEffect, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';

export default function LiveDataComponent() {
  const [liveData, setLiveData] = useState(null);
  const [isConnected, setIsConnected] = useState(false);
  const [lastUpdate, setLastUpdate] = useState(null);
  const [error, setError] = useState(null);

  // Fetch live data from NatureOS
  const fetchLiveData = useCallback(async () => {
    try {
      const response = await fetch('/api/dashboard?type=live');
      if (!response.ok) throw new Error('Failed to fetch data');
      
      const data = await response.json();
      setLiveData(data);
      setLastUpdate(new Date());
      setIsConnected(true);
      setError(null);
    } catch (err) {
      setError(err.message);
      setIsConnected(false);
    }
  }, []);

  // Set up real-time updates
  useEffect(() => {
    fetchLiveData();
    const interval = setInterval(fetchLiveData, 5000); // Update every 5 seconds
    return () => clearInterval(interval);
  }, [fetchLiveData]);

  if (!liveData) {
    return (
      <div className="flex items-center justify-center p-8">
        <motion.div
          animate={{ rotate: 360 }}
          transition={{ duration: 2, repeat: Infinity, ease: "linear" }}
          className="w-8 h-8 border-2 border-green-500 border-t-transparent rounded-full"
        />
        <span className="ml-3 text-gray-600">Connecting to NatureOS...</span>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Connection Status */}
      <div className="flex items-center justify-between">
        <div className="flex items-center space-x-2">
          <motion.div
            animate={{ scale: isConnected ? [1, 1.2, 1] : 1 }}
            transition={{ duration: 1, repeat: isConnected ? Infinity : 0 }}
            className={`w-3 h-3 rounded-full ${
              isConnected ? 'bg-green-500' : 'bg-red-500'
            }`}
          />
          <span className="text-sm text-gray-600">
            {isConnected ? 'Connected to NatureOS' : 'Disconnected'}
          </span>
        </div>
        {lastUpdate && (
          <span className="text-xs text-gray-500">
            Last update: {lastUpdate.toLocaleTimeString()}
          </span>
        )}
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4">
          <p className="text-red-700 text-sm">⚠️ {error}</p>
        </div>
      )}

      {/* System Stats */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard
          title="Active Devices"
          value={liveData.stats?.activeDevices || 0}
          change="+2"
          icon="📡"
        />
        <StatCard
          title="Species Detected"
          value={liveData.stats?.speciesDetected || 0}
          change="+1"
          icon="🍄"
        />
        <StatCard
          title="Total Events"
          value={liveData.stats?.totalEvents || 0}
          change="+150"
          icon="📊"
        />
        <StatCard
          title="Online Users"
          value={liveData.stats?.onlineUsers || 0}
          change="+5"
          icon="👥"
        />
      </div>

      {/* Live Readings */}
      <div className="bg-white rounded-lg border border-gray-200 p-6">
        <h3 className="text-lg font-semibold text-gray-900 mb-4">
          🌡️ Live Environmental Readings
        </h3>
        <div className="space-y-3">
          <AnimatePresence>
            {liveData.liveData?.readings?.map((reading, index) => (
              <motion.div
                key={reading.device}
                initial={{ opacity: 0, x: -20 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: 20 }}
                transition={{ delay: index * 0.1 }}
                className="flex items-center justify-between p-3 bg-gray-50 rounded-lg"
              >
                <div className="flex items-center space-x-3">
                  <div className={`w-2 h-2 rounded-full ${
                    reading.status === 'active' ? 'bg-green-500' : 'bg-gray-400'
                  }`} />
                  <span className="font-medium text-gray-700">{reading.device}</span>
                </div>
                <div className="text-right">
                  <div className="font-semibold text-gray-900">{reading.value}</div>
                  <div className="text-xs text-gray-500">
                    {new Date(reading.timestamp).toLocaleTimeString()}
                  </div>
                </div>
              </motion.div>
            ))}
          </AnimatePresence>
        </div>
      </div>

      {/* Network Health */}
      <div className="bg-white rounded-lg border border-gray-200 p-6">
        <h3 className="text-lg font-semibold text-gray-900 mb-4">
          🔗 Network Health
        </h3>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="text-center">
            <div className="text-2xl font-bold text-green-600">
              {liveData.networkHealth?.status || 'Unknown'}
            </div>
            <div className="text-sm text-gray-600">Status</div>
          </div>
          <div className="text-center">
            <div className="text-2xl font-bold text-blue-600">
              {liveData.networkHealth?.connections || 0}
            </div>
            <div className="text-sm text-gray-600">Connections</div>
          </div>
          <div className="text-center">
            <div className="text-2xl font-bold text-purple-600">
              {liveData.networkHealth?.throughput || '0 MB/s'}
            </div>
            <div className="text-sm text-gray-600">Throughput</div>
          </div>
        </div>
      </div>

      {/* Trending Insights */}
      <div className="bg-white rounded-lg border border-gray-200 p-6">
        <h3 className="text-lg font-semibold text-gray-900 mb-4">
          🔬 Trending Insights
        </h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <h4 className="font-medium text-gray-700 mb-2">Trending Compounds</h4>
            <ul className="space-y-1">
              {liveData.insights?.trendingCompounds?.map((compound, index) => (
                <motion.li
                  key={compound}
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: index * 0.1 }}
                  className="text-sm text-gray-600 flex items-center space-x-2"
                >
                  <span className="w-2 h-2 bg-green-500 rounded-full" />
                  <span>{compound}</span>
                </motion.li>
              ))}
            </ul>
          </div>
          <div>
            <h4 className="font-medium text-gray-700 mb-2">Recent Discoveries</h4>
            <ul className="space-y-1">
              {liveData.insights?.recentDiscoveries?.map((discovery, index) => (
                <motion.li
                  key={discovery}
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: index * 0.1 }}
                  className="text-sm text-gray-600"
                >
                  • {discovery}
                </motion.li>
              ))}
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}

function StatCard({ title, value, change, icon }) {
  return (
    <motion.div
      whileHover={{ scale: 1.02 }}
      className="bg-white rounded-lg border border-gray-200 p-4"
    >
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-medium text-gray-600">{title}</p>
          <motion.p
            key={value}
            initial={{ scale: 1.1 }}
            animate={{ scale: 1 }}
            className="text-2xl font-bold text-gray-900"
          >
            {typeof value === 'number' ? value.toLocaleString() : value}
          </motion.p>
          {change && (
            <p className="text-xs text-green-600 font-medium">{change}</p>
          )}
        </div>
        <div className="text-2xl">{icon}</div>
      </div>
    </motion.div>
  );
} 
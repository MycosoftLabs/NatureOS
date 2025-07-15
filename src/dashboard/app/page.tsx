'use client';

import { useState, useEffect } from 'react';
import { ChartBarIcon, DevicePhoneMobileIcon, GlobeAltIcon, CubeIcon } from '@heroicons/react/24/outline';
import EventsList from '../components/EventsList';
import StatisticsCards from '../components/StatisticsCards';
import DeviceMap from '../components/DeviceMap';
import { EventStatistics } from '../types/events';

export default function Dashboard() {
  const [statistics, setStatistics] = useState<EventStatistics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchStatistics();
  }, []);

  const fetchStatistics = async () => {
    try {
      setLoading(true);
      const response = await fetch('/api/events/statistics');
      if (!response.ok) {
        throw new Error('Failed to fetch statistics');
      }
      const data = await response.json();
      setStatistics(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-32 w-32 border-b-2 border-green-500 mx-auto"></div>
          <p className="mt-4 text-gray-600">Loading NatureOS Dashboard...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <div className="text-red-500 text-xl mb-4">⚠️ Error</div>
          <p className="text-gray-600">{error}</p>
          <button 
            onClick={fetchStatistics}
            className="mt-4 px-4 py-2 bg-green-500 text-white rounded hover:bg-green-600"
          >
            Retry
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-white shadow">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between items-center py-6">
            <div className="flex items-center">
              <CubeIcon className="h-8 w-8 text-green-500 mr-3" />
              <div>
                <h1 className="text-3xl font-bold text-gray-900">NatureOS</h1>
                <p className="text-sm text-gray-500">Cloud-native operating system for nature</p>
              </div>
            </div>
            <div className="flex items-center space-x-4">
              <div className="text-sm text-gray-500">
                Last updated: {new Date().toLocaleTimeString()}
              </div>
              <button
                onClick={fetchStatistics}
                className="bg-green-500 hover:bg-green-600 text-white px-4 py-2 rounded-md text-sm font-medium"
              >
                Refresh
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* Statistics Cards */}
        <div className="mb-8">
          <h2 className="text-lg font-medium text-gray-900 mb-4">System Overview</h2>
          <StatisticsCards statistics={statistics} />
        </div>

        {/* Grid Layout */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
          {/* Events List */}
          <div className="bg-white shadow rounded-lg">
            <div className="px-6 py-4 border-b border-gray-200">
              <h3 className="text-lg font-medium text-gray-900 flex items-center">
                <ChartBarIcon className="h-5 w-5 mr-2 text-green-500" />
                Recent Events
              </h3>
            </div>
            <EventsList />
          </div>

          {/* Device Map */}
          <div className="bg-white shadow rounded-lg">
            <div className="px-6 py-4 border-b border-gray-200">
              <h3 className="text-lg font-medium text-gray-900 flex items-center">
                <GlobeAltIcon className="h-5 w-5 mr-2 text-green-500" />
                Device Locations
              </h3>
            </div>
            <DeviceMap />
          </div>
        </div>

        {/* Domain Statistics */}
        {statistics && (
          <div className="mt-8 bg-white shadow rounded-lg">
            <div className="px-6 py-4 border-b border-gray-200">
              <h3 className="text-lg font-medium text-gray-900 flex items-center">
                <DevicePhoneMobileIcon className="h-5 w-5 mr-2 text-green-500" />
                Domain Activity
              </h3>
            </div>
            <div className="p-6">
              <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                {Object.entries(statistics.eventsByDomain).map(([domain, count]) => (
                  <div key={domain} className="text-center">
                    <div className="text-2xl font-bold text-green-600">{count.toLocaleString()}</div>
                    <div className="text-sm text-gray-500">{domain}</div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* Footer */}
        <div className="mt-12 text-center text-sm text-gray-500">
          <p>
            Powered by the Mycorrhizae Protocol | 
            <a href="https://github.com/MycosoftLabs/NatureOS" className="text-green-500 hover:text-green-600 ml-1">
              View on GitHub
            </a>
          </p>
        </div>
      </main>
    </div>
  );
} 
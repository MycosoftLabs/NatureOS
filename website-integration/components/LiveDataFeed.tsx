import React, { useState, useEffect, useCallback } from 'react';
import { liveDataAPI, LiveDataResponse, RealtimeEvent, formatNumber, formatTimestamp } from '../api/live-data';

interface LiveDataFeedProps {
  refreshInterval?: number;
  showEvents?: boolean;
  maxEvents?: number;
  className?: string;
}

export const LiveDataFeed: React.FC<LiveDataFeedProps> = ({
  refreshInterval = 5000,
  showEvents = true,
  maxEvents = 10,
  className = ''
}) => {
  const [data, setData] = useState<LiveDataResponse | null>(null);
  const [events, setEvents] = useState<RealtimeEvent[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  // Handle real-time event updates
  const handleEventUpdate = useCallback((newEvents: RealtimeEvent[]) => {
    setEvents(prevEvents => {
      const combined = [...newEvents, ...prevEvents];
      return combined.slice(0, maxEvents);
    });
  }, [maxEvents]);

  // Handle dashboard data updates
  const handleDataUpdate = useCallback((newData: LiveDataResponse) => {
    setData(newData);
    setIsLoading(false);
    setError(null);
  }, []);

  // Handle connection errors
  const handleError = useCallback((error: Event) => {
    console.error('Live data connection error:', error);
    setError('Connection lost. Attempting to reconnect...');
    setIsConnected(false);
  }, []);

  // Initialize live data streams
  useEffect(() => {
    const startStreams = async () => {
      try {
        // Get initial data
        const initialData = await liveDataAPI.getDashboardData();
        setData(initialData);
        setIsLoading(false);
        setIsConnected(true);

        // Start real-time streams
        if (showEvents) {
          liveDataAPI.startEventStream(handleEventUpdate, handleError);
        }
        liveDataAPI.startDashboardStream(handleDataUpdate, handleError);

      } catch (err) {
        setError('Failed to load live data');
        setIsLoading(false);
      }
    };

    startStreams();

    // Cleanup on unmount
    return () => {
      liveDataAPI.stopStreams();
    };
  }, [showEvents, handleEventUpdate, handleDataUpdate, handleError]);

  // Loading state
  if (isLoading) {
    return (
      <div className={`live-data-feed loading ${className}`}>
        <div className="loading-spinner">
          <div className="spinner"></div>
          <p>Loading live data...</p>
        </div>
      </div>
    );
  }

  // Error state
  if (error) {
    return (
      <div className={`live-data-feed error ${className}`}>
        <div className="error-message">
          <h3>Connection Error</h3>
          <p>{error}</p>
          <button 
            onClick={() => window.location.reload()} 
            className="retry-button"
          >
            Retry Connection
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className={`live-data-feed ${className}`}>
      {/* Connection Status */}
      <div className={`connection-status ${isConnected ? 'connected' : 'disconnected'}`}>
        <span className="status-indicator"></span>
        {isConnected ? 'Live' : 'Reconnecting...'}
      </div>

      {/* Stats Overview */}
      {data && (
        <div className="stats-overview">
          <div className="stat-card">
            <h3>Total Events</h3>
            <span className="stat-value">{formatNumber(data.stats.totalEvents)}</span>
          </div>
          <div className="stat-card">
            <h3>Active Devices</h3>
            <span className="stat-value">{data.stats.activeDevices}</span>
          </div>
          <div className="stat-card">
            <h3>Species Detected</h3>
            <span className="stat-value">{data.stats.speciesDetected}</span>
          </div>
          <div className="stat-card">
            <h3>Online Users</h3>
            <span className="stat-value">{data.stats.onlineUsers}</span>
          </div>
        </div>
      )}

      {/* Live Readings */}
      {data?.liveData && (
        <div className="live-readings">
          <h3>Latest Readings</h3>
          <div className="readings-list">
            {data.liveData.readings.map((reading, index) => (
              <div key={index} className="reading-item">
                <span className="device">{reading.sourceDevice}</span>
                <span className="value">{reading.kingdomDomain}</span>
                <span className="timestamp">{formatTimestamp(reading.timestamp)}</span>
              </div>
            ))}
          </div>
          <p className="last-update">
            Last updated: {formatTimestamp(data.liveData.lastUpdate)}
          </p>
        </div>
      )}

      {/* Real-time Events */}
      {showEvents && events.length > 0 && (
        <div className="realtime-events">
          <h3>Live Events</h3>
          <div className="events-list">
            {events.map((event) => (
              <div key={event.eventId} className="event-item">
                <div className="event-header">
                  <span className="device-id">{event.sourceDevice}</span>
                  <span className="timestamp">{formatTimestamp(event.timestamp)}</span>
                </div>
                <div className="event-content">
                  <span className="domain">{event.kingdomDomain}</span>
                  {event.decodedMeaning && (
                    <span className="confidence">
                      {Math.round(event.decodedMeaning.confidence * 100)}% confidence
                    </span>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Insights */}
      {data?.insights && (
        <div className="insights">
          <h3>Current Insights</h3>
          
          <div className="trending-compounds">
            <h4>Trending Compounds</h4>
            <div className="compound-tags">
              {data.insights.trendingCompounds.map((compound, index) => (
                <span key={index} className="compound-tag">{compound}</span>
              ))}
            </div>
          </div>

          {data.insights.recentDiscoveries.length > 0 && (
            <div className="recent-discoveries">
              <h4>Recent Discoveries</h4>
              <ul>
                {data.insights.recentDiscoveries.map((discovery, index) => (
                  <li key={index}>{discovery.kingdomDomain}</li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}

      {/* Inline Styles */}
      <style jsx>{`
        .live-data-feed {
          padding: 20px;
          background: #f8fafc;
          border-radius: 8px;
          box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }

        .connection-status {
          display: flex;
          align-items: center;
          gap: 8px;
          margin-bottom: 20px;
          font-weight: 600;
        }

        .status-indicator {
          width: 8px;
          height: 8px;
          border-radius: 50%;
          background: #ef4444;
        }

        .connected .status-indicator {
          background: #10b981;
        }

        .stats-overview {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
          gap: 16px;
          margin-bottom: 24px;
        }

        .stat-card {
          background: white;
          padding: 16px;
          border-radius: 6px;
          text-align: center;
          box-shadow: 0 1px 3px rgba(0,0,0,0.1);
        }

        .stat-card h3 {
          margin: 0 0 8px 0;
          font-size: 14px;
          color: #6b7280;
          text-transform: uppercase;
          letter-spacing: 0.05em;
        }

        .stat-value {
          font-size: 24px;
          font-weight: 700;
          color: #1f2937;
        }

        .live-readings, .realtime-events, .insights {
          background: white;
          padding: 16px;
          border-radius: 6px;
          margin-bottom: 16px;
        }

        .live-readings h3, .realtime-events h3, .insights h3 {
          margin-top: 0;
          color: #1f2937;
        }

        .readings-list, .events-list {
          max-height: 300px;
          overflow-y: auto;
        }

        .reading-item, .event-item {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 8px 0;
          border-bottom: 1px solid #e5e7eb;
        }

        .event-item {
          flex-direction: column;
          align-items: flex-start;
        }

        .event-header, .event-content {
          display: flex;
          justify-content: space-between;
          align-items: center;
          width: 100%;
        }

        .device-id, .device {
          font-weight: 600;
          color: #3b82f6;
        }

        .timestamp {
          font-size: 12px;
          color: #6b7280;
        }

        .confidence {
          font-size: 12px;
          background: #dbeafe;
          color: #1e40af;
          padding: 2px 6px;
          border-radius: 4px;
        }

        .last-update {
          font-size: 12px;
          color: #6b7280;
          margin-top: 12px;
        }

        .compound-tags {
          display: flex;
          flex-wrap: wrap;
          gap: 8px;
          margin-top: 8px;
        }

        .compound-tag {
          background: #fef3c7;
          color: #92400e;
          padding: 4px 8px;
          border-radius: 4px;
          font-size: 12px;
          font-weight: 500;
        }

        .loading-spinner {
          text-align: center;
          padding: 40px;
        }

        .spinner {
          width: 40px;
          height: 40px;
          border: 4px solid #e5e7eb;
          border-top: 4px solid #3b82f6;
          border-radius: 50%;
          animation: spin 1s linear infinite;
          margin: 0 auto 16px auto;
        }

        @keyframes spin {
          0% { transform: rotate(0deg); }
          100% { transform: rotate(360deg); }
        }

        .error-message {
          text-align: center;
          padding: 40px;
        }

        .retry-button {
          background: #3b82f6;
          color: white;
          border: none;
          padding: 8px 16px;
          border-radius: 4px;
          cursor: pointer;
          margin-top: 16px;
        }

        .retry-button:hover {
          background: #2563eb;
        }
      `}</style>
    </div>
  );
};

export default LiveDataFeed; 
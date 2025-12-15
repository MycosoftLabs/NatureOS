import axios, { AxiosResponse } from 'axios';

const NATUREOS_API_BASE = process.env.NATUREOS_API_URL || 'https://natureos-api-production.azurewebsites.net';

/**
 * Live data from NatureOS Core API
 */
export interface LiveDataResponse {\n  Stats: {\n    TotalEvents: number;\n    ActiveDevices: number;\n    ErrorsLast24h: number;\n  };\n  LiveData: {\n    Readings: Array<{ deviceId: string; value: any; timestamp: string }>;\n    LastUpdate: string;\n  };\n  Insights: {\n    TrendingCompounds: string[];\n    RecentDiscoveries: any[];\n  };\n}\n\nexport interface WebsiteLiveDataResponse {\n  readings: Array<{ deviceId: string; value: any; ts: string }>;\n  context: any;\n}\n\n/**
 * Real-time event data structure
 */
export interface RealtimeEvent {
  eventId: string;
  timestamp: string;
  sourceDevice: string;
  kingdomDomain: string;
  signalVector: any;
  decodedMeaning?: {
    type: string;
    confidence: number;
  };
}

/**
 * Live data API client
 */
export class LiveDataAPI {
  private baseURL: string;
  private eventSource?: EventSource;

  constructor(baseURL: string = NATUREOS_API_BASE) {
    this.baseURL = baseURL;
  }

  /**
   * Get current dashboard data
   */
  async getDashboardData(): Promise<LiveDataResponse> {
    try {
      const response: AxiosResponse<LiveDataResponse> = await axios.get(
        `${this.baseURL}/api/mycosoft/website/dashboard`
      );
      return response.data;
    } catch (error) {
      console.error('Failed to fetch dashboard data:', error);
      throw new Error('Unable to retrieve live data');
    }
  }
  /**
   * Get website live data (readings + lightweight context)
   */
  async getWebsiteLiveData(): Promise<WebsiteLiveDataResponse> {
    try {
      const response: AxiosResponse<WebsiteLiveDataResponse> = await axios.get(
        ${this.baseURL}/api/mycosoft/website/live-data
      );
      return response.data;
    } catch (error) {
      console.error('Failed to fetch website live data:', error);
      throw new Error('Unable to retrieve website live data');
    }
  }

  /**
   * Start real-time event stream using Server-Sent Events
   */
  startEventStream(onEvent: (event: RealtimeEvent[]) => void, onError?: (error: Event) => void): void {
    if (this.eventSource) {
      this.eventSource.close();
    }

    this.eventSource = new EventSource(`${this.baseURL}/api/mycosoft/events/stream`);

    this.eventSource.onmessage = (event) => {
      try {
        const events: RealtimeEvent[] = JSON.parse(event.data);
        onEvent(events);
      } catch (error) {
        console.error('Failed to parse event data:', error);
      }
    };

    this.eventSource.onerror = (error) => {
      console.error('EventSource error:', error);
      if (onError) {
        onError(error);
      }
    };
  }

  /**
   * Start real-time dashboard stream
   */
  startDashboardStream(onUpdate: (data: LiveDataResponse) => void, onError?: (error: Event) => void): void {
    if (this.eventSource) {
      this.eventSource.close();
    }

    this.eventSource = new EventSource(`${this.baseURL}/api/mycosoft/dashboard/stream`);

    this.eventSource.onmessage = (event) => {
      try {
        const data: LiveDataResponse = JSON.parse(event.data);
        onUpdate(data);
      } catch (error) {
        console.error('Failed to parse dashboard data:', error);
      }
    };

    this.eventSource.onerror = (error) => {
      console.error('Dashboard stream error:', error);
      if (onError) {
        onError(error);
      }
    };
  }

  /**
   * Stop real-time streams
   */
  stopStreams(): void {
    if (this.eventSource) {
      this.eventSource.close();
      this.eventSource = undefined;
    }
  }

  /**
   * Get system status
   */
  async getSystemStatus() {
    try {
      const response = await axios.get(`${this.baseURL}/api/mycosoft/status`);
      return response.data;
    } catch (error) {
      console.error('Failed to fetch system status:', error);
      throw new Error('Unable to retrieve system status');
    }
  }

  /**
   * Trigger ecosystem synchronization
   */
  async triggerSync() {
    try {
      const response = await axios.post(`${this.baseURL}/api/mycosoft/sync`);
      return response.data;
    } catch (error) {
      console.error('Failed to trigger sync:', error);
      throw new Error('Unable to trigger synchronization');
    }
  }
}

// Export singleton instance
export const liveDataAPI = new LiveDataAPI();

// Export utility functions
export const formatTimestamp = (timestamp: string): string => {
  return new Date(timestamp).toLocaleString();
};

export const formatNumber = (num: number): string => {
  if (num >= 1000000) {
    return (num / 1000000).toFixed(1) + 'M';
  }
  if (num >= 1000) {
    return (num / 1000).toFixed(1) + 'K';
  }
  return num.toString();
};

export const getDeviceStatusColor = (status: string): string => {
  switch (status.toLowerCase()) {
    case 'online': return '#10B981';
    case 'offline': return '#EF4444';
    case 'maintenance': return '#F59E0B';
    case 'error': return '#DC2626';
    default: return '#6B7280';
  }
};

export default LiveDataAPI; 

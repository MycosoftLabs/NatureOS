#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory=$false)]
    [string]$ProjectPath = ".",
    [Parameter(Mandatory=$false)]
    [string]$ApiUrl = "https://natureos-api-production.azurewebsites.net"
)

Write-Host "🌟 NatureOS Frontend Integration Setup" -ForegroundColor Magenta
Write-Host "Setting up development environment for NatureOS integration..." -ForegroundColor Cyan

# Check if npm is available
try {
    $npmVersion = npm --version
    Write-Host "✅ npm found: $npmVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ npm not found. Please install Node.js and npm first." -ForegroundColor Red
    exit 1
}

# Install required packages
Write-Host "`n📦 Installing integration dependencies..." -ForegroundColor Yellow

$dependencies = @(
    "@microsoft/signalr",
    "axios", 
    "swr",
    "@tanstack/react-query",
    "recharts",
    "lucide-react",
    "date-fns",
    "framer-motion",
    "react-hot-toast",
    "react-hook-form",
    "@hookform/resolvers",
    "zod"
)

foreach ($dep in $dependencies) {
    Write-Host "Installing $dep..." -ForegroundColor Gray
    try {
        npm install $dep --save
        Write-Host "✅ $dep installed" -ForegroundColor Green
    } catch {
        Write-Host "⚠️ Failed to install $dep" -ForegroundColor Yellow
    }
}

# Create integration directories
Write-Host "`n🔧 Creating integration directories..." -ForegroundColor Yellow
$directories = @("lib", "hooks", "components/ui", "types", "utils")
foreach ($dir in $directories) {
    if (!(Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "✅ Created directory: $dir" -ForegroundColor Green
    }
}

# Create environment template
Write-Host "`n📝 Creating environment template..." -ForegroundColor Yellow
$envTemplate = @"
# NatureOS API Configuration
NEXT_PUBLIC_NATUREOS_API_URL=$ApiUrl
NEXT_PUBLIC_SIGNALR_HUB_URL=$ApiUrl/natureos-hub

# Feature Flags
NEXT_PUBLIC_ENABLE_REALTIME=true
NEXT_PUBLIC_ENABLE_MYCA=true
NEXT_PUBLIC_ENABLE_DEVICE_MONITORING=true
NEXT_PUBLIC_ENABLE_EXTERNAL_DATA=true

# Development Settings
NEXT_PUBLIC_DEBUG_MODE=false
NEXT_PUBLIC_LOG_LEVEL=info

# API Keys (replace with actual values)
NEXT_PUBLIC_MAPS_API_KEY=your_maps_api_key_here
NEXT_PUBLIC_ANALYTICS_ID=your_analytics_id_here
"@

$envTemplate | Out-File -FilePath ".env.local.template" -Encoding UTF8
Write-Host "✅ Environment template created: .env.local.template" -ForegroundColor Green

# Create TypeScript types
Write-Host "`n⚙️ Creating TypeScript types..." -ForegroundColor Yellow
$typesContent = @"
// NatureOS Integration Types

export interface MycorrhizaeEvent {
  event_id: string;
  timestamp: string;
  source_device: string;
  kingdom_domain: string;
  signal_vector: any;
  decoded_meaning?: {
    type: string;
    confidence: number;
    ontology?: Record<string, any>;
  };
  references?: {
    location?: GeoLocation;
    taxonomy?: TaxonomyRecord;
    environment?: EnvironmentalContext;
  };
  metadata?: EventMetadata;
}

export interface GeoLocation {
  latitude: number;
  longitude: number;
  accuracy?: number;
  altitude?: number;
}

export interface Device {
  deviceId: string;
  name: string;
  deviceType: string;
  location?: GeoLocation;
  status: 'Unknown' | 'Online' | 'Offline' | 'Maintenance' | 'Error';
  lastSeen?: string;
  batteryLevel?: number;
  metadata?: Record<string, any>;
}

export interface SystemStatus {
  overall: string;
  timestamp: string;
  services: {
    activeDevices: number;
    totalDevices: number;
    eventsToday: number;
    systemHealth: string;
  };
  devices: DeviceStatistics;
  events: EventStatistics;
  integrations: {
    website: string;
    mas: string;
    externalDatabases: string;
  };
}

export interface DeviceStatistics {
  totalDevices: number;
  onlineCount: number;
  devicesByStatus: Record<string, number>;
  devicesByType: Record<string, number>;
  activeDevices: number;
}

export interface EventStatistics {
  totalEvents: number;
  todayCount: number;
  averagePerHour: number;
  averagePerDay: number;
  uniqueSpeciesCount: number;
  eventsByDomain: Record<string, number>;
  eventsByDevice: Record<string, number>;
}

export interface MycaResponse {
  answer: string;
  confidence: number;
  timestamp: string;
  suggestedQuestions?: string[];
  sources?: string[];
}

export interface LiveDataResponse {
  stats: {
    totalEvents: number;
    activeDevices: number;
    speciesDetected: number;
    onlineUsers: number;
  };
  liveData: {
    readings: any[];
    lastUpdate: string;
  };
  insights: {
    trendingCompounds: string[];
    recentDiscoveries: any[];
  };
}

export interface NatureOSConfig {
  apiUrl: string;
  signalRUrl: string;
  enableRealtime: boolean;
  enableMyca: boolean;
  enableDeviceMonitoring: boolean;
  enableExternalData: boolean;
}
"@

$typesContent | Out-File -FilePath "types/natureos.ts" -Encoding UTF8
Write-Host "✅ TypeScript types created: types/natureos.ts" -ForegroundColor Green

# Create API client
Write-Host "`n⚙️ Creating API client..." -ForegroundColor Yellow
$apiClientContent = @"
import axios, { AxiosInstance, AxiosResponse } from 'axios';
import type { 
  MycorrhizaeEvent, 
  Device, 
  SystemStatus, 
  MycaResponse, 
  LiveDataResponse,
  NatureOSConfig 
} from '../types/natureos';

class NatureOSAPI {
  private client: AxiosInstance;
  private config: NatureOSConfig;

  constructor(config: NatureOSConfig) {
    this.config = config;
    this.client = axios.create({
      baseURL: config.apiUrl,
      timeout: 30000,
      headers: {
        'Content-Type': 'application/json',
      },
    });

    // Add request interceptor for logging
    if (config.enableRealtime) {
      this.client.interceptors.request.use((config) => {
        console.log(`API Request: `${config.method?.toUpperCase()} `${config.url});
        return config;
      });
    }

    // Add response interceptor for error handling
    this.client.interceptors.response.use(
      (response) => response,
      (error) => {
        console.error('API Error:', error.response?.data || error.message);
        return Promise.reject(error);
      }
    );
  }

  // System endpoints
  async getSystemStatus(): Promise<SystemStatus> {
    const response = await this.client.get<SystemStatus>('/api/mycosoft/status');
    return response.data;
  }

  async getHealth(): Promise<any> {
    const response = await this.client.get('/health');
    return response.data;
  }

  // Events endpoints
  async getEvents(query: any = {}): Promise<{ items: MycorrhizaeEvent[] }> {
    const response = await this.client.get<{ items: MycorrhizaeEvent[] }>('/api/events', { params: query });
    return response.data;
  }

  async createEvent(event: Partial<MycorrhizaeEvent>): Promise<MycorrhizaeEvent> {
    const response = await this.client.post<MycorrhizaeEvent>('/api/events', event);
    return response.data;
  }

  // Devices endpoints
  async getDevices(): Promise<Device[]> {
    const response = await this.client.get<Device[]>('/api/devices');
    return response.data;
  }

  async getDevice(deviceId: string): Promise<Device> {
    const response = await this.client.get<Device>(`/api/devices/`${deviceId});
    return response.data;
  }

  async registerDevice(device: Partial<Device>): Promise<Device> {
    const response = await this.client.post<Device>('/api/devices', device);
    return response.data;
  }

  // MYCA endpoints
  async queryMyca(question: string, context?: string, userId?: string): Promise<MycaResponse> {
    const response = await this.client.post<MycaResponse>('/api/mycosoft/myca/query', {
      question,
      context,
      userId
    });
    return response.data;
  }

  // Website integration endpoints
  async getWebsiteDashboard(): Promise<LiveDataResponse> {
    const response = await this.client.get<LiveDataResponse>('/api/mycosoft/website/dashboard');
    return response.data;
  }

  // Device telemetry
  async sendTelemetry(deviceId: string, data: any): Promise<any> {
    const response = await this.client.post(`/api/mycosoft/`${deviceId}/telemetry`, data);
    return response.data;
  }

  // External data integration
  async triggerDataSync(): Promise<any> {
    const response = await this.client.post('/api/mycosoft/sync');
    return response.data;
  }

  // Utility methods
  getStreamUrl(endpoint: string): string {
    return `${this.config.apiUrl}/api/mycosoft${endpoint}`;
  }

  getSignalRUrl(): string {
    return this.config.signalRUrl;
  }
}

// Create and export API instance
const createNatureOSAPI = (config: NatureOSConfig): NatureOSAPI => {
  return new NatureOSAPI(config);
};

export { NatureOSAPI, createNatureOSAPI };
export default NatureOSAPI;
"@

$apiClientContent | Out-File -FilePath "lib/natureos-api.ts" -Encoding UTF8
Write-Host "✅ API client created: lib/natureos-api.ts" -ForegroundColor Green

# Create React Query hooks
Write-Host "`n⚙️ Creating React Query hooks..." -ForegroundColor Yellow
$hooksContent = @"
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { 
  MycorrhizaeEvent, 
  Device, 
  SystemStatus, 
  MycaResponse, 
  LiveDataResponse 
} from '../types/natureos';
import { createNatureOSAPI } from '../lib/natureos-api';

// Get API configuration from environment
const getApiConfig = () => ({
  apiUrl: process.env.NEXT_PUBLIC_NATUREOS_API_URL || 'http://localhost:8080',
  signalRUrl: process.env.NEXT_PUBLIC_SIGNALR_HUB_URL || 'http://localhost:8080/natureos-hub',
  enableRealtime: process.env.NEXT_PUBLIC_ENABLE_REALTIME === 'true',
  enableMyca: process.env.NEXT_PUBLIC_ENABLE_MYCA === 'true',
  enableDeviceMonitoring: process.env.NEXT_PUBLIC_ENABLE_DEVICE_MONITORING === 'true',
  enableExternalData: process.env.NEXT_PUBLIC_ENABLE_EXTERNAL_DATA === 'true',
});

const api = createNatureOSAPI(getApiConfig());

// System hooks
export const useSystemStatus = () => {
  return useQuery({
    queryKey: ['system-status'],
    queryFn: () => api.getSystemStatus(),
    refetchInterval: 30000, // Refresh every 30 seconds
  });
};

export const useHealth = () => {
  return useQuery({
    queryKey: ['health'],
    queryFn: () => api.getHealth(),
    refetchInterval: 10000, // Refresh every 10 seconds
  });
};

// Events hooks
export const useEvents = (query: any = {}) => {
  return useQuery({
    queryKey: ['events', query],
    queryFn: () => api.getEvents(query),
    refetchInterval: 5000, // Refresh every 5 seconds for live data
  });
};

export const useCreateEvent = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (event: Partial<MycorrhizaeEvent>) => api.createEvent(event),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['events'] });
    },
  });
};

// Devices hooks
export const useDevices = () => {
  return useQuery({
    queryKey: ['devices'],
    queryFn: () => api.getDevices(),
    refetchInterval: 15000, // Refresh every 15 seconds
  });
};

export const useDevice = (deviceId: string) => {
  return useQuery({
    queryKey: ['device', deviceId],
    queryFn: () => api.getDevice(deviceId),
    enabled: !!deviceId,
  });
};

export const useRegisterDevice = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (device: Partial<Device>) => api.registerDevice(device),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['devices'] });
    },
  });
};

// MYCA hooks
export const useMycaQuery = () => {
  return useMutation({
    mutationFn: ({ question, context, userId }: { 
      question: string; 
      context?: string; 
      userId?: string 
    }) => api.queryMyca(question, context, userId),
  });
};

// Website integration hooks
export const useWebsiteDashboard = () => {
  return useQuery({
    queryKey: ['website-dashboard'],
    queryFn: () => api.getWebsiteDashboard(),
    refetchInterval: 10000, // Refresh every 10 seconds
  });
};

// Device telemetry hooks
export const useSendTelemetry = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: ({ deviceId, data }: { deviceId: string; data: any }) => 
      api.sendTelemetry(deviceId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['events'] });
      queryClient.invalidateQueries({ queryKey: ['devices'] });
    },
  });
};

// Data sync hooks
export const useDataSync = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: () => api.triggerDataSync(),
    onSuccess: () => {
      queryClient.invalidateQueries(); // Refresh all data
    },
  });
};

// Utility hooks
export const useApiConfig = () => getApiConfig();

export { api as natureOSApi };
"@

$hooksContent | Out-File -FilePath "hooks/useNatureOSData.ts" -Encoding UTF8
Write-Host "✅ React Query hooks created: hooks/useNatureOSData.ts" -ForegroundColor Green

# Create SignalR integration
Write-Host "`n⚙️ Creating SignalR integration..." -ForegroundColor Yellow
$signalRContent = @"
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useEffect, useState, useRef, useCallback } from 'react';
import type { MycorrhizaeEvent, Device, SystemStatus } from '../types/natureos';

interface SignalRCallbacks {
  onDeviceUpdate?: (device: Device) => void;
  onEventCreated?: (event: MycorrhizaeEvent) => void;
  onSystemUpdate?: (status: SystemStatus) => void;
  onMycaActivity?: (activity: any) => void;
  onAlert?: (alert: any) => void;
  onConnected?: () => void;
  onDisconnected?: () => void;
  onError?: (error: Error) => void;
}

export const useSignalR = (hubUrl: string, callbacks: SignalRCallbacks = {}) => {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [connectionError, setConnectionError] = useState<string | null>(null);
  const reconnectAttempts = useRef(0);
  const maxReconnectAttempts = 5;

  const connect = useCallback(async () => {
    try {
      const newConnection = new HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            if (retryContext.previousRetryCount < 3) {
              return Math.random() * 10000; // 0-10 seconds
            } else {
              return 30000; // 30 seconds
            }
          }
        })
        .configureLogging(LogLevel.Information)
        .build();

      // Set up event handlers
      newConnection.on('DeviceUpdate', (device: Device) => {
        console.log('Device update received:', device);
        callbacks.onDeviceUpdate?.(device);
      });

      newConnection.on('EventCreated', (event: MycorrhizaeEvent) => {
        console.log('Event created:', event);
        callbacks.onEventCreated?.(event);
      });

      newConnection.on('SystemUpdate', (status: SystemStatus) => {
        console.log('System update:', status);
        callbacks.onSystemUpdate?.(status);
      });

      newConnection.on('MycaActivity', (activity: any) => {
        console.log('MYCA activity:', activity);
        callbacks.onMycaActivity?.(activity);
      });

      newConnection.on('SystemAlert', (alert: any) => {
        console.log('System alert:', alert);
        callbacks.onAlert?.(alert);
      });

      newConnection.onclose((error) => {
        setIsConnected(false);
        setConnectionError(error?.message || 'Connection closed');
        callbacks.onDisconnected?.();
        console.log('SignalR connection closed:', error);
      });

      newConnection.onreconnecting((error) => {
        setIsConnected(false);
        setConnectionError('Reconnecting...');
        console.log('SignalR reconnecting:', error);
      });

      newConnection.onreconnected((connectionId) => {
        setIsConnected(true);
        setConnectionError(null);
        reconnectAttempts.current = 0;
        callbacks.onConnected?.();
        console.log('SignalR reconnected:', connectionId);
      });

      await newConnection.start();
      
      // Join default groups
      await newConnection.invoke('JoinDashboardGroup');
      
      setConnection(newConnection);
      setIsConnected(true);
      setConnectionError(null);
      reconnectAttempts.current = 0;
      callbacks.onConnected?.();
      
      console.log('SignalR connected successfully');
      
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      setConnectionError(errorMessage);
      callbacks.onError?.(error instanceof Error ? error : new Error(errorMessage));
      
      // Retry connection
      if (reconnectAttempts.current < maxReconnectAttempts) {
        reconnectAttempts.current++;
        const delay = Math.min(1000 * Math.pow(2, reconnectAttempts.current), 30000);
        console.log(`Retrying connection in ${delay}ms (attempt ${reconnectAttempts.current})`);
        setTimeout(connect, delay);
      } else {
        console.error('Max reconnection attempts reached');
      }
    }
  }, [hubUrl, callbacks]);

  const disconnect = useCallback(async () => {
    if (connection) {
      try {
        await connection.stop();
      } catch (error) {
        console.error('Error stopping SignalR connection:', error);
      }
      setConnection(null);
      setIsConnected(false);
    }
  }, [connection]);

  const joinGroup = useCallback(async (groupName: string) => {
    if (connection && isConnected) {
      try {
        await connection.invoke('JoinGroup', groupName);
        console.log(`Joined group: ${groupName}`);
      } catch (error) {
        console.error(`Failed to join group ${groupName}:`, error);
      }
    }
  }, [connection, isConnected]);

  const leaveGroup = useCallback(async (groupName: string) => {
    if (connection && isConnected) {
      try {
        await connection.invoke('LeaveGroup', groupName);
        console.log(`Left group: ${groupName}`);
      } catch (error) {
        console.error(`Failed to leave group ${groupName}:`, error);
      }
    }
  }, [connection, isConnected]);

  const sendMessage = useCallback(async (methodName: string, ...args: any[]) => {
    if (connection && isConnected) {
      try {
        await connection.invoke(methodName, ...args);
      } catch (error) {
        console.error(`Failed to send message ${methodName}:`, error);
        throw error;
      }
    } else {
      throw new Error('SignalR connection not available');
    }
  }, [connection, isConnected]);

  useEffect(() => {
    connect();

    return () => {
      disconnect();
    };
  }, [connect, disconnect]);

  return {
    connection,
    isConnected,
    connectionError,
    connect,
    disconnect,
    joinGroup,
    leaveGroup,
    sendMessage,
  };
};

export default useSignalR;
"@

$signalRContent | Out-File -FilePath "hooks/useSignalR.ts" -Encoding UTF8
Write-Host "✅ SignalR integration created: hooks/useSignalR.ts" -ForegroundColor Green

# Create integration checklist
Write-Host "`n📋 Creating integration checklist..." -ForegroundColor Yellow
$checklistContent = @"
# NatureOS Frontend Integration Checklist

## Setup Tasks
- [ ] Install all required dependencies
- [ ] Configure environment variables in .env.local
- [ ] Set up API client configuration
- [ ] Test basic API connectivity

## Core Integration
- [ ] Implement React Query for data fetching
- [ ] Set up SignalR for real-time updates
- [ ] Create reusable UI components
- [ ] Implement error handling and loading states

## Feature Integration
- [ ] Device monitoring dashboard
- [ ] Real-time event streaming
- [ ] MYCA AI chat interface
- [ ] System status indicators
- [ ] Data visualization components

## Real-time Features
- [ ] SignalR connection management
- [ ] Device status updates
- [ ] Live event streaming
- [ ] System alerts and notifications
- [ ] Performance monitoring

## MYCA Integration
- [ ] Chat interface implementation
- [ ] Context-aware queries
- [ ] Conversation history
- [ ] Suggested questions
- [ ] Confidence indicators

## Performance Optimization
- [ ] Implement caching strategies
- [ ] Optimize re-rendering
- [ ] Add loading skeletons
- [ ] Implement virtual scrolling for large datasets
- [ ] Add offline support

## Testing
- [ ] Unit tests for hooks and utilities
- [ ] Integration tests for API calls
- [ ] E2E tests for critical user flows
- [ ] Performance testing
- [ ] Cross-browser compatibility

## Mobile Support
- [ ] Responsive design implementation
- [ ] Touch-friendly interactions
- [ ] Mobile-specific optimizations
- [ ] Push notification support (if applicable)

## Security
- [ ] API key management
- [ ] Input validation and sanitization
- [ ] XSS protection
- [ ] CSRF protection
- [ ] Secure data transmission

## Documentation
- [ ] Component documentation
- [ ] API integration guide
- [ ] Deployment instructions
- [ ] Troubleshooting guide

## Deployment
- [ ] Build optimization
- [ ] Environment-specific configurations
- [ ] CI/CD pipeline setup
- [ ] Monitoring and analytics
- [ ] Error reporting

## Next Steps
1. Copy .env.local.template to .env.local and fill in your values
2. Import the created components into your main application
3. Set up React Query provider in your app root
4. Implement the dashboard using the provided hooks
5. Test the SignalR connection and real-time features
6. Customize the UI components to match your design system

## Support
- Review the created TypeScript types for data structures
- Use the API client for all backend communication
- Leverage React Query hooks for efficient data management
- Implement SignalR for real-time features
- Check the console for connection status and errors

## Success Metrics
- [ ] API connectivity working
- [ ] Real-time updates functioning
- [ ] MYCA responses displaying correctly
- [ ] Device monitoring operational
- [ ] Performance within acceptable limits
- [ ] Mobile experience optimized
"@

$checklistContent | Out-File -FilePath "INTEGRATION_CHECKLIST.md" -Encoding UTF8
Write-Host "✅ Integration checklist created: INTEGRATION_CHECKLIST.md" -ForegroundColor Green

# Create package.json scripts (if package.json exists)
if (Test-Path "package.json") {
    Write-Host "`n📦 Adding npm scripts..." -ForegroundColor Yellow
    
    # Read existing package.json
    $packageJson = Get-Content "package.json" | ConvertFrom-Json
    
    # Add NatureOS specific scripts
    if (-not $packageJson.scripts) {
        $packageJson | Add-Member -NotePropertyName "scripts" -NotePropertyValue @{}
    }
    
    $packageJson.scripts | Add-Member -NotePropertyName "natureos:test" -NotePropertyValue "npm run test -- --testPathPattern=natureos" -Force
    $packageJson.scripts | Add-Member -NotePropertyName "natureos:build" -NotePropertyValue "npm run build" -Force
    $packageJson.scripts | Add-Member -NotePropertyName "natureos:dev" -NotePropertyValue "npm run dev" -Force
    
    $packageJson | ConvertTo-Json -Depth 10 | Out-File "package.json" -Encoding UTF8
    Write-Host "✅ npm scripts added to package.json" -ForegroundColor Green
}

Write-Host "`n✅ Setup complete!" -ForegroundColor Green
Write-Host "`n🎯 Next steps:" -ForegroundColor Cyan
Write-Host "1. Copy .env.local.template to .env.local and configure your values" -ForegroundColor White
Write-Host "2. Review INTEGRATION_CHECKLIST.md for detailed integration steps" -ForegroundColor White
Write-Host "3. Import the created hooks and components into your application" -ForegroundColor White
Write-Host "4. Test the API connectivity and real-time features" -ForegroundColor White
Write-Host "`n🌐 API URL configured: $ApiUrl" -ForegroundColor Gray
Write-Host "📁 Integration files created in: $ProjectPath" -ForegroundColor Gray

Write-Host "`n🚀 Your NatureOS frontend integration is ready!" -ForegroundColor Magenta 
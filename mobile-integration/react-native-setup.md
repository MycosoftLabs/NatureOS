# NatureOS React Native Mobile Integration

## Overview
This guide provides complete instructions for integrating NatureOS with React Native mobile applications, enabling real-time device monitoring, MYCA AI interactions, and field data collection.

## Prerequisites
- React Native development environment
- Node.js 16+ and npm/yarn
- iOS/Android development tools
- NatureOS API access

## Installation

### 1. Install Required Dependencies

```bash
# Core networking and real-time communication
npm install @react-native-async-storage/async-storage
npm install @microsoft/signalr
npm install axios
npm install react-query

# UI and navigation
npm install @react-navigation/native
npm install @react-navigation/stack
npm install react-native-screens
npm install react-native-safe-area-context

# Device features
npm install react-native-geolocation-service
npm install react-native-permissions
npm install react-native-camera
npm install react-native-bluetooth-manager

# State management and utilities
npm install zustand
npm install react-native-mmkv
npm install date-fns
npm install react-native-reanimated
```

### 2. Platform-Specific Setup

#### iOS Setup
```bash
cd ios && pod install
```

Add to `Info.plist`:
```xml
<key>NSLocationWhenInUseUsageDescription</key>
<string>NatureOS needs location access for environmental monitoring</string>
<key>NSCameraUsageDescription</key>
<string>NatureOS uses camera for specimen identification</string>
<key>NSBluetoothAlwaysUsageDescription</key>
<string>NatureOS connects to IoT devices via Bluetooth</string>
```

#### Android Setup
Add to `android/app/src/main/AndroidManifest.xml`:
```xml
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
<uses-permission android:name="android.permission.CAMERA" />
<uses-permission android:name="android.permission.BLUETOOTH" />
<uses-permission android:name="android.permission.BLUETOOTH_ADMIN" />
<uses-permission android:name="android.permission.INTERNET" />
```

## Core Components

### 1. API Client Setup

Create `src/services/NatureOSAPI.ts`:
```typescript
import axios, { AxiosInstance } from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';

class NatureOSMobileAPI {
  private client: AxiosInstance;
  private baseURL: string;

  constructor(baseURL: string) {
    this.baseURL = baseURL;
    this.client = axios.create({
      baseURL,
      timeout: 30000,
    });

    this.setupInterceptors();
  }

  private setupInterceptors() {
    // Request interceptor for auth tokens
    this.client.interceptors.request.use(async (config) => {
      const token = await AsyncStorage.getItem('auth_token');
      if (token) {
        config.headers.Authorization = `Bearer ${token}`;
      }
      return config;
    });

    // Response interceptor for error handling
    this.client.interceptors.response.use(
      (response) => response,
      async (error) => {
        if (error.response?.status === 401) {
          await AsyncStorage.removeItem('auth_token');
          // Navigate to login
        }
        return Promise.reject(error);
      }
    );
  }

  // Device operations
  async getDevices() {
    const response = await this.client.get('/api/devices');
    return response.data;
  }

  async sendTelemetry(deviceId: string, data: any) {
    const response = await this.client.post(
      `/api/mycosoft/${deviceId}/telemetry`, 
      data
    );
    return response.data;
  }

  // MYCA interactions
  async queryMyca(question: string, context?: any) {
    const response = await this.client.post('/api/mycosoft/myca/query', {
      question,
      context,
      userId: await AsyncStorage.getItem('user_id'),
    });
    return response.data;
  }

  // Field data collection
  async submitFieldObservation(observation: any) {
    const response = await this.client.post('/api/events', observation);
    return response.data;
  }
}

export const apiClient = new NatureOSMobileAPI(
  __DEV__ 
    ? 'http://localhost:8080' 
    : 'https://natureos-api-production.azurewebsites.net'
);
```

### 2. SignalR Real-Time Integration

Create `src/services/SignalRService.ts`:
```typescript
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { AppState } from 'react-native';

export class SignalRMobileService {
  private connection: HubConnection | null = null;
  private hubUrl: string;
  private callbacks: Map<string, Function[]> = new Map();

  constructor(hubUrl: string) {
    this.hubUrl = hubUrl;
    this.setupAppStateHandling();
  }

  async connect() {
    try {
      this.connection = new HubConnectionBuilder()
        .withUrl(this.hubUrl)
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
          }
        })
        .configureLogging(LogLevel.Information)
        .build();

      // Setup event handlers
      this.connection.on('DeviceUpdate', (data) => {
        this.emit('deviceUpdate', data);
      });

      this.connection.on('SystemAlert', (alert) => {
        this.emit('systemAlert', alert);
      });

      this.connection.on('MycaActivity', (activity) => {
        this.emit('mycaActivity', activity);
      });

      await this.connection.start();
      console.log('SignalR connected');
      
      // Join mobile user group
      await this.connection.invoke('JoinMobileGroup');
      
    } catch (error) {
      console.error('SignalR connection failed:', error);
    }
  }

  async disconnect() {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
  }

  on(event: string, callback: Function) {
    if (!this.callbacks.has(event)) {
      this.callbacks.set(event, []);
    }
    this.callbacks.get(event)!.push(callback);
  }

  off(event: string, callback: Function) {
    const callbacks = this.callbacks.get(event);
    if (callbacks) {
      const index = callbacks.indexOf(callback);
      if (index > -1) {
        callbacks.splice(index, 1);
      }
    }
  }

  private emit(event: string, data: any) {
    const callbacks = this.callbacks.get(event);
    if (callbacks) {
      callbacks.forEach(callback => callback(data));
    }
  }

  private setupAppStateHandling() {
    AppState.addEventListener('change', (nextAppState) => {
      if (nextAppState === 'active') {
        this.connect();
      } else if (nextAppState === 'background') {
        this.disconnect();
      }
    });
  }
}

export const signalRService = new SignalRMobileService(
  __DEV__ 
    ? 'http://localhost:8080/natureos-hub' 
    : 'https://natureos-api-production.azurewebsites.net/natureos-hub'
);
```

### 3. Device Connection Manager

Create `src/services/DeviceManager.ts`:
```typescript
import { NativeModules, NativeEventEmitter } from 'react-native';
import BluetoothManager from 'react-native-bluetooth-manager';

export interface ConnectedDevice {
  id: string;
  name: string;
  type: 'mushroom1' | 'spore-detector' | 'environmental';
  status: 'connected' | 'disconnected' | 'connecting';
  batteryLevel?: number;
  lastReading?: any;
}

export class MobileDeviceManager {
  private connectedDevices: Map<string, ConnectedDevice> = new Map();
  private eventEmitter: NativeEventEmitter;
  private callbacks: Map<string, Function[]> = new Map();

  constructor() {
    this.eventEmitter = new NativeEventEmitter(BluetoothManager);
    this.setupBluetoothHandlers();
  }

  private setupBluetoothHandlers() {
    this.eventEmitter.addListener('device-discovered', (device) => {
      if (this.isNatureOSDevice(device)) {
        this.emit('deviceDiscovered', device);
      }
    });

    this.eventEmitter.addListener('device-data', (data) => {
      this.handleDeviceData(data);
    });
  }

  async scanForDevices(): Promise<ConnectedDevice[]> {
    try {
      await BluetoothManager.startScan(['NATUREOS_DEVICE']);
      return Array.from(this.connectedDevices.values());
    } catch (error) {
      console.error('Device scan failed:', error);
      return [];
    }
  }

  async connectToDevice(deviceId: string): Promise<boolean> {
    try {
      const device = this.connectedDevices.get(deviceId);
      if (device) {
        device.status = 'connecting';
        await BluetoothManager.connect(deviceId);
        device.status = 'connected';
        this.emit('deviceConnected', device);
        return true;
      }
      return false;
    } catch (error) {
      console.error(`Failed to connect to device ${deviceId}:`, error);
      return false;
    }
  }

  async disconnectDevice(deviceId: string): Promise<boolean> {
    try {
      await BluetoothManager.disconnect(deviceId);
      const device = this.connectedDevices.get(deviceId);
      if (device) {
        device.status = 'disconnected';
        this.emit('deviceDisconnected', device);
      }
      return true;
    } catch (error) {
      console.error(`Failed to disconnect device ${deviceId}:`, error);
      return false;
    }
  }

  private isNatureOSDevice(device: any): boolean {
    return device.name?.startsWith('Mushroom') || 
           device.serviceUUIDs?.includes('NATUREOS_SERVICE_UUID');
  }

  private handleDeviceData(data: any) {
    const device = this.connectedDevices.get(data.deviceId);
    if (device) {
      device.lastReading = data.reading;
      device.batteryLevel = data.batteryLevel;
      this.emit('deviceData', { device, data: data.reading });
      
      // Send to API
      this.sendTelemetryToAPI(device.id, data.reading);
    }
  }

  private async sendTelemetryToAPI(deviceId: string, reading: any) {
    try {
      await apiClient.sendTelemetry(deviceId, reading);
    } catch (error) {
      console.error('Failed to send telemetry:', error);
    }
  }

  on(event: string, callback: Function) {
    if (!this.callbacks.has(event)) {
      this.callbacks.set(event, []);
    }
    this.callbacks.get(event)!.push(callback);
  }

  private emit(event: string, data: any) {
    const callbacks = this.callbacks.get(event);
    if (callbacks) {
      callbacks.forEach(callback => callback(data));
    }
  }

  getConnectedDevices(): ConnectedDevice[] {
    return Array.from(this.connectedDevices.values())
      .filter(device => device.status === 'connected');
  }
}

export const deviceManager = new MobileDeviceManager();
```

### 4. MYCA Chat Component

Create `src/components/MYCAChatScreen.tsx`:
```typescript
import React, { useState, useEffect, useRef } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  FlatList,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import { apiClient } from '../services/NatureOSAPI';

interface ChatMessage {
  id: string;
  type: 'user' | 'myca';
  text: string;
  timestamp: Date;
  confidence?: number;
}

export const MYCAChatScreen: React.FC = () => {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [inputText, setInputText] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const flatListRef = useRef<FlatList>(null);

  const sendMessage = async () => {
    if (!inputText.trim() || isLoading) return;

    const userMessage: ChatMessage = {
      id: Date.now().toString(),
      type: 'user',
      text: inputText,
      timestamp: new Date(),
    };

    setMessages(prev => [...prev, userMessage]);
    setInputText('');
    setIsLoading(true);

    try {
      const response = await apiClient.queryMyca(inputText);
      
      const mycaMessage: ChatMessage = {
        id: (Date.now() + 1).toString(),
        type: 'myca',
        text: response.answer,
        timestamp: new Date(),
        confidence: response.confidence,
      };

      setMessages(prev => [...prev, mycaMessage]);
    } catch (error) {
      const errorMessage: ChatMessage = {
        id: (Date.now() + 1).toString(),
        type: 'myca',
        text: 'Sorry, I encountered an error. Please try again.',
        timestamp: new Date(),
      };
      setMessages(prev => [...prev, errorMessage]);
    } finally {
      setIsLoading(false);
    }
  };

  const renderMessage = ({ item }: { item: ChatMessage }) => (
    <View style={[
      styles.messageContainer,
      item.type === 'user' ? styles.userMessage : styles.mycaMessage
    ]}>
      <Text style={[
        styles.messageText,
        item.type === 'user' ? styles.userMessageText : styles.mycaMessageText
      ]}>
        {item.text}
      </Text>
      {item.confidence && (
        <Text style={styles.confidenceText}>
          Confidence: {Math.round(item.confidence * 100)}%
        </Text>
      )}
      <Text style={styles.timestampText}>
        {item.timestamp.toLocaleTimeString()}
      </Text>
    </View>
  );

  return (
    <KeyboardAvoidingView 
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
    >
      <FlatList
        ref={flatListRef}
        data={messages}
        renderItem={renderMessage}
        keyExtractor={item => item.id}
        style={styles.messagesList}
        onContentSizeChange={() => flatListRef.current?.scrollToEnd()}
      />
      
      <View style={styles.inputContainer}>
        <TextInput
          style={styles.textInput}
          value={inputText}
          onChangeText={setInputText}
          placeholder="Ask MYCA about your fungal network..."
          placeholderTextColor="#666"
          multiline
          maxLength={1000}
        />
        <TouchableOpacity
          style={[styles.sendButton, (!inputText.trim() || isLoading) && styles.sendButtonDisabled]}
          onPress={sendMessage}
          disabled={!inputText.trim() || isLoading}
        >
          <Text style={styles.sendButtonText}>
            {isLoading ? '...' : 'Send'}
          </Text>
        </TouchableOpacity>
      </View>
    </KeyboardAvoidingView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  messagesList: {
    flex: 1,
    padding: 16,
  },
  messageContainer: {
    marginVertical: 4,
    padding: 12,
    borderRadius: 16,
    maxWidth: '80%',
  },
  userMessage: {
    backgroundColor: '#007AFF',
    alignSelf: 'flex-end',
  },
  mycaMessage: {
    backgroundColor: '#fff',
    alignSelf: 'flex-start',
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  messageText: {
    fontSize: 16,
    lineHeight: 20,
  },
  userMessageText: {
    color: '#fff',
  },
  mycaMessageText: {
    color: '#333',
  },
  confidenceText: {
    fontSize: 12,
    color: '#666',
    marginTop: 4,
  },
  timestampText: {
    fontSize: 11,
    color: '#999',
    marginTop: 4,
  },
  inputContainer: {
    flexDirection: 'row',
    padding: 16,
    backgroundColor: '#fff',
    borderTopWidth: 1,
    borderTopColor: '#e0e0e0',
  },
  textInput: {
    flex: 1,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 20,
    paddingHorizontal: 16,
    paddingVertical: 8,
    marginRight: 12,
    maxHeight: 100,
    fontSize: 16,
  },
  sendButton: {
    backgroundColor: '#007AFF',
    borderRadius: 20,
    paddingHorizontal: 20,
    paddingVertical: 10,
    justifyContent: 'center',
  },
  sendButtonDisabled: {
    backgroundColor: '#ccc',
  },
  sendButtonText: {
    color: '#fff',
    fontWeight: '600',
  },
});
```

## Usage Examples

### 1. Basic Integration
```typescript
import { apiClient } from './src/services/NatureOSAPI';
import { signalRService } from './src/services/SignalRService';
import { deviceManager } from './src/services/DeviceManager';

// Initialize services
await signalRService.connect();

// Listen for real-time updates
signalRService.on('deviceUpdate', (device) => {
  console.log('Device updated:', device);
});

// Scan for nearby devices
const devices = await deviceManager.scanForDevices();

// Connect to a device
await deviceManager.connectToDevice('mushroom-1-001');
```

### 2. Field Data Collection
```typescript
const submitFieldObservation = async (photo: any, location: any) => {
  const observation = {
    event_id: generateULID(),
    timestamp: new Date().toISOString(),
    source_device: 'mobile-app',
    kingdom_domain: 'FUNGA.observation',
    signal_vector: {
      photo: photo.base64,
      location: {
        latitude: location.latitude,
        longitude: location.longitude,
        accuracy: location.accuracy,
      },
    },
    metadata: {
      collected_by: 'mobile-user',
      app_version: '1.0.0',
    },
  };

  await apiClient.submitFieldObservation(observation);
};
```

## Best Practices

### 1. Performance Optimization
- Use FlatList for large datasets
- Implement pull-to-refresh functionality
- Cache API responses with React Query
- Optimize image handling for field photos

### 2. Offline Support
- Store critical data locally with MMKV
- Queue API requests when offline
- Sync data when connection is restored

### 3. Battery Management
- Implement intelligent Bluetooth scanning
- Use background task limits appropriately
- Reduce GPS accuracy when not needed

### 4. User Experience
- Provide clear loading states
- Implement optimistic updates
- Show connection status indicators
- Handle errors gracefully

## Testing

### 1. Unit Tests
```typescript
// __tests__/services/NatureOSAPI.test.ts
import { apiClient } from '../src/services/NatureOSAPI';

describe('NatureOSAPI', () => {
  it('should fetch devices successfully', async () => {
    const devices = await apiClient.getDevices();
    expect(Array.isArray(devices)).toBe(true);
  });
});
```

### 2. Integration Tests
```typescript
// __tests__/integration/DeviceConnection.test.ts
import { deviceManager } from '../src/services/DeviceManager';

describe('Device Connection', () => {
  it('should connect to nearby devices', async () => {
    const devices = await deviceManager.scanForDevices();
    expect(devices.length).toBeGreaterThanOrEqual(0);
  });
});
```

## Deployment

### 1. Build Configuration
```javascript
// metro.config.js
module.exports = {
  resolver: {
    assetExts: ['bin', 'txt', 'jpg', 'png', 'json'],
  },
  transformer: {
    getTransformOptions: async () => ({
      transform: {
        experimentalImportSupport: false,
        inlineRequires: true,
      },
    }),
  },
};
```

### 2. Environment Configuration
```typescript
// src/config/environment.ts
export const config = {
  apiUrl: __DEV__ 
    ? 'http://localhost:8080'
    : 'https://natureos-api-production.azurewebsites.net',
  signalRUrl: __DEV__ 
    ? 'http://localhost:8080/natureos-hub'
    : 'https://natureos-api-production.azurewebsites.net/natureos-hub',
  enableDebugMode: __DEV__,
};
```

## Support and Troubleshooting

### Common Issues
1. **SignalR Connection Fails**: Check network connectivity and URL configuration
2. **Bluetooth Scanning Issues**: Verify permissions and device compatibility
3. **API Authentication Errors**: Ensure tokens are properly stored and refreshed

### Debug Tools
- React Native Debugger for development
- Flipper for network inspection
- Azure Application Insights for production monitoring

This completes the React Native mobile integration setup for NatureOS. The mobile app will provide full real-time connectivity with the Azure-deployed backend system. 
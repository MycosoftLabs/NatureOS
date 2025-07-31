#!/bin/bash

# NatureOS Frontend Integration Setup Script
# This script helps integrate your v0.dev frontend with the NatureOS backend

set -e

echo "🌟 NatureOS Frontend Integration Setup"
echo "======================================"

# Check if we're in a frontend project directory
if [ ! -f "package.json" ]; then
    echo "❌ Error: package.json not found. Please run this script from your frontend project root."
    exit 1
fi

echo "📦 Installing required dependencies..."

# Install essential packages
npm install @microsoft/signalr axios swr @tanstack/react-query recharts lucide-react date-fns

# Install optional UI packages
echo "🎨 Installing optional UI enhancement packages..."
npm install framer-motion react-hot-toast react-hook-form @hookform/resolvers zod

echo "🔧 Creating integration directories..."
mkdir -p lib hooks components/ui

echo "📝 Creating environment template..."
cat > .env.local.template << EOF
# NatureOS Backend Configuration
NEXT_PUBLIC_NATUREOS_API_URL=https://natureos-api-prod001.azurewebsites.net
NEXT_PUBLIC_NATUREOS_API_KEY=your-api-key-here
NEXT_PUBLIC_WEBSOCKET_URL=wss://natureos-api-prod001.azurewebsites.net/natureos-hub
NEXT_PUBLIC_ENVIRONMENT=production

# For local development (uncomment these and comment above)
# NEXT_PUBLIC_NATUREOS_API_URL=http://localhost:8080
# NEXT_PUBLIC_WEBSOCKET_URL=ws://localhost:8080/natureos-hub
# NEXT_PUBLIC_ENVIRONMENT=development
EOF

echo "⚙️ Creating basic integration files..."

# Create API client
cat > lib/api-client.ts << 'EOF'
import axios from 'axios';

const apiClient = axios.create({
  baseURL: process.env.NEXT_PUBLIC_NATUREOS_API_URL,
  headers: {
    'Content-Type': 'application/json',
    'X-API-Key': process.env.NEXT_PUBLIC_NATUREOS_API_KEY,
  },
});

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('auth-token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('auth-token');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export default apiClient;
EOF

# Create NatureOS API service
cat > lib/natureos-api.ts << 'EOF'
import apiClient from './api-client';

export interface DashboardData {
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

class NatureOSAPI {
  async getDashboardData(): Promise<DashboardData> {
    const response = await apiClient.get('/api/mycosoft/website/dashboard');
    return response.data;
  }

  async getDevices() {
    const response = await apiClient.get('/api/devices');
    return response.data;
  }

  async getEvents(params?: any) {
    const response = await apiClient.get('/api/events', { params });
    return response.data;
  }

  async queryMyca(question: string, context?: string) {
    const response = await apiClient.post('/api/mycosoft/myca/query', {
      question,
      context: context || 'website-chat',
      userId: 'current-user'
    });
    return response.data;
  }
}

export const natureOSAPI = new NatureOSAPI();
EOF

# Create basic hook
cat > hooks/useNatureOSData.ts << 'EOF'
import { useQuery, useMutation } from '@tanstack/react-query';
import { natureOSAPI } from '../lib/natureos-api';

export const useDashboardData = () => {
  return useQuery({
    queryKey: ['dashboard'],
    queryFn: () => natureOSAPI.getDashboardData(),
    refetchInterval: 30000,
  });
};

export const useDevices = () => {
  return useQuery({
    queryKey: ['devices'],
    queryFn: () => natureOSAPI.getDevices(),
    refetchInterval: 10000,
  });
};

export const useMycaQuery = () => {
  return useMutation({
    mutationFn: ({ question, context }: { question: string; context?: string }) =>
      natureOSAPI.queryMyca(question, context),
  });
};
EOF

# Create basic UI components if using standard UI library structure
if [ ! -f "components/ui/card.tsx" ]; then
    echo "📄 Creating basic UI components..."
    
    mkdir -p components/ui
    
    cat > components/ui/card.tsx << 'EOF'
import React from 'react';

export const Card: React.FC<{ children: React.ReactNode; className?: string }> = ({ 
  children, 
  className = '' 
}) => (
  <div className={`bg-white rounded-lg border shadow-sm ${className}`}>
    {children}
  </div>
);

export const CardHeader: React.FC<{ children: React.ReactNode; className?: string }> = ({ 
  children, 
  className = '' 
}) => (
  <div className={`p-6 pb-2 ${className}`}>
    {children}
  </div>
);

export const CardTitle: React.FC<{ children: React.ReactNode; className?: string }> = ({ 
  children, 
  className = '' 
}) => (
  <h3 className={`text-lg font-semibold ${className}`}>
    {children}
  </h3>
);

export const CardContent: React.FC<{ children: React.ReactNode; className?: string }> = ({ 
  children, 
  className = '' 
}) => (
  <div className={`p-6 pt-2 ${className}`}>
    {children}
  </div>
);
EOF

    cat > components/ui/button.tsx << 'EOF'
import React from 'react';

export const Button: React.FC<{
  children: React.ReactNode;
  onClick?: () => void;
  disabled?: boolean;
  type?: 'button' | 'submit';
  size?: 'sm' | 'md' | 'lg' | 'icon';
  variant?: 'primary' | 'secondary' | 'outline';
  className?: string;
}> = ({ 
  children, 
  onClick, 
  disabled, 
  type = 'button', 
  size = 'md',
  variant = 'primary',
  className = '' 
}) => {
  const baseClasses = 'inline-flex items-center justify-center rounded-md font-medium transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2 disabled:opacity-50 disabled:pointer-events-none';
  
  const sizeClasses = {
    sm: 'h-8 px-3 text-sm',
    md: 'h-10 px-4 py-2',
    lg: 'h-12 px-6 text-lg',
    icon: 'h-10 w-10'
  };
  
  const variantClasses = {
    primary: 'bg-blue-600 text-white hover:bg-blue-700 focus:ring-blue-500',
    secondary: 'bg-gray-200 text-gray-900 hover:bg-gray-300 focus:ring-gray-500',
    outline: 'border border-gray-300 bg-white text-gray-700 hover:bg-gray-50 focus:ring-gray-500'
  };
  
  return (
    <button
      type={type}
      onClick={onClick}
      disabled={disabled}
      className={`${baseClasses} ${sizeClasses[size]} ${variantClasses[variant]} ${className}`}
    >
      {children}
    </button>
  );
};
EOF

    cat > components/ui/input.tsx << 'EOF'
import React from 'react';

export const Input: React.FC<{
  value?: string;
  onChange?: (e: React.ChangeEvent<HTMLInputElement>) => void;
  placeholder?: string;
  disabled?: boolean;
  type?: string;
  className?: string;
}> = ({ 
  value, 
  onChange, 
  placeholder, 
  disabled, 
  type = 'text',
  className = '' 
}) => (
  <input
    type={type}
    value={value}
    onChange={onChange}
    placeholder={placeholder}
    disabled={disabled}
    className={`flex h-10 w-full rounded-md border border-gray-300 bg-white px-3 py-2 text-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 ${className}`}
  />
);
EOF
fi

echo "📋 Creating integration checklist..."
cat > INTEGRATION_CHECKLIST.md << 'EOF'
# NatureOS Frontend Integration Checklist

## ✅ Setup Complete
- [x] Dependencies installed
- [x] Basic integration files created
- [x] Environment template created

## 🔧 Next Steps

### 1. Configure Environment Variables
```bash
# Copy the template and fill in your values
cp .env.local.template .env.local

# Edit .env.local with your actual values:
# - Get API key from NatureOS backend team
# - Update API URL if different
# - Configure WebSocket URL
```

### 2. Test API Connection
```bash
# Test your configuration
npm run dev

# Open browser console and check:
# - No CORS errors
# - API responses working
# - WebSocket connection established
```

### 3. Integrate with Your v0.dev Components
```bash
# Import the hooks in your components:
import { useDashboardData, useDevices, useMycaQuery } from './hooks/useNatureOSData';

# Use real-time connection:
import { useRealTimeConnection } from './hooks/useRealTimeConnection';
```

### 4. Add QueryClient Provider
```jsx
// In your main App component:
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const queryClient = new QueryClient();

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      {/* Your app components */}
    </QueryClientProvider>
  );
}
```

## 📚 Resources
- See `docs/frontend-integration-guide.md` for detailed instructions
- Example components in the guide
- Troubleshooting section for common issues

## 🆘 Need Help?
- Check browser console for errors
- Verify environment variables are loaded
- Test API endpoints manually with curl
- Check CORS configuration on backend
EOF

echo "✅ Setup complete!"
echo ""
echo "📋 Next steps:"
echo "1. Copy and configure environment variables:"
echo "   cp .env.local.template .env.local"
echo "   # Edit .env.local with your actual API key and URLs"
echo ""
echo "2. Start your development server:"
echo "   npm run dev"
echo ""
echo "3. Follow the integration checklist:"
echo "   cat INTEGRATION_CHECKLIST.md"
echo ""
echo "4. Check the detailed guide at:"
echo "   docs/frontend-integration-guide.md"
echo ""
echo "🎉 Your frontend is ready to integrate with NatureOS!" 
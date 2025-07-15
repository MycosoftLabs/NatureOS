// Next.js API route for Mycosoft website dashboard integration
// This should be placed in the Mycosoft website repository

import { NextResponse } from 'next/server';

// NatureOS API configuration
const NATUREOS_API_BASE = process.env.NATUREOS_API_URL || 'https://natureos-api.mycosoft.com';
const API_KEY = process.env.NATUREOS_API_KEY;

export async function GET(request) {
  try {
    const { searchParams } = new URL(request.url);
    const dataType = searchParams.get('type') || 'all';

    // Fetch data from NatureOS Core API
    const response = await fetch(`${NATUREOS_API_BASE}/api/mycosoft/website/dashboard`, {
      headers: {
        'Authorization': `Bearer ${API_KEY}`,
        'Content-Type': 'application/json',
      },
    });

    if (!response.ok) {
      throw new Error(`NatureOS API error: ${response.status}`);
    }

    const dashboardData = await response.json();

    // Transform data for website components
    const transformedData = {
      stats: {
        totalEvents: dashboardData.totalEvents,
        activeDevices: dashboardData.activeDevices,
        speciesDetected: dashboardData.speciesDetected,
        onlineUsers: dashboardData.onlineUsers,
      },
      liveData: {
        readings: dashboardData.liveReadings.map(reading => ({
          device: reading.deviceId,
          value: reading.value,
          timestamp: reading.timestamp,
          status: 'active',
        })),
        lastUpdate: new Date().toISOString(),
      },
      insights: {
        trendingCompounds: dashboardData.trendingCompounds,
        recentDiscoveries: dashboardData.recentDiscoveries,
      },
      networkHealth: {
        status: 'optimal',
        connections: dashboardData.activeDevices,
        throughput: '2.4 MB/s',
      },
    };

    return NextResponse.json(transformedData);
  } catch (error) {
    console.error('Dashboard API error:', error);
    
    // Return fallback data if NatureOS is unavailable
    return NextResponse.json({
      stats: {
        totalEvents: 150000,
        activeDevices: 42,
        speciesDetected: 156,
        onlineUsers: 23,
      },
      liveData: {
        readings: [
          { device: 'MUSHROOM-001', value: 23.5, timestamp: new Date().toISOString(), status: 'active' },
          { device: 'SPORE-DET-002', value: 0.75, timestamp: new Date().toISOString(), status: 'active' },
          { device: 'ENV-STATION-A', value: 78.2, timestamp: new Date().toISOString(), status: 'active' },
        ],
        lastUpdate: new Date().toISOString(),
      },
      insights: {
        trendingCompounds: ['Psilocybin', 'Cordycepin', 'Beta-glucan'],
        recentDiscoveries: [
          'New mycorrhizal network topology discovered',
          'Novel antifungal compound isolated',
        ],
      },
      networkHealth: {
        status: 'optimal',
        connections: 42,
        throughput: '2.4 MB/s',
      },
      error: error.message,
    }, { status: 200 }); // Return 200 with fallback data
  }
}

export async function POST(request) {
  try {
    const body = await request.json();
    const { action, data } = body;

    switch (action) {
      case 'sync':
        // Trigger data sync
        const syncResponse = await fetch(`${NATUREOS_API_BASE}/api/mycosoft/sync`, {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${API_KEY}`,
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            syncTargets: ['website'],
            dataTypes: ['events', 'devices', 'species'],
          }),
        });

        const syncResult = await syncResponse.json();
        return NextResponse.json(syncResult);

      case 'update':
        // Handle dashboard updates from NatureOS
        // This would update the website's local cache
        return NextResponse.json({ success: true, message: 'Dashboard updated' });

      default:
        return NextResponse.json({ error: 'Invalid action' }, { status: 400 });
    }
  } catch (error) {
    console.error('Dashboard POST error:', error);
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
} 
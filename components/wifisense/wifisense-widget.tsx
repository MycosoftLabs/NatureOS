'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Activity, Wifi, Users } from 'lucide-react';

interface WiFiSenseStatus {
  device_id: string;
  device_name: string;
  link_id: string;
  channel?: number;
  bandwidth?: number;
  csi_samples_count: number;
  last_csi_timestamp?: number;
  presence_events_count: number;
  last_presence_timestamp?: string;
}

export function WiFiSenseWidget() {
  const [status, setStatus] = useState<WiFiSenseStatus[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchStatus = async () => {
      try {
        const response = await fetch('/api/wifisense/status');
        if (!response.ok) throw new Error('Failed to fetch status');
        const data = await response.json();
        setStatus(data);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Unknown error');
      } finally {
        setLoading(false);
      }
    };

    fetchStatus();
    const interval = setInterval(fetchStatus, 5000); // Refresh every 5 seconds

    return () => clearInterval(interval);
  }, []);

  if (loading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>WiFi Sense</CardTitle>
          <CardDescription>Loading...</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  if (error) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>WiFi Sense</CardTitle>
          <CardDescription className="text-destructive">{error}</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  const totalSamples = status.reduce((sum, s) => sum + s.csi_samples_count, 0);
  const totalEvents = status.reduce((sum, s) => sum + s.presence_events_count, 0);
  const activeDevices = status.filter(s => s.csi_samples_count > 0).length;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Wifi className="h-5 w-5" />
          WiFi Sense
        </CardTitle>
        <CardDescription>Radio-based presence and motion sensing</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="grid grid-cols-3 gap-4 mb-4">
          <div className="flex flex-col">
            <div className="text-2xl font-bold">{activeDevices}</div>
            <div className="text-sm text-muted-foreground">Active Devices</div>
          </div>
          <div className="flex flex-col">
            <div className="text-2xl font-bold">{totalSamples.toLocaleString()}</div>
            <div className="text-sm text-muted-foreground">CSI Samples</div>
          </div>
          <div className="flex flex-col">
            <div className="text-2xl font-bold">{totalEvents.toLocaleString()}</div>
            <div className="text-sm text-muted-foreground">Presence Events</div>
          </div>
        </div>

        <div className="space-y-2">
          {status.length === 0 ? (
            <p className="text-sm text-muted-foreground">No WiFi Sense devices configured</p>
          ) : (
            status.map((device) => (
              <div
                key={device.device_id}
                className="flex items-center justify-between p-2 border rounded"
              >
                <div className="flex-1">
                  <div className="font-medium">{device.device_name}</div>
                  <div className="text-sm text-muted-foreground">
                    Link: {device.link_id}
                    {device.channel && ` • Channel ${device.channel}`}
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant={device.csi_samples_count > 0 ? 'default' : 'secondary'}>
                    {device.csi_samples_count} samples
                  </Badge>
                  {device.presence_events_count > 0 && (
                    <Badge variant="outline">
                      <Users className="h-3 w-3 mr-1" />
                      {device.presence_events_count}
                    </Badge>
                  )}
                </div>
              </div>
            ))
          )}
        </div>
      </CardContent>
    </Card>
  );
}


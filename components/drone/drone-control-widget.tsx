'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Drone, Battery, MapPin, Package } from 'lucide-react';

interface DroneStatus {
  drone_id: string;
  drone_name: string;
  drone_type: string;
  max_payload_kg: number;
  current_latitude?: number;
  current_longitude?: number;
  current_altitude?: number;
  battery_percent?: number;
  flight_mode?: string;
  mission_state?: string;
  payload_latched?: boolean;
  payload_type?: string;
  active_mission_status?: string;
  active_mission_progress?: number;
  last_telemetry_time?: string;
}

export function DroneControlWidget() {
  const [status, setStatus] = useState<DroneStatus[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchStatus = async () => {
      try {
        const response = await fetch('/api/drone/status');
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
    const interval = setInterval(fetchStatus, 2000); // Refresh every 2 seconds

    return () => clearInterval(interval);
  }, []);

  if (loading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>MycoDRONE</CardTitle>
          <CardDescription>Loading...</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  if (error) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>MycoDRONE</CardTitle>
          <CardDescription className="text-destructive">{error}</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Drone className="h-5 w-5" />
          MycoDRONE
        </CardTitle>
        <CardDescription>Autonomous deployment and recovery</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          {status.length === 0 ? (
            <p className="text-sm text-muted-foreground">No drones registered</p>
          ) : (
            status.map((drone) => (
              <div
                key={drone.drone_id}
                className="p-4 border rounded-lg space-y-3"
              >
                <div className="flex items-center justify-between">
                  <div>
                    <div className="font-medium">{drone.drone_name}</div>
                    <div className="text-sm text-muted-foreground">
                      {drone.drone_type} • Max payload: {drone.max_payload_kg}kg
                    </div>
                  </div>
                  <Badge variant={drone.battery_percent && drone.battery_percent > 20 ? 'default' : 'destructive'}>
                    <Battery className="h-3 w-3 mr-1" />
                    {drone.battery_percent ?? 'N/A'}%
                  </Badge>
                </div>

                {drone.current_latitude && drone.current_longitude && (
                  <div className="flex items-center gap-2 text-sm">
                    <MapPin className="h-4 w-4 text-muted-foreground" />
                    <span>
                      {drone.current_latitude.toFixed(6)}, {drone.current_longitude.toFixed(6)}
                    </span>
                    {drone.current_altitude && (
                      <span className="text-muted-foreground">
                        • {drone.current_altitude.toFixed(1)}m
                      </span>
                    )}
                  </div>
                )}

                <div className="flex items-center gap-2">
                  {drone.flight_mode && (
                    <Badge variant="outline">{drone.flight_mode}</Badge>
                  )}
                  {drone.mission_state && (
                    <Badge variant="secondary">{drone.mission_state}</Badge>
                  )}
                  {drone.payload_latched && (
                    <Badge variant="default">
                      <Package className="h-3 w-3 mr-1" />
                      {drone.payload_type || 'Payload'}
                    </Badge>
                  )}
                </div>

                {drone.active_mission_status && (
                  <div className="space-y-1">
                    <div className="flex items-center justify-between text-sm">
                      <span>Mission: {drone.active_mission_status}</span>
                      {drone.active_mission_progress !== undefined && (
                        <span>{drone.active_mission_progress}%</span>
                      )}
                    </div>
                    {drone.active_mission_progress !== undefined && (
                      <div className="w-full bg-secondary rounded-full h-2">
                        <div
                          className="bg-primary h-2 rounded-full transition-all"
                          style={{ width: `${drone.active_mission_progress}%` }}
                        />
                      </div>
                    )}
                  </div>
                )}

                <div className="flex gap-2">
                  <Button size="sm" variant="outline">
                    View Details
                  </Button>
                  <Button size="sm" variant="outline">
                    Plan Mission
                  </Button>
                </div>
              </div>
            ))
          )}
        </div>
      </CardContent>
    </Card>
  );
}


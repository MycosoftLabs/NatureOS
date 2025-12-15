'use client';

import React, { useEffect, useMemo, useState } from 'react';

type MycoBrainDevice = {
  device_id: string;
  firmware_version: string;
  status: string;
  last_seen?: string;
  i2c_addresses?: string[];
  analog_labels?: Record<string, string>;
  mosfet_labels?: Record<string, string>;
  purpose?: string;
};

type MycoBrainTelemetry = {
  seq: number;
  serial: string;
  fw_version: string;
  ts: number;
  side_a?: {
    ai_volts?: Record<string, number>;
    bme688?: {
      temperature: number;
      humidity: number;
      pressure: number;
      gas_resistance: number;
    };
    i2c_devices?: string[];
    mosfet_states?: Record<string, boolean>;
    uptime_ms?: number;
  };
  side_b?: {
    rssi?: number;
    snr?: number;
    tx_count?: number;
    rx_count?: number;
    ack_count?: number;
    retry_count?: number;
  };
};

export default function MycoBrainWidget(props: { apiBaseUrl?: string }) {
  const apiBaseUrl = props.apiBaseUrl ?? process.env.NEXT_PUBLIC_API_URL ?? '';
  const [devices, setDevices] = useState<MycoBrainDevice[]>([]);
  const [selected, setSelected] = useState<string>('');
  const [telemetry, setTelemetry] = useState<MycoBrainTelemetry | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        const res = await fetch(`${apiBaseUrl}/api/mycobrain/devices`);
        if (!res.ok) throw new Error(`devices: ${res.status}`);
        const list = await res.json();
        setDevices(list);
        if (!selected && list?.length) setSelected(list[0].device_id);
      } catch (e: any) {
        setError(e?.message ?? 'Failed to load devices');
      }
    })();
  }, [apiBaseUrl]);

  useEffect(() => {
    if (!selected) return;
    (async () => {
      try {
        const since = new Date(Date.now() - 60 * 60 * 1000).toISOString();
        const res = await fetch(`${apiBaseUrl}/api/mycobrain/devices/${selected}/telemetry?startTime=${encodeURIComponent(since)}`);
        if (!res.ok) throw new Error(`telemetry: ${res.status}`);
        const rows: MycoBrainTelemetry[] = await res.json();
        setTelemetry(rows?.[0] ?? null);
      } catch (e: any) {
        setError(e?.message ?? 'Failed to load telemetry');
      }
    })();
  }, [apiBaseUrl, selected]);

  const currentDevice = useMemo(() => devices.find(d => d.device_id === selected) ?? null, [devices, selected]);

  if (!apiBaseUrl) {
    return (
      <div className="bg-white shadow rounded-lg p-6">
        <div className="text-sm text-gray-600">Set <code className="font-mono">NEXT_PUBLIC_API_URL</code> to your Core API base URL to enable MycoBrain widget.</div>
      </div>
    );
  }

  return (
    <div className="bg-white shadow rounded-lg">
      <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
        <h3 className="text-lg font-medium text-gray-900">MycoBrain</h3>
        <div className="flex items-center gap-2">
          <select
            value={selected}
            onChange={(e) => setSelected(e.target.value)}
            className="px-3 py-2 border border-gray-300 rounded-md text-sm"
          >
            {devices.map(d => (
              <option key={d.device_id} value={d.device_id}>{d.device_id}{d.purpose ? ` â€” ${d.purpose}` : ''}</option>
            ))}
          </select>
        </div>
      </div>

      <div className="p-6 space-y-4">
        {error && <div className="text-sm text-red-600">{error}</div>}

        {!currentDevice ? (
          <div className="text-sm text-gray-600">No MycoBrain devices registered.</div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="bg-gray-50 rounded p-3">
              <div className="text-xs text-gray-500">Firmware</div>
              <div className="font-semibold">{currentDevice.firmware_version || 'â€”'}</div>
            </div>
            <div className="bg-gray-50 rounded p-3">
              <div className="text-xs text-gray-500">Status</div>
              <div className="font-semibold">{currentDevice.status || 'â€”'}</div>
            </div>
            <div className="bg-gray-50 rounded p-3">
              <div className="text-xs text-gray-500">IÂ²C</div>
              <div className="font-semibold">{(currentDevice.i2c_addresses?.length ?? 0).toString()}</div>
            </div>
            <div className="bg-gray-50 rounded p-3">
              <div className="text-xs text-gray-500">Last seen</div>
              <div className="font-semibold text-sm">{currentDevice.last_seen ? new Date(currentDevice.last_seen).toLocaleString() : 'â€”'}</div>
            </div>
          </div>
        )}

        {telemetry?.side_a?.bme688 && (
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="bg-blue-50 rounded p-3">
              <div className="text-xs text-gray-500">Temp</div>
              <div className="font-semibold">{telemetry.side_a.bme688.temperature.toFixed(1)}Â°C</div>
            </div>
            <div className="bg-blue-50 rounded p-3">
              <div className="text-xs text-gray-500">Humidity</div>
              <div className="font-semibold">{telemetry.side_a.bme688.humidity.toFixed(1)}%</div>
            </div>
            <div className="bg-blue-50 rounded p-3">
              <div className="text-xs text-gray-500">Pressure</div>
              <div className="font-semibold">{telemetry.side_a.bme688.pressure.toFixed(1)} hPa</div>
            </div>
            <div className="bg-blue-50 rounded p-3">
              <div className="text-xs text-gray-500">Gas R</div>
              <div className="font-semibold">{Math.round(telemetry.side_a.bme688.gas_resistance).toLocaleString()} Î©</div>
            </div>
          </div>
        )}

        {telemetry?.side_b && (
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="bg-purple-50 rounded p-3">
              <div className="text-xs text-gray-500">RSSI</div>
              <div className="font-semibold">{telemetry.side_b.rssi ?? 'â€”'}</div>
            </div>
            <div className="bg-purple-50 rounded p-3">
              <div className="text-xs text-gray-500">SNR</div>
              <div className="font-semibold">{telemetry.side_b.snr ?? 'â€”'}</div>
            </div>
            <div className="bg-purple-50 rounded p-3">
              <div className="text-xs text-gray-500">TX/RX</div>
              <div className="font-semibold">{telemetry.side_b.tx_count ?? 0}/{telemetry.side_b.rx_count ?? 0}</div>
            </div>
            <div className="bg-purple-50 rounded p-3">
              <div className="text-xs text-gray-500">Retries</div>
              <div className="font-semibold">{telemetry.side_b.retry_count ?? 0}</div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

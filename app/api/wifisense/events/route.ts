import { NextResponse } from 'next/server';

export async function GET(request: Request) {
  try {
    const { searchParams } = new URL(request.url);
    const zoneId = searchParams.get('zone_id');
    const since = searchParams.get('since');

    const params = new URLSearchParams();
    if (zoneId) params.append('zone_id', zoneId);
    if (since) params.append('since', since);

    const response = await fetch(
      `${process.env.MINDEX_API_BASE_URL}/wifisense/events?${params.toString()}`,
      {
        headers: {
          'X-API-Key': process.env.MINDEX_API_KEY!,
        },
        cache: 'no-store',
      }
    );

    if (!response.ok) {
      return NextResponse.json(
        { error: 'Failed to fetch WiFi Sense events' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('WiFi Sense events error:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}


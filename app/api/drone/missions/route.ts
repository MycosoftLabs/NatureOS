import { NextResponse } from 'next/server';

export async function POST(request: Request) {
  try {
    const body = await request.json();

    const response = await fetch(
      `${process.env.MINDEX_API_BASE_URL}/drone/missions`,
      {
        method: 'POST',
        headers: {
          'X-API-Key': process.env.MINDEX_API_KEY!,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(body),
      }
    );

    if (!response.ok) {
      const error = await response.json();
      return NextResponse.json(
        { error: error.detail || 'Failed to create mission' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Drone mission creation error:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}

export async function GET(request: Request) {
  try {
    const { searchParams } = new URL(request.url);
    const droneId = searchParams.get('drone_id');
    const status = searchParams.get('status');

    const params = new URLSearchParams();
    if (droneId) params.append('drone_id', droneId);
    if (status) params.append('status', status);

    const response = await fetch(
      `${process.env.MINDEX_API_BASE_URL}/drone/missions?${params.toString()}`,
      {
        headers: {
          'X-API-Key': process.env.MINDEX_API_KEY!,
        },
        cache: 'no-store',
      }
    );

    if (!response.ok) {
      return NextResponse.json(
        { error: 'Failed to fetch missions' },
        { status: response.status }
      );
    }

    const data = await response.json();
    return NextResponse.json(data);
  } catch (error) {
    console.error('Drone missions fetch error:', error);
    return NextResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }
}


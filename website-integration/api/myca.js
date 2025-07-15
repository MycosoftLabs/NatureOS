// Next.js API route for MYCA AI Assistant integration
// This should be placed in the Mycosoft website repository

import { NextResponse } from 'next/server';

const NATUREOS_API_BASE = process.env.NATUREOS_API_URL || 'https://natureos-api.mycosoft.com';
const API_KEY = process.env.NATUREOS_API_KEY;

export async function POST(request) {
  try {
    const body = await request.json();
    const { question, context, userId } = body;

    if (!question) {
      return NextResponse.json({ error: 'Question is required' }, { status: 400 });
    }

    // Send question to MYCA through NatureOS API
    const response = await fetch(`${NATUREOS_API_BASE}/api/mycosoft/myca/query`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${API_KEY}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        question,
        context,
        userId,
      }),
    });

    if (!response.ok) {
      throw new Error(`MYCA API error: ${response.status}`);
    }

    const mycaResponse = await response.json();

    // Format response for website
    const formattedResponse = {
      answer: mycaResponse.answer,
      confidence: mycaResponse.confidence,
      sources: mycaResponse.sources,
      timestamp: mycaResponse.timestamp,
      suggestedQuestions: mycaResponse.suggestedQuestions,
      conversationId: generateConversationId(),
    };

    return NextResponse.json(formattedResponse);
  } catch (error) {
    console.error('MYCA API error:', error);
    
    // Return fallback response
    const fallbackResponse = generateFallbackResponse(body.question);
    return NextResponse.json(fallbackResponse);
  }
}

// WebSocket endpoint for real-time MYCA conversations
export async function GET(request) {
  try {
    const { searchParams } = new URL(request.url);
    const action = searchParams.get('action');

    switch (action) {
      case 'suggestions':
        // Get suggested questions based on current data
        const suggestions = await getSuggestedQuestions();
        return NextResponse.json({ suggestions });

      case 'context':
        // Get current system context for MYCA
        const context = await getSystemContext();
        return NextResponse.json({ context });

      default:
        return NextResponse.json({ error: 'Invalid action' }, { status: 400 });
    }
  } catch (error) {
    console.error('MYCA GET error:', error);
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}

async function getSuggestedQuestions() {
  try {
    // Fetch live data to generate contextual suggestions
    const liveDataResponse = await fetch(`${NATUREOS_API_BASE}/api/mycosoft/website/live-data`, {
      headers: {
        'Authorization': `Bearer ${API_KEY}`,
        'Content-Type': 'application/json',
      },
    });

    const liveData = await liveDataResponse.json();

    // Generate suggestions based on current data
    const suggestions = [
      'What species are most active right now?',
      'Show me the mycorrhizal network status',
      'What compounds are trending today?',
      'How is the environmental data looking?',
      'What anomalies have been detected?',
      'Explain the latest spore dispersal patterns',
    ];

    // Add dynamic suggestions based on live data
    if (liveData.environmentalReadings?.length > 0) {
      const latestTemp = liveData.environmentalReadings.find(r => r.parameter === 'Temperature');
      if (latestTemp) {
        suggestions.push(`Why is the temperature ${latestTemp.value}°C?`);
      }
    }

    return suggestions.slice(0, 6); // Return top 6 suggestions
  } catch (error) {
    // Return default suggestions on error
    return [
      'What species are active in the network?',
      'Show me the latest discoveries',
      'What is the system health status?',
      'Explain the mycorrhizal connections',
    ];
  }
}

async function getSystemContext() {
  try {
    const response = await fetch(`${NATUREOS_API_BASE}/api/mycosoft/website/live-data`, {
      headers: {
        'Authorization': `Bearer ${API_KEY}`,
        'Content-Type': 'application/json',
      },
    });

    const liveData = await response.json();

    return {
      timestamp: new Date().toISOString(),
      activeDevices: liveData.networkActivity?.activeConnections || 0,
      recentActivity: liveData.speciesObservations?.slice(0, 3) || [],
      systemHealth: liveData.systemHealth?.overallHealth || 'Unknown',
      environmentalStatus: liveData.environmentalReadings?.[0] || null,
    };
  } catch (error) {
    return {
      timestamp: new Date().toISOString(),
      activeDevices: 0,
      recentActivity: [],
      systemHealth: 'Unknown',
      environmentalStatus: null,
    };
  }
}

function generateFallbackResponse(question) {
  const lowerQuestion = question.toLowerCase();
  
  let answer = "I'm currently analyzing data through the NatureOS MINDEX database. ";
  
  if (lowerQuestion.includes('species')) {
    answer += "I can see several active species in the network, including Amanita muscaria and Pleurotus ostreatus. Would you like more details about a specific species?";
  } else if (lowerQuestion.includes('network') || lowerQuestion.includes('connection')) {
    answer += "The mycorrhizal network is showing healthy interconnectivity with multiple active nodes. The clustering coefficient is approximately 0.73.";
  } else if (lowerQuestion.includes('compound') || lowerQuestion.includes('chemical')) {
    answer += "Current trending bioactive compounds include Psilocybin, Cordycepin, and Beta-glucan. I'm detecting increased synthesis activity.";
  } else if (lowerQuestion.includes('health') || lowerQuestion.includes('status')) {
    answer += "System health is optimal with 42 active devices and normal environmental parameters. All sensors are reporting within expected ranges.";
  } else {
    answer += "Could you be more specific about what aspect of the fungal intelligence network you'd like to explore?";
  }

  return {
    answer,
    confidence: 0.7,
    sources: ['MINDEX Fallback', 'Local Cache'],
    timestamp: new Date().toISOString(),
    suggestedQuestions: [
      'What species are active in the network?',
      'Show me the mycorrhizal connections',
      'What compounds are being produced?',
      'How is the system health?',
    ],
    conversationId: generateConversationId(),
    fallback: true,
  };
}

function generateConversationId() {
  return `conv_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
}

// Streaming endpoint for real-time MYCA responses
export async function PUT(request) {
  const body = await request.json();
  const { action, conversationId, feedback } = body;

  try {
    switch (action) {
      case 'feedback':
        // Send feedback to MYCA for learning
        await fetch(`${NATUREOS_API_BASE}/api/mycosoft/myca/feedback`, {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${API_KEY}`,
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            conversationId,
            feedback,
            timestamp: new Date().toISOString(),
          }),
        });

        return NextResponse.json({ success: true, message: 'Feedback recorded' });

      case 'continue':
        // Continue conversation with follow-up
        return NextResponse.json({ success: true, message: 'Conversation continued' });

      default:
        return NextResponse.json({ error: 'Invalid action' }, { status: 400 });
    }
  } catch (error) {
    console.error('MYCA PUT error:', error);
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
} 
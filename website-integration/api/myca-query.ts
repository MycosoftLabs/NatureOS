/**
 * MYCA (Mycosoft Cognitive Assistant) Integration API
 * Handles AI queries and responses for the NatureOS platform
 */

const NATUREOS_API_BASE = process.env.NATUREOS_API_URL || 'https://natureos-api-production.azurewebsites.net';

/**
 * MYCA query request structure
 */
export interface MycaQueryRequest {
  question: string;
  context?: string;
  userId?: string;
}

/**
 * MYCA response structure
 */
export interface MycaResponse {
  answer: string;
  confidence: number;
  timestamp: string;
  suggestedQuestions?: string[];
  sources?: string[];
}

/**
 * MYCA conversation history item
 */
export interface MycaConversationItem {
  id: string;
  question: string;
  response: MycaResponse;
  timestamp: string;
}

/**
 * MYCA API client for AI assistant integration
 */
export class MycaAPI {
  private baseURL: string;
  private conversationHistory: MycaConversationItem[] = [];

  constructor(baseURL: string = NATUREOS_API_BASE) {
    this.baseURL = baseURL;
  }

  /**
   * Send a query to MYCA AI assistant
   */
  async query(question: string, context?: string, userId?: string): Promise<MycaResponse> {
    try {
      const requestBody: MycaQueryRequest = {
        question,
        context: context || this.buildContextFromHistory(),
        userId: userId || 'anonymous'
      };

      const response = await fetch(`${this.baseURL}/api/mycosoft/myca/query`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestBody)
      });

      if (!response.ok) {
        throw new Error(`MYCA query failed: ${response.statusText}`);
      }

      const mycaResponse: MycaResponse = await response.json();
      
      // Add to conversation history
      this.addToHistory(question, mycaResponse);
      
      return mycaResponse;
    } catch (error) {
      console.error('MYCA query error:', error);
      throw new Error('Failed to query MYCA assistant');
    }
  }

  /**
   * Get conversation history
   */
  getConversationHistory(): MycaConversationItem[] {
    return [...this.conversationHistory];
  }

  /**
   * Clear conversation history
   */
  clearHistory(): void {
    this.conversationHistory = [];
  }

  /**
   * Add item to conversation history
   */
  private addToHistory(question: string, response: MycaResponse): void {
    const item: MycaConversationItem = {
      id: this.generateId(),
      question,
      response,
      timestamp: new Date().toISOString()
    };

    this.conversationHistory.push(item);
    
    // Keep only last 10 conversations to manage memory
    if (this.conversationHistory.length > 10) {
      this.conversationHistory = this.conversationHistory.slice(-10);
    }
  }

  /**
   * Build context string from recent conversation history
   */
  private buildContextFromHistory(): string {
    const recentHistory = this.conversationHistory.slice(-3); // Last 3 exchanges
    return recentHistory
      .map(item => `Q: ${item.question}\nA: ${item.response.answer}`)
      .join('\n\n');
  }

  /**
   * Generate unique ID for conversation items
   */
  private generateId(): string {
    return Date.now().toString(36) + Math.random().toString(36).substr(2);
  }

  /**
   * Get suggested questions based on system state
   */
  async getSuggestedQuestions(): Promise<string[]> {
    try {
      // This could be enhanced to fetch dynamic suggestions
      const commonQuestions = [
        "What's the current system health?",
        "Show me recent device activity",
        "What species have been detected today?",
        "Are there any system alerts?",
        "What are the trending compounds?",
        "How many devices are online?",
        "Show me network connectivity patterns",
        "What's the data quality score?",
        "Are there any anomalies detected?",
        "What's the current processing throughput?"
      ];

      // Randomize and return subset
      const shuffled = commonQuestions.sort(() => 0.5 - Math.random());
      return shuffled.slice(0, 4);
    } catch (error) {
      console.error('Failed to get suggested questions:', error);
      return [
        "Help me understand the system status",
        "What can you tell me about recent activity?",
        "Show me system insights",
        "What should I know about the current state?"
      ];
    }
  }

  /**
   * Validate query before sending
   */
  validateQuery(question: string): { valid: boolean; message?: string } {
    if (!question || question.trim().length === 0) {
      return { valid: false, message: 'Question cannot be empty' };
    }

    if (question.length > 1000) {
      return { valid: false, message: 'Question is too long (max 1000 characters)' };
    }

    if (question.length < 3) {
      return { valid: false, message: 'Question is too short (min 3 characters)' };
    }

    return { valid: true };
  }

  /**
   * Format MYCA response for display
   */
  formatResponse(response: MycaResponse): string {
    let formatted = response.answer;

    // Add confidence indicator if low
    if (response.confidence < 0.7) {
      formatted += `\n\n*Note: This response has moderate confidence (${Math.round(response.confidence * 100)}%). Please verify the information.*`;
    }

    // Add timestamp
    formatted += `\n\n*Response generated at ${this.formatTimestamp(response.timestamp)}*`;

    return formatted;
  }

  /**
   * Format timestamp for display
   */
  private formatTimestamp(timestamp: string): string {
    return new Date(timestamp).toLocaleString();
  }

  /**
   * Export conversation history as JSON
   */
  exportHistory(): string {
    return JSON.stringify(this.conversationHistory, null, 2);
  }

  /**
   * Import conversation history from JSON
   */
  importHistory(historyJson: string): boolean {
    try {
      const imported = JSON.parse(historyJson);
      if (Array.isArray(imported)) {
        this.conversationHistory = imported;
        return true;
      }
      return false;
    } catch (error) {
      console.error('Failed to import history:', error);
      return false;
    }
  }
}

// Export singleton instance
export const mycaAPI = new MycaAPI();

// Export utility functions
export const formatConfidence = (confidence: number): string => {
  const percentage = Math.round(confidence * 100);
  if (percentage >= 90) return `${percentage}% (Excellent)`;
  if (percentage >= 70) return `${percentage}% (Good)`;
  if (percentage >= 50) return `${percentage}% (Moderate)`;
  return `${percentage}% (Low)`;
};

export const getConfidenceColor = (confidence: number): string => {
  if (confidence >= 0.9) return '#10B981'; // Green
  if (confidence >= 0.7) return '#F59E0B'; // Yellow
  if (confidence >= 0.5) return '#EF4444'; // Red
  return '#6B7280'; // Gray
};

export default MycaAPI; 
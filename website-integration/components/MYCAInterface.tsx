import React, { useState, useEffect, useRef } from 'react';
import { mycaAPI, MycaResponse, MycaConversationItem, formatConfidence, getConfidenceColor } from '../api/myca-query';

interface MYCAInterfaceProps {
  userId?: string;
  className?: string;
  showHistory?: boolean;
  maxHeight?: number;
}

export const MYCAInterface: React.FC<MYCAInterfaceProps> = ({
  userId = 'anonymous',
  className = '',
  showHistory = true,
  maxHeight = 500
}) => {
  const [question, setQuestion] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [conversation, setConversation] = useState<MycaConversationItem[]>([]);
  const [suggestedQuestions, setSuggestedQuestions] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [showSuggestions, setShowSuggestions] = useState(true);
  
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  // Scroll to bottom when new messages arrive
  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  // Load conversation history and suggested questions on mount
  useEffect(() => {
    setConversation(mycaAPI.getConversationHistory());
    loadSuggestedQuestions();
  }, []);

  // Scroll to bottom when conversation updates
  useEffect(() => {
    scrollToBottom();
  }, [conversation]);

  // Load suggested questions
  const loadSuggestedQuestions = async () => {
    try {
      const suggestions = await mycaAPI.getSuggestedQuestions();
      setSuggestedQuestions(suggestions);
    } catch (err) {
      console.error('Failed to load suggestions:', err);
    }
  };

  // Handle question submission
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!question.trim()) return;

    // Validate question
    const validation = mycaAPI.validateQuery(question);
    if (!validation.valid) {
      setError(validation.message || 'Invalid question');
      return;
    }

    setIsLoading(true);
    setError(null);
    setShowSuggestions(false);

    try {
      const response = await mycaAPI.query(question, undefined, userId);
      
      // Update conversation from API history
      setConversation(mycaAPI.getConversationHistory());
      
      // Clear input
      setQuestion('');
      
      // Focus back on input
      inputRef.current?.focus();
      
    } catch (err) {
      setError('Failed to get response from MYCA. Please try again.');
      console.error('MYCA query error:', err);
    } finally {
      setIsLoading(false);
    }
  };

  // Handle suggested question click
  const handleSuggestionClick = (suggestion: string) => {
    setQuestion(suggestion);
    setShowSuggestions(false);
    inputRef.current?.focus();
  };

  // Clear conversation
  const handleClearHistory = () => {
    mycaAPI.clearHistory();
    setConversation([]);
    setShowSuggestions(true);
  };

  // Export conversation
  const handleExportHistory = () => {
    const historyJson = mycaAPI.exportHistory();
    const blob = new Blob([historyJson], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `myca-conversation-${new Date().toISOString().split('T')[0]}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  return (
    <div className={`myca-interface ${className}`}>
      {/* Header */}
      <div className="myca-header">
        <div className="header-content">
          <h3>MYCA AI Assistant</h3>
          <p>Ask questions about your NatureOS system and fungal network</p>
        </div>
        <div className="header-actions">
          {conversation.length > 0 && (
            <>
              <button onClick={handleExportHistory} className="action-btn" title="Export conversation">
                📁 Export
              </button>
              <button onClick={handleClearHistory} className="action-btn" title="Clear conversation">
                🗑️ Clear
              </button>
            </>
          )}
        </div>
      </div>

      {/* Conversation Area */}
      <div className="conversation-area" style={{ maxHeight: `${maxHeight}px` }}>
        {conversation.length === 0 && showSuggestions ? (
          <div className="welcome-screen">
            <div className="welcome-message">
              <h4>👋 Hello! I'm MYCA, your AI assistant</h4>
              <p>I can help you understand your NatureOS system, analyze data, and provide insights about your fungal network.</p>
            </div>
            
            <div className="suggested-questions">
              <h5>Try asking me:</h5>
              <div className="suggestions-grid">
                {suggestedQuestions.map((suggestion, index) => (
                  <button
                    key={index}
                    onClick={() => handleSuggestionClick(suggestion)}
                    className="suggestion-btn"
                  >
                    {suggestion}
                  </button>
                ))}
              </div>
            </div>
          </div>
        ) : (
          <div className="messages">
            {conversation.map((item) => (
              <div key={item.id} className="message-group">
                {/* User Question */}
                <div className="message user-message">
                  <div className="message-content">
                    <p>{item.question}</p>
                  </div>
                  <div className="message-time">
                    {new Date(item.timestamp).toLocaleTimeString()}
                  </div>
                </div>

                {/* MYCA Response */}
                <div className="message myca-message">
                  <div className="message-content">
                    <p>{item.response.answer}</p>
                    
                    {/* Confidence Indicator */}
                    <div className="confidence-indicator">
                      <span 
                        className="confidence-badge"
                        style={{ backgroundColor: getConfidenceColor(item.response.confidence) }}
                      >
                        {formatConfidence(item.response.confidence)}
                      </span>
                    </div>

                    {/* Suggested Follow-ups */}
                    {item.response.suggestedQuestions && item.response.suggestedQuestions.length > 0 && (
                      <div className="follow-up-questions">
                        <p className="follow-up-label">You might also ask:</p>
                        {item.response.suggestedQuestions.map((suggestion, index) => (
                          <button
                            key={index}
                            onClick={() => handleSuggestionClick(suggestion)}
                            className="follow-up-btn"
                          >
                            {suggestion}
                          </button>
                        ))}
                      </div>
                    )}
                  </div>
                  <div className="message-time">
                    {new Date(item.response.timestamp).toLocaleTimeString()}
                  </div>
                </div>
              </div>
            ))}
            
            {/* Loading Indicator */}
            {isLoading && (
              <div className="message myca-message loading">
                <div className="message-content">
                  <div className="typing-indicator">
                    <span></span>
                    <span></span>
                    <span></span>
                  </div>
                  <p>MYCA is thinking...</p>
                </div>
              </div>
            )}
          </div>
        )}
        
        <div ref={messagesEndRef} />
      </div>

      {/* Error Display */}
      {error && (
        <div className="error-banner">
          <span>⚠️ {error}</span>
          <button onClick={() => setError(null)} className="error-close">×</button>
        </div>
      )}

      {/* Input Form */}
      <form onSubmit={handleSubmit} className="input-form">
        <div className="input-container">
          <input
            ref={inputRef}
            type="text"
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            placeholder="Ask MYCA about your fungal network..."
            disabled={isLoading}
            className="question-input"
            maxLength={1000}
          />
          <button
            type="submit"
            disabled={isLoading || !question.trim()}
            className="send-button"
          >
            {isLoading ? '⏳' : '🚀'}
          </button>
        </div>
        <div className="input-footer">
          <span className="char-count">{question.length}/1000</span>
        </div>
      </form>

      {/* Inline Styles */}
      <style jsx>{`
        .myca-interface {
          display: flex;
          flex-direction: column;
          height: 100%;
          background: white;
          border-radius: 8px;
          box-shadow: 0 2px 10px rgba(0,0,0,0.1);
          overflow: hidden;
        }

        .myca-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 16px 20px;
          background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
          color: white;
        }

        .header-content h3 {
          margin: 0 0 4px 0;
          font-size: 18px;
        }

        .header-content p {
          margin: 0;
          font-size: 14px;
          opacity: 0.9;
        }

        .header-actions {
          display: flex;
          gap: 8px;
        }

        .action-btn {
          background: rgba(255,255,255,0.2);
          color: white;
          border: none;
          padding: 6px 12px;
          border-radius: 4px;
          cursor: pointer;
          font-size: 12px;
          transition: background 0.2s;
        }

        .action-btn:hover {
          background: rgba(255,255,255,0.3);
        }

        .conversation-area {
          flex: 1;
          overflow-y: auto;
          padding: 20px;
        }

        .welcome-screen {
          text-align: center;
        }

        .welcome-message {
          margin-bottom: 32px;
        }

        .welcome-message h4 {
          color: #1f2937;
          margin-bottom: 8px;
        }

        .welcome-message p {
          color: #6b7280;
          line-height: 1.5;
        }

        .suggested-questions h5 {
          color: #374151;
          margin-bottom: 16px;
        }

        .suggestions-grid {
          display: grid;
          gap: 12px;
          max-width: 500px;
          margin: 0 auto;
        }

        .suggestion-btn {
          background: #f3f4f6;
          border: 1px solid #d1d5db;
          padding: 12px 16px;
          border-radius: 6px;
          cursor: pointer;
          text-align: left;
          transition: all 0.2s;
          font-size: 14px;
        }

        .suggestion-btn:hover {
          background: #e5e7eb;
          border-color: #9ca3af;
        }

        .messages {
          space-y: 24px;
        }

        .message-group {
          margin-bottom: 24px;
        }

        .message {
          display: flex;
          margin-bottom: 12px;
        }

        .user-message {
          justify-content: flex-end;
        }

        .user-message .message-content {
          background: #3b82f6;
          color: white;
          max-width: 70%;
          padding: 12px 16px;
          border-radius: 18px 18px 4px 18px;
        }

        .myca-message .message-content {
          background: #f3f4f6;
          color: #1f2937;
          max-width: 85%;
          padding: 16px;
          border-radius: 18px 18px 18px 4px;
        }

        .message-content p {
          margin: 0;
          line-height: 1.5;
        }

        .message-time {
          font-size: 11px;
          color: #6b7280;
          margin-top: 4px;
          padding: 0 8px;
        }

        .confidence-indicator {
          margin-top: 12px;
        }

        .confidence-badge {
          color: white;
          padding: 4px 8px;
          border-radius: 12px;
          font-size: 11px;
          font-weight: 500;
        }

        .follow-up-questions {
          margin-top: 16px;
          padding-top: 12px;
          border-top: 1px solid #e5e7eb;
        }

        .follow-up-label {
          font-size: 12px;
          color: #6b7280;
          margin-bottom: 8px;
        }

        .follow-up-btn {
          display: block;
          width: 100%;
          background: white;
          border: 1px solid #d1d5db;
          padding: 8px 12px;
          border-radius: 4px;
          cursor: pointer;
          text-align: left;
          margin-bottom: 6px;
          font-size: 13px;
          transition: background 0.2s;
        }

        .follow-up-btn:hover {
          background: #f9fafb;
        }

        .loading .message-content {
          background: #f9fafb;
        }

        .typing-indicator {
          display: flex;
          gap: 4px;
          margin-bottom: 8px;
        }

        .typing-indicator span {
          width: 8px;
          height: 8px;
          border-radius: 50%;
          background: #9ca3af;
          animation: typing 1.4s infinite ease-in-out;
        }

        .typing-indicator span:nth-child(1) { animation-delay: -0.32s; }
        .typing-indicator span:nth-child(2) { animation-delay: -0.16s; }

        @keyframes typing {
          0%, 80%, 100% { transform: scale(0.8); opacity: 0.5; }
          40% { transform: scale(1); opacity: 1; }
        }

        .error-banner {
          display: flex;
          justify-content: space-between;
          align-items: center;
          background: #fef2f2;
          color: #dc2626;
          padding: 12px 20px;
          border-bottom: 1px solid #fecaca;
        }

        .error-close {
          background: none;
          border: none;
          color: #dc2626;
          cursor: pointer;
          font-size: 18px;
          line-height: 1;
        }

        .input-form {
          padding: 16px 20px;
          border-top: 1px solid #e5e7eb;
          background: #fafafa;
        }

        .input-container {
          display: flex;
          gap: 8px;
        }

        .question-input {
          flex: 1;
          padding: 12px 16px;
          border: 1px solid #d1d5db;
          border-radius: 24px;
          outline: none;
          font-size: 14px;
        }

        .question-input:focus {
          border-color: #3b82f6;
          box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
        }

        .send-button {
          background: #3b82f6;
          color: white;
          border: none;
          width: 44px;
          height: 44px;
          border-radius: 50%;
          cursor: pointer;
          font-size: 16px;
          transition: background 0.2s;
        }

        .send-button:hover:not(:disabled) {
          background: #2563eb;
        }

        .send-button:disabled {
          background: #9ca3af;
          cursor: not-allowed;
        }

        .input-footer {
          display: flex;
          justify-content: flex-end;
          margin-top: 8px;
        }

        .char-count {
          font-size: 11px;
          color: #6b7280;
        }
      `}</style>
    </div>
  );
};

export default MYCAInterface; 
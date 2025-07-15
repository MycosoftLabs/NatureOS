// MYCA AI Assistant chat component for Mycosoft website
// This integrates with NatureOS through the MYCA API

'use client';

import { useState, useEffect, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';

export default function MycaChatComponent() {
  const [messages, setMessages] = useState([
    {
      id: 1,
      type: 'assistant',
      content: "Hello! I'm MYCA, your fungal intelligence assistant. I'm connected to the NatureOS MINDEX database and can help you explore the mycological network. What would you like to know?",
      timestamp: new Date(),
    }
  ]);
  const [input, setInput] = useState('');
  const [isTyping, setIsTyping] = useState(false);
  const [suggestions, setSuggestions] = useState([]);
  const [isConnected, setIsConnected] = useState(false);
  const messagesEndRef = useRef(null);

  // Load suggested questions on mount
  useEffect(() => {
    loadSuggestions();
    checkConnection();
  }, []);

  // Auto-scroll to bottom
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const loadSuggestions = async () => {
    try {
      const response = await fetch('/api/myca?action=suggestions');
      const data = await response.json();
      setSuggestions(data.suggestions || []);
    } catch (error) {
      console.error('Failed to load suggestions:', error);
      setSuggestions([
        'What species are active in the network?',
        'Show me the latest discoveries',
        'How is the system health?',
        'Explain the mycorrhizal connections',
      ]);
    }
  };

  const checkConnection = async () => {
    try {
      const response = await fetch('/api/myca?action=context');
      setIsConnected(response.ok);
    } catch (error) {
      setIsConnected(false);
    }
  };

  const sendMessage = async (messageText = input) => {
    if (!messageText.trim()) return;

    const userMessage = {
      id: Date.now(),
      type: 'user',
      content: messageText.trim(),
      timestamp: new Date(),
    };

    setMessages(prev => [...prev, userMessage]);
    setInput('');
    setIsTyping(true);

    try {
      const response = await fetch('/api/myca', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          question: messageText.trim(),
          context: 'website-chat',
          userId: 'web-user',
        }),
      });

      const data = await response.json();

      const assistantMessage = {
        id: Date.now() + 1,
        type: 'assistant',
        content: data.answer,
        confidence: data.confidence,
        sources: data.sources,
        suggestedQuestions: data.suggestedQuestions,
        timestamp: new Date(),
        fallback: data.fallback,
      };

      setMessages(prev => [...prev, assistantMessage]);
      
      // Update suggestions with new ones from response
      if (data.suggestedQuestions) {
        setSuggestions(data.suggestedQuestions);
      }
    } catch (error) {
      const errorMessage = {
        id: Date.now() + 1,
        type: 'assistant',
        content: "I'm having trouble connecting to the NatureOS network right now. Please try again in a moment.",
        timestamp: new Date(),
        error: true,
      };
      setMessages(prev => [...prev, errorMessage]);
    }

    setIsTyping(false);
  };

  const handleKeyPress = (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  };

  const sendFeedback = async (messageId, feedbackType) => {
    try {
      await fetch('/api/myca', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          action: 'feedback',
          conversationId: `msg_${messageId}`,
          feedback: { type: feedbackType, messageId },
        }),
      });
    } catch (error) {
      console.error('Failed to send feedback:', error);
    }
  };

  return (
    <div className="flex flex-col h-96 bg-white rounded-lg border border-gray-200 shadow-sm">
      {/* Header */}
      <div className="flex items-center justify-between p-4 border-b border-gray-200">
        <div className="flex items-center space-x-3">
          <div className="w-8 h-8 bg-gradient-to-r from-green-500 to-blue-500 rounded-full flex items-center justify-center">
            <span className="text-white font-bold text-sm">🧠</span>
          </div>
          <div>
            <h3 className="font-semibold text-gray-900">MYCA AI Assistant</h3>
            <div className="flex items-center space-x-2">
              <div className={`w-2 h-2 rounded-full ${isConnected ? 'bg-green-500' : 'bg-red-500'}`} />
              <span className="text-xs text-gray-500">
                {isConnected ? 'Connected to NatureOS' : 'Offline'}
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        <AnimatePresence>
          {messages.map((message) => (
            <motion.div
              key={message.id}
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -20 }}
              className={`flex ${message.type === 'user' ? 'justify-end' : 'justify-start'}`}
            >
              <div className={`max-w-xs lg:max-w-md px-4 py-2 rounded-lg ${
                message.type === 'user'
                  ? 'bg-blue-500 text-white'
                  : message.error
                  ? 'bg-red-50 text-red-700 border border-red-200'
                  : 'bg-gray-100 text-gray-900'
              }`}>
                <p className="text-sm">{message.content}</p>
                
                {/* Assistant message metadata */}
                {message.type === 'assistant' && !message.error && (
                  <div className="mt-2 space-y-2">
                    {message.confidence && (
                      <div className="text-xs text-gray-500">
                        Confidence: {Math.round(message.confidence * 100)}%
                        {message.fallback && ' (Fallback mode)'}
                      </div>
                    )}
                    
                    {message.sources && (
                      <div className="text-xs text-gray-500">
                        Sources: {message.sources.join(', ')}
                      </div>
                    )}

                    {/* Feedback buttons */}
                    <div className="flex space-x-2">
                      <button
                        onClick={() => sendFeedback(message.id, 'helpful')}
                        className="text-xs text-green-600 hover:text-green-700"
                        title="This was helpful"
                      >
                        👍
                      </button>
                      <button
                        onClick={() => sendFeedback(message.id, 'not-helpful')}
                        className="text-xs text-red-600 hover:text-red-700"
                        title="This wasn't helpful"
                      >
                        👎
                      </button>
                    </div>
                  </div>
                )}
                
                <div className="text-xs opacity-75 mt-1">
                  {message.timestamp.toLocaleTimeString()}
                </div>
              </div>
            </motion.div>
          ))}
        </AnimatePresence>

        {/* Typing indicator */}
        {isTyping && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            className="flex justify-start"
          >
            <div className="bg-gray-100 rounded-lg px-4 py-2">
              <div className="flex space-x-1">
                <motion.div
                  animate={{ scale: [1, 1.2, 1] }}
                  transition={{ duration: 0.6, repeat: Infinity, delay: 0 }}
                  className="w-2 h-2 bg-gray-400 rounded-full"
                />
                <motion.div
                  animate={{ scale: [1, 1.2, 1] }}
                  transition={{ duration: 0.6, repeat: Infinity, delay: 0.2 }}
                  className="w-2 h-2 bg-gray-400 rounded-full"
                />
                <motion.div
                  animate={{ scale: [1, 1.2, 1] }}
                  transition={{ duration: 0.6, repeat: Infinity, delay: 0.4 }}
                  className="w-2 h-2 bg-gray-400 rounded-full"
                />
              </div>
            </div>
          </motion.div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Suggested Questions */}
      {suggestions.length > 0 && (
        <div className="p-4 border-t border-gray-200">
          <p className="text-xs text-gray-500 mb-2">Suggested questions:</p>
          <div className="flex flex-wrap gap-2">
            {suggestions.slice(0, 3).map((suggestion, index) => (
              <motion.button
                key={suggestion}
                initial={{ opacity: 0, scale: 0.9 }}
                animate={{ opacity: 1, scale: 1 }}
                transition={{ delay: index * 0.1 }}
                onClick={() => sendMessage(suggestion)}
                className="px-3 py-1 text-xs bg-gray-100 hover:bg-gray-200 rounded-full text-gray-700 transition-colors"
              >
                {suggestion}
              </motion.button>
            ))}
          </div>
        </div>
      )}

      {/* Input */}
      <div className="p-4 border-t border-gray-200">
        <div className="flex space-x-2">
          <input
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyPress={handleKeyPress}
            placeholder="Ask MYCA about the fungal network..."
            className="flex-1 px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent text-sm"
            disabled={isTyping}
          />
          <motion.button
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
            onClick={() => sendMessage()}
            disabled={isTyping || !input.trim()}
            className="px-4 py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600 disabled:opacity-50 disabled:cursor-not-allowed text-sm font-medium"
          >
            Send
          </motion.button>
        </div>
      </div>
    </div>
  );
} 
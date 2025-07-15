#!/bin/bash

# NatureOS Local Development Startup Script
echo "=========================================="
echo "     Starting NatureOS Development"
echo "=========================================="
echo ""

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running. Please start Docker first."
    exit 1
fi

echo "🚀 Starting NatureOS services with Docker Compose..."
docker-compose up -d

echo ""
echo "⏳ Waiting for services to start..."
sleep 10

echo ""
echo "✅ NatureOS services are starting up!"
echo ""
echo "🌐 Available services:"
echo "  • Core API:      http://localhost:8080"
echo "  • Dashboard:     http://localhost:3000"
echo "  • Cosmos DB:     https://localhost:8081/_explorer/index.html"
echo "  • Prometheus:    http://localhost:9090"
echo "  • Grafana:       http://localhost:3001 (admin/admin)"
echo ""
echo "📊 Service status:"
docker-compose ps

echo ""
echo "📝 To view logs:"
echo "  docker-compose logs -f [service-name]"
echo ""
echo "🛑 To stop all services:"
echo "  docker-compose down" 
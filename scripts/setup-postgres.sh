#!/bin/bash

# PostgreSQL Container Setup Script for Odin Project
# Uses Docker with persistent volume for data storage

set -e

# Configuration
CONTAINER_NAME="odin_postgres"
VOLUME_NAME="odin_postgres_data"
POSTGRES_USER="odin"
POSTGRES_PASSWORD="odin_secret"
POSTGRES_DB="odin_db"
HOST_PORT="5433"
CONTAINER_PORT="5432"
POSTGRES_VERSION="16"

echo "🐘 Setting up PostgreSQL container for Odin..."

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install Docker first."
    exit 1
fi

# Check if container already exists
if docker ps -a --format "{{.Names}}" | grep -q "^${CONTAINER_NAME}$"; then
    echo "⚠️  Container '${CONTAINER_NAME}' already exists."
    read -p "Do you want to remove it and create a new one? (y/N): " confirm
    if [[ "$confirm" =~ ^[Yy]$ ]]; then
        echo "🗑️  Stopping and removing existing container..."
        docker stop ${CONTAINER_NAME} 2>/dev/null || true
        docker rm ${CONTAINER_NAME}
    else
        echo "Starting existing container..."
        docker start ${CONTAINER_NAME}
        echo "✅ Container started!"
        exit 0
    fi
fi

# Create volume if it doesn't exist
if ! docker volume ls --format "{{.Name}}" | grep -q "^${VOLUME_NAME}$"; then
    echo "📦 Creating persistent volume '${VOLUME_NAME}'..."
    docker volume create ${VOLUME_NAME}
else
    echo "📦 Using existing volume '${VOLUME_NAME}'"
fi

# Run PostgreSQL container
echo "🚀 Starting PostgreSQL container..."
docker run -d \
    --name ${CONTAINER_NAME} \
    -e POSTGRES_USER=${POSTGRES_USER} \
    -e POSTGRES_PASSWORD=${POSTGRES_PASSWORD} \
    -e POSTGRES_DB=${POSTGRES_DB} \
    -p ${HOST_PORT}:${CONTAINER_PORT} \
    -v ${VOLUME_NAME}:/var/lib/postgresql/data \
    postgres:${POSTGRES_VERSION}

# Wait for PostgreSQL to be ready
echo "⏳ Waiting for PostgreSQL to be ready..."
sleep 3

# Verify connection
if docker exec ${CONTAINER_NAME} psql -U ${POSTGRES_USER} -d ${POSTGRES_DB} -c "SELECT 1;" &> /dev/null; then
    echo "✅ PostgreSQL is ready!"
else
    echo "⏳ Still initializing, please wait..."
    sleep 5
fi

echo ""
echo "=========================================="
echo "PostgreSQL Container Setup Complete!"
echo "=========================================="
echo ""
echo "Connection Details:"
echo "  Host:     localhost"
echo "  Port:     ${HOST_PORT}"
echo "  Database: ${POSTGRES_DB}"
echo "  Username: ${POSTGRES_USER}"
echo "  Password: ${POSTGRES_PASSWORD}"
echo ""
echo "Connection String:"
echo "  Host=localhost;Port=${HOST_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
echo ""
echo "Useful Commands:"
echo "  Stop:     docker stop ${CONTAINER_NAME}"
echo "  Start:    docker start ${CONTAINER_NAME}"
echo "  Logs:     docker logs ${CONTAINER_NAME}"
echo "  Shell:    docker exec -it ${CONTAINER_NAME} psql -U ${POSTGRES_USER} -d ${POSTGRES_DB}"
echo "  Remove:   docker rm -f ${CONTAINER_NAME}"
echo ""

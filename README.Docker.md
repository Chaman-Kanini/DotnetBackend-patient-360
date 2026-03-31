# Docker Deployment Guide

This guide explains how to deploy the TrustFirst Platform using Docker.

## Prerequisites

- Docker Desktop installed on your machine
- Docker Compose (included with Docker Desktop)

## Quick Start

### 1. Build and Run with Docker Compose

```bash
# Build and start all services
docker-compose up --build

# Or run in detached mode (background)
docker-compose up -d --build
```

### 2. Access the Application

- API: http://localhost:5000
- Swagger UI: http://localhost:5000/swagger
- PostgreSQL: localhost:5432

### 3. Stop the Services

```bash
# Stop services
docker-compose down

# Stop services and remove volumes (clears database)
docker-compose down -v
```

## Service Details

### API Service
- **Port**: 5000 (mapped to container port 8080)
- **Environment**: Docker
- **Includes**: .NET 10.0 runtime, Python 3 with required packages
- **Volumes**: `./uploads` mounted for file uploads

### PostgreSQL Service
- **Port**: 5432
- **Database**: TrustFirstPlatform
- **User**: postgres
- **Password**: admin
- **Volumes**: `postgres-data` for persistent storage

## Configuration

### Environment Variables

You can override environment variables in `docker-compose.yml`:

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Docker
  - ConnectionStrings__DefaultConnection=Host=postgres;Database=TrustFirstPlatform;Username=postgres;Password=admin
  # Add more as needed
```

### Configuration Files

- `appsettings.json` - Default configuration
- `appsettings.Docker.json` - Docker-specific overrides

## Common Commands

```bash
# View logs
docker-compose logs -f api

# View logs for all services
docker-compose logs -f

# Restart a service
docker-compose restart api

# Rebuild a service
docker-compose up -d --build api

# Execute commands in the API container
docker exec -it trustfirst-api bash

# Execute commands in the PostgreSQL container
docker exec -it trustfirst-postgres psql -U postgres -d TrustFirstPlatform
```

## Troubleshooting

### Database Connection Issues

If the API can't connect to the database:
1. Ensure PostgreSQL is healthy: `docker-compose ps`
2. Check logs: `docker-compose logs postgres`
3. Wait for initialization: First startup takes 10-30 seconds

### Python Script Issues

If Python scripts fail:
1. Verify Python is installed: `docker exec -it trustfirst-api python3 --version`
2. Check Python packages: `docker exec -it trustfirst-api pip list`

### Port Already in Use

If port 5000 or 5432 is already in use, modify the ports in `docker-compose.yml`:

```yaml
ports:
  - "5001:8080"  # Changed from 5000 to 5001
```

## Production Deployment

For production:

1. **Update Secrets**: Change passwords and API keys in `docker-compose.yml` or use environment variables
2. **Enable HTTPS**: Configure SSL certificates
3. **Use Docker Secrets**: For sensitive data
4. **Set up Volumes**: For persistent data
5. **Configure Logging**: Set up centralized logging
6. **Resource Limits**: Add CPU and memory constraints

Example production override:

```yaml
# docker-compose.prod.yml
services:
  api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=${DATABASE_CONNECTION_STRING}
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 4G
```

Run with: `docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d`

## Database Migrations

Migrations run automatically on startup. To run manually:

```bash
# Access the API container
docker exec -it trustfirst-api bash

# Run migrations
dotnet ef database update --project /app/TrustFirstPlatform.API.dll
```

## Backup and Restore

### Backup Database

```bash
docker exec trustfirst-postgres pg_dump -U postgres TrustFirstPlatform > backup.sql
```

### Restore Database

```bash
cat backup.sql | docker exec -i trustfirst-postgres psql -U postgres TrustFirstPlatform
```

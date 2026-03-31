# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY TrustFirstPlatform.sln .
COPY TrustFirstPlatform.API/TrustFirstPlatform.API.csproj TrustFirstPlatform.API/
COPY src/TrustFirstPlatform.Application/TrustFirstPlatform.Application.csproj src/TrustFirstPlatform.Application/
COPY src/TrustFirstPlatform.Domain/TrustFirstPlatform.Domain.csproj src/TrustFirstPlatform.Domain/
COPY src/TrustFirstPlatform.Infrastructure/TrustFirstPlatform.Infrastructure.csproj src/TrustFirstPlatform.Infrastructure/
COPY tests/TrustFirstPlatform.Application.Tests/TrustFirstPlatform.Application.Tests.csproj tests/TrustFirstPlatform.Application.Tests/

# Restore dependencies
RUN dotnet restore

# Copy the rest of the source code
COPY . .

# Build the application
WORKDIR /src/TrustFirstPlatform.API
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install Python and required packages
RUN apt-get update && \
    apt-get install -y python3 python3-pip python3-venv && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Create a virtual environment and install Python dependencies
RUN python3 -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

# Copy Python requirements and install
COPY src/requirements.txt /app/requirements.txt
RUN pip install --no-cache-dir -r /app/requirements.txt

# Copy published application
COPY --from=publish /app/publish .

# Create uploads directory
RUN mkdir -p /app/uploads && chmod 777 /app/uploads

# Expose port
EXPOSE 8080
EXPOSE 8081

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "TrustFirstPlatform.API.dll"]

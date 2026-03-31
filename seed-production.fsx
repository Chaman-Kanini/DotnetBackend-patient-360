#!/usr/bin/env dotnet fsi

// Production Database Seeder Script
// Usage: dotnet fsi seed-production.fsx

#r "nuget: Npgsql.EntityFrameworkCore.PostgreSQL, 10.0.0"
#r "nuget: Microsoft.EntityFrameworkCore, 10.0.1"
#r "nuget: Microsoft.Extensions.Logging.Console, 10.0.1"

open System
open System.Text.Json
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Logging

// Set your connection string as environment variable DATABASE_URL
let connectionString = System.Environment.GetEnvironmentVariable("DATABASE_URL")

printfn "🚀 Starting production database seeding..."
printfn "📍 Connection: %s" (connectionString.Substring(0, Math.Min(50, connectionString.Length)) + "...")

// Note: Full seeding requires loading the actual DbContext and entities
// This is a placeholder - use the C# version or run via the API
printfn "⚠️  Please use the C# seeder or run the API with seeding enabled"
printfn "✅ Script completed"

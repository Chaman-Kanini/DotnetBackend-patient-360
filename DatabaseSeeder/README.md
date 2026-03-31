# Database Seeder

This tool seeds the production database with initial users and data.

## ⚠️ Security Notice

**NEVER** hardcode credentials in this file. Always use environment variables or command-line arguments.

## Usage

### Option 1: Environment Variable (Recommended)

```powershell
# Set environment variable
$env:DATABASE_URL="Host=your-host;Port=5432;Database=yourdb;Username=user;Password=pass;SslMode=Require"

# Run seeder
dotnet run
```

### Option 2: Command Line Argument

```powershell
dotnet run "Host=your-host;Port=5432;Database=yourdb;Username=user;Password=pass;SslMode=Require"
```

## What Gets Seeded

- **Admin User**: admin@trustfirst.com (Password: Admin@123)
- **Standard User**: user@trustfirst.com (Password: User@123)

## Important

⚠️ Change default passwords immediately after first login!

## For Production Deployment

Use environment variables on your hosting platform (Render, Azure, AWS, etc.) to pass the connection string securely.

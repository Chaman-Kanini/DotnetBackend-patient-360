# Render Deployment Guide

This guide explains how to deploy the TrustFirst Platform on Render using environment variables.

## Prerequisites

- A Render account (https://render.com)
- PostgreSQL database (can be created on Render)
- Azure OpenAI credentials

## Deployment Steps

### 1. Create a PostgreSQL Database on Render

1. Go to Render Dashboard → New → PostgreSQL
2. Name: `trustfirst-db` (or your preferred name)
3. Database: `TrustFirstPlatform`
4. User: `postgres` (default)
5. Region: Choose closest to your users
6. Plan: Select appropriate plan
7. Copy the **Internal Database URL** (you'll need this)

### 2. Create a Web Service

1. Go to Render Dashboard → New → Web Service
2. Connect your GitHub repository
3. Configure the service:
   - **Name**: `trustfirst-api`
   - **Region**: Same as your database
   - **Branch**: `main` (or your default branch)
   - **Root Directory**: Leave empty (or specify if needed)
   - **Runtime**: Docker
   - **Plan**: Select appropriate plan

### 3. Configure Environment Variables

In the Render service settings, add the following environment variables:

#### Database Connection
```
ConnectionStrings__DefaultConnection=<YOUR_RENDER_POSTGRES_INTERNAL_URL>
```
Format: `postgresql://user:password@host:5432/database`

Example: `postgresql://postgres:password@dpg-xxxxx-a.oregon-postgres.render.com:5432/TrustFirstPlatform`

#### JWT Settings
```
JwtSettings__SecretKey=<GENERATE_A_SECURE_256_BIT_KEY>
JwtSettings__Issuer=TrustFirstPlatform
JwtSettings__Audience=TrustFirstPlatform
JwtSettings__ExpirationMinutes=15
JwtSettings__RefreshTokenExpirationDays=7
```

**Generate a secure secret key:**
```bash
# PowerShell
-join ((65..90) + (97..122) + (48..57) | Get-Random -Count 64 | ForEach-Object {[char]$_})

# Or use any 256-bit random string generator
```

#### Email Settings (Gmail)
```
EmailSettings__SmtpServer=smtp.gmail.com
EmailSettings__SmtpPort=587
EmailSettings__SmtpUsername=<YOUR_GMAIL_EMAIL>
EmailSettings__SmtpPassword=<YOUR_GMAIL_APP_PASSWORD>
EmailSettings__FromEmail=<YOUR_GMAIL_EMAIL>
EmailSettings__FromName=TrustFirst Platform
EmailSettings__EnableSsl=true
```

**Note:** Use Gmail App Password, not your regular password. Generate one at: https://myaccount.google.com/apppasswords

#### Azure OpenAI Settings
```
AzureOpenAI__Endpoint=<YOUR_AZURE_OPENAI_ENDPOINT>
AzureOpenAI__DeploymentName=<YOUR_DEPLOYMENT_NAME>
AzureOpenAI__ApiVersion=2024-12-01-preview
AzureOpenAI__ApiKey=<YOUR_AZURE_OPENAI_API_KEY>
```

#### Application Settings
```
App__FrontendBaseUrl=<YOUR_FRONTEND_URL>
RagApi__BaseUrl=https://rag-service-patient-360.onrender.com
Python__Path=python3
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
```

Replace `<YOUR_FRONTEND_URL>` with your actual frontend URL (e.g., `https://your-app.vercel.app`)

#### Rate Limiting (Optional)
```
RateLimitOptions__LoginAttemptsPerMinute=5
RateLimitOptions__PasswordResetAttemptsPerHour=3
RateLimitOptions__RegistrationAttemptsPerHour=3
```

### 4. Deploy

1. Click **Create Web Service**
2. Render will automatically:
   - Pull your code from GitHub
   - Build the Docker image
   - Deploy the application
   - Run database migrations

### 5. Verify Deployment

Once deployed, your API will be available at:
```
https://trustfirst-api.onrender.com
```

Test endpoints:
- Health check: `https://trustfirst-api.onrender.com/health` (if you have one)
- Swagger: `https://trustfirst-api.onrender.com/swagger`

## Environment Variable Format

Render uses this format for nested configuration:
```
SectionName__SubSectionName__Key=Value
```

This maps to appsettings.json:
```json
{
  "SectionName": {
    "SubSectionName": {
      "Key": "Value"
    }
  }
}
```

## Important Notes

### Database Connection String
- Use the **Internal Database URL** from Render (not external)
- Internal connections are faster and free
- Format: `postgresql://user:password@internal-host:5432/database`

### Auto-Deploy
- Enable auto-deploy in Render settings to deploy on every push to main branch
- Or deploy manually from the Render dashboard

### Logs
- View logs in real-time: Render Dashboard → Your Service → Logs
- Debug startup issues and errors

### Health Checks
Render will check if your service is healthy. The default is to check if the HTTP server responds.

### Disk Storage
- Render uses ephemeral storage (resets on deploy)
- For file uploads, consider using:
  - Render Disks (persistent storage)
  - Cloud storage (AWS S3, Azure Blob, Cloudflare R2)

## Troubleshooting

### Database Connection Failed
- Verify you're using the Internal Database URL
- Check database is in the same region
- Ensure database is running

### Migrations Not Running
- Check logs for migration errors
- Ensure EF Core tools are installed in Dockerfile
- Database user must have create/alter permissions

### Python Scripts Failing
- Verify Python 3 is installed in Dockerfile
- Check requirements.txt is copied
- Ensure virtual environment is set up correctly

### Port Issues
- Render expects your app on port 8080 by default
- Set `ASPNETCORE_URLS=http://+:8080` in environment variables

## Security Checklist

✅ Never commit actual credentials to Git
✅ Use Render's environment variables for all secrets
✅ Generate a strong JWT secret key (256+ bits)
✅ Use Gmail App Passwords, not regular passwords
✅ Enable HTTPS only in production (Render provides this automatically)
✅ Review and update CORS origins for production
✅ Consider adding Render IP restrictions for database access

## Cost Optimization

- Use Render's free tier for initial testing
- Upgrade to paid plans for production workloads
- Set up auto-scaling for traffic spikes
- Monitor resource usage in Render dashboard

## Additional Resources

- [Render Documentation](https://render.com/docs)
- [.NET on Render](https://render.com/docs/deploy-dotnet)
- [Docker Deployments](https://render.com/docs/docker)
- [Environment Variables](https://render.com/docs/environment-variables)

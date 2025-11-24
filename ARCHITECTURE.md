# Azure Expense Management System Architecture

This document describes the Azure architecture deployed by this repository.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                                                                     │
│                         Azure Resource Group                        │
│                      (rg-expensemgmt-demo)                         │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐ │
│  │                                                              │ │
│  │  User Assigned Managed Identity                             │ │
│  │  (mid-expensemgmt-XXXXX)                                    │ │
│  │                                                              │ │
│  │  Used for secure authentication between services            │ │
│  └─────────────────────┬────────────────────────────────────────┘ │
│                        │                                           │
│                        │ Assigned To                              │
│                        ▼                                           │
│  ┌──────────────────────────────────────────────────────────────┐ │
│  │                                                              │ │
│  │  App Service (Linux)                                        │ │
│  │  (app-expensemgmt-XXXXX)                                    │ │
│  │                                                              │ │
│  │  - .NET 8 ASP.NET Core                                      │ │
│  │  - Razor Pages UI                                           │ │
│  │  - REST APIs with Swagger                                   │ │
│  │  - AI Chat Interface                                        │ │
│  │  - Standard S1 SKU                                          │ │
│  │                                                              │ │
│  └──────────┬────────────────────────┬──────────────────────────┘ │
│             │                        │                            │
│             │                        │                            │
│    Connects to (Managed Identity)   │  Connects to (MI)          │
│             │                        │                            │
│             ▼                        ▼                            │
│  ┌─────────────────────┐  ┌──────────────────────────────────┐  │
│  │                     │  │                                  │  │
│  │  Azure SQL Database │  │  Azure OpenAI (Sweden Central)  │  │
│  │  (Northwind)        │  │  (openai-expensemgmt-XXXXX)     │  │
│  │                     │  │                                  │  │
│  │  - Basic Tier       │  │  - GPT-4o Model                 │  │
│  │  - AAD Auth Only    │  │  - S0 SKU                       │  │
│  │  - Stored Procs     │  │  - Function Calling             │  │
│  │  - Firewall Rules   │  │                                  │  │
│  │                     │  │                                  │  │
│  └─────────────────────┘  └──────────────────────────────────┘  │
│                                                                   │
│                           ┌───────────────────────────────────┐  │
│                           │                                   │  │
│                           │  Azure Cognitive Search           │  │
│                           │  (srch-expensemgmt-XXXXX)        │  │
│                           │                                   │  │
│                           │  - Basic SKU                      │  │
│                           │  - RAG Support                    │  │
│                           │  - Managed Identity Access        │  │
│                           │                                   │  │
│                           └───────────────────────────────────┘  │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
```

## Components

### 1. User Assigned Managed Identity
- **Purpose**: Provides secure authentication between Azure services without storing credentials
- **Used by**: App Service to connect to Azure SQL, Azure OpenAI, and Cognitive Search
- **Permissions**: 
  - Database Reader/Writer on SQL Database
  - Cognitive Services OpenAI User on Azure OpenAI
  - Search Index Data Contributor on Cognitive Search

### 2. App Service (Linux)
- **Runtime**: .NET 8
- **SKU**: Standard S1 (Always On enabled)
- **Features**:
  - Expense management web interface
  - REST APIs for CRUD operations
  - Swagger documentation
  - AI-powered chat assistant
  - Error handling with fallback to dummy data

### 3. Azure SQL Database
- **Tier**: Basic (development)
- **Authentication**: Azure AD-Only (no SQL authentication)
- **Database**: Northwind
- **Schema**: Expenses, Users, Categories, Statuses
- **Access Pattern**: All data access through stored procedures

### 4. Azure OpenAI (Optional - deployed with deploy-with-chat.sh)
- **Location**: Sweden Central (for GPT-4o availability)
- **Model**: GPT-4o
- **Capacity**: 10 TPM
- **Features**:
  - Natural language querying of expenses
  - Function calling for database operations
  - Conversational AI assistant

### 5. Azure Cognitive Search (Optional - deployed with deploy-with-chat.sh)
- **SKU**: Basic
- **Purpose**: RAG (Retrieval-Augmented Generation) support
- **Integration**: Connected to OpenAI for enhanced AI responses

## Security

- **No SQL Authentication**: Azure AD-only authentication enforced
- **Managed Identity**: No credentials stored in application configuration
- **HTTPS Only**: All communication encrypted
- **Firewall Rules**: SQL Server allows Azure services only
- **TLS**: Minimum TLS 1.2 enforced

## Deployment Options

### Option 1: Basic Deployment (Without Gen AI)
```bash
./deploy.sh
```
Deploys:
- App Service
- SQL Database
- Basic expense management features

### Option 2: Full Deployment (With Gen AI)
```bash
./deploy-with-chat.sh
```
Deploys:
- Everything from Option 1
- Azure OpenAI with GPT-4o
- Azure Cognitive Search
- AI Chat Assistant

## URLs

After deployment, access:
- **Application**: `https://app-expensemgmt-XXXXX.azurewebsites.net/Index`
- **API Docs**: `https://app-expensemgmt-XXXXX.azurewebsites.net/swagger`
- **AI Chat**: `https://app-expensemgmt-XXXXX.azurewebsites.net/Chat/Chat` (if Gen AI deployed)

## Cost Estimation

### Basic Deployment (Monthly)
- App Service S1: ~$70
- SQL Database Basic: ~$5
- **Total**: ~$75/month

### Full Deployment with Gen AI (Monthly)
- App Service S1: ~$70
- SQL Database Basic: ~$5
- Azure OpenAI S0: Pay-per-use (varies with usage)
- Cognitive Search Basic: ~$75
- **Total**: ~$150/month + OpenAI usage

*Note: Costs are approximate and may vary by region and usage*

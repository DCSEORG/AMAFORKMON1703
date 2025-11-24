![Header image](https://github.com/DougChisholm/App-Mod-Assist/blob/main/repo-header.png)

# Expense Management System - Cloud Native Azure Solution

A modernized cloud-native expense management application built with ASP.NET Core 8, deployed to Azure with optional AI-powered chat capabilities.

## 🌟 Features

### Core Features
- ✅ **Modern Web Interface**: Clean, responsive Razor Pages UI with Bootstrap 5
- 📊 **Expense Management**: Create, submit, approve/reject expenses
- 🔐 **Secure Authentication**: Azure AD Managed Identity (no credentials stored)
- 📦 **RESTful APIs**: Complete API with Swagger documentation
- 🛡️ **Error Handling**: Graceful fallback with dummy data if database unavailable
- 💾 **Stored Procedures**: All database access through secure stored procedures

### AI Features (Optional)
- 🤖 **AI Chat Assistant**: Natural language queries using GPT-4o
- 🔧 **Function Calling**: AI can directly interact with your database
- 📚 **RAG Support**: Azure Cognitive Search integration
- 💬 **Conversational**: Maintains context across chat sessions

## 🏗️ Architecture

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed architecture diagram and component descriptions.

**Key Components:**
- Azure App Service (Linux, .NET 8)
- Azure SQL Database (Azure AD-only authentication)
- User-Assigned Managed Identity
- Azure OpenAI with GPT-4o (optional)
- Azure Cognitive Search (optional)

## 🚀 Quick Start

### Prerequisites
- Azure CLI installed and authenticated (`az login`)
- Bash shell (Linux, macOS, or WSL on Windows)
- Python 3 with pip
- jq command-line tool

### Option 1: Basic Deployment (No AI)

Deploy core expense management features:

```bash
git clone https://github.com/DCSEORG/AMAFORKMON1703.git
cd AMAFORKMON1703
chmod +x deploy.sh
./deploy.sh
```

**Deploys:**
- App Service with expense management
- Azure SQL Database
- REST APIs
- Web interface

**Cost:** ~$75/month

### Option 2: Full Deployment with AI Chat

Deploy everything including AI features:

```bash
git clone https://github.com/DCSEORG/AMAFORKMON1703.git
cd AMAFORKMON1703
chmod +x deploy-with-chat.sh
./deploy-with-chat.sh
```

**Deploys:**
- Everything from Option 1
- Azure OpenAI with GPT-4o model
- Azure Cognitive Search
- AI chat interface

**Cost:** ~$150/month + usage-based OpenAI costs

## 📖 Usage

After deployment completes (wait 2-3 minutes):

1. **Access the Application**: Navigate to the URL shown in deployment output
   - Main UI: `https://{your-app}.azurewebsites.net/Index`
   - API Docs: `https://{your-app}.azurewebsites.net/swagger`
   - AI Chat: `https://{your-app}.azurewebsites.net/Chat/Chat` (if deployed)

2. **Create Expenses**: Click "New Expense" button
3. **Submit for Approval**: Draft expenses can be submitted
4. **Approve/Reject**: Managers can approve or reject submitted expenses
5. **Use AI Chat**: Ask questions like:
   - "Show me all submitted expenses"
   - "Create a new travel expense for £50"
   - "What's the total of all approved expenses?"

## 🛠️ Local Development

1. **Clone and restore packages:**
```bash
cd ExpenseManagementApp
dotnet restore
```

2. **Configure `appsettings.Development.json`:**
```json
{
  "SqlServer": "your-sql-server.database.windows.net",
  "Database": "Northwind",
  "AuthenticationMode": "Active Directory Default"
}
```

3. **Login to Azure:**
```bash
az login
```

4. **Run the application:**
```bash
dotnet run
```

5. **Access locally:** `https://localhost:5001/Index`

## 📁 Project Structure

```
.
├── bicep/                          # Infrastructure as Code
│   ├── main.bicep                  # Main deployment template
│   ├── app-service.bicep           # App Service + Managed Identity
│   ├── azure-sql.bicep             # SQL Server + Database
│   └── genai.bicep                 # OpenAI + Cognitive Search
│
├── ExpenseManagementApp/           # .NET 8 Application
│   ├── Pages/                      # Razor Pages
│   │   ├── Index.cshtml            # Main expense list
│   │   └── Chat/Chat.cshtml        # AI chat interface
│   ├── Services/                   # Business logic
│   │   ├── ExpenseService.cs       # Database operations
│   │   └── ChatService.cs          # AI chat with function calling
│   └── Models/                     # Data models
│
├── Database-Schema/                # SQL schema
│   └── database_schema.sql         # Table definitions and seed data
│
├── stored-procedures.sql           # All stored procedures
├── run-sql.py                      # Schema import script
├── run-sql-dbrole.py              # Managed identity setup
├── run-sql-stored-procs.py        # Stored procedure deployment
├── script.sql                      # Role assignment SQL
│
├── deploy.sh                       # Basic deployment script
├── deploy-with-chat.sh            # Full deployment with AI
├── app.zip                         # Compiled application
│
├── ARCHITECTURE.md                 # Architecture documentation
├── DEPLOYMENT.md                   # Deployment guide
└── README.md                       # This file
```

## 📚 Documentation

- **[ARCHITECTURE.md](ARCHITECTURE.md)**: Detailed architecture diagram and component descriptions
- **[DEPLOYMENT.md](DEPLOYMENT.md)**: Deployment order, considerations, and troubleshooting
- **Legacy Screenshots**: See `Legacy-Screenshots/` for original app design reference
- **API Documentation**: Available at `/swagger` endpoint after deployment

## 🔒 Security

- ✅ Azure AD-only authentication (no SQL passwords)
- ✅ Managed Identity for all service-to-service communication
- ✅ HTTPS enforced
- ✅ Minimum TLS 1.2
- ✅ No credentials in code or configuration
- ✅ Firewall rules for SQL Server
- ✅ Error messages don't expose sensitive information

## 🧪 Testing

The application includes:
- Dummy data fallback for offline/error scenarios
- Comprehensive error handling with detailed messages
- Sample data for testing (Alice & Bob users, various expenses)

## 🐛 Troubleshooting

### Database Connection Errors
If you see error messages about database connectivity:
1. Check Managed Identity has database permissions
2. Verify firewall rules allow your IP
3. Re-run: `python3 run-sql-dbrole.py`

### AI Chat Not Working
If chat returns dummy responses:
1. Verify Gen AI was deployed (`deploy-with-chat.sh`)
2. Check App Service settings include `OpenAI__Endpoint`
3. Verify Managed Identity has OpenAI permissions

See [DEPLOYMENT.md](DEPLOYMENT.md) for more troubleshooting tips.

## 💰 Cost Management

### Basic Deployment
- Development: ~$75/month (Basic SQL + S1 App Service)
- To reduce costs: Use Free tier App Service (cold starts)

### Full Deployment with AI
- Development: ~$150/month + OpenAI usage
- Production: Scale based on actual usage
- Monitor costs in Azure Portal Cost Management

**Tip**: Delete resource group when not in use to avoid charges.

## 🤝 Contributing

This project demonstrates:
- Modern .NET 8 development
- Azure cloud-native patterns
- Secure authentication with Managed Identity
- AI integration with function calling
- Infrastructure as Code with Bicep

## 📄 License

See [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built using Azure best practices from https://learn.microsoft.com/azure
- Inspired by legacy expense management systems
- GPT-4o model from Azure OpenAI

## 📞 Support

For issues or questions:
1. Check [DEPLOYMENT.md](DEPLOYMENT.md) for troubleshooting
2. Review [ARCHITECTURE.md](ARCHITECTURE.md) for understanding components
3. Check application logs in Azure Portal
4. Review Swagger documentation at `/swagger`

---

**Note**: This is a demonstration application for learning Azure cloud-native development. For production use, consider additional security hardening, monitoring, backup strategies, and high availability configurations.

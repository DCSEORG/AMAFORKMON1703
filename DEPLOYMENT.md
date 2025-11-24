# Deployment Order and Considerations

This document outlines the deployment sequence and important considerations for the Expense Management System.

## Deployment Sequence

### 1. Infrastructure Deployment (Bicep)

The deployment follows this specific order to handle dependencies:

```
1. Resource Group
   └─→ 2. User Assigned Managed Identity
       └─→ 3. App Service (with Managed Identity assigned)
           └─→ 4. SQL Server & Database (with AAD admin configured)
               └─→ 5. (Optional) Azure OpenAI & Cognitive Search
```

**Critical**: The Managed Identity must be created before the App Service and SQL resources because:
- App Service needs the identity assigned during creation
- SQL Database role assignments require the identity's Principal ID
- Gen AI services need the identity's Principal ID for RBAC assignments

### 2. Wait Period (30 seconds)

After infrastructure deployment, we wait 30 seconds to ensure:
- SQL Server is fully provisioned and ready
- RBAC role assignments have propagated
- All Azure services are in a ready state

### 3. SQL Server Firewall Configuration

Add the deployment machine's IP address to SQL Server firewall to allow:
- Database schema import
- Stored procedure deployment
- Role assignment for Managed Identity

### 4. Database Configuration

Execute in order:
1. **Schema Import** (`run-sql.py`): Creates tables and seed data
2. **Role Assignment** (`run-sql-dbrole.py`): Grants Managed Identity database permissions
3. **Stored Procedures** (`run-sql-stored-procs.py`): Deploys all stored procedures

### 5. App Service Configuration

Configure application settings **after** infrastructure deployment:
- SQL connection details
- Managed Identity Client ID
- (Optional) OpenAI endpoint and model
- (Optional) Cognitive Search endpoint

This must happen after infrastructure deployment because the settings reference resources that don't exist until deployment completes.

### 6. Application Deployment

Deploy the application code zip file to App Service.

## Important Considerations

### Unique Resource Naming

Use `uniqueString(resourceGroup().id)` for resource naming instead of timestamps:
- **Why**: Bicep variables cannot use `utcNow()` - it's only allowed in parameter defaults
- **Benefit**: Generates consistent, unique names based on resource group ID
- **Pattern**: `{prefix}-{service}-{uniqueString}`

### Azure AD-Only Authentication

SQL Server enforces Azure AD-only authentication:
- **Required by**: MCAPS Governance Policy SFI-ID4.2.2
- **Implication**: No SQL username/password authentication
- **Deployment Requirement**: Admin login and Object ID must be provided

### Circular Dependency Handling

OpenAI configuration creates a circular dependency:
- App Service needs Managed Identity → GenAI needs Managed Identity Principal ID → App Service needs OpenAI endpoint

**Solution**: Configure OpenAI settings via `az webapp config appsettings set` after deployment, not in Bicep.

### Managed Identity Client ID

The `AZURE_CLIENT_ID` environment variable is critical:
- **Purpose**: Tells DefaultAzureCredential which identity to use
- **Required**: When App Service has user-assigned managed identity
- **Why**: Prevents "Unable to load the proper Managed Identity" errors

### Cross-Platform Compatibility

All scripts use cross-platform `sed` commands:
```bash
# macOS and Linux compatible
sed -i.bak "s/old/new/g" file.txt && rm -f file.txt.bak
```

### Python Dependencies

Scripts require these packages:
- `pyodbc`: SQL Server connectivity
- `azure-identity`: Azure AD authentication

Install with: `pip3 install --quiet pyodbc azure-identity`

### Regional Considerations

- **App Service, SQL**: Deployed to UK South (or specified region)
- **Azure OpenAI**: Deployed to Sweden Central
  - **Why**: GPT-4o model availability
  - **Implication**: Cross-region network latency (minimal impact)

### Conditional Deployment

The system supports two deployment modes:

1. **Basic** (`deployGenAI=false`): Core expense management
2. **Full** (`deployGenAI=true`): Includes AI chat features

Use conditional outputs to handle optional resources:
```bicep
output openAIEndpoint string = deployGenAI ? genAI.outputs.openAIEndpoint : ''
```

### Asynchronous App Deployment

Application deployment uses `--async true` flag:
- Deployment completes in background
- User should wait 2-3 minutes before accessing application
- Allows script to complete faster

### Local Development

For local development:
- Use `AuthenticationMode=Active Directory Default`
- Requires `az login` before running application
- Automatically uses developer's Azure credentials

## Deployment Scripts

### deploy.sh (Basic)
- Deploys core infrastructure
- Sets up database and application
- No Gen AI features

### deploy-with-chat.sh (Full)
- Deploys everything from deploy.sh
- Adds Azure OpenAI and Cognitive Search
- Enables AI chat assistant
- Configures additional app settings

## Post-Deployment Validation

1. Wait 2-3 minutes for app deployment to complete
2. Access `{appUrl}/Index` to verify application
3. Check `{appUrl}/swagger` for API documentation
4. If Gen AI deployed, test `{appUrl}/Chat/Chat`
5. Verify database connectivity (check for error messages)

## Troubleshooting

### Database Connection Issues
- **Symptom**: Application shows dummy data with error message
- **Check**: Managed Identity has database permissions
- **Fix**: Re-run `python3 run-sql-dbrole.py`

### OpenAI Connection Issues
- **Symptom**: Chat returns dummy responses
- **Check**: App Service settings have OpenAI configuration
- **Fix**: Verify `OpenAI__Endpoint` and `OpenAI__DeploymentName` settings

### Firewall Issues
- **Symptom**: Python scripts cannot connect to SQL
- **Fix**: Ensure your IP is added to SQL Server firewall

## Best Practices

1. **Always** deploy to a new resource group for testing
2. **Always** use `az login` before running deployment scripts
3. **Review** all settings in bicep files before deploying
4. **Test** basic deployment before trying full Gen AI deployment
5. **Monitor** Azure Portal during deployment for any errors
6. **Clean up** unused resource groups to avoid costs

## Dependencies Graph

```
Resource Group
  ├─ Managed Identity (no dependencies)
  │
  ├─ App Service Plan (no dependencies)
  │   └─ App Service (depends on: Managed Identity)
  │
  ├─ SQL Server (depends on: Admin login/Object ID)
  │   └─ SQL Database (depends on: SQL Server)
  │       └─ Firewall Rule (depends on: SQL Server)
  │
  └─ (Optional) Gen AI Resources
      ├─ Azure OpenAI (depends on: Managed Identity Principal ID)
      └─ Cognitive Search (depends on: Managed Identity Principal ID)
```

## Timeline

Typical deployment takes:
- Bicep infrastructure: 5-10 minutes
- Database configuration: 2-3 minutes
- Application deployment: 2-3 minutes
- **Total**: 10-15 minutes

With Gen AI:
- Additional 5-10 minutes for OpenAI and Search deployment
- **Total**: 15-25 minutes

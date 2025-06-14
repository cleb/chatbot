# ChatApp

This sample demonstrates a simple ChatGPT-like interface built with **Blazor Server**.
It authenticates users with **Microsoft Entra ID** and sends prompts to **Azure OpenAI**.
Each user's conversation history is stored in **Azure Blob Storage**.

## Running the application

1. Update `appsettings.json` with your Azure AD, Azure OpenAI and Blob Storage settings. When deploying to Azure App Service you can use the app's managed identity by leaving `AzureOpenAI:Key` and `BlobStorage:ConnectionString` empty and setting `AzureOpenAI:UseManagedIdentity` and `BlobStorage:UseManagedIdentity` to `true`. For Blob Storage also provide `BlobStorage:AccountName` with your storage account name.
2. Restore and build the project:
   ```bash
   dotnet restore
   dotnet run --project ChatApp
   ```
3. Navigate to `https://localhost:5001/chat` and sign in.

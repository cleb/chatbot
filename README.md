# ChatApp

This sample demonstrates a simple ChatGPT-like interface built with **Blazor Server**.
It authenticates users with **Microsoft Entra ID** and sends prompts to **Azure OpenAI**.
Each user's conversation history is stored in **Azure Blob Storage**.

## Running the application

1. Update `appsettings.json` with your Azure AD, Azure OpenAI and Blob Storage settings.
2. Restore and build the project:
   ```bash
   dotnet restore
   dotnet run --project ChatApp
   ```
3. Navigate to `https://localhost:5001/chat` and sign in.

# GTasks for Windows

A modern, keyboard-first Google Tasks client for Windows 11 built with WinUI 3 and .NET 10.

## Features

- **Google Tasks Integration** - Full sync with Google Tasks API
- **Offline Support** - SQLite local storage with background sync
- **Smart Filters** - Today, Tomorrow, Overdue views
- **Natural Language Input** - Type "buy milk tomorrow" to set due dates
- **Keyboard-First** - Designed for power users
- **Modern UI** - Native Windows 11 look with Mica backdrop

## Prerequisites

- Windows 10 version 1809 or later (Windows 11 recommended)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/)

## Setup

### 1. Clone the Repository

```bash
git clone https://github.com/dan0/gtasks-win.git
cd gtasks-win
```

### 2. Configure Google OAuth Credentials

You need Google OAuth 2.0 credentials to use the Google Tasks API.

#### Create OAuth Credentials

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the **Google Tasks API**
4. Go to **APIs & Services > Credentials**
5. Click **Create Credentials > OAuth client ID**
6. Select **Desktop app** as the application type
7. Note your **Client ID** and **Client Secret**

#### Configure the App

**Option A: Environment Variables**

```powershell
$env:GTASKS_CLIENT_ID = "your-client-id.apps.googleusercontent.com"
$env:GTASKS_CLIENT_SECRET = "your-client-secret"
```

**Option B: Config File**

Create a file at `%LOCALAPPDATA%\GTasks\oauth.json`:

```json
{
  "ClientId": "your-client-id.apps.googleusercontent.com",
  "ClientSecret": "your-client-secret"
}
```

### 3. Build and Run

```bash
dotnet build
dotnet run --project src/GTasks.App/GTasks.App.csproj
```

## Usage

### Quick Add

Type in the quick add box and press Enter. Natural language is supported:
- "Buy groceries" - Creates a task with no due date
- "Call mom tomorrow" - Creates a task due tomorrow
- "Meeting notes today" - Creates a task due today

### Filters

- **Today** - Tasks due today
- **Tomorrow** - Tasks due tomorrow
- **Overdue** - Past-due tasks
- **Clear** - Show all tasks in selected list

### Account Management

Click the **Settings** (gear) icon in the header:
- **Switch Account** - Sign out and sign in with a different Google account
- **Sign Out** - Return to login screen

### Task Actions

- **Right-click** a task to delete it
- **Click the checkbox** to mark complete/incomplete

## Project Structure

```
src/
├── GTasks.App/      # WinUI 3 application entry point
├── GTasks.Core/     # Business logic and services
├── GTasks.Data/     # SQLite database and repositories
└── GTasks.UI/       # Views, ViewModels, and controls
```

## Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | WinUI 3 (Windows App SDK 1.8) |
| Language | C# 12, .NET 10 |
| Architecture | MVVM with CommunityToolkit.Mvvm |
| Auth | Google.Apis.Auth (OAuth 2.0) |
| API | Google.Apis.Tasks.v1 |
| Database | SQLite with Entity Framework Core |

## License

MIT

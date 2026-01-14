# Figma Version Diff System

This project implements a system for tracking version differences in Figma designs and integrating them with Jira and Slack.

## Structure

The project consists of two main components:

- **backend/**: A .NET 8 Web API that handles image diffing, storage, and external integrations (Jira, Slack).
- **plugin/**: A Figma plugin (TypeScript) that allows users to select frames, capture baselines, and initiate comparisons.

## Setup

### Backend
1. Navigate to the `backend` directory.
2. Configure `appsettings.json` with your Jira and Slack, and Database settings.
3. Run the application:
   ```bash
   dotnet run
   ```

### Plugin
1. Navigate to the `plugin` directory.
2. Install dependencies:
   ```bash
   npm install
   ```
3. Build the plugin:
   ```bash
   npm run build
   ```
4. Import the `manifest.json` into Figma Desktop App to load the plugin.

## Prerequisites

To run and test this system locally, you need:

1.  **Frameworks/Runtimes**:
    *   [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (for the Backend)
    *   [Node.js](https://nodejs.org/) (for the Plugin build)
2.  **Tools**:
    *   **Figma Desktop App**: Required to load local plugins during development.

## How to Test Loop (Local)

You can test the core logic (Image Capture -> Diff -> Report) without valid Jira/Slack credentials.

1.  **Start the Backend**:
    *   Open terminal in `backend/` folder.
    *   Run `dotnet run`.
    *   Keep this terminal open. It will listen on `http://localhost:5000` (or similar, check output).

2.  **Build & Load Plugin**:
    *   Open terminal in `plugin/` folder.
    *   Run `npm install && npm run build`.
    *   Open Figma Desktop App.
    *   Go to **Plugins > Development > Import plugin from manifest...**
    *   Select the `manifest.json` file in your `plugin/` folder.

3.  **Run the Flow**:
    *   **Baseline**: Select a Frame in Figma -> Open Plugin -> Enter a fake issue key (e.g., `TEST-1`) -> Click **Capture Baseline**.
    *   **Change**: Modify the Frame in Figma (change color, move text).
    *   **Compare**: Select the same Frame -> Open Plugin -> Click **Compare & Publish**.
    *   **View Report**: The plugin will show a link to the HTML report (e.g., `http://localhost:5000/reports/...`). Click to view the visual diff.

*Note: Jira comments and Slack messages will fail silently if not configured in `appsettings.json`, but the visual diff report will still work.*

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

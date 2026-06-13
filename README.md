# MyJournal

MyJournal is a .NET MAUI Blazor Hybrid journal app built for people who want a private, structured, and visually polished space to capture daily thoughts. It combines secure account setup, rich-text journaling, mood tracking, tags, analytics, calendar views, and PDF export in a single local-first experience.

This project is designed to demonstrate practical product thinking: real authentication flow, offline-friendly storage, data visualization, responsive UI, and a clean separation between UI, services, and persistence.

## Why This Project Stands Out

- Secure by design: user credentials are hashed with BCrypt, the PIN is stored in SecureStorage, and journal data lives locally in SQLite.
- Product-focused: the app goes beyond note-taking with streak tracking, mood insights, tag analysis, calendar views, and export tooling.
- Cross-platform: one codebase targets Windows, Android, iOS, and MacCatalyst.
- Recruiter-friendly polish: the UI is built with MudBlazor and MAUI patterns for a modern, responsive experience.

## Key Features

- First-time setup flow with username, email, and a 4-digit PIN.
- Login, forgot-password, and PIN reset flows.
- Daily journal editor with formatted content, title support, and optional PIN protection per entry.
- Mood tracking with one primary mood and up to two secondary moods.
- Custom tag management plus a built-in tag library.
- Search, sort, pagination, and filters for saved journals.
- Dashboard analytics for mood distribution, most frequent moods, tag breakdowns, and word-count trends.
- Streak tracking with current streak, longest streak, and missed days.
- Calendar view that shows journal entries alongside personal events.
- PDF export for selected journal ranges.
- Profile settings for account details, theme preference, timezone, and PIN management.

## Tech Stack

- .NET MAUI Blazor Hybrid
- Blazor components and Razor pages
- MudBlazor UI components
- SQLite with sqlite-net-pcl
- QuestPDF for export generation
- BCrypt.Net-Next for PIN hashing
- MailKit for email delivery
- Bogus for generated sample content

## Architecture Overview

The app keeps the UI, business logic, and data access separated:

- `Components/Pages` contains the user-facing screens such as the dashboard, journal editor, calendar, login, and profile views.
- `Services` contains authentication, database access, export, notification hooks, streak calculation, profile management, and app state.
- `Models` contains the persisted journal, profile, tag, and analytics data structures.
- `wwwroot` contains the web assets used by the Blazor hybrid shell.

Journal entries are stored locally in SQLite, while credentials and PIN data use platform storage APIs. The database layer also includes migration logic so the app can evolve without breaking existing data.

## Screens In The App

- Home dashboard with streak cards and recent entries.
- Journal editor for creating and protecting daily entries.
- Saved journals page with filters and PDF export.
- Calendar page for navigating by date.
- Tags page for managing prebuilt and custom tags.
- Profile page for account and security settings.
- Analytics dashboard for chart-based insights.

## Getting Started

### Prerequisites

- .NET 10 SDK
- Visual Studio with the .NET MAUI workload installed
- Target platform tooling for the platform you want to run, such as Windows, Android, iOS, or MacCatalyst

### Restore and Build

From the repository root:

```bash
dotnet restore MyJournal.slnx
dotnet build MyJournal/MyJournal.csproj
```

### Run

Open the solution in Visual Studio and select the target platform you want to launch. The app targets:

- `net10.0-android`
- `net10.0-ios`
- `net10.0-maccatalyst`
- `net10.0-windows10.0.19041.0`

## Configuration

Password reset email support uses environment variables, with Brevo preferred and Gmail available as a fallback.

### Brevo

```bash
BREVO_SMTP_USERNAME=your_brevo_login_email
BREVO_SMTP_PASSWORD=your_brevo_smtp_key
```

### Gmail

```bash
GMAIL_SMTP_USERNAME=your_gmail_address@gmail.com
GMAIL_APP_PASSWORD=your_16_character_app_password
```

For debug builds, the project also supports user secrets for local configuration.

## Repository Structure

- `MyJournal/Components` - Blazor routes, pages, dialogs, and layout components
- `MyJournal/Models` - Data models used by the app and SQLite persistence
- `MyJournal/Services` - Authentication, storage, analytics, PDF export, and app state
- `MyJournal/Resources` - Fonts, images, splash screen assets, and app icons
- `MyJournal/Platforms` - Platform-specific MAUI entry points and manifests
- `MyJournal/wwwroot` - Static assets and client-side interop resources

## Notes For Reviewers

This app is intentionally personal and local-first. The main goal was to build a polished journaling experience with real product value, secure handling of user access, and enough analytics to make the writing habit feel measurable and engaging.

If you want to extend the project further, the most natural next steps would be cloud sync, multi-user support, and richer reporting dashboards.

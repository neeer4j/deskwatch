# DeskWatch

DeskWatch is a modern Windows desktop application that tracks the amount of time you spend in each application. It features a beautiful dark-themed UI with smooth animations and is built with WPF and .NET 10.

## Features
- ✅ Tracks active window usage per application
- ✅ Modern card-based UI with usage statistics
- ✅ Start, Stop, and Reset tracking controls
- ✅ **Persistent data storage** - your tracking data is saved automatically
- ✅ **System tray support** - minimize to tray and continue tracking in background
- ✅ Auto-start with Windows (configurable)
- ✅ Add apps from running processes or browse for executables
- ✅ Session/focus count tracking per app

## Usage
1. Run the application (starts tracking automatically)
2. Click **Add Application** to add apps you want to track
3. Click on any app card to view detailed statistics
4. Use **Stop Tracking** to pause, **Start Tracking** to resume
5. Close the window to minimize to system tray (right-click tray icon to exit)

## Development
- Built with WPF (.NET 10)
- Uses System.Drawing.Common for icon extraction
- All tracking is local and private

## Planned Features
- [ ] Idle detection (pause tracking when away)
- [ ] Exclude list for apps/windows
- [ ] Daily/weekly usage reports

## License
MIT
